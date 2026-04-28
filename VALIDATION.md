# VALIDATION — RNTBD Dispatcher idle-timer thread-pool starvation fix

## 1. Summary

`Dispatcher.OnIdleTimer` ran on a thread-pool worker and called
`WaitTask(receiveTaskCopy)`, which performs `Task.Wait()` on a
receive loop that does not complete until the connection is
disposed. Under sustained scale, every channel's idle timer fire
parked one worker, starving the pool (issue #4393). The fix
converts the timer callback to async (`OnIdleTimerAsync` +
`WaitTaskAsync`), wires it via `.ContinueWith(...).Unwrap()`, and
preserves the sync `WaitTask` for any other callers. Evidence:
deterministic unit test (5/5) directly demonstrating the
calling-thread behavior change, plus one integration run during
investigation that reproduced the production pathology end-to-end
(48s probe latency, 46-thread pool growth) in this environment.

Issue: https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4393

## 2. The fix

Three edits to `Microsoft.Azure.Cosmos/src/direct/Dispatcher.cs`.

**Edit 1 — `OnIdleTimer` becomes `OnIdleTimerAsync`:**

```csharp
private async Task OnIdleTimerAsync(Task precedentTask)
{
    DefaultTrace.TraceInformation(
        "[RNTBD Dispatcher {0}][{1}] Idle timer fired.",
        this.ConnectionCorrelationId, this);

    Task receiveTaskCopy = null;
    // ... unchanged body that ends with:
    await this.WaitTaskAsync(receiveTaskCopy, "receive loop")
        .ConfigureAwait(false);
}
```

The omitted body of `OnIdleTimerAsync` is byte-for-byte identical
to the pre-fix `OnIdleTimer` body: lock blocks remain synchronous,
no `await` appears inside any lock, and the existing
`Debug.Assert(!Monitor.IsEntered(...))` lock guards on `callLock`
and `connectionLock` are preserved.

**Edit 2 — new `WaitTaskAsync` alongside the still-present sync `WaitTask`:**

```csharp
private async Task WaitTaskAsync(Task t, string description)
{
    if (t == null) { return; }
    try
    {
        Debug.Assert(!Monitor.IsEntered(this.callLock));
        Debug.Assert(!Monitor.IsEntered(this.connectionLock));
        await t.ConfigureAwait(false);
    }
    catch (Exception e)
    {
        DefaultTrace.TraceWarning(
            "[RNTBD Dispatcher {0}][{1}] Parallel task failed: {2}. " +
            "Exception: {3}: {4}",
            this.ConnectionCorrelationId, this, description,
            e.GetType().Name, e.Message);
    }
}
```

**Edit 3 — `ScheduleIdleTimer` uses `.Unwrap()`:**

```csharp
private void ScheduleIdleTimer(TimeSpan timeToIdle)
{
    Debug.Assert(Monitor.IsEntered(this.connectionLock));
    this.idleTimer = this.idleTimerPool.GetPooledTimer((int)timeToIdle.TotalSeconds);
    // IMPORTANT: .Unwrap() is essential here. Without it, idleTimerTask
    // would be Task<Task> and would complete when OnIdleTimerAsync
    // returns its inner Task (at the first await), not when it
    // finishes. StopIdleTimer() waits on idleTimerTask during
    // shutdown; if idleTimerTask completes early, shutdown proceeds
    // while OnIdleTimerAsync is still running, causing
    // use-after-dispose on the connection. Do not remove .Unwrap().
    this.idleTimerTask = this.idleTimer.StartTimerAsync()
        .ContinueWith(this.OnIdleTimerAsync, TaskContinuationOptions.OnlyOnRanToCompletion)
        .Unwrap();
    // ... existing failure-trace ContinueWith chain unchanged
}
```

## 3. Why this bug is hard to test

The minimum effective idle-timer arm in this codebase is **750
seconds** (12.5 minutes). `StoreClientFactory` enforces a 600s
floor on `IdleTcpConnectionTimeout`, and `Connection`
adds a `2 * (sendHang + receiveHang) = 150s` race buffer on top
before the dispatcher idle timer is armed. Any single-client
test that wants to observe an idle-timer fire must idle the SDK
for at least that long; CI suites do not. Beyond runtime, the
production trigger requires sustained scale and timing
conditions across many channels — connection counts, partition
distribution, and backend replica state — that a single test
client cannot synthesize on demand. This is why #4393 sat from
2024 to 2026 without being caught: nobody runs a 13-minute idle
test in CI, and even when one is run, the conditions that turn
"timer fires" into "thread pool starves" are not deterministic
from one client. The testing strategy below reflects this:
deterministic unit-test evidence at the changed line,
integration test as wiring guard, plus the one investigation run
that did reproduce the end-to-end pathology preserved as
artifact.

## 4. Evidence

### 4a. Unit test (canonical evidence)

`Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/DispatcherIdleTimerFixTests.cs`
isolates the exact line the fix changes. It uses
`InternalsVisibleTo` and reflection to invoke `WaitTaskAsync`
and `WaitTask` directly on a `Dispatcher` instance, passing each
a `TaskCompletionSource<bool>().Task` that is never completed —
the same shape `OnIdleTimer` saw in production when it called
`.Wait()` on a receive loop. Probes measure thread-pool
behavior during the wait window.

Result over 5/5 runs: the **async path** completes synchronously
from the caller's perspective (the `await` yields the calling
thread; the awaited task remains pending in the background, with
10+ distinct thread IDs observed in `WaitingForActivation` over
the probe window). The **sync path** blocks the calling thread
for the full measurement window — directly demonstrating the
pre-fix pathology that, multiplied across N idle channels in
production, exhausts the pool. This test runs in <1 second per
case and is the canonical proof that the fix changes the
thread-blocking behavior at the line that matters.

### 4b. Integration test (regression guard)

`Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.EmulatorTests/RntbdIdleTimerStarvationTests.cs`
opens N=50 channels against a live Azure Cosmos DB account by
forcing `MaxRequestsPerTcpConnection=1` and firing N concurrent
reads, then waits 810 seconds for every channel's idle timer to
fire. It asserts:

- `delta < 10` (peak − baseline thread count) — guards against
  the thread-pool growth signature of the production bug
- `timersFired > 0` — guards against `.Unwrap()` chain
  breakage; if a future change removes `.Unwrap()`,
  `OnIdleTimerAsync` stops being reached via the timer
  continuation and this assertion catches it. Counted via a
  `TraceListener` attached (by reflection) to
  `DefaultTrace.TraceSource`, observing the
  `TraceInformation` line at `OnIdleTimerAsync` entry.
  Reflection is used because `DefaultTrace.TraceSource` is
  internal; adding a public test seam was considered and
  rejected as unjustified API expansion for a regression-only
  assertion.

The previous max-probe-latency assertion was dropped: across six
clean runs of the integration test on the unpatched baseline the
latency clustered at ~1001ms regardless of N, which is a probe-
implementation ceiling artifact, not a Dispatcher signal.
Asserting on it would either flap or test the wrong thing.

### 4c. Bug-exists-in-environment evidence

A single integration run on the unpatched baseline at N=50
against the same live account, with Stopwatch instrumentation
around every `.Wait()` call inside `WaitTask`:

- Max probe latency: **48,093 ms**
- Thread count delta: **46** (baseline → peak during idle-fire window)
- Probes never executed: 2
- `.Wait()` durations:
  - Median: 12.5 s
  - Mean: 21.2 s
  - p95: 64.1 s
  - p99: 67.6 s
  - Max: 67.6 s
- 67/110 `.Wait()` calls blocked ≥1 s; 57/110 blocked ≥10 s

This run reproduced the production pathology once. Six
subsequent runs (3 at N=50, 3 at N=200) did not reproduce it;
all six showed the ~1001ms ceiling and delta=0. We interpret
this as confirmation that the bug exists in this environment,
but is not reliably triggerable from a single test client at
the scales we tested. The numbers above are preserved verbatim
as artifact; we did not chase the question of why one run
reproduced and others did not (likely Azure-side timing,
network conditions, or backend replica state — outside our
control).

## 5. Results table

| Test | N | Max latency | Thread delta | Result |
|------|---|-------------|--------------|--------|
| Unit test (sync path) | n/a | (blocks calling thread) | n/a | Demonstrates pre-fix behavior |
| Unit test (async path) | n/a | (yields calling thread) | n/a | 5/5 PASS |
| Integration baseline (single dramatic run) | 50 | 48,093ms | 46 | Bug reproduced |
| Integration baseline (6 subsequent runs) | 50, 200 | ~1001ms | 0 | Bug not reproduced |
| Integration fix (sanity run) | 50 | 900ms | 0 | PASS |

## 6. What this PR does NOT do

- Does not add `DisposeAsync` or `IAsyncDisposable` to any class
- Does not modify `Channel`, `ChannelDictionary`,
  `LoadBalancingChannel`, `LoadBalancingPartition`,
  `LbChannelState`, or `IChannel`
- Does not change `bool disposed` to `int disposed` or add
  `Interlocked.CompareExchange` guards
- Does not modify any public API surface
- Does not change behavior of the existing sync `WaitTask`
  (preserved untouched in case other callers exist)
- Only modifies `Dispatcher.cs` plus one new unit-test file plus
  one reshaped integration-test file

## 7. Limitations

- Tested only on Linux (matches the production bug environment;
  Windows not tested in this round)
- Test endpoint was a live Azure Cosmos DB account; emulator
  validation not performed
- Integration test does not gate on starvation behavior because
  starvation could not be reliably reproduced at N=50 or N=200
  from a single test client
- The single dramatic baseline reproduction is one data point;
  we did not pursue investigation into why that run reproduced
  and others did not (likely Azure-side timing, network
  conditions, or backend replica state — outside our control)
- The async disposal path described in the original PR
  description (Path 2 in issue #4393) is not addressed by this
  PR and remains a separate follow-up
