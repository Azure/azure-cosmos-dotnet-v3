//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.Configuration
{
    using System;

    /// <summary>
    /// Options to control various aspects of partition distribution happening within <see cref="ChangeFeedProcessorCore"/> instance.
    /// </summary>
    public class ChangeFeedLeaseOptions
    {
        private static readonly TimeSpan DefaultRenewInterval = TimeSpan.FromSeconds(17);
        private static readonly TimeSpan DefaultAcquireInterval = TimeSpan.FromSeconds(13);
        private static readonly TimeSpan DefaultExpirationInterval = TimeSpan.FromSeconds(60);

        /// <summary>Initializes a new instance of the <see cref="ChangeFeedLeaseOptions" /> class.</summary>
        public ChangeFeedLeaseOptions()
        {
            this.LeaseRenewInterval = DefaultRenewInterval;
            this.LeaseAcquireInterval = DefaultAcquireInterval;
            this.LeaseExpirationInterval = DefaultExpirationInterval;
        }

        /// <summary>
        /// Gets or sets renew interval for all leases currently held by <see cref="ChangeFeedProcessorCore"/> instance.
        /// </summary>
        public TimeSpan LeaseRenewInterval { get; set; }

        /// <summary>
        /// Gets or sets the interval to kick off a task to compute if leases are distributed evenly among known host instances.
        /// </summary>
        public TimeSpan LeaseAcquireInterval { get; set; }

        /// <summary>
        /// Gets or sets the interval for which the lease is taken. If the lease is not renewed within this
        /// interval, it will cause it to expire and ownership of the lease will move to another <see cref="ChangeFeedProcessorCore"/> instance.
        /// </summary>
        public TimeSpan LeaseExpirationInterval { get; set; }

        /// <summary>
        /// Gets or sets a prefix to be used as part of the lease id. This can be used to support multiple instances of <see cref="ChangeFeedProcessorCore"/>
        /// instances pointing at the same feed while using the same auxiliary collection.
        /// </summary>
        public string LeasePrefix { get; set; }

        /// <summary>
        /// Gets or sets the minimum leases count for the host.
        /// This can be used to increase the number of leases for the host and thus override equal distribution (which is the default) of leases between hosts.
        /// </summary>
        internal int MinLeaseCount { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of leases the host can serve.
        /// This can be used property to limit the number of leases for the host and thus override equal distribution (which is the default) of leases between hosts.
        /// Default is 0 (unlimited).
        /// </summary>
        internal int MaxLeaseCount { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether on start of the host all existing leases should be deleted and the host should start from scratch.
        /// </summary>
        internal bool DiscardExistingLeases { get; set; }

        /// <summary>
        /// Gets or sets the Batch size of query API.
        /// </summary>
        internal int QueryFeedMaxBatchSize { get; set; }

        /// <summary>
        /// Gets maximum number of tasks to use for auxiliary calls.
        /// </summary>
        internal int DegreeOfParallelism => this.MaxLeaseCount > 0 ? this.MaxLeaseCount : 25;
    }
}