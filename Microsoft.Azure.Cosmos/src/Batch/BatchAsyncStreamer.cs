//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Handles operation queueing and dispatching. 
    /// </summary>
    /// <remarks>
    /// <see cref="AddAsync(BatchAsyncOperationContext)"/> will add the operation to the current batcher or if full, dispatch it, create a new one and add the operation to it.
    /// </remarks>
    /// <seealso cref="BatchAsyncBatcher"/>
    internal class BatchAsyncStreamer : IDisposable
    {
        private readonly List<Task> previousDispatchedTasks = new List<Task>();
        private readonly SemaphoreSlim dispatchLimiter;
        private readonly int maxBatchOperationCount;
        private readonly int maxBatchByteSize;
        private readonly Func<IReadOnlyList<BatchAsyncOperationContext>, CancellationToken, Task<PartitionKeyBatchResponse>> executor;
        private readonly int dispatchTimerInSeconds;
        private readonly CosmosSerializer CosmosSerializer;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private BatchAsyncBatcher currentBatcher;
        private TimerPool timerPool;
        private PooledTimer currentTimer;
        private Task timerTask;
        private bool disposed;

        public BatchAsyncStreamer(
            int maxBatchOperationCount,
            int maxBatchByteSize,
            int dispatchTimerInSeconds,
            TimerPool timerPool,
            CosmosSerializer cosmosSerializer,
            Func<IReadOnlyList<BatchAsyncOperationContext>, CancellationToken, Task<PartitionKeyBatchResponse>> executor)
        {
            if (maxBatchOperationCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBatchOperationCount));
            }

            if (maxBatchByteSize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBatchByteSize));
            }

            if (dispatchTimerInSeconds < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(dispatchTimerInSeconds));
            }

            if (executor == null)
            {
                throw new ArgumentNullException(nameof(executor));
            }

            if (cosmosSerializer == null)
            {
                throw new ArgumentNullException(nameof(cosmosSerializer));
            }

            this.maxBatchOperationCount = maxBatchOperationCount;
            this.maxBatchByteSize = maxBatchByteSize;
            this.executor = executor;
            this.dispatchTimerInSeconds = dispatchTimerInSeconds;
            this.timerPool = timerPool;
            this.CosmosSerializer = cosmosSerializer;
            this.dispatchLimiter = new SemaphoreSlim(1, 1);
            this.currentBatcher = this.CreateBatchAsyncBatcher();

            this.StartTimer();
        }

        public async Task AddAsync(BatchAsyncOperationContext context)
        {
            if (!await this.currentBatcher.TryAddAsync(context))
            {
                // Batcher is full
                BatchAsyncBatcher toDispatch = await this.GetBatchToDispatchAndCreateAsync();
                this.previousDispatchedTasks.Add(toDispatch.DispatchAsync(this.cancellationTokenSource.Token));
                bool addedContext = await this.currentBatcher.TryAddAsync(context);
                Debug.Assert(addedContext, "Could not add context to batcher.");
            }
        }

        public void Dispose()
        {
            this.disposed = true;
            this.cancellationTokenSource.Cancel();
            this.currentBatcher?.Dispose();
            this.currentTimer.CancelTimer();
            foreach (Task previousDispatchedTask in this.previousDispatchedTasks)
            {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
                previousDispatchedTask.GetAwaiter().GetResult();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
            }
        }

        private void StartTimer()
        {
            this.currentTimer = this.timerPool.GetPooledTimer(this.dispatchTimerInSeconds);
            this.timerTask = this.currentTimer.StartTimerAsync().ContinueWith((task) =>
            {
                return this.DispatchTimerAsync();
            });
        }

        private async Task DispatchTimerAsync()
        {
            if (this.disposed)
            {
                return;
            }

            BatchAsyncBatcher toDispatch = await this.GetBatchToDispatchAndCreateAsync();
            if (toDispatch != null)
            {
                this.previousDispatchedTasks.Add(toDispatch.DispatchAsync(this.cancellationTokenSource.Token));
            }

            this.StartTimer();
        }

        private async Task<BatchAsyncBatcher> GetBatchToDispatchAndCreateAsync()
        {
            if (this.currentBatcher.IsEmpty)
            {
                return null;
            }

            await this.dispatchLimiter.WaitAsync(this.cancellationTokenSource.Token);
            try
            {
                BatchAsyncBatcher previousBatcher = this.currentBatcher;
                this.currentBatcher = this.CreateBatchAsyncBatcher();
                return previousBatcher;
            }
            finally
            {
                this.dispatchLimiter.Release();
            }
        }

        private BatchAsyncBatcher CreateBatchAsyncBatcher()
        {
            return new BatchAsyncBatcher(this.maxBatchOperationCount, this.maxBatchByteSize, this.CosmosSerializer, this.executor);
        }
    }
}
