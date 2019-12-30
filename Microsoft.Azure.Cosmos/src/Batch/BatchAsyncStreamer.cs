//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Handles operation queueing and dispatching.
    /// Fills batches efficiently and maintains a timer for early dispatching in case of partially-filled batches and to optimize for throughput.
    /// </summary>
    /// <remarks>
    /// There is always one batch at a time being filled. Locking is in place to avoid concurrent threads trying to Add operations while the timer might be Dispatching the current batch.
    /// The current batch is dispatched and a new one is readied to be filled by new operations, the dispatched batch runs independently through a fire and forget pattern.
    /// </remarks>
    /// <seealso cref="BatchAsyncBatcher"/>
    internal class BatchAsyncStreamer : IDisposable
    {
        private readonly object dispatchLimiter = new object();
        private readonly int maxBatchOperationCount;
        private readonly int maxBatchByteSize;
        private readonly BatchAsyncBatcherExecuteDelegate executor;
        private readonly BatchAsyncBatcherRetryDelegate retrier;
        private readonly int dispatchTimerInSeconds;
        private readonly CosmosSerializerCore serializerCore;
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
            CosmosSerializerCore serializerCore,
            BatchAsyncBatcherExecuteDelegate executor,
            BatchAsyncBatcherRetryDelegate retrier)
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

            if (retrier == null)
            {
                throw new ArgumentNullException(nameof(retrier));
            }

            if (serializerCore == null)
            {
                throw new ArgumentNullException(nameof(serializerCore));
            }

            this.maxBatchOperationCount = maxBatchOperationCount;
            this.maxBatchByteSize = maxBatchByteSize;
            this.executor = executor;
            this.retrier = retrier;
            this.dispatchTimerInSeconds = dispatchTimerInSeconds;
            this.timerPool = timerPool;
            this.serializerCore = serializerCore;
            this.currentBatcher = this.CreateBatchAsyncBatcher();

            this.ResetTimer();
        }

        public void Add(ItemBatchOperation operation)
        {
            BatchAsyncBatcher toDispatch = null;
            lock (this.dispatchLimiter)
            {
                while (!this.currentBatcher.TryAdd(operation))
                {
                    // Batcher is full
                    toDispatch = this.GetBatchToDispatchAndCreate();
                }
            }

            if (toDispatch != null)
            {
                // Discarded for Fire & Forget
                _ = toDispatch.DispatchAsync(this.cancellationTokenSource.Token);
            }
        }

        public void Dispose()
        {
            this.cancellationTokenSource.Cancel();
            this.cancellationTokenSource.Dispose();
            this.currentTimer.CancelTimer();
            this.currentTimer = null;
            this.timerTask = null;
        }

        private void ResetTimer()
        {
            this.currentTimer = this.timerPool.GetPooledTimer(this.dispatchTimerInSeconds);
            this.timerTask = this.currentTimer.StartTimerAsync().ContinueWith((task) =>
            {
                this.DispatchTimer();
            }, this.cancellationTokenSource.Token);
        }

        private void DispatchTimer()
        {
            if (this.cancellationTokenSource.IsCancellationRequested)
            {
                return;
            }

            BatchAsyncBatcher toDispatch;
            lock (this.dispatchLimiter)
            {
                toDispatch = this.GetBatchToDispatchAndCreate();
            }

            if (toDispatch != null)
            {
                // Discarded for Fire & Forget
                _ = toDispatch.DispatchAsync(this.cancellationTokenSource.Token);
            }

            this.ResetTimer();
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
            return new BatchAsyncBatcher(this.maxBatchOperationCount, this.maxBatchByteSize, this.serializerCore, this.executor, this.retrier);
        }
    }
}
