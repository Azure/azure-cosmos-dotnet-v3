// Copyright (c) Microsoft Corporation.  All rights reserved.

namespace Microsoft.Azure.Cosmos.NativeAsyncPoc
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using static Microsoft.Azure.Cosmos.NativeAsyncPoc.NativeMethods;

    /// <summary>
    /// Friendly TaskCompletionSource-backed wrapper over the raw P/Invoke
    /// surface. This is the API a real .NET SDK would expose to user code —
    /// it returns <c>Task&lt;RawResponse&gt;</c> and never blocks the caller.
    /// </summary>
    /// <remarks>
    /// The TCS is constructed with <see cref="TaskCreationOptions.RunContinuationsAsynchronously"/>
    /// so .NET continuations don't piggyback on the CQ pump thread (mirrors
    /// the V3 SDK Rntbd2 ReceiveLoop pattern — keep transport threads doing
    /// transport, run user continuations on the thread pool).
    /// </remarks>
    public sealed class NativeCosmosClient : IDisposable
    {
        private readonly IntPtr runtimeHandle;
        private readonly IntPtr driverHandle;
        private readonly CompletionQueueLoop cqLoop;
        private int disposed;

        public NativeCosmosClient(string endpoint, string masterKey, uint workerThreads = 0)
        {
            this.runtimeHandle = cosmos_runtime_new(workerThreads);
            if (this.runtimeHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("cosmos_runtime_new returned NULL");
            }

            try
            {
                CosmosStatus rc = cosmos_driver_new(
                    this.runtimeHandle, endpoint, masterKey, out IntPtr driver);
                if (rc != CosmosStatus.Ok || driver == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"cosmos_driver_new failed: {rc}");
                }
                this.driverHandle = driver;
                this.cqLoop = new CompletionQueueLoop();
            }
            catch
            {
                cosmos_runtime_free(this.runtimeHandle);
                throw;
            }
        }

        public CompletionQueueLoop CompletionQueue => this.cqLoop;

        /// <summary>
        /// Reads an item asynchronously. Returns a task that completes when
        /// the dedicated CQ-pump thread receives the corresponding completion
        /// from the Rust runtime.
        /// </summary>
        /// <param name="cancellationToken">
        /// Cancels the in-flight operation by invoking <c>cosmos_cancel</c>.
        /// The task transitions to Canceled when the Rust task observes the
        /// abort and emits its cancelled completion.
        /// </param>
        public Task<RawResponse> ReadItemAsync(
            string databaseId,
            string containerId,
            string partitionKey,
            string itemId,
            CancellationToken cancellationToken = default)
        {
            // RunContinuationsAsynchronously is critical: if user continuations
            // run inline on TrySetResult, they'd run on the CQ pump thread
            // and block subsequent completions. This matches what the V3 SDK
            // does inside Rntbd2 (`new TaskCompletionSource<...>(RunContinuationsAsynchronously)`).
            var tcs = new TaskCompletionSource<RawResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            ulong token = this.cqLoop.Register(tcs);

            CosmosStatus rc = cosmos_read_item(
                this.driverHandle,
                databaseId,
                containerId,
                partitionKey,
                itemId,
                this.cqLoop.Handle,
                (UIntPtr)token,
                out IntPtr opHandle);

            if (rc != CosmosStatus.Ok)
            {
                // Submission itself failed — we never enqueued anything, so
                // the pump won't see a completion. Fail the TCS synchronously.
                tcs.TrySetException(
                    new InvalidOperationException($"cosmos_read_item submit failed: {rc}"));
                return tcs.Task;
            }

            // Wire cancellation: cosmos_cancel flips the cancel flag and
            // aborts the Tokio task; the Rust side then emits a Cancelled
            // completion, which the pump translates to TCS.TrySetCanceled.
            // We release the op handle in a continuation so the host's
            // strong reference dies only after the completion is observed.
            CancellationTokenRegistration ctr = default;
            if (cancellationToken.CanBeCanceled)
            {
                ctr = cancellationToken.Register(static state =>
                {
                    cosmos_cancel((IntPtr)state!);
                }, opHandle);
            }

            tcs.Task.ContinueWith(static (_, state) =>
            {
                var st = (Tuple<IntPtr, CancellationTokenRegistration>)state!;
                st.Item2.Dispose();
                cosmos_op_release(st.Item1);
            }, Tuple.Create(opHandle, ctr), TaskContinuationOptions.ExecuteSynchronously);

            return tcs.Task;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.disposed, 1) != 0)
            {
                return;
            }
            this.cqLoop.Dispose();
            cosmos_driver_free(this.driverHandle);
            cosmos_runtime_free(this.runtimeHandle);
        }
    }
}
