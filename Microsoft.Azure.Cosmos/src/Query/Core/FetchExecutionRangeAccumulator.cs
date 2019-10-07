//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    /// <summary>
    /// Accumlator that acts as a builder of FetchExecutionRanges
    /// </summary>
    internal sealed class FetchExecutionRangeAccumulator
    {
        private readonly DateTime constructionTime;
        private readonly Stopwatch stopwatch;
        private List<FetchExecutionRange> fetchExecutionRanges;
        private DateTime startTime;
        private DateTime endTime;
        private bool isFetching;

        /// <summary>
        /// Initializes a new instance of the <see cref="FetchExecutionRangeAccumulator"/> class.
        /// </summary>
        public FetchExecutionRangeAccumulator()
        {
            this.constructionTime = DateTime.UtcNow;
            // This stopwatch is always running and is only used to calculate deltas that are synchronized with the construction time.
            this.stopwatch = Stopwatch.StartNew();
            this.fetchExecutionRanges = new List<FetchExecutionRange>();
        }

        /// <summary>
        /// Gets the FetchExecutionRanges and resets the accumulator.
        /// </summary>
        /// <returns>the SchedulingMetricsResult.</returns>
        public IEnumerable<FetchExecutionRange> GetExecutionRanges()
        {
            IEnumerable<FetchExecutionRange> returnValue = this.fetchExecutionRanges;
            this.fetchExecutionRanges = new List<FetchExecutionRange>();
            return returnValue;
        }

        /// <summary>
        /// Updates the most recent start time internally.
        /// </summary>
        public void BeginFetchRange()
        {
            if (!this.isFetching)
            {
                // Calculating the start time as the construction time and the stopwatch as a delta.
                this.startTime = this.constructionTime.Add(this.stopwatch.Elapsed);
                this.isFetching = true;
            }
        }

        /// <summary>
        /// Updates the most recent end time internally and constructs a new FetchExecutionRange
        /// </summary>
        /// <param name="partitionIdentifier">The identifier for the partition.</param>
        /// <param name="activityId">The activity of the fetch.</param>
        /// <param name="numberOfDocuments">The number of documents that were fetched for this range.</param>
        /// <param name="retryCount">The number of times we retried for this fetch execution range.</param>
        public void EndFetchRange(string partitionIdentifier, string activityId, long numberOfDocuments, long retryCount)
        {
            if (this.isFetching)
            {
                // Calculating the end time as the construction time and the stopwatch as a delta.
                this.endTime = this.constructionTime.Add(this.stopwatch.Elapsed);
                FetchExecutionRange fetchExecutionRange = new FetchExecutionRange(
                    partitionIdentifier,
                    activityId,
                    this.startTime,
                    this.endTime,
                    numberOfDocuments,
                    retryCount);
                this.fetchExecutionRanges.Add(fetchExecutionRange);
                this.isFetching = false;
            }
        }
    }
}
