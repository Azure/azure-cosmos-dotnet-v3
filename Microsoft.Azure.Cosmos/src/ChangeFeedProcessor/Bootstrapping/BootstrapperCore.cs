//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.Bootstrapping
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Logging;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.PartitionManagement;

    internal sealed class BootstrapperCore : Bootstrapper
    {
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();
        private readonly PartitionSynchronizer synchronizer;
        private readonly DocumentServiceLeaseStore leaseStore;
        private readonly TimeSpan lockTime;
        private readonly TimeSpan sleepTime;

        public BootstrapperCore(PartitionSynchronizer synchronizer, DocumentServiceLeaseStore leaseStore, TimeSpan lockTime, TimeSpan sleepTime)
        {
            if (synchronizer == null) throw new ArgumentNullException(nameof(synchronizer));
            if (leaseStore == null) throw new ArgumentNullException(nameof(leaseStore));
            if (lockTime <= TimeSpan.Zero) throw new ArgumentException("should be positive", nameof(lockTime));
            if (sleepTime <= TimeSpan.Zero) throw new ArgumentException("should be positive", nameof(sleepTime));

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
                if (initialized) break;

                bool isLockAcquired = await this.leaseStore.AcquireInitializationLockAsync(this.lockTime).ConfigureAwait(false);

                try
                {
                    if (!isLockAcquired)
                    {
                        Logger.InfoFormat("Another instance is initializing the store");
                        await Task.Delay(this.sleepTime).ConfigureAwait(false);
                        continue;
                    }

                    Logger.InfoFormat("Initializing the store");
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

            Logger.InfoFormat("The store is initialized");
        }
    }
}