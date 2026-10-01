# Microsoft.Azure.Cosmos.NativeDriverPoc — .NET architecture

How the .NET host wraps `azurecosmosdriver.dll` (the Rust cdylib from
[Azure/azure-sdk-for-rust#4515](https://github.com/Azure/azure-sdk-for-rust/pull/4515))
and turns its **callback-free, opaque-completion** FFI into idiomatic
`Task<T>`-returning methods.

The interesting story is `CompletionQueueLoop` — one pump thread, one
unmanaged completion queue, an unbounded number of in-flight ops, all
multiplexed back onto `TaskCompletionSource<T>` without any callback ever
crossing the FFI boundary.

---

## The four-layer cake

```
   ┌─────────────────────────────────────────────────────────────┐
   │  4. Your code                                               │
   │     await client.ReadItemAsync("id");                       │
   └─────────────────────────────────────────────────────────────┘
                              │
   ┌─────────────────────────────────────────────────────────────┐
   │  3. NativeCosmosClient                                      │
   │     • Owns the object graph (runtime/account/db/driver/cq)  │
   │     • Exposes Task-returning CRUD methods                   │
   │     • Builds operation, wires CT, submits, returns Task     │
   └─────────────────────────────────────────────────────────────┘
                              │
   ┌─────────────────────────────────────────────────────────────┐
   │  2. CompletionQueueLoop  +  NativeAsyncOperation            │
   │     • One BG thread per CQ                                  │
   │     • user_data = GCHandle.ToIntPtr(boxed NativeAsyncOp)    │
   │     • Pumps cosmos_cq_wait → GCHandle.FromIntPtr → settle   │
   └─────────────────────────────────────────────────────────────┘
                              │
   ┌─────────────────────────────────────────────────────────────┐
   │  1. NativeMethods (P/Invoke)                                │
   │     • ~45 [DllImport] entries, line-tagged to header        │
   │     • Pure 1:1 with azurecosmosdriver.h                     │
   └─────────────────────────────────────────────────────────────┘
                              │
   ┌─────────────────────────────────────────────────────────────┐
   │  0. azurecosmosdriver.dll  (Rust cdylib, PR #4515)          │
   └─────────────────────────────────────────────────────────────┘
```

Each layer talks ONLY to the layer directly below it. Replacing the DLL
re-skins layer 0; the rest is unchanged. Bumping the binding to a new
header line range stays inside layer 1.

### Layer 1 — `NativeMethods.cs`

~45 `[DllImport("azurecosmosdriver")]` entries. Every entry has a doc
comment pointing at the line range in `azurecosmosdriver.h` it mirrors.
This is a deliberately **thin** layer — no logic, no marshalling tricks,
just type-faithful signatures. If a signature is wrong, you debug here;
if behavior is wrong, you debug higher up. The boundary is sharp on
purpose.

The header is the source of truth — re-running cbindgen would regenerate
it. We hand-port to .NET because (a) `LibraryImport`/`DllImport`
attributes don't have a generator that targets a foreign header, and
(b) it gives us a place to comment on each function's contract
(consuming ownership, lifetime, return semantics).

### Layer 2 — `CompletionQueueLoop.cs` (the heart)

See the deep-dive below — this is the one you actually need to
understand.

### Layer 3 — `NativeCosmosClient.cs`

The "object graph" owner. The Rust FFI organizes its world as a chain of
handles:

```
runtime → account → database → container
   │                              │
   └──► driver ──► operation ────►┴─► submit ──► CQ ──► completion
                                         │
                                         └──► operation_handle (for cancel)
```

The client builds that whole chain once in its constructor (in the order
shown above), holds it in `IntPtr` fields, and frees children before
parents in `Dispose()`. Construction uses a **staged-build pattern**: any
failure inside the `try` triggers the `finally` to free everything
allocated so far (`staged*` locals are nulled out only after the final
`Commit` line). That keeps the constructor exception-safe — no leaks
even if step 5 of 7 fails.

### Layer 4 — Your code

Just `await`s the returned `Task<CosmosNativeResponse>`. No idea any of
the above exists.

---

## The Rust FFI's async model — what makes it weird

Before the deep dive on `CompletionQueueLoop`, you have to know what
problem it's solving. The Rust crate offers a **callback-free async
model**:

| What it does NOT do | What it DOES do |
|---|---|
| Invoke a function pointer from a foreign thread when the op completes | Push a `cosmos_completion_t*` onto a completion queue you poll |
| Hand back a future/promise object | Hand back an opaque `cosmos_operation_handle_t*` for cancellation only |
| Care what language you're calling from | Just sits there, posting completions, waiting for you to drain them |

This is intentional. Callbacks from foreign threads into a managed runtime
are a portability and lifetime nightmare. The CQ model says: **"I'll
tell you when I'm done by posting to this queue. You decide how to
schedule that back onto your runtime."**

That's exactly what `CompletionQueueLoop` is for.

---

## CompletionQueueLoop — deep dive

### What it owns

```csharp
internal sealed class CompletionQueueLoop : IDisposable
{
    private readonly IntPtr cqHandle;     // 1
    private readonly Thread pumpThread;   // 2
    private int disposed;                 // 3
}
```

1. **`cqHandle`** — the native CQ created via `cosmos_cq_create*`. Tied to
   one runtime. Released by `cosmos_cq_free` in `Dispose`.
2. **`pumpThread`** — exactly one background thread per CQ, named
   `"cosmos-native-driver-cq-pump"`. `IsBackground = true` so it doesn't
   keep the process alive. It does **nothing but `cosmos_cq_wait`** in a
   loop.
3. **`disposed`** — `int` flag, mutated via `Interlocked.Exchange`. Both
   the pump's loop guard and the Dispose idempotency hinge on it.

That's it. There is **no `pending` map, no counter, no central routing
table.** The CLR's GCHandle table IS the routing table — see the next
section.

### GCHandle as `user_data` — routing without a map

The Rust FFI exposes the two submit entry points
(`cosmos_driver_execute_singleton_operation_submit` and
`cosmos_driver_execute_operation_submit`) with a `user_data: IntPtr`
parameter. Whatever we pass in is handed back to us on the matching
completion via `cosmos_completion_user_data`. The DLL never dereferences
it — it's just an 8-byte cookie that round-trips.

**We use that cookie as a `GCHandle` to a boxed per-op object.** That's
the key design choice, settled by the Aaron Robinson + Kevin Jones +
Ashley Schroder thread in May 2026:

```csharp
// Submit side (NativeCosmosClient.RunSingletonAsync)
var op       = new NativeAsyncOperation();              // { Id, Tcs }
IntPtr cookie = op.AllocateUserData();                  // GCHandle.Alloc + ToIntPtr
builder.Submit(driver, cq.Handle, cookie, OperationBucket.Singleton, out _);

// Pump side (CompletionQueueLoop.DispatchCompletion)
IntPtr userData = cosmos_completion_user_data(completion);
NativeAsyncOperation op = NativeAsyncOperation.FromUserData(userData, out var gch);
op.Tcs.TrySetResult(MaterializeResponse(...));
gch.Free();                                              // exactly once
```

Why GCHandle and not a `ConcurrentDictionary<ulong, TCS>` keyed by a
monotonic counter? Three reasons, in Aaron's words and our reasoning:

1. **The GC is already a perfectly good routing primitive.** Aaron:
   *"The GCHandle approach is going to defer the mapping and threading
   issue to the GC, which is generally just as good as Rust... I would
   avoid the ConcurrentDictionary&lt;&gt; unless there is a real need for
   a disjoint mapping."* The GCHandle table is a process-global,
   constant-time identity map maintained by the runtime — we don't need
   to maintain a parallel one.
2. **Boxed, so we get an `Id` for diagnostics.** Aaron again: *"I'd
   prefer the box because you want an ID to help fish out problems and
   where things might go wrong (that is, logging and trace)."* The
   `Id` is a process-wide monotonic counter on
   `NativeAsyncOperation` — useful for log correlation, not for routing.
3. **Lower per-op cost on the chatty path.** No dict node allocation,
   no concurrent-hash contention. `GCHandle.Alloc`/`Free` and
   `GCHandle.FromIntPtr` are roughly the same wall-clock cost as a
   `ConcurrentDictionary.TryAdd`/`TryRemove`, but they avoid a shared
   data structure that becomes a hotspot when completions are
   chatty (Aaron: *"if this is going to be chatty a
   ConcurrentDictionary&lt;,&gt; is not something I'd suggest"*).

The blog that crystallised the pattern —
[Nazarii Piontko, *"Rust↔C# async interop"*
(2026-04-26)](https://www.npiontko.pro/2026/04/26/rust-csharp-async-interop) —
uses the same primitive, with an abstract `AsyncOperation` base + typed
`AsyncOperation<T>` leaves. Our POC only has one completion shape
(`CosmosNativeResponse`), so we collapse the hierarchy to a single
sealed class.

### The boxed payload — `NativeAsyncOperation`

```csharp
internal sealed class NativeAsyncOperation
{
    public ulong Id { get; }                                       // diagnostic, not routing
    public TaskCompletionSource<CosmosNativeResponse> Tcs { get; } // RunContinuationsAsynchronously

    public IntPtr AllocateUserData() =>
        GCHandle.ToIntPtr(GCHandle.Alloc(this, GCHandleType.Normal));

    public static NativeAsyncOperation FromUserData(IntPtr ud, out GCHandle gch)
    {
        gch = GCHandle.FromIntPtr(ud);
        return (NativeAsyncOperation)gch.Target!;
    }
}
```

Three deliberate choices:

* **`Id` field** — a process-wide monotonic counter (`Interlocked.Increment`
  on a static). Aaron explicitly called this out: an ID makes it possible
  to grep traces, correlate logs, and identify orphaned ops in heap dumps.
  Routing doesn't need it; debugging does.
* **`GCHandleType.Normal`** — keeps the object alive but does **not**
  pin its memory. Rust never reads the bytes; it only carries the
  `IntPtr` from `ToIntPtr` back to us. The handle slot itself is stable;
  the underlying object can move freely during GC.
* **`TaskCreationOptions.RunContinuationsAsynchronously`** — without
  this, `tcs.TrySetResult` would inline the awaiter's continuation on
  the pump thread. Kevin called this out in the thread: *"sets the
  result on the TCS, with an asynchronous continuation, so that the C#
  side doesn't tie up the completion thread."*

### The pump loop

```csharp
private void Pump()
{
    while (Volatile.Read(ref this.disposed) == 0)
    {
        IntPtr completion = cosmos_cq_wait(this.cqHandle, timeoutMs: 200);
        if (completion == IntPtr.Zero)
        {
            CosmosCqState state = cosmos_cq_state(this.cqHandle);
            if (state == CosmosCqState.Running) continue;     // timeout / spurious — re-arm
            return;                                            // CQ drained or shutting down
        }
        try { this.DispatchCompletion(completion); }
        catch (Exception ex) { Console.Error.WriteLine($"[pump] dispatch error: {ex}"); }
        finally { cosmos_completion_free(completion); }
    }
    cosmos_cq_shutdown(this.cqHandle);
    this.DrainAfterShutdown();
}
```

Four things to notice:

**A. 200 ms cooperative timeout.** The pump wakes every 200 ms even with
zero traffic so it can re-check `disposed`. Without it, `Dispose()` would
have to call `cosmos_cq_shutdown` to unblock the wait — that's an extra
piece of state to manage and a different shutdown path under load. With
the timeout, the same code path handles "no traffic + caller wants to
shut down" and "real disposal." Worst-case shutdown latency is one
timeout window. Tunable.

**B. NULL is overloaded.** The FFI returns NULL from `cosmos_cq_wait` for
four different reasons: timeout, shutdown, drained, spurious wake.
Disambiguation requires a second call — `cosmos_cq_state` — exactly per
header §720. We split: `Running` → re-arm; anything else → exit and
trigger the shutdown drain. This is the **single most contract-sensitive
line** in the file.

**C. Per-completion lifecycle.** Every completion must be `free`'d
exactly once. The `try/finally` guarantees that even if `DispatchCompletion`
throws (e.g. a deserialization bug in `MaterializeResponse`), the native
allocation is released. Loose memory in Rust-land would be much harder
to track down than a managed exception.

**D. Exceptions are eaten with a log.** The pump must never die. If one
dispatch hits a bug, the next completion still has to be drained.
Logging to `stderr` keeps the issue visible without taking down the
process. (For prod, this would route to a structured logger.)

### Dispatch — turning a completion into a Task outcome

```csharp
private void DispatchCompletion(IntPtr completion)
{
    IntPtr userData = cosmos_completion_user_data(completion);
    if (userData == IntPtr.Zero) { /* log + drop */ return; }

    NativeAsyncOperation op;
    GCHandle gch;
    try { op = NativeAsyncOperation.FromUserData(userData, out gch); }
    catch (Exception ex)
    {
        // GCHandle.FromIntPtr throws if the slot was already freed —
        // i.e. duplicate delivery or a slot that was rolled back at
        // pre-flight. Drop + log; never crash the pump.
        Console.Error.WriteLine($"[pump] invalid user_data 0x{userData:X16} dropped: {ex.Message}");
        return;
    }

    try { SettleCompletion(op, completion); }
    finally { gch.Free(); }    // exactly once per submission
}
```

Three outcomes inside `SettleCompletion`, three TCS endings:

| Outcome | What we do | TCS state |
|---|---|---|
| `Ok` | `take_response` → `MaterializeResponse` (copy out bytes) → `response_free` | `TrySetResult` |
| `Error` | `take_error` → `new CosmosNativeException` (copy out fields) → `error_free` | `TrySetException` |
| `Cancelled` | nothing else needed | `TrySetCanceled` |

`take_response` / `take_error` transfer ownership of the inner handle
from the completion to the caller — after that call, the completion no
longer holds either. We immediately copy what we need into a managed
`CosmosNativeResponse` / `CosmosNativeException` and free the native
side. **No part of `CosmosNativeResponse` holds a pointer into native
memory once construction returns** — the bytes are a `byte[]`, the
strings are .NET `string`s, the response can outlive the entire client.

`TrySet*` (not `Set*`) is deliberate: if the user cancelled before the
result landed, their cancellation already completed the TCS — we don't
want a second `Set*` to throw.

**One subtle but important invariant:** `gch.Free()` runs **after**
`SettleCompletion` has handed the result to the `TCS`. The TCS is a
separate object referenced by the awaiter, so the `NativeAsyncOperation`
becoming GC-eligible after the Free doesn't strand any state — the
result is already on its way to the awaiter via the threadpool
continuation we attached in `RunSingletonAsync`.

### How the pump routes completions — no lookup, ever

A common misconception is that the pump needs a dictionary to find the
right TCS. It does not — and with Option C it doesn't even own one.
**Routing is one `GCHandle.FromIntPtr` per completion, regardless of how
many ops are in flight,** because each completion carries the GCHandle
directly.

The mechanism: at submit time, we box a `NativeAsyncOperation`,
GCHandle-pin it (`GCHandleType.Normal`), and pass the
`GCHandle.ToIntPtr` to Rust as the `user_data` cookie. When that op
finishes, Rust attaches the same cookie to the completion it pushes onto
the CQ. The pump reads the cookie via `cosmos_completion_user_data`,
does one `GCHandle.FromIntPtr` (constant time, no contention), sets the
result on `op.Tcs`, and frees the handle. Done.

> "Which op finished?" is answered **by Rust** (it carries the cookie),
> not by .NET. The pump never enumerates anything. There is nothing to
> enumerate.

### `cosmos_cq_wait` is a block, not a poll

The pump does not spin. `cosmos_cq_wait(timeoutMs)` is a blocking park
— under the hood Rust uses a condvar / event / semaphore. The thread is
**asleep at the OS level** when no traffic. It wakes via condvar signal
when (a) a completion is enqueued, (b) the timeout fires, or (c)
`cosmos_cq_shutdown` is called. So an idle client costs zero CPU; a
busy client costs ~5–10 µs of pump time per completion.

### Worked example — three concurrent reads

Three callers fire `ReadItemAsync` in quick succession on the same
client. They run on three different threadpool threads. Wall-clock
times are illustrative.

**Setup phase (T=0):**

```
T=0  Caller A:  op_A = new NativeAsyncOperation();  cookie_A = GCHandle.Alloc(op_A).ToIntPtr();
                submit(opReq, user_data=cookie_A);  return op_A.Tcs.Task   →  caller awaits

T=0  Caller B:  op_B = new NativeAsyncOperation();  cookie_B = GCHandle.Alloc(op_B).ToIntPtr();
                submit(opReq, user_data=cookie_B);  return op_B.Tcs.Task   →  caller awaits

T=0  Caller C:  op_C = new NativeAsyncOperation();  cookie_C = GCHandle.Alloc(op_C).ToIntPtr();
                submit(opReq, user_data=cookie_C);  return op_C.Tcs.Task   →  caller awaits
```

After T=0: three GCHandle table entries, three Tasks held by awaiters.
All three callers are `await`-ing. Rust worker tasks are doing the HTTPS
round-trips — they will finish in whatever order the network decides,
**not** in submission order.

**Pump (before any completion):**

```
while loop iter N:
   cosmos_cq_wait(200)   ──►  parked on OS condvar, asleep
```

**T=15 ms — op B finishes first (shortest round-trip happened to be B):**

```
Rust worker B:  build completion{ user_data=cookie_B, outcome=Ok, response=... }
                enqueue onto CQ
                signal condvar

Pump:           wakes from cosmos_cq_wait → returns completion pointer
                DispatchCompletion:
                   cosmos_completion_user_data(completion) → cookie_B
                   NativeAsyncOperation.FromUserData(cookie_B, out gch) → op_B
                   op_B.Tcs.TrySetResult(MaterializeResponse(...))
                       → Task_B continuation scheduled on threadpool
                       → pump does NOT run the continuation itself
                   gch.Free()    ← op_B is now GC-eligible
                cosmos_completion_free(completion)
                loop → cosmos_cq_wait → parked again
```

After T=15: two GCHandle entries left (cookie_A, cookie_C). Caller B's
`await` is unwinding on some threadpool thread. **Critical:** the pump
did not touch op_A or op_C — it didn't even read those entries.

**T=22 ms — op C finishes:**

```
Pump:  wake, user_data=cookie_C, FromUserData → op_C
       op_C.Tcs.TrySetResult(...) → Task_C continuation scheduled
       gch.Free(), free completion, loop, park
```

**T=30 ms — op A finishes last:**

```
Pump:  wake, user_data=cookie_A, FromUserData → op_A
       op_A.Tcs.TrySetResult(...) → Task_A continuation scheduled
       gch.Free(), free completion, loop, park
```

After T=30: zero GCHandle entries. Pump is asleep on `cosmos_cq_wait`.
Nothing to do.

### Why this scales to N concurrent ops

The example above with N=3 generalizes to N=1,000 or N=100,000 with
**no change to the per-completion cost**:

- Caller side: each submit does one `new NativeAsyncOperation()` + one
  `GCHandle.Alloc` + one Rust call. O(1) per caller, no shared state.
- Pump side: each completion does one `cq_wait` return + one
  `user_data` read + one `GCHandle.FromIntPtr` (constant-time CLR
  table lookup) + one `TrySetResult` + one `gch.Free` + one
  `completion_free`. **O(1) per completion, independent of N.**
- TCS continuations run on the **threadpool**, not on the pump, because
  we built each TCS with `TaskCreationOptions.RunContinuationsAsynchronously`.
  With the default (synchronous continuations), `TrySetResult` would
  inline the awaiter's callback onto the pump thread — with 1,000
  awaiters, the pump would have to run 1,000 user callbacks
  sequentially before draining the next completion. The async option
  prevents that: `TrySetResult` schedules the continuation and
  returns immediately; pump goes right back to `cq_wait`.

The pump's CPU is therefore proportional to **throughput**
(completions/sec), not **concurrency** (in-flight ops). Empirically,
dispatch is microseconds; even at 100K ops/sec the pump is loafing.

### Cancellation — the "second handle" model

The most subtle bit. A `CancellationToken` arrives on the caller side
in .NET-land, but the in-flight work lives in Rust-land — how do we
poke it?

The Rust FFI returns TWO things from `cosmos_driver_execute_*_submit`:

- The completion (eventually, via the CQ) — this is what tells us the
  op finished.
- An `operation_handle_t*` (immediately, by return value) — this is
  what tells the driver to cancel.

In `NativeCosmosClient.RunSingletonAsync`:

```csharp
opHandle = builder.Submit(this.driver, this.cq.Handle, userData,
                          OperationBucket.Singleton, out preError);

CancellationTokenRegistration ctr = default;
if (ct.CanBeCanceled)
{
    ctr = ct.Register(static state =>
    {
        cosmos_operation_handle_cancel((IntPtr)state!);
    }, opHandle);
}

op.Tcs.Task.ContinueWith(static (_, state) =>
{
    var st = (Tuple<IntPtr, CancellationTokenRegistration>)state!;
    st.Item2.Dispose();                       // synchronously unwires the cancel callback
    cosmos_operation_handle_free(st.Item1);   // then it's safe to free the handle
}, Tuple.Create(opHandle, ctr), TaskContinuationOptions.ExecuteSynchronously);
```

The CT registration captures the **op handle** (not anything managed).
When the user cancels, the registered delegate fires
`cosmos_operation_handle_cancel(opHandle)` on whatever thread triggered
the cancel.

The Rust side then has two options:

- The op hadn't started its critical section yet → it posts a completion
  with outcome `Cancelled`. Pump dispatches it, TCS becomes Canceled,
  awaiter sees `TaskCanceledException`.
- The op already finished → cancel is a no-op. The completion that
  arrives is `Ok` (or `Error`). Awaiter sees the normal result.

**Both outcomes are legal** — there's an inherent race between the
caller cancelling and the wire completing. F4 in the F-check harness
asserts exactly that (TaskCanceled OR natural-completion is accepted
on 100 trials).

The `ContinueWith` at the bottom is the cleanup: when the Task settles
(any outcome), we unregister the CT and free the op handle. The
GCHandle around `NativeAsyncOperation` was already freed by the pump
inside `DispatchCompletion` — this continuation only owns the op handle
and the CT registration.

### Pre-flight rollback — who frees the GCHandle when Submit fails?

There is exactly one path where the pump will **not** see a completion:
when `cosmos_driver_execute_*_submit` returns `NULL` (pre-flight reject
with `out_pre_error` populated). In that case the submitter
(`RunSingletonAsync`) owns the cleanup:

```csharp
if (opHandle == IntPtr.Zero)
{
    GCHandle.FromIntPtr(userData).Free();     // pump won't see a completion → we free
    op.Tcs.TrySetException(new InvalidOperationException(
        $"submit pre-flight rejected: {preError} (op#{op.Id})"));
    return op.Tcs.Task;
}
```

This is the only Free outside the pump. Everywhere else,
`DispatchCompletion` owns it.

### Shutdown — let Rust drain, then let the pump finish

Dispose is "polite":

```csharp
public void Dispose()
{
    if (Interlocked.Exchange(ref this.disposed, 1) != 0) return;
    this.pumpThread.Join(TimeSpan.FromSeconds(5));
    cosmos_cq_free(this.cqHandle);
}
```

Setting the flag, the pump's next `cosmos_cq_wait` returns (within one
200 ms window). It sees `disposed != 0`, falls into
`cosmos_cq_shutdown(...)` + `DrainAfterShutdown()`. The Rust contract
for shutdown is to post completions for every in-flight op (typically
with `Cancelled` outcome), so `DrainAfterShutdown` keeps calling
`cosmos_cq_wait` (with a tight 50 ms timeout) and dispatching every
completion it sees — settling TCSes and freeing GCHandles via the
normal path. Once `cosmos_cq_wait` returns NULL with a terminal CQ
state, the pump exits, `Join` completes, and we free the CQ.

Awaiters whose ops were in flight at Dispose time wake up with whatever
outcome Rust posted on shutdown (usually `TaskCanceledException`),
never hang. The deliberate trade-off vs the old map-based drain: we
trust the Rust contract instead of maintaining a parallel registry. If
that contract is ever broken on the Rust side we'd see GCHandle table
growth across client churn and add an outstanding-handles set here as
defense in depth.

### Why one CQ per client (today)?

`NativeCosmosClient` constructs one `CompletionQueueLoop` in step 7 of
its build. That's sufficient for single-item CRUD throughput up to many
thousands of ops/sec — the bottleneck is always the wire latency, not
the pump.

If we later want to scale to per-partition or per-region CQs (so
different traffic classes don't share queue space), the design accommodates
it cleanly: `CompletionQueueLoop` is constructed from an `IntPtr runtime`
plus optional `CosmosCqOptions`. You just instantiate more. Each pump
owns its own slice of GCHandle entries, so there is no cross-talk.

---

## How CRUD methods actually flow (NativeCosmosClient.RunSingletonAsync)

```csharp
private Task<CosmosNativeResponse> RunSingletonAsync(
    Action<CosmosOperationRequestBuilder> configure, CancellationToken ct)
{
    var op = new NativeAsyncOperation();                              // 1

    if (ct.IsCancellationRequested)                                   // 2
    { op.Tcs.TrySetCanceled(ct); return op.Tcs.Task; }

    IntPtr userData = op.AllocateUserData();                          // 3

    IntPtr opHandle;
    CosmosErrorCode preError;
    using (var builder = new CosmosOperationRequestBuilder())
    {
        try { configure(builder); }                                   // 4
        catch (Exception ex)
        {
            GCHandle.FromIntPtr(userData).Free();                     // 4a
            op.Tcs.TrySetException(ex);
            return op.Tcs.Task;
        }

        opHandle = builder.Submit(                                    // 5
            this.driver, this.cq.Handle, userData,
            OperationBucket.Singleton, out preError);
    }

    if (opHandle == IntPtr.Zero)                                      // 6
    {
        GCHandle.FromIntPtr(userData).Free();
        op.Tcs.TrySetException(new InvalidOperationException(...));
        return op.Tcs.Task;
    }

    CancellationTokenRegistration ctr = default;                      // 7
    if (ct.CanBeCanceled)
        ctr = ct.Register(static s => cosmos_operation_handle_cancel((IntPtr)s!), opHandle);

    op.Tcs.Task.ContinueWith(static (_, state) =>                     // 8
    {
        var st = (Tuple<IntPtr, CancellationTokenRegistration>)state!;
        st.Item2.Dispose();
        cosmos_operation_handle_free(st.Item1);
    }, Tuple.Create(opHandle, ctr), TaskContinuationOptions.ExecuteSynchronously);

    return op.Tcs.Task;
}
```

Per call:

1. **Allocate the boxed payload** (`NativeAsyncOperation`) — gives us
   the `Id` for diagnostics and the `Tcs` for the awaiter (built with
   `RunContinuationsAsynchronously`).
2. **Fast-path early-cancel.** Don't even ask Rust to do work if the
   caller already gave up.
3. **Allocate the `GCHandle`** rooting `op` and grab its `IntPtr` — this
   is the cookie Rust will round-trip. The CLR's GCHandle table is now
   carrying the routing.
4. **Build the operation request** via the supplied configure action.
   The builder owns the lifetime of every borrowed pointer (UTF-8
   strings via `Marshal.StringToCoTaskMemUTF8`, body bytes via pinned
   `GCHandle`, options sub-struct pinned in place). If `configure`
   throws (4a), we roll back the GCHandle ourselves before failing the
   TCS — no completion is coming.
5. **Submit.** The wrapper deep-copies all borrowed pointers before
   returning, so the builder is safe to dispose on `using` exit. The
   submit returns the `operation_handle` immediately; the actual
   completion arrives later via the CQ.
6. **Pre-flight rollback.** If submit returned NULL, Rust rejected
   before queuing the op — no completion will arrive, so we own the
   GCHandle free.
7. **Wire CT** → cancel pokes `cosmos_operation_handle_cancel`.
8. **Wire cleanup ContinueWith** → when the Task settles (any outcome),
   the CT registration is unregistered and the op handle is freed. The
   `GCHandle` around `NativeAsyncOperation` was already freed by the
   pump in `DispatchCompletion`; this continuation only owns the op
   handle and the CT registration.

Return the Task. Done. The caller is now `await`ing; the pump will
eventually deliver the result.

---

## CosmosNativeException — the error story

When the pump dispatches a completion with outcome `Error`, it builds a
`CosmosNativeException`. The constructor **copies every field out of
native memory** in one shot:

- `HttpStatusCode`, `SubStatus`, `IsFromWire`, `RetryAfterMs` — primitives.
- `ActivityId`, `SessionToken`, `ETag`, `Backtrace` — UTF-8 strings copied
  via `PtrToUtf8`.
- `CoarseCode` — the FFI's discriminator (e.g. `NotFound = 2404`).

After construction the exception holds no live pointer into Rust memory.
You can keep it, log it, hand it to a retry policy. The native side is
freed by the pump immediately after construction returns.

The convenience predicates (`IsNotFound`, `IsConflict`, `IsThrottled`,
`IsTransient`, …) are **derived from `CoarseCode`**, not from per-error
ABI predicates — the spec-draft predicates like `cosmos_error_is_*` were
dropped between spec and PR #4515. Doing the classification in managed
code keeps us robust against the FFI changing its mind about exposing
them.

---

## File map

| File | What it is |
|---|---|
| `NativeMethods.cs` | Raw P/Invoke surface. ~45 fns, each tagged with `azurecosmosdriver.h` line range. No logic. |
| `NativeAsyncOperation.cs` | The boxed `{ Id, Tcs }` payload that gets GCHandle-rooted as `user_data`. Routing primitive. |
| `CompletionQueueLoop.cs` | The pump. One thread, one CQ, GCHandle-based dispatch, idempotent dispose. |
| `CosmosOperationRequestBuilder.cs` | Fluent staging of every borrowed pointer for one submit call. Owns the lifetime contract. |
| `NativeCosmosClient.cs` | Object-graph owner. Staged-build ctor, `*ItemAsync` methods, CT→cancel wiring, leak-free cleanup. |
| `CosmosNativeException.cs` | Strongly-typed rich error (CoarseCode + HTTP + sub-status + retry-after + …). All fields copied at construction. |
| `Program.cs` | F-check harness (default) + `-- crud` dispatcher to `Samples/CrudSample`. |
| `Samples/CrudSample.cs` | V3/Rust-sample-style CREATE→READ→REPLACE→READ→DELETE walk-through. |

## Build & run

```powershell
# Build (warns once if azurecosmosdriver.dll isn't on the probing path).
dotnet build .\tools\Microsoft.Azure.Cosmos.NativeDriverPoc\

# If you don't have the DLL yet:
pwsh .\tools\Microsoft.Azure.Cosmos.NativeDriverPoc\scripts\build-native-dll.ps1

# F1-F5 harness (default) — runs against the emulator at https://localhost:8081/
dotnet run --project .\tools\Microsoft.Azure.Cosmos.NativeDriverPoc

# CRUD sample (env-var driven; falls back to emulator if not set)
dotnet run --project .\tools\Microsoft.Azure.Cosmos.NativeDriverPoc -- crud
```

## Source-of-truth references

Every signature in `NativeMethods.cs` mirrors a line range in
`include/azurecosmosdriver.h` from PR #4515. The header is cached at
`Q:\src\.poc-artifacts\pr4515\azurecosmosdriver.h` and the DLL at
`Q:\src\.poc-artifacts\azurecosmosdriver\`. Re-running cbindgen on the
crate regenerates the header; we hand-port to .NET to keep per-function
contract comments inline.

## F-checks (what `dotnet run` proves)

| | Check | What it validates |
|---|---|---|
| F1 | Single read returns 200 + seeded body marker | Whole layer cake end-to-end on the happy path |
| F2 | 1000 submits, average < 100µs | `cosmos_driver_execute_singleton_operation_submit` is non-blocking — submit returns before the wire round-trip |
| F3 | 1000 concurrent reads on one pump complete in <5s | Pump scales horizontally over a single CQ |
| F4 | CT → `cosmos_operation_handle_cancel` honored on 100 trials | The "second handle" cancel model works; race with natural-completion is tolerated |
| F5 | Read non-existent item → `IsNotFound == true && HttpStatusCode == 404` | `CoarseCode` discriminator survives the Error path; predicates work |

F3 is the concurrency proof. F4 is the cancellation proof. F2 is the
"submit doesn't block" proof — the whole point of the FFI design.

---

## TL;DR

- The Rust DLL is **callback-free**. It posts completions to a queue;
  you drain.
- `CompletionQueueLoop` is a **single pump thread** that drains the
  queue, looks up which `TaskCompletionSource` is waiting (via a token
  → TCS dictionary), and fans the outcome onto it.
- Concurrency is **unbounded from the .NET side**: callers register
  tokens at O(1), the pump dispatches at O(1), and TCS continuations run
  on the threadpool (not the pump) thanks to
  `RunContinuationsAsynchronously`.
- Cancellation uses a **separate `operation_handle`** returned by
  submit. The pump never participates in cancellation.
- Memory is **copied at the FFI boundary** — no managed object holds a
  live pointer into Rust memory after dispatch returns.
- Disposal is **drain-then-free**: the pump's 200ms timeout doubles as
  the shutdown poll; in-flight ops fail with a clear exception, no
  hangs.
