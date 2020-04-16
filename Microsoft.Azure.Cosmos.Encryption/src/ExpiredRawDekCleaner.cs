//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Collections;

    internal class ExpiredRawDekCleaner : IDisposable
    {
        private readonly PriorityQueue<InMemoryRawDek> InMemoryRawDeks;

        private readonly int minimumDispatchTimerInSeconds = 1;
        private readonly int iterationDelayInSeconds = 60;
        private readonly TimeSpan bufferTimeAfterExpiry = TimeSpan.FromSeconds(60);
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly TimerPool timerPool;
        private PooledTimer pooledTimer;
        private Task continuationTask;

        private bool isDisposed = false;

        public ExpiredRawDekCleaner(int? iterationDelayInSeconds = null, TimeSpan? bufferTimeAfterExpiry = null)
        {
            if (iterationDelayInSeconds.HasValue)
            {
                this.iterationDelayInSeconds = iterationDelayInSeconds.Value;
            }

            if (bufferTimeAfterExpiry.HasValue)
            {
                this.bufferTimeAfterExpiry = bufferTimeAfterExpiry.Value;
            }

            this.timerPool = new TimerPool(this.minimumDispatchTimerInSeconds);
            this.InMemoryRawDeks = new PriorityQueue<InMemoryRawDek>(new InMemoryRawDekExpiryComparer(), true);
            this.StartCleanupProcess();
        }

        private void StartCleanupProcess()
        {
            this.pooledTimer = this.timerPool.GetPooledTimer(this.iterationDelayInSeconds);
            this.continuationTask = this.pooledTimer.StartTimerAsync().ContinueWith((task) =>
            {
                this.RunCleanup();
            }, this.cancellationTokenSource.Token);
        }

        private void RunCleanup()
        {
            while (!this.cancellationTokenSource.Token.IsCancellationRequested)
            {
                while (this.InMemoryRawDeks.TryPeek(out InMemoryRawDek inMemoryRawDek))
                {
                    if (inMemoryRawDek.RawDekExpiry + this.bufferTimeAfterExpiry <= DateTime.UtcNow)
                    {
                        inMemoryRawDek.DataEncryptionKey.Dispose();
                        this.InMemoryRawDeks.Dequeue();
                    }
                }
            }

            this.StartCleanupProcess();
        }

        internal void EnqueueInMemoryRawDek(InMemoryRawDek inMemoryRawDek)
        {
            this.InMemoryRawDeks.Enqueue(inMemoryRawDek);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                if (disposing)
                {
                    this.pooledTimer.CancelTimer();
                    this.pooledTimer = null;
                    this.continuationTask = null;

                    this.InMemoryRawDeks.Clear();
                }

                this.isDisposed = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            this.Dispose(true);
        }
    }
}
