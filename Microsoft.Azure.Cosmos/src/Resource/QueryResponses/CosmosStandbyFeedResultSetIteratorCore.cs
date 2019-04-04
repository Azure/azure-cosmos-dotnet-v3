//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Routing;

    /// <summary>
    /// Cosmos Stand-By Feed iterator implementing Composite Continuation Token
    /// </summary>
    internal class CosmosStandbyFeedResultSetIteratorCore : CosmosFeedResultSetIterator
    {
        private static readonly int DefaultMaxItemCount = 100;

        private readonly PartitionRoutingHelper partitionRoutingHelper = new PartitionRoutingHelper();
        private readonly CosmosContainerCore cosmosContainer;
        private StandByFeedContinuationToken compositeContinuationToken;
        private string containerRid;
        private int? originalMaxItemCount;

        internal delegate Task<CosmosResponseMessage> NextResultSetDelegate(
            int? maxItemCount,
            string continuationToken,
            CosmosChangeFeedRequestOptions options,
            CancellationToken cancellationToken);

        internal readonly NextResultSetDelegate nextResultSetDelegate;

        internal CosmosStandbyFeedResultSetIteratorCore(
            CosmosContainerCore cosmosContainer,
            int? maxItemCount,
            string continuationToken,
            CosmosChangeFeedRequestOptions options,
            NextResultSetDelegate nextDelegate)
        {
            this.cosmosContainer = cosmosContainer;
            this.nextResultSetDelegate = nextDelegate;
            this.HasMoreResults = true;
            this.changeFeedOptions = options;
            this.MaxItemCount = maxItemCount;
            this.originalMaxItemCount = maxItemCount;
            if (!string.IsNullOrEmpty(continuationToken))
            {
                this.compositeContinuationToken = new StandByFeedContinuationToken(continuationToken);
            }
        }

        /// <summary>
        /// The Continuation Token
        /// </summary>
        protected string continuationToken => this.compositeContinuationToken?.NextToken;

        /// <summary>
        /// The query options for the result set
        /// </summary>
        protected readonly CosmosChangeFeedRequestOptions changeFeedOptions;

        /// <summary>
        /// The max item count to return as part of the query
        /// </summary>
        protected int? MaxItemCount;

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override async Task<CosmosResponseMessage> FetchNextSetAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            PartitionKeyRangeCache pkRangeCache = await this.cosmosContainer.Client.DocumentClient.GetPartitionKeyRangeCacheAsync();
            if (this.containerRid == null)
            {
                this.containerRid = await this.cosmosContainer.GetRID(cancellationToken);
            }

            await this.InitializeCompositeToken(pkRangeCache);

            IReadOnlyList<Documents.PartitionKeyRange> keyRanges = await this.GetCurrentPartitionKeyRanges(pkRangeCache);

            this.changeFeedOptions.PartitionKeyRangeId = keyRanges[0].Id;
            if (keyRanges.Count > 1)
            {
                // Original range contains now more than 1 Key Range
                // Push the rest and update the current range
                this.compositeContinuationToken.HandleSplit(keyRanges);
            }

            CosmosResponseMessage response = await this.nextResultSetDelegate(this.MaxItemCount, this.continuationToken, this.changeFeedOptions, cancellationToken);
            if (await this.ShouldRetryFailure(pkRangeCache, response, cancellationToken))
            {
                this.HasMoreResults = true;
                CosmosResponseMessage retryMessage = new CosmosResponseMessage(HttpStatusCode.NoContent);
                retryMessage.Headers.Continuation = this.compositeContinuationToken.NextToken;
                return retryMessage;
            }

            // Change Feed read uses Etag for continuation
            string responseContinuationToken = response.Headers.ETag;
            this.HasMoreResults = GetHasMoreResults(responseContinuationToken, response.StatusCode);
            if (!this.HasMoreResults)
            {
                // Current Range is done, push it to the end
                response.Headers.Continuation = this.compositeContinuationToken.PushCurrentToBack();
                this.HasMoreResults = true;
            }
            else
            {
                response.Headers.Continuation = this.compositeContinuationToken.UpdateCurrentToken(responseContinuationToken);
            }

            return response;
        }

        internal static bool GetHasMoreResults(
            string continuationToken, 
            HttpStatusCode statusCode)
        {
            return continuationToken != null && statusCode != HttpStatusCode.NotModified;
        }

        private async Task<IReadOnlyList<Documents.PartitionKeyRange>> GetCurrentPartitionKeyRanges(PartitionKeyRangeCache pkRangeCache)
        {
            return await pkRangeCache.TryGetOverlappingRangesAsync(this.containerRid, new Documents.Routing.Range<string>(
                    this.compositeContinuationToken.MinInclusiveRange,
                    this.compositeContinuationToken.MaxExclusiveRange,
                    true,
                    false));
        }

        private async Task InitializeCompositeToken(
            PartitionKeyRangeCache pkRangeCache, 
            bool forceRefresh = false)
        {
            if (this.compositeContinuationToken == null || forceRefresh)
            {
                // Initialize composite token with all the ranges
                IReadOnlyList<Documents.PartitionKeyRange> allRanges = await pkRangeCache.TryGetOverlappingRangesAsync(this.containerRid, new Documents.Routing.Range<string>(
                    Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                    Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                    true,
                    false));

                this.compositeContinuationToken = new StandByFeedContinuationToken(allRanges);
            }
        }

        /// <summary>
        /// During Feed read, split can happen or Max Item count can go beyond the max response size
        /// </summary>
        internal async Task<bool> ShouldRetryFailure(
            PartitionKeyRangeCache pkRangeCache, 
            CosmosResponseMessage response, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotModified)
            {
                if (this.MaxItemCount != this.originalMaxItemCount)
                {
                    this.MaxItemCount = this.originalMaxItemCount;   // Reset after successful execution.
                }

                return false;
            }

            bool partitionNotFound = response.StatusCode == HttpStatusCode.NotFound 
                && response.Headers.SubStatusCode != Documents.SubStatusCodes.ReadSessionNotAvailable;
            if (partitionNotFound)
            {
                this.containerRid = await this.cosmosContainer.GetRID(cancellationToken);
                await this.InitializeCompositeToken(pkRangeCache, true);
                return true;
            }

            bool partitionSplit = response.StatusCode == HttpStatusCode.Gone 
                && (response.Headers.SubStatusCode == Documents.SubStatusCodes.PartitionKeyRangeGone || response.Headers.SubStatusCode == Documents.SubStatusCodes.CompletingSplit);
            if (partitionSplit)
            {
                // Get all new children
                IReadOnlyList<Documents.PartitionKeyRange> keyRanges = await this.GetCurrentPartitionKeyRanges(pkRangeCache);

                if (keyRanges.Count > 0)
                {
                    this.compositeContinuationToken.HandleSplit(keyRanges);
                    return true;
                }
            }

            bool pageSizeError = response.ErrorMessage.Contains("Reduce page size and try again.");
            if (pageSizeError)
            {
                if (!this.MaxItemCount.HasValue)
                {
                    this.MaxItemCount = CosmosStandbyFeedResultSetIteratorCore.DefaultMaxItemCount;
                }
                else if (this.MaxItemCount <= 1)
                {
                    return false;
                }

                this.MaxItemCount /= 2;
                return true;
            }

            return false;
        }
    }
}