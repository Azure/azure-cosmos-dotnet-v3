//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Bootstrapping
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal sealed class BootstrapperCore : Bootstrapper
    {
        internal static readonly TimeSpan DefaultSleepTime = TimeSpan.FromSeconds(15);
        internal static readonly TimeSpan DefaultLockTime = TimeSpan.FromSeconds(30);

        private readonly PartitionSynchronizer synchronizer;
        private readonly DocumentServiceLeaseStore leaseStore;
        private readonly TimeSpan lockTime;
        private readonly TimeSpan sleepTime;

        public BootstrapperCore(PartitionSynchronizer synchronizer, DocumentServiceLeaseStore leaseStore, TimeSpan lockTime, TimeSpan sleepTime)
        {
            if (synchronizer == null)
            {
                throw new ArgumentNullException(nameof(synchronizer));
            }

            if (leaseStore == null)
            {
                throw new ArgumentNullException(nameof(leaseStore));
            }

            if (lockTime <= TimeSpan.Zero)
            {
                throw new ArgumentException("should be positive", nameof(lockTime));
            }

            if (sleepTime <= TimeSpan.Zero)
            {
                throw new ArgumentException("should be positive", nameof(sleepTime));
            }

            this.synchronizer = synchronizer;
            this.leaseStore = leaseStore;
            this.lockTime = lockTime;
            this.sleepTime = sleepTime;
        }

        public override async Task InitializeAsync()
        {
            while (true)
            {
                bool initialized = await this.leaseStore.IsInitializedAsync().ConfigureAwait(false);
                if (initialized)
                {
                    break;
                }

                bool isLockAcquired = await this.leaseStore.AcquireInitializationLockAsync(this.lockTime).ConfigureAwait(false);

                try
                {
                    if (!isLockAcquired)
                    {
                        DefaultTrace.TraceInformation("Another instance is initializing the store");
                        await Task.Delay(this.sleepTime).ConfigureAwait(false);
                        continue;
                    }

                    DefaultTrace.TraceInformation("Initializing the store");
                    await this.synchronizer.CreateMissingLeasesAsync().ConfigureAwait(false);
                    await this.leaseStore.MarkInitializedAsync().ConfigureAwait(false);
                }
                finally
                {
                    if (isLockAcquired)
                    {
                        await this.leaseStore.ReleaseInitializationLockAsync().ConfigureAwait(false);
                    }
                }

                break;
            }

            DefaultTrace.TraceInformation("The store is initialized");
        }
    }
}