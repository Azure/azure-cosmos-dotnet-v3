//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Configuration
{
    using System;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Options to control various aspects of partition distribution happening within <see cref="ChangeFeedProcessorCore"/> instance.
    /// </summary>
    internal class ChangeFeedProcessorOptions
    {
        private static readonly TimeSpan DefaultFeedPollDelay = TimeSpan.FromSeconds(5);
        private DateTime? startTime;

        /// <summary>Initializes a new instance of the <see cref="ChangeFeedProcessorOptions" /> class.</summary>
        public ChangeFeedProcessorOptions()
        {
            this.FeedPollDelay = DefaultFeedPollDelay;
        }

        /// <summary>
        /// Gets or sets the delay in between polling the change feed for new changes, after all current changes are drained.
        /// <remarks>
        /// Applies only after a read on the change feed yielded no results.
        /// </remarks>
        /// </summary>
        public TimeSpan FeedPollDelay { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of items to be returned in the enumeration operation in the Azure Cosmos DB service.
        /// </summary>
        public int? MaxItemCount { get; set; }

        /// <summary>
        /// Gets or sets the start request continuation token to start looking for changes after.
        /// </summary>
        /// <remarks>
        /// This is only used when lease store is not initialized and is ignored if a lease exists and has continuation token.
        /// If this is specified, both StartTime and StartFromBeginning are ignored.
        /// </remarks>
        /// <seealso cref="ChangeFeedOptions.RequestContinuation"/>
        public string StartContinuation { get; set; }

        /// <summary>
        /// Gets or sets the time (exclusive) to start looking for changes after.
        /// </summary>
        /// <remarks>
        /// This is only used when:
        /// (1) Lease store is not initialized and is ignored if a lease exists and has continuation token.
        /// (2) StartContinuation is not specified.
        /// If this is specified, StartFromBeginning is ignored.
        /// </remarks>
        /// <seealso cref="ChangeFeedOptions.StartTime"/>
        public DateTime? StartTime
        {
            get => this.startTime;

            set
            {
                if (value.HasValue && value.Value.Kind == DateTimeKind.Unspecified)
                {
                    throw new ArgumentException("StartTime cannot have DateTimeKind.Unspecified", nameof(value));
                }

                this.startTime = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether change feed in the Azure Cosmos DB service should start from beginning (true) or from current (false).
        /// By default it's start from current (false).
        /// </summary>
        /// <remarks>
        /// This is only used when:
        /// (1) Lease store is not initialized and is ignored if a lease exists and has continuation token.
        /// (2) StartContinuation is not specified.
        /// (3) StartTime is not specified.
        /// </remarks>
        /// <seealso cref="ChangeFeedOptions.StartFromBeginning"/>
        public bool StartFromBeginning { get; set; }
    }
}
