//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Stores information about fetch execution (for cross partition queries).
    /// </summary>
    internal sealed class FetchExecutionRange
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FetchExecutionRange"/> class.
        /// </summary>
        /// <param name="activityId">The activityId of the fetch</param>
        /// <param name="startTime">The start time of the fetch.</param>
        /// <param name="endTime">The end time of the fetch.</param>
        /// <param name="partitionKeyRangeId">The partitionkeyrangeid from which you are fetching for.</param>
        /// <param name="numberOfDocuments">The number of documents that were fetched in the particular execution range.</param>
        /// <param name="retryCount">The number of times we retried for this fetch execution range.</param>
        public FetchExecutionRange(string partitionKeyRangeId, string activityId, DateTime startTime, DateTime endTime, long numberOfDocuments, long retryCount)
        {
            this.PartitionId = partitionKeyRangeId;
            this.ActivityId = activityId;
            this.StartTime = startTime;
            this.EndTime = endTime;
            this.NumberOfDocuments = numberOfDocuments;
            this.RetryCount = retryCount;
        }

        /// <summary>
        /// Gets the partition id that was fetched from.
        /// </summary>
        public string PartitionId
        {
            get;
        }

        /// <summary>
        /// Gets the activityId of the fetch.
        /// </summary>
        public string ActivityId
        {
            get;
        }

        /// <summary>
        /// Gets the start time of the fetch.
        /// </summary>
        public DateTime StartTime
        {
            get;
        }

        /// <summary>
        /// Gets the end time of the fetch.
        /// </summary>
        public DateTime EndTime
        {
            get;
        }

        /// <summary>
        /// Gets the number of documents that where fetched in the particular execution range.
        /// </summary>
        public long NumberOfDocuments
        {
            get;
        }

        /// <summary>
        /// Gets the number of times we retried for this fetch execution range.
        /// </summary>
        public long RetryCount
        {
            get;
        }
    }
}
