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
    internal class BatchAsyncStreamer
    {
        private readonly List<Task> previousDispatchedTasks = new List<Task>();
        private readonly SemaphoreSlim dispatchLimiter;
        private readonly int maxBatchOperationCount;
        private readonly int maxBatchByteSize;
        private readonly Func<IReadOnlyList<BatchAsyncOperationContext>, CancellationToken, Task<PartitionKeyBatchResponse>> executor;
        private readonly int dispatchTimerInSeconds;
        private readonly CosmosSerializer cosmosSerializer;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private volatile BatchAsyncBatcher currentBatcher;
        private TimerPool timerPool;
        private PooledTimer currentTimer;
        private Task timerTask;

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
            this.cosmosSerializer = cosmosSerializer;
            this.dispatchLimiter = new SemaphoreSlim(1, 1);
            this.currentBatcher = this.CreateBatchAsyncBatcher();

            this.StartTimer();
        }

        public async Task AddAsync(BatchAsyncOperationContext context)
        {
            using (await this.dispatchLimiter.UsingWaitAsync(this.cancellationTokenSource.Token))
            {
                while (!this.currentBatcher.TryAdd(context))
                {
                    // Batcher is full
                    BatchAsyncBatcher toDispatch = this.GetBatchToDispatchAndCreate();
                    if (toDispatch != null)
                    {
                        this.previousDispatchedTasks.Add(toDispatch.DispatchAsync(this.cancellationTokenSource.Token));
                    }
                }
            }
        }

        public async Task DisposeAsync()
        {
            this.cancellationTokenSource.Cancel();
            this.cancellationTokenSource.Dispose();
            this.currentTimer.CancelTimer();
            foreach (Task previousDispatchedTask in this.previousDispatchedTasks)
            {
                await previousDispatchedTask;
            }

            this.dispatchLimiter.Dispose();
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
            if (this.cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            using (await this.dispatchLimiter.UsingWaitAsync(this.cancellationTokenSource.Token))
            {
                BatchAsyncBatcher toDispatch = this.GetBatchToDispatchAndCreate();
                if (toDispatch != null)
                {
                    this.previousDispatchedTasks.Add(toDispatch.DispatchAsync(this.cancellationTokenSource.Token));
                }
            }

            this.StartTimer();
        }

        private BatchAsyncBatcher GetBatchToDispatchAndCreate()
        {
            if (this.currentBatcher.IsEmpty)
            {
                return null;
            }

            BatchAsyncBatcher previousBatcher = this.currentBatcher;
            this.currentBatcher = this.CreateBatchAsyncBatcher();
            return previousBatcher;
        }

        private BatchAsyncBatcher CreateBatchAsyncBatcher()
        {
            return new BatchAsyncBatcher(this.maxBatchOperationCount, this.maxBatchByteSize, this.cosmosSerializer, this.executor);
        }
    }
}
