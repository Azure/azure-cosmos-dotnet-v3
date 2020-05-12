//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Collections;

    internal sealed class ExpiredRawDekCleaner : IDisposable
    {
        private readonly PriorityQueue<InMemoryRawDek> inMemoryRawDeks;
        private readonly int minimumDispatchTimerInSeconds = 1;
        private readonly TimeSpan iterationDelayInSeconds = TimeSpan.FromSeconds(60);
        private readonly TimeSpan bufferTimeAfterExpiry = TimeSpan.FromSeconds(60);
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly TimerPool timerPool;
        private PooledTimer pooledTimer;
        private Task continuationTask;
        private bool isDisposed = false;

        public ExpiredRawDekCleaner(
            TimeSpan? iterationDelayInSeconds = null,
            TimeSpan? bufferTimeAfterExpiry = null)
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
            this.inMemoryRawDeks = new PriorityQueue<InMemoryRawDek>(
                new InMemoryRawDekExpiryComparer(),
                isSynchronized: true);
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
            if (!this.cancellationTokenSource.Token.IsCancellationRequested)
            {
                while (this.inMemoryRawDeks.TryPeek(out InMemoryRawDek inMemoryRawDek))
                {
                    if (inMemoryRawDek.RawDekExpiry + this.bufferTimeAfterExpiry > DateTime.UtcNow)
                    {
                        break;
                    }

                    if (inMemoryRawDek.DataEncryptionKey is AeadAes256CbcHmac256Algorithm encryptionKey)
                    {
                        encryptionKey.Dispose();
                    }

                    this.inMemoryRawDeks.Dequeue();
                }
            }

            this.StartCleanupProcess();
        }

        internal void EnqueueInMemoryRawDek(InMemoryRawDek inMemoryRawDek)
        {
            this.inMemoryRawDeks.Enqueue(inMemoryRawDek);
        }

        private void Dispose(bool disposing)
        {
            if (disposing && !this.isDisposed)
            {
                this.inMemoryRawDeks.Clear();
                this.cancellationTokenSource.Dispose();
                this.timerPool.Dispose();
                this.pooledTimer.CancelTimer();
                this.pooledTimer = null;
                this.continuationTask.Dispose();
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