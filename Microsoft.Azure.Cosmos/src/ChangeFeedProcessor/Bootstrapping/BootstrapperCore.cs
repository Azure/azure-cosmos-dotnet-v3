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
        /// Maximum number of times <see cref="InitializeAsync"/> will retry when any step of
        /// initialization (<see cref="DocumentServiceLeaseStore.IsInitializedAsync"/>,
        /// <see cref="DocumentServiceLeaseStore.AcquireInitializationLockAsync"/>,
        /// <see cref="PartitionSynchronizer.CreateMissingLeasesAsync"/>, or
        /// <see cref="DocumentServiceLeaseStore.MarkInitializedAsync"/>) fails with a
        /// regional error (e.g., <see cref="CosmosException"/> with 503 or
        /// <see cref="HttpRequestException"/>). The retry is most impactful for
        /// <see cref="PartitionSynchronizer.CreateMissingLeasesAsync"/>, since its
        /// partition-key-range metadata refresh uses <see cref="MetadataRequestThrottleRetryPolicy"/>
        /// (bypassing <see cref="ClientRetryPolicy"/>'s built-in cross-region failover), which marks
        /// the failing endpoint unavailable before propagating the error so the next attempt is
        /// routed to a different region. The other lease-store calls flow through the normal SDK
        /// pipeline and already get up to 120 cross-region failovers via <see cref="ClientRetryPolicy"/>;
        /// this retry is a backstop for the (rarer) case where that budget is also exhausted.
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
                bool isLockAcquired = false;
                bool shouldRetryAfterDelay = false;

                try
                {
                    bool initialized = await this.leaseStore.IsInitializedAsync().ConfigureAwait(false);
                    if (initialized)
                    {
                        break;
                    }

                    isLockAcquired = await this.leaseStore.AcquireInitializationLockAsync(this.lockTime).ConfigureAwait(false);

                    if (!isLockAcquired)
                    {
                        DefaultTrace.TraceInformation("Another instance is initializing the store");
                        shouldRetryAfterDelay = true;
                    }
                    else
                    {
                        DefaultTrace.TraceInformation("Initializing the store");
                        await this.synchronizer.CreateMissingLeasesAsync().ConfigureAwait(false);
                        await this.leaseStore.MarkInitializedAsync().ConfigureAwait(false);
                    }
                }
                catch (CosmosException ex) when (retryCount < MaxInitializationRetries
                    && (ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable
                        || ex.StatusCode == System.Net.HttpStatusCode.InternalServerError
                        || (ex.StatusCode == System.Net.HttpStatusCode.Gone
                            && ex.SubStatusCode == (int)Documents.SubStatusCodes.LeaseNotFound)
                        || (ex.StatusCode == System.Net.HttpStatusCode.Forbidden
                            && ex.SubStatusCode == (int)Documents.SubStatusCodes.DatabaseAccountNotFound)))
                {
                    // The SDK's retry infrastructure (ClientRetryPolicy or
                    // MetadataRequestThrottleRetryPolicy) may have marked the failing
                    // endpoint unavailable. If so, the next iteration will route to a
                    // different region. Otherwise, the delay gives the region time to recover.
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

                    shouldRetryAfterDelay = true;
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

                    shouldRetryAfterDelay = true;
                }
                finally
                {
                    // Release the lock before sleeping (see below) so a held-but-sleeping lock
                    // does not undermine multi-instance coordination or expire out from under us.
                    if (isLockAcquired)
                    {
                        await this.leaseStore.ReleaseInitializationLockAsync().ConfigureAwait(false);
                    }
                }

                if (shouldRetryAfterDelay)
                {
                    // The delay intentionally happens after the lock has been released (see the
                    // finally block above), rather than while still holding it. Awaiting inside a
                    // try/catch would keep the lock held for the full sleep duration (up to 45s
                    // across all retries), during which peer CFP instances would see the lock as
                    // unavailable, and the lease's lockTime TTL could expire mid-sleep, letting a
                    // peer take over while this instance still believes it holds the lock.
                    await Task.Delay(this.sleepTime).ConfigureAwait(false);
                    continue;
                }

                break;
            }

            DefaultTrace.TraceInformation("The store is initialized");
        }
    }
}