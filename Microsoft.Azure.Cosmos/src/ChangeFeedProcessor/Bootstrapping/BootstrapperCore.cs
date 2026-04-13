//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Bootstrapping
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal sealed class BootstrapperCore : Bootstrapper
    {
        /// <summary>
        /// Maximum number of times <see cref="InitializeAsync"/> will retry when
        /// <see cref="PartitionSynchronizer.CreateMissingLeasesAsync"/> fails with a
        /// regional error (e.g., <see cref="CosmosException"/> with 503 or
        /// <see cref="HttpRequestException"/>). The retry is useful because
        /// <see cref="MetadataRequestThrottleRetryPolicy"/> marks the failing
        /// endpoint unavailable before propagating the error, so the next attempt
        /// will be routed to a different region.
        /// </summary>
        internal const int MaxInitializationRetries = 3;

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
            int retryCount = 0;

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
                catch (CosmosException ex) when (retryCount < MaxInitializationRetries)
                {
                    // MetadataRequestThrottleRetryPolicy has already marked the
                    // failing endpoint unavailable, so the next iteration will
                    // route to a different region.
                    retryCount++;
                    DefaultTrace.TraceWarning(
                        "BootstrapperCore: Regional failure during initialization "
                        + "(StatusCode: {0}, SubStatusCode: {1}). "
                        + "Attempt {2} of {3}. Retrying after {4}.",
                        ex.StatusCode,
                        ex.SubStatusCode,
                        retryCount,
                        MaxInitializationRetries,
                        this.sleepTime);

                    await Task.Delay(this.sleepTime).ConfigureAwait(false);
                    continue;
                }
                catch (HttpRequestException ex) when (retryCount < MaxInitializationRetries)
                {
                    retryCount++;
                    DefaultTrace.TraceWarning(
                        "BootstrapperCore: HttpRequestException during initialization: {0}. "
                        + "Attempt {1} of {2}. Retrying after {3}.",
                        ex.Message,
                        retryCount,
                        MaxInitializationRetries,
                        this.sleepTime);

                    await Task.Delay(this.sleepTime).ConfigureAwait(false);
                    continue;
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