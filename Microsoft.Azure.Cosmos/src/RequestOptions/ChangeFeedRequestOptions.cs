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
    internal class ChangeFeedRequestOptions : RequestOptions
    {
        internal const string IfNoneMatchAllHeaderValue = "*";

        /// <summary>
        /// Specifies a particular point in time to start to read the change feed.
        /// </summary>
        /// <remarks>
        /// In order to read the Change Feed from the beginning, set this to DateTime.MinValue.
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
                    request.Headers.IfNoneMatch = ChangeFeedRequestOptions.IfNoneMatchAllHeaderValue;
                }
                else if (this.StartTime != null)
                {
                    request.Headers.Add(HttpConstants.HttpHeaders.IfModifiedSince, this.StartTime.Value.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture));
                }
            }

            request.Headers.Add(HttpConstants.HttpHeaders.A_IM, HttpConstants.A_IMHeaderValues.IncrementalFeed);

            base.PopulateRequestOptions(request);
        }

        internal static void FillPartitionKeyRangeId(RequestMessage request, string partitionKeyRangeId)
        {
            Debug.Assert(request != null);

            if (!string.IsNullOrEmpty(partitionKeyRangeId))
            {
                request.PartitionKeyRangeId = partitionKeyRangeId;
            }
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