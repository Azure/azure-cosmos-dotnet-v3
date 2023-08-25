//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The Cosmos Change Feed request options
    /// </summary>
    internal class StandByFeedIteratorRequestOptions : RequestOptions
    {
        internal const string IfNoneMatchAllHeaderValue = "*";
        internal static readonly DateTime DateTimeStartFromBeginning = DateTime.MinValue.ToUniversalTime();

        /// <summary>
        /// Gets or sets the maximum number of items to be returned in the enumeration operation in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The maximum number of items to be returned in the enumeration operation.
        /// </value> 
        public int? MaxItemCount { get; set; }

        /// <summary>
        /// Gets or sets a particular point in time to start to read the change feed.
        /// </summary>
        /// <remarks>
        /// Only applies in the case where no FeedToken is provided or the FeedToken was never used in a previous iterator.
        /// In order to read the Change Feed from the beginning, set this to DateTime.MinValue.ToUniversalTime().
        /// </remarks>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// Fill the CosmosRequestMessage headers with the set properties
        /// </summary>
        /// <param name="request">The <see cref="RequestMessage"/></param>
        internal override void PopulateRequestOptions(RequestMessage request)
        {
            // Check if no Continuation Token is present
            if (string.IsNullOrEmpty(request.Headers.IfNoneMatch))
            {
                if (this.StartTime == null)
                {
                    request.Headers.IfNoneMatch = StandByFeedIteratorRequestOptions.IfNoneMatchAllHeaderValue;
                }
                else if (this.StartTime != null
                    && this.StartTime != StandByFeedIteratorRequestOptions.DateTimeStartFromBeginning)
                {
                    request.Headers.Add(HttpConstants.HttpHeaders.IfModifiedSince, this.StartTime.Value.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture));
                }
            }

            StandByFeedIteratorRequestOptions.FillMaxItemCount(request, this.MaxItemCount);
            request.Headers.Add(HttpConstants.HttpHeaders.A_IM, HttpConstants.A_IMHeaderValues.IncrementalFeed);

            base.PopulateRequestOptions(request);
        }

        internal static void FillPartitionKeyRangeId(RequestMessage request, string partitionKeyRangeId)
        {
            Debug.Assert(request != null);

            if (!string.IsNullOrEmpty(partitionKeyRangeId))
            {
                request.PartitionKeyRangeId = new PartitionKeyRangeIdentity(partitionKeyRangeId);
            }
        }

        internal static void FillPartitionKey(RequestMessage request, PartitionKey partitionKey)
        {
            Debug.Assert(request != null);

            request.Headers.PartitionKey = partitionKey.ToJsonString();
        }

        internal static void FillContinuationToken(RequestMessage request, string continuationToken)
        {
            Debug.Assert(request != null);

            if (!string.IsNullOrWhiteSpace(continuationToken))
            {
                // On REST level, change feed is using IfNoneMatch/ETag instead of continuation
                request.Headers.IfNoneMatch = continuationToken;
            }
        }

        internal static void FillMaxItemCount(RequestMessage request, int? maxItemCount)
        {
            Debug.Assert(request != null);

            if (maxItemCount.HasValue)
            {
                request.Headers.Add(HttpConstants.HttpHeaders.PageSize, maxItemCount.Value.ToString(CultureInfo.InvariantCulture));
            }
        }
    }
}