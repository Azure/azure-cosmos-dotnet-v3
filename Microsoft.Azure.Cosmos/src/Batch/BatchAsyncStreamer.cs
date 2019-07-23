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
    using Microsoft.Azure.Cosmos.Internal;
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
        private readonly SemaphoreSlim addLimiter;
        private readonly int maxBatchOperationCount;
        private readonly int maxBatchByteSize;
        private readonly Func<IReadOnlyList<BatchAsyncOperationContext>, CancellationToken, Task<CrossPartitionKeyBatchResponse>> executor;
        private readonly int dispatchTimerInSeconds;
        private readonly CosmosSerializer CosmosSerializer;
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
            Func<IReadOnlyList<BatchAsyncOperationContext>, CancellationToken, Task<CrossPartitionKeyBatchResponse>> executor)
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
            this.addLimiter = new SemaphoreSlim(1, 1);
            this.currentBatcher = this.GetBatchAsyncBatcher();

            this.StartTimer();
        }

        public Task<BatchOperationResult> AddAsync(BatchAsyncOperationContext context)
        {
            BatchAsyncBatcher toDispatch = null;
#pragma warning disable VSTHRD103 // Call async methods when in an async method
            this.addLimiter.Wait();
#pragma warning restore VSTHRD103 // Call async methods when in an async method

            try
            {
                if (!this.currentBatcher.TryAdd(context))
                {
                    // Batcher is full
                    toDispatch = this.GetBatchToDispatch();
                    Debug.Assert(this.currentBatcher.TryAdd(context), "Could not add context to batcher.");
                }
            }
            finally
            {
                this.addLimiter.Release();
            }

            if (toDispatch != null)
            {
                this.previousDispatchedTasks.Add(toDispatch.DispatchAsync());
            }

            return context.Task;
        }

        public void Dispose()
        {
            this.disposed = true;
            this.addLimiter?.Dispose();
            this.currentBatcher?.Dispose();

            foreach (Task previousDispatch in this.previousDispatchedTasks)
            {
                try
                {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
                    previousDispatch.GetAwaiter().GetResult();
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
                }
                catch
                {
                    // Any internal exceptions are disregarded during dispose
                }
            }

            this.currentTimer.CancelTimer();
        }

        private void StartTimer()
        {
            this.currentTimer = this.timerPool.GetPooledTimer(this.dispatchTimerInSeconds);
            this.timerTask = this.currentTimer.StartTimerAsync().ContinueWith((task) =>
            {
                this.DispatchTimer();
            });
        }

        private void DispatchTimer()
        {
            if (this.disposed)
            {
                return;
            }

            this.addLimiter.Wait();

            BatchAsyncBatcher toDispatch;
            try
            {
                toDispatch = this.GetBatchToDispatch();
            }
            finally
            {
                this.addLimiter.Release();
            }

            if (toDispatch != null)
            {
                this.previousDispatchedTasks.Add(toDispatch.DispatchAsync());
            }

            this.StartTimer();
        }

        private BatchAsyncBatcher GetBatchToDispatch()
        {
            if (this.currentBatcher.IsEmpty)
            {
                return null;
            }

            BatchAsyncBatcher previousBatcher = this.currentBatcher;
            this.currentBatcher = this.GetBatchAsyncBatcher();
            return previousBatcher;
        }

        private BatchAsyncBatcher GetBatchAsyncBatcher()
        {
            return new BatchAsyncBatcher(this.maxBatchOperationCount, this.maxBatchByteSize, this.CosmosSerializer, this.executor);
        }
    }
}
