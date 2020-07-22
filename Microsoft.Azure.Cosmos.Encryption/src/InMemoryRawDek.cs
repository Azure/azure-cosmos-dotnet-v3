//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    internal class InMemoryRawDek
    {
        private readonly double dekRefreshFrequencyAsPercentageOfTtl;
        private TimeSpan clientCacheTimeToLive;
        private DateTime lastUsageTime;
        private DateTime nextRefreshTime;

        public DataEncryptionKey DataEncryptionKey { get; }

        public DateTime RawDekExpiry { get; private set; }

        public DataEncryptionKeyProperties DataEncryptionKeyProperties { get; }

        public InMemoryRawDek(
            DataEncryptionKey dataEncryptionKey,
            DataEncryptionKeyProperties dekProperties,
            TimeSpan clientCacheTimeToLive,
            double dekRefreshFrequencyAsPercentageOfTtl)
        {
            this.DataEncryptionKey = dataEncryptionKey;
            this.RawDekExpiry = DateTime.UtcNow + clientCacheTimeToLive;
            this.DataEncryptionKeyProperties = dekProperties;
            this.clientCacheTimeToLive = clientCacheTimeToLive;
            this.dekRefreshFrequencyAsPercentageOfTtl = dekRefreshFrequencyAsPercentageOfTtl;
            this.nextRefreshTime = DateTime.UtcNow.AddSeconds(this.RefreshInterval());
        }

        public void UpdateLastUsageTime()
        {
            this.lastUsageTime = DateTime.UtcNow;
        }

        public void UpdateNextRefreshTime()
        {
            this.nextRefreshTime = this.nextRefreshTime.AddSeconds(this.RefreshInterval());
        }

        public bool IsRefreshNeeded()
        {
            return this.nextRefreshTime <= DateTime.UtcNow
                && this.lastUsageTime > this.nextRefreshTime.AddSeconds(-this.RefreshInterval()); // Check if unwrapped DEK has been used after last refresh.
        }

        public void RefreshTimeToLive(TimeSpan clientCacheTimeToLive)
        {
            this.clientCacheTimeToLive = clientCacheTimeToLive;
            this.RawDekExpiry = DateTime.UtcNow.Add(clientCacheTimeToLive);
            this.UpdateNextRefreshTime();
        }

        private double RefreshInterval()
        {
            return this.clientCacheTimeToLive.TotalSeconds * (this.dekRefreshFrequencyAsPercentageOfTtl / 100.0);
        }
    }
}
