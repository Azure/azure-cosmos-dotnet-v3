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
   │  2. CompletionQueueLoop                                     │
   │     • One BG thread per CQ                                  │
   │     • Tokens ↔ TaskCompletionSource<CosmosNativeResponse>   │
   │     • Pumps cosmos_cq_wait → fans onto pending TCS          │
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
    private readonly IntPtr cqHandle;                                          // 1
    private readonly Thread pumpThread;                                        // 2
    private readonly ConcurrentDictionary<ulong, TaskCompletionSource<...>>    // 3
        pending;
    private long nextUserData;                                                 // 4
    private int disposed;                                                      // 5
}
```

1. **`cqHandle`** — the native CQ created via `cosmos_cq_create*`. Tied to
   one runtime. Released by `cosmos_cq_free` in `Dispose`.
2. **`pumpThread`** — exactly one background thread per CQ, named
   `"cosmos-native-driver-cq-pump"`. `IsBackground = true` so it doesn't
   keep the process alive. It does **nothing but `cosmos_cq_wait`** in a
   loop.
3. **`pending`** — the routing table. Maps a **token** (a 64-bit counter)
   to the `TaskCompletionSource` waiting on that op's result.
   `ConcurrentDictionary` because writes (Register) come from N caller
   threads while reads (DispatchCompletion) come from the single pump.
4. **`nextUserData`** — monotonic counter for tokens. `Interlocked.Increment`
   keeps it allocation-free and threadsafe.
5. **`disposed`** — `int` flag, mutated via `Interlocked.Exchange`. Both
   the pump's loop guard and the Dispose idempotency hinge on it.

### The token-as-user_data trick

The Rust FFI exposes `cosmos_driver_submit` with a `user_data: IntPtr`
parameter. Whatever you pass in is handed back to you on the matching
completion via `cosmos_completion_user_data`. The DLL never dereferences
it — it's just a 64-bit cookie.

**We use that cookie as a routing token, not a pointer.** That's the key
design choice:

```csharp
public IntPtr Register(TaskCompletionSource<CosmosNativeResponse> tcs, out ulong token)
{
    token = (ulong)Interlocked.Increment(ref this.nextUserData);
    this.pending[token] = tcs;
    return new IntPtr((long)token);   // pass to cosmos_driver_submit as user_data
}
```

Why a token and not a `GCHandle.ToIntPtr(handle)`? Three reasons:

1. **No pinning.** A `GCHandle` would pin the managed TCS for the
   duration. With tokens, the TCS is just an entry in a dictionary — GC
   moves it freely.
2. **Safety on stale completions.** If a completion ever arrives for a
   token we don't recognize (shutdown race, cancelled op, double-free
   bug in the driver), `TryRemove` returns false and we log + drop. With
   `GCHandle` a stale pointer would be a use-after-free.
3. **Cheap.** `Interlocked.Increment` + dict put is faster than allocating
   a `GCHandle`, and the routing table doubles as our "still pending"
   view for shutdown drain.

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
            this.DrainPendingOnShutdown();
            return;                                            // CQ drained or shutting down
        }
        try { this.DispatchCompletion(completion); }
        catch (Exception ex) { Console.Error.WriteLine($"[pump] dispatch error: {ex}"); }
        finally { cosmos_completion_free(completion); }
    }
    cosmos_cq_shutdown(this.cqHandle);
    this.DrainPendingOnShutdown();
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
header §720. We split: `Running` → re-arm; anything else → drain
pending TCSes and exit. This is the **single most contract-sensitive
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
    IntPtr userDataPtr = cosmos_completion_user_data(completion);
    ulong token = (ulong)userDataPtr.ToInt64();

    if (!this.pending.TryRemove(token, out var tcs))
    {
        Console.Error.WriteLine($"[pump] completion for unknown user_data 0x{token:X16} dropped");
        return;
    }

    CosmosCompletionOutcome outcome = cosmos_completion_outcome(completion);
    switch (outcome) { /* Ok | Error | Cancelled */ }
}
```

Three outcomes, three TCS endings:

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

### Why this enables unbounded concurrency from ONE pump thread

This is the part worth slowing down on.

Imagine 1,000 callers all do `await client.ReadItemAsync(...)` more or
less simultaneously. The lifecycle for ONE call looks like:

```
caller thread T1    pump thread P            CQ (native)
─────────────────   ──────────────────        ─────────────────
Register(tcs) ─────►  pending[7] = tcs
submit(op,...,7) ─────────────────────────►   queue ops, return op_handle
return Task<>       (caller awaits)

                    (Rust workers do I/O)

                                              ◄── posts completion(user_data=7)
                    cosmos_cq_wait() ─────────► returns completion ptr
                    user_data → 7
                    pending.TryRemove(7) → tcs
                    TrySetResult(...)
                                              ─── continuation resumes
                    cosmos_completion_free
                    loop
```

Now scale that to 1,000 concurrent calls. What changes?

- **The dictionary grows to 1,000 entries.** O(1) writes from caller
  threads. O(1) lookups from the pump.
- **The pump still does one `cosmos_cq_wait` at a time.** It pulls one
  completion, fans it onto the matching TCS, frees, loops. The TCS
  continuation runs on the **thread pool** (because we created the TCS
  with `RunContinuationsAsynchronously`) — not on the pump thread.
- **None of the 1,000 callers are blocked on the pump.** They returned
  from `submit` immediately. They're just `await`ing a `Task`.
- **The pump is never the bottleneck** as long as the per-completion
  work (dispatch + free) is faster than the inter-completion arrival
  rate. Empirically, dispatch is ~microseconds; even at 100K ops/sec
  the pump is loafing.

The critical design choice that makes this work:

> **`TaskCreationOptions.RunContinuationsAsynchronously`**

If we used the default (synchronous continuations), `tcs.TrySetResult`
would inline the awaiter's continuation onto the pump thread. With 1,000
awaiters, the pump would run 1,000 user callbacks sequentially before
draining the next completion. With the async option, `TrySetResult`
just schedules the continuation on the threadpool and returns — pump
keeps draining the queue at full speed. **The continuation runs in
parallel with the next dispatch.**

### What if the pump can't keep up?

If callers create completions faster than the pump drains them, the CQ
fills up. The Rust side bounds that with a configurable capacity. If
the cap is hit, the Rust workers backpressure (they don't post more
until headroom appears). Today we use `cosmos_cq_create_default` which
picks a sane bound; for true sustained high throughput you'd want
multiple CQs and multiple pumps (the `CompletionQueueLoop` class is
designed to be instantiated N times — though `NativeCosmosClient`
currently only creates one).

### Cancellation — the "second handle" model

The most subtle bit. A `CancellationToken` arrives on the caller side
in .NET-land, but the in-flight work lives in Rust-land — how do we
poke it?

The Rust FFI returns TWO things from `cosmos_driver_submit`:

- The completion (eventually, via the CQ) — this is what tells us the
  op finished.
- An `operation_handle_t*` (immediately, by return value) — this is
  what tells the driver to cancel.

In `NativeCosmosClient.RunOperationAsync`:

```csharp
IntPtr opHandle = cosmos_driver_submit(this.driver, op, IntPtr.Zero,
    this.cq.Handle, userData, out preError);

CancellationTokenRegistration ctr = default;
if (ct.CanBeCanceled)
{
    ctr = ct.Register(static state =>
    {
        cosmos_operation_handle_cancel((IntPtr)state!);
    }, opHandle);
}

tcs.Task.ContinueWith(static (_, state) =>
{
    var st = (Tuple<IntPtr, CancellationTokenRegistration>)state!;
    st.Item2.Dispose();
    cosmos_operation_handle_free(st.Item1);
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
(any outcome), we unregister the CT and free the op handle. That's what
keeps the design leak-free.

### Shutdown — the drain pattern

Dispose is "polite":

```csharp
public void Dispose()
{
    if (Interlocked.Exchange(ref this.disposed, 1) != 0) return;
    this.pumpThread.Join(TimeSpan.FromSeconds(5));
    cosmos_cq_free(this.cqHandle);
}
```

The set-and-check pattern makes it idempotent (subsequent calls are
no-ops). The `Join` gives the pump up to one timeout window plus one
final iteration to exit gracefully. The pump's loop sees `disposed != 0`
on its next iteration after the wait returns, falls into
`DrainPendingOnShutdown`, fails every still-pending TCS with a clear
exception message, and exits. Only THEN do we free the CQ handle.

Awaiters whose ops were in-flight at Dispose time get a clear
`InvalidOperationException: "Completion queue was shut down or drained"`
— never a hang.

### Why one CQ per client (today)?

`NativeCosmosClient` constructs one `CompletionQueueLoop` in step 7 of
its build. That's sufficient for single-item CRUD throughput up to many
thousands of ops/sec — the bottleneck is always the wire latency, not
the pump.

If we later want to scale to per-partition or per-region CQs (so
different traffic classes don't share queue space), the design accommodates
it cleanly: `CompletionQueueLoop` is constructed from an `IntPtr runtime`
plus optional `CosmosCqOptions`. You just instantiate more. The routing
table is per-loop, so there's no cross-talk between pumps.

---

## How CRUD methods actually flow (NativeCosmosClient.RunOperationAsync)

```csharp
private Task<CosmosNativeResponse> RunOperationAsync(
    OperationFactory factory, string? bodyJson, CancellationToken ct)
{
    var tcs = new TaskCompletionSource<CosmosNativeResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);          // 1

    if (ct.IsCancellationRequested)                                   // 2
    { tcs.TrySetCanceled(ct); return tcs.Task; }

    CosmosErrorCode rc = factory(out IntPtr op);                      // 3
    if (rc != Success || op == 0) { /* fail */ }

    if (bodyJson != null) { /* cosmos_operation_with_body */ }         // 4

    IntPtr userData = this.cq.Register(tcs, out ulong token);         // 5

    IntPtr opHandle = cosmos_driver_submit(                           // 6
        this.driver, op, 0, this.cq.Handle, userData, out preError);

    if (opHandle == 0) { /* unregister + fail */ }

    /* CT wiring + cleanup ContinueWith */                            // 7

    return tcs.Task;
}
```

Per call:

1. **Allocate a TCS** with async continuations. Cheap.
2. **Fast-path early-cancel.** Don't even ask Rust to do work if the
   caller already gave up.
3. **Build the operation** via a per-op factory (`read_item` /
   `create_item` / etc). The factory is the only thing that differs
   between CRUD methods; everything else is shared.
4. **Attach body** (writes only). Per header §1251 the wrapper copies
   the bytes into its own storage, so the managed `byte[]` is free to
   GC immediately.
5. **Register the TCS** into the routing table, get a token, cast token
   to `IntPtr` for the `user_data` slot.
6. **Submit.** Consumes the `op` on success per header §1764. Returns
   the `operation_handle` immediately; the actual completion arrives
   later via the CQ.
7. **Wire CT** → cancel pokes `cosmos_operation_handle_cancel`.
   **Wire cleanup** → when the Task settles (any way), the CT is
   unregistered and the op handle is freed.

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
| `CompletionQueueLoop.cs` | The pump. One thread, one CQ, token→TCS routing, idempotent dispose. |
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
| F2 | 1000 submits, average < 100µs | `cosmos_driver_submit` is non-blocking — submit returns before the wire round-trip |
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
