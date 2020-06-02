//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Timers;
    using Microsoft.Azure.Cosmos.Query.Core.Collections;

    internal sealed class ExpiredRawDekCleaner : IDisposable
    {
        private readonly PriorityQueue<InMemoryRawDek> inMemoryRawDeks;
        private readonly TimeSpan iterationInterval = TimeSpan.FromSeconds(60);
        private readonly TimeSpan bufferTimeAfterExpiry = TimeSpan.FromSeconds(60);
        private readonly Timer timer;
        private bool isDisposed = false;

        public ExpiredRawDekCleaner(
            TimeSpan? iterationInterval = null,
            TimeSpan? bufferTimeAfterExpiry = null)
        {
            if (iterationInterval.HasValue)
            {
                if (iterationInterval.Value < TimeSpan.FromSeconds(1))
                {
                    throw new ArgumentOutOfRangeException("Time interval between successive iterations should be at least 1 seconds.");
                }

                this.iterationInterval = iterationInterval.Value;
            }

            if (bufferTimeAfterExpiry.HasValue)
            {
                this.bufferTimeAfterExpiry = bufferTimeAfterExpiry.Value;
            }

            this.inMemoryRawDeks = new PriorityQueue<InMemoryRawDek>(
                new InMemoryRawDekExpiryComparer(),
                isSynchronized: true);

            this.timer = new Timer(this.iterationInterval.TotalMilliseconds);
            this.timer.Elapsed += this.RunCleanup;
            this.timer.AutoReset = true;
            this.timer.Enabled = true;
        }

        private void RunCleanup(Object source, ElapsedEventArgs e)
        {
            while (this.inMemoryRawDeks.TryPeek(out InMemoryRawDek inMemoryRawDek))
            {
                if (inMemoryRawDek.RawDekExpiry + this.bufferTimeAfterExpiry > DateTime.UtcNow)
                {
                    break;
                }

                inMemoryRawDek.DataEncryptionKey.Dispose();
                this.inMemoryRawDeks.Dequeue();
            }
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
                this.timer.Stop();
                this.timer.Dispose();
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