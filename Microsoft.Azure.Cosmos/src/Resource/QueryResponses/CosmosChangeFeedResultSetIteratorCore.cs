//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Routing;

    /// <summary>
    /// Cosmos Stand-By Feed iterator implementing Composite Continuation Token
    /// </summary>
    internal class CosmosChangeFeedResultSetIteratorCore : CosmosFeedResultSetIterator
    {
        private const int DefaultMaxItemCount = 100;
        private const string PageSizeErrorOnChangeFeedText = "Reduce page size and try again.";

        internal StandByFeedContinuationToken compositeContinuationToken;

        private readonly CosmosContainerCore cosmosContainer;
        private readonly int? originalMaxItemCount;
        private string containerRid;
        private string continuationToken;
        private string partitionKeyRangeId;
        private int? maxItemCount;

        internal CosmosChangeFeedResultSetIteratorCore(
            CosmosContainerCore cosmosContainer,
            string continuationToken,
            int? maxItemCount,
            CosmosChangeFeedRequestOptions options)
        {
            if (cosmosContainer == null) throw new ArgumentNullException(nameof(cosmosContainer));

            this.cosmosContainer = cosmosContainer;
            this.changeFeedOptions = options;
            this.maxItemCount = maxItemCount;
            this.originalMaxItemCount = maxItemCount;
            this.continuationToken = continuationToken;
            this.HasMoreResults = true;
        }

        /// <summary>
        /// The query options for the result set
        /// </summary>
        protected readonly CosmosChangeFeedRequestOptions changeFeedOptions;

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override async Task<CosmosResponseMessage> FetchNextSetAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (this.compositeContinuationToken == null)
            {
                PartitionKeyRangeCache pkRangeCache = await this.cosmosContainer.Client.DocumentClient.GetPartitionKeyRangeCacheAsync();
                this.containerRid = await this.cosmosContainer.GetRID(cancellationToken);
                this.compositeContinuationToken = await StandByFeedContinuationToken.CreateAsync(this.containerRid, this.continuationToken, pkRangeCache.TryGetOverlappingRangesAsync);
            }

            (CompositeContinuationToken currentRangeToken, string rangeId) = await this.compositeContinuationToken.GetCurrentTokenAsync();
            this.partitionKeyRangeId = rangeId;
            this.continuationToken = currentRangeToken.Token;

            CosmosResponseMessage response = await this.NextResultSetDelegate(this.continuationToken, this.partitionKeyRangeId, this.maxItemCount, this.changeFeedOptions, cancellationToken);
            if (await this.ShouldRetryFailureAsync(response, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                (CompositeContinuationToken currentRangeTokenForRetry, string rangeIdForRetry) = await this.compositeContinuationToken.GetCurrentTokenAsync();
                currentRangeToken = currentRangeTokenForRetry;
                this.partitionKeyRangeId = rangeIdForRetry;
                this.continuationToken = currentRangeToken.Token;
                response = await this.NextResultSetDelegate(this.continuationToken, this.partitionKeyRangeId, this.maxItemCount, this.changeFeedOptions, cancellationToken);
            }

            // Change Feed read uses Etag for continuation
            string responseContinuationToken = response.Headers.ETag;
            bool hasMoreResults = response.StatusCode != HttpStatusCode.NotModified;
            if (!hasMoreResults)
            {
                // Current Range is done, push it to the end
                this.compositeContinuationToken.MoveToNextToken();
            }
            else if (response.IsSuccessStatusCode)
            {
                currentRangeToken.Token = responseContinuationToken;
            }

            // Send to the user the composite state for all ranges
            response.Headers.Continuation = this.compositeContinuationToken.ToString();
            return response;
        }

        /// <summary>
        /// During Feed read, split can happen or Max Item count can go beyond the max response size
        /// </summary>
        internal async Task<bool> ShouldRetryFailureAsync(
            CosmosResponseMessage response, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotModified)
            {
                if (this.maxItemCount != this.originalMaxItemCount)
                {
                    this.maxItemCount = this.originalMaxItemCount;   // Reset after successful execution.
                }

                return false;
            }

            bool partitionSplit = response.StatusCode == HttpStatusCode.Gone 
                && (response.Headers.SubStatusCode == Documents.SubStatusCodes.PartitionKeyRangeGone || response.Headers.SubStatusCode == Documents.SubStatusCodes.CompletingSplit);
            if (partitionSplit)
            {
                // Forcing stale refresh of Partition Key Ranges Cache
                await this.compositeContinuationToken.GetCurrentTokenAsync(forceRefresh: true);
                return true;
            }

            bool pageSizeError = response.ErrorMessage.Contains(CosmosChangeFeedResultSetIteratorCore.PageSizeErrorOnChangeFeedText);
            if (pageSizeError)
            {
                if (!this.maxItemCount.HasValue)
                {
                    this.maxItemCount = CosmosChangeFeedResultSetIteratorCore.DefaultMaxItemCount;
                }
                else if (this.maxItemCount <= 1)
                {
                    return false;
                }

                this.maxItemCount /= 2;
                return true;
            }

            return false;
        }

        internal virtual Task<CosmosResponseMessage> NextResultSetDelegate(
            string continuationToken,
            string partitionKeyRangeId,
            int? maxItemCount,
            CosmosChangeFeedRequestOptions options,
            CancellationToken cancellationToken)
        {
            Uri resourceUri = this.cosmosContainer.LinkUri;
            return ExecUtils.ProcessResourceOperationAsync<CosmosResponseMessage>(
                client: this.cosmosContainer.Database.Client,
                resourceUri: resourceUri,
                resourceType: Documents.ResourceType.Document,
                operationType: Documents.OperationType.ReadFeed,
                requestOptions: options,
                cosmosContainerCore: this.cosmosContainer,
                requestEnricher: request => {
                    CosmosChangeFeedRequestOptions.FillContinuationToken(request, continuationToken);
                    CosmosChangeFeedRequestOptions.FillMaxItemCount(request, maxItemCount);
                    CosmosChangeFeedRequestOptions.FillPartitionKeyRangeId(request, partitionKeyRangeId);
                },
                responseCreator: response => response,
                partitionKey: null,
                streamPayload: null,
                cancellationToken: cancellationToken);
        }
    }
}