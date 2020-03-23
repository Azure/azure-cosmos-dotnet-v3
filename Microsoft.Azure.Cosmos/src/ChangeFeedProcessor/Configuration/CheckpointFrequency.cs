//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Configuration
{
    using System;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;

    /// <summary>
    /// Specifies the frequency of lease event. The event will trigger when either of conditions is satisfied.
    /// </summary>
    internal sealed class CheckpointFrequency
    {
        /// <summary>
        /// Gets or sets a value indicating whether explicit check pointing is enabled. By default false. 
        /// Setting to true means change feed host will never checkpoint. 
        /// Client code needs to explicitly checkpoint via <see cref="PartitionCheckpointer"/>
        /// </summary>
        public bool ExplicitCheckpoint { get; set; }

        /// <summary>
        /// Gets or sets the value that specifies to checkpoint every specified number of docs.
        /// </summary>
        public int? ProcessedDocumentCount { get; set; }

        /// <summary>
        /// Gets or sets the value that specifies to checkpoint every specified time interval.
        /// </summary>
        public TimeSpan? TimeInterval { get; set; }
    }
}