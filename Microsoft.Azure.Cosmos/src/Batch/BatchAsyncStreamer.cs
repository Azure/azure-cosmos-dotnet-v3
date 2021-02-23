//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;

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
        private static readonly TimeSpan congestionControllerDelay = TimeSpan.FromMilliseconds(1000);
        private static readonly TimeSpan batchTimeout = TimeSpan.FromMilliseconds(100);

        private readonly object dispatchLimiter = new object();
        private readonly int maxBatchOperationCount;
        private readonly int maxBatchByteSize;
        private readonly BatchAsyncBatcherExecuteDelegate executor;
        private readonly BatchAsyncBatcherRetryDelegate retrier;
        private readonly CosmosSerializerCore serializerCore;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private readonly int congestionIncreaseFactor = 1;
        private readonly int congestionDecreaseFactor = 5;
        private readonly int maxDegreeOfConcurrency;
        private readonly TimerWheel timerWheel;
        private readonly SemaphoreSlim limiter;
        private readonly BatchPartitionMetric oldPartitionMetric;
        private readonly BatchPartitionMetric partitionMetric;

        private volatile BatchAsyncBatcher currentBatcher;
        private TimerWheelTimer currentTimer;
        private Task timerTask;

        private TimerWheelTimer congestionControlTimer;
        private Task congestionControlTask;

        private int congestionDegreeOfConcurrency = 1;
        private long congestionWaitTimeInMilliseconds = 1000;

        public BatchAsyncStreamer(
            int maxBatchOperationCount,
            int maxBatchByteSize,
            TimerWheel timerWheel,
            SemaphoreSlim limiter,
            int maxDegreeOfConcurrency,
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

            if (limiter == null)
            {
                throw new ArgumentNullException(nameof(limiter));
            }

            if (maxDegreeOfConcurrency < 1)
            {
                throw new ArgumentNullException(nameof(maxDegreeOfConcurrency));
            }

            this.maxBatchOperationCount = maxBatchOperationCount;
            this.maxBatchByteSize = maxBatchByteSize;
            this.executor = executor;
            this.retrier = retrier;
            this.timerWheel = timerWheel;
            this.serializerCore = serializerCore;
            this.currentBatcher = this.CreateBatchAsyncBatcher();
            this.ResetTimer();

            this.limiter = limiter;
            this.oldPartitionMetric = new BatchPartitionMetric();
            this.partitionMetric = new BatchPartitionMetric();
            this.maxDegreeOfConcurrency = maxDegreeOfConcurrency;

            this.StartCongestionControlTimer();
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
                _ = toDispatch.DispatchAsync(this.partitionMetric, this.cancellationTokenSource.Token);
            }
        }

        public void Dispose()
        {
            this.cancellationTokenSource.Cancel();
            this.cancellationTokenSource.Dispose();

            this.currentTimer.CancelTimer();
            this.currentTimer = null;
            this.timerTask = null;

            if (this.congestionControlTimer != null)
            {
                this.congestionControlTimer.CancelTimer();
                this.congestionControlTimer = null;
                this.congestionControlTask = null;
            }
        }

        private void ResetTimer()
        {
            this.currentTimer = this.timerWheel.CreateTimer(BatchAsyncStreamer.batchTimeout);
            this.timerTask = this.currentTimer.StartTimerAsync().ContinueWith((task) =>
            {
                if (task.IsCompleted)
                {
                    this.DispatchTimer();
                }
            }, this.cancellationTokenSource.Token);
        }

        private void StartCongestionControlTimer()
        {
            this.congestionControlTimer = this.timerWheel.CreateTimer(BatchAsyncStreamer.congestionControllerDelay);
            this.congestionControlTask = this.congestionControlTimer.StartTimerAsync().ContinueWith(async (task) =>
            {
                await this.RunCongestionControlAsync();
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
                _ = toDispatch.DispatchAsync(this.partitionMetric, this.cancellationTokenSource.Token);
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

        private async Task RunCongestionControlAsync()
        {
            while (!this.cancellationTokenSource.Token.IsCancellationRequested)
            {
                long elapsedTimeInMilliseconds = this.partitionMetric.TimeTakenInMilliseconds - this.oldPartitionMetric.TimeTakenInMilliseconds;

                if (elapsedTimeInMilliseconds >= this.congestionWaitTimeInMilliseconds)
                {
                    long diffThrottle = this.partitionMetric.NumberOfThrottles - this.oldPartitionMetric.NumberOfThrottles;
                    long changeItemsCount = this.partitionMetric.NumberOfItemsOperatedOn - this.oldPartitionMetric.NumberOfItemsOperatedOn;
                    this.oldPartitionMetric.Add(changeItemsCount, elapsedTimeInMilliseconds, diffThrottle);

                    if (diffThrottle > 0)
                    {
                        // Decrease should not lead to degreeOfConcurrency 0 as this will just block the thread here and no one would release it.
                        int decreaseCount = Math.Min(this.congestionDecreaseFactor, this.congestionDegreeOfConcurrency / 2);

                        // We got a throttle so we need to back off on the degree of concurrency.
                        for (int i = 0; i < decreaseCount; i++)
                        {
                            await this.limiter.WaitAsync(this.cancellationTokenSource.Token);
                        }

                        this.congestionDegreeOfConcurrency -= decreaseCount;

                        // In case of throttling increase the wait time, so as to converge max degreeOfConcurrency
                        this.congestionWaitTimeInMilliseconds += 1000;
                    }

                    if (changeItemsCount > 0 && diffThrottle == 0)
                    {
                        if (this.congestionDegreeOfConcurrency + this.congestionIncreaseFactor <= this.maxDegreeOfConcurrency)
                        {
                            // We aren't getting throttles, so we should bump up the degree of concurrency.
                            this.limiter.Release(this.congestionIncreaseFactor);
                            this.congestionDegreeOfConcurrency += this.congestionIncreaseFactor;
                        }
                    }
                }
                else
                {
                    break;
                }
            }

            this.StartCongestionControlTimer();
        }
    }
}
