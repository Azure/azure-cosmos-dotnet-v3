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
    internal class ChangeFeedResultSetIteratorCore : FeedIterator
    {
        private const int DefaultMaxItemCount = 100;
        private const string PageSizeErrorOnChangeFeedText = "Reduce page size and try again.";

        internal StandByFeedContinuationToken compositeContinuationToken;

        private readonly CosmosClientContext clientContext;
        private readonly ContainerCore container;
        private readonly int? originalMaxItemCount;
        private string containerRid;
        private string continuationToken;
        private string partitionKeyRangeId;
        private int? maxItemCount;
        private bool hasMoreResultsInternal;

        internal ChangeFeedResultSetIteratorCore(
            CosmosClientContext clientContext,
            ContainerCore container,
            string continuationToken,
            int? maxItemCount,
            ChangeFeedRequestOptions options)
        {
            if (container == null) throw new ArgumentNullException(nameof(container));

            this.clientContext = clientContext;
            this.container = container;
            this.changeFeedOptions = options;
            this.maxItemCount = maxItemCount;
            this.originalMaxItemCount = maxItemCount;
            this.continuationToken = continuationToken;
            this.hasMoreResultsInternal = true;
        }

        /// <summary>
        /// The query options for the result set
        /// </summary>
        protected readonly ChangeFeedRequestOptions changeFeedOptions;

        public override bool HasMoreResults => this.hasMoreResultsInternal;

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (this.compositeContinuationToken == null)
            {
                PartitionKeyRangeCache pkRangeCache = await this.clientContext.DocumentClient.GetPartitionKeyRangeCacheAsync();
                this.containerRid = await this.container.GetRIDAsync(cancellationToken);
                this.compositeContinuationToken = await StandByFeedContinuationToken.CreateAsync(this.containerRid, this.continuationToken, pkRangeCache.TryGetOverlappingRangesAsync);
            }

            (CompositeContinuationToken currentRangeToken, string rangeId) = await this.compositeContinuationToken.GetCurrentTokenAsync();
            this.partitionKeyRangeId = rangeId;
            this.continuationToken = currentRangeToken.Token;

            ResponseMessage response = await this.NextResultSetDelegateAsync(this.continuationToken, this.partitionKeyRangeId, this.maxItemCount, this.changeFeedOptions, cancellationToken);
            if (await this.ShouldRetryFailureAsync(response, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                (CompositeContinuationToken currentRangeTokenForRetry, string rangeIdForRetry) = await this.compositeContinuationToken.GetCurrentTokenAsync();
                currentRangeToken = currentRangeTokenForRetry;
                this.partitionKeyRangeId = rangeIdForRetry;
                this.continuationToken = currentRangeToken.Token;
                response = await this.NextResultSetDelegateAsync(this.continuationToken, this.partitionKeyRangeId, this.maxItemCount, this.changeFeedOptions, cancellationToken);
            }

            // Change Feed read uses Etag for continuation
            string responseContinuationToken = response.Headers.ETag;
            bool hasMoreResults = response.StatusCode != HttpStatusCode.NotModified;
            if (!hasMoreResults)
            {
                currentRangeToken.Token = responseContinuationToken;
                // Current Range is done, push it to the end
                this.compositeContinuationToken.MoveToNextToken();
            }
            else if (response.IsSuccessStatusCode)
            {
                currentRangeToken.Token = responseContinuationToken;
            }

            // Send to the user the composite state for all ranges
            response.Headers.ContinuationToken = this.compositeContinuationToken.ToString();
            return response;
        }

        /// <summary>
        /// During Feed read, split can happen or Max Item count can go beyond the max response size
        /// </summary>
        internal async Task<bool> ShouldRetryFailureAsync(
            ResponseMessage response, 
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

            bool pageSizeError = response.ErrorMessage.Contains(ChangeFeedResultSetIteratorCore.PageSizeErrorOnChangeFeedText);
            if (pageSizeError)
            {
                if (!this.maxItemCount.HasValue)
                {
                    this.maxItemCount = ChangeFeedResultSetIteratorCore.DefaultMaxItemCount;
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

        internal virtual Task<ResponseMessage> NextResultSetDelegateAsync(
            string continuationToken,
            string partitionKeyRangeId,
            int? maxItemCount,
            ChangeFeedRequestOptions options,
            CancellationToken cancellationToken)
        {
            Uri resourceUri = this.container.LinkUri;
            return this.clientContext.ProcessResourceOperationAsync<ResponseMessage>(
                resourceUri: resourceUri,
                resourceType: Documents.ResourceType.Document,
                operationType: Documents.OperationType.ReadFeed,
                requestOptions: options,
                cosmosContainerCore: this.container,
                requestEnricher: request => 
                {
                    ChangeFeedRequestOptions.FillContinuationToken(request, continuationToken);
                    ChangeFeedRequestOptions.FillMaxItemCount(request, maxItemCount);
                    ChangeFeedRequestOptions.FillPartitionKeyRangeId(request, partitionKeyRangeId);
                },
                responseCreator: response => response,
                partitionKey: null,
                streamPayload: null,
                cancellationToken: cancellationToken);
        }
    }
}