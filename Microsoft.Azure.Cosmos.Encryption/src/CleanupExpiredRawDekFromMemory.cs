//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Collections;

    internal class CleanupExpiredRawDekFromMemory : IDisposable
    {
        internal PriorityQueue<InMemoryRawDek> InMemoryRawDeks;

        private readonly int MinimumDispatchTimerInSeconds = 1;
        private readonly int IterationDelayInSeconds = 60;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly TimerPool timerPool;
        private PooledTimer pooledTimer;
        private Task continuationTask;

        private bool isDisposed = false;

        public CleanupExpiredRawDekFromMemory()
        {
            this.timerPool = new TimerPool(this.MinimumDispatchTimerInSeconds);
            this.InMemoryRawDeks = new PriorityQueue<InMemoryRawDek>(new InMemoryRawDekComparer(), true);
            this.StartCleanupProcess();
        }

        private void StartCleanupProcess()
        {
            this.pooledTimer = this.timerPool.GetPooledTimer(this.IterationDelayInSeconds);
            this.continuationTask = this.pooledTimer.StartTimerAsync().ContinueWith((task) =>
            {
                this.RunCleanUp();
            }, this.cancellationTokenSource.Token);
        }

        private void RunCleanUp()
        {
            while (!this.cancellationTokenSource.Token.IsCancellationRequested)
            {
                while (this.InMemoryRawDeks.TryPeek(out InMemoryRawDek inMemoryRawDek))
                {
                    if (inMemoryRawDek.ExpiryTime <= DateTime.UtcNow)
                    {
                        //inMemoryRawDek.DataEncryptionKey.RawKey.
                        //inMemoryRawDek.AlgorithmUsingRawDek.Dispose();
                        this.InMemoryRawDeks.Dequeue();
                    }
                }
            }

            this.StartCleanupProcess();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                if (disposing)
                {
                    this.cancellationTokenSource.Cancel();
                    this.cancellationTokenSource.Dispose();

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
