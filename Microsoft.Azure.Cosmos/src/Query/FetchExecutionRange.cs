//-----------------------------------------------------------------------
// <copyright file="FetchExecutionRange.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Stores information about fetch execution (for cross partition queries).
    /// </summary>
    internal sealed class FetchExecutionRange
    {
        private readonly DateTime startTime;
        private readonly DateTime endTime;
        private readonly string partitionKeyRangeId;
        private readonly long numberOfDocuments;
        private readonly long retryCount;

        /// <summary>
        /// Initializes a new instance of the FetchExecutionRange class.
        /// </summary>
        /// <param name="startTime">The start time of the fetch.</param>
        /// <param name="endTime">The end time of the fetch.</param>
        /// <param name="partitionKeyRangeId">The partitionkeyrangeid from which you are fetching for.</param>
        /// <param name="numberOfDocuments">The number of documents that were fetched in the particular execution range.</param>
        /// <param name="retryCount">The number of times we retried for this fetch execution range.</param>
        public FetchExecutionRange(DateTime startTime, DateTime endTime, string partitionKeyRangeId, long numberOfDocuments, long retryCount)
        {
            this.startTime = startTime;
            this.endTime = endTime;
            this.partitionKeyRangeId = partitionKeyRangeId;
            this.numberOfDocuments = numberOfDocuments;
            this.retryCount = retryCount;
        }

        /// <summary>
        /// Gets the start time of the fetch.
        /// </summary>
        public DateTime StartTime
        {
            get
            {
                return this.startTime;
            }
        }

        /// <summary>
        /// Gets the end time of the fetch.
        /// </summary>
        public DateTime EndTime
        {
            get
            {
                return this.endTime;
            }
        }

        /// <summary>
        /// Gets the partition id that was fetched from.
        /// </summary>
        public string PartitionId
        {
            get
            {
                return this.partitionKeyRangeId;
            }
        }

        /// <summary>
        /// Gets the number of documents that where fetched in the particular execution range.
        /// </summary>
        public long NumberOfDocuments
        {
            get
            {
                return this.numberOfDocuments;
            }
        }

        /// <summary>
        /// Gets the number of times we retried for this fetch execution range.
        /// </summary>
        public long RetryCount
        {
            get
            {
                return this.retryCount;
            }
        }
    }
}
