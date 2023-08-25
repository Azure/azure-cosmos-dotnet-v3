﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a lease that is persisted as a document in the lease collection.
    /// Leases are used to:
    /// * Keep track of the <see cref="ChangeFeedProcessor"/> progress for a particular Partition Key Range.
    /// * Distribute load between different instances of <see cref="ChangeFeedProcessor"/>.
    /// * Ensure reliable recovery for cases when an instance of <see cref="ChangeFeedProcessor"/> gets disconnected, hangs or crashes.
    /// </summary>
    [Serializable]
    [JsonConverter(typeof(DocumentServiceLeaseConverter))]
    internal abstract class DocumentServiceLease
    {
        public const string IdPropertyName = "id";
        public const string LeasePartitionKeyPropertyName = "partitionKey";

        /// <summary>
        /// Gets the processing distribution unit identifier.
        /// </summary>
        public abstract string CurrentLeaseToken { get; }

        /// <summary>
        /// Gets the range associated with this lease.
        /// </summary>
        public abstract FeedRangeInternal FeedRange { get; set; }

        /// <summary>
        /// Gets or sets the host name owner of the lease.
        /// The Owner keeps track which <see cref="ChangeFeedProcessor"/> is currently processing that Partition Key Range.
        /// </summary>
        public abstract string Owner { get; set; }

        /// <summary>
        /// Gets or sets the Timestamp of the lease.
        /// Timestamp is used to determine lease expiration.
        /// </summary>
        public abstract DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the Continuation Token.
        /// Continuation Token is used to determine the last processed point of the Change Feed.
        /// </summary>
        public abstract string ContinuationToken { get; set; }

        /// <summary>
        /// Gets the lease Id.
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// Gets the lease PartitionKey.
        /// </summary>
        public abstract string PartitionKey { get; }

        /// <summary>
        /// Gets the Concurrency Token.
        /// </summary>
        public abstract string ConcurrencyToken { get; }

        /// <summary>
        /// Gets or sets custom lease properties which can be managed from <see cref="LoadBalancingStrategy"/>.
        /// </summary>
        public abstract Dictionary<string, string> Properties { get; set; }
    }
}