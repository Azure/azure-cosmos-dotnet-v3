//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The Cosmos Change Feed request options
    /// </summary>
    public class CosmosChangeFeedRequestOptions : CosmosRequestOptions
    {
        private const string IfNoneMatchAllHeaderValue = "*";

        /// <summary>
        /// Marks whether the change feed should be read from the start.
        /// </summary>
        /// <remarks>
        /// If this is specified, StartTime is ignored.
        /// </remarks>
        public virtual bool StartFromBeginning { get; set; }

        /// <summary>
        /// Specifies a particular point in time to start to read the change feed.
        /// </summary>
        public virtual DateTime? StartTime { get; set; }

        internal virtual string StartEffectivePartitionKeyString { get; set; }

        internal virtual string EndEffectivePartitionKeyString { get; set; }

        /// <summary>
        /// Fill the CosmosRequestMessage headers with the set properties
        /// </summary>
        /// <param name="request">The <see cref="CosmosRequestMessage"/></param>
        public override void FillRequestOptions(CosmosRequestMessage request)
        {
            if (string.IsNullOrEmpty(request.Headers.IfNoneMatch))
            {
                if (!this.StartFromBeginning && this.StartTime == null)
                {
                    request.Headers.IfNoneMatch = CosmosChangeFeedRequestOptions.IfNoneMatchAllHeaderValue;
                }
                else if (this.StartTime != null)
                {
                    request.Headers.Add(HttpConstants.HttpHeaders.IfModifiedSince, this.StartTime.Value.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture));
                }
            }

            request.Headers.IncrementalFeed = HttpConstants.A_IMHeaderValues.IncrementalFeed;

            request.Properties.Add(WFConstants.BackendHeaders.EffectivePartitionKeyString, this.StartEffectivePartitionKeyString);
            if (!string.IsNullOrEmpty(this.StartEffectivePartitionKeyString))
            {
                request.Properties.Add(HandlerConstants.StartEpkString, this.StartEffectivePartitionKeyString);
            }

            if (!string.IsNullOrEmpty(this.EndEffectivePartitionKeyString))
            {
                request.Properties.Add(HandlerConstants.EndEpkString, this.EndEffectivePartitionKeyString);
            }

            base.FillRequestOptions(request);
        }

        internal static void FillContinuationToken(
            CosmosRequestMessage request,
            string continuationToken)
        {
            if (!string.IsNullOrWhiteSpace(continuationToken))
            {
                // On REST level, change feed is using IfNoneMatch/ETag instead of continuation
                request.Headers.IfNoneMatch = continuationToken;
            }
        }

        internal static void FillMaxItemCount(
            CosmosRequestMessage request,
            int? maxItemCount)
        {
            if (maxItemCount != null && maxItemCount.HasValue)
            {
                request.Headers.Add(HttpConstants.HttpHeaders.PageSize, maxItemCount.Value.ToString(CultureInfo.InvariantCulture));
            }
        }
    }
}