//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Cosmos Stand-By Feed iterator implementing Composite Continuation Token
    /// </summary>
    /// <remarks>
    /// Legacy, see <see cref="ChangeFeedIteratorCore"/>.
    /// </remarks>
    /// <seealso cref="ChangeFeedIteratorCore"/>
    internal class StandByFeedIteratorCore : FeedIteratorInternal
    {
        internal StandByFeedContinuationToken compositeContinuationToken;

        private readonly CosmosClientContext clientContext;
        private readonly ContainerInternal container;
        private string containerRid;
        private string continuationToken;
        private int? maxItemCount;

        internal StandByFeedIteratorCore(
            CosmosClientContext clientContext,
            ContainerInternal container,
            string continuationToken,
            int? maxItemCount,
            StandByFeedIteratorRequestOptions options)
        {
            if (container == null) throw new ArgumentNullException(nameof(container));

            this.clientContext = clientContext;
            this.container = container;
            this.changeFeedOptions = options;
            this.maxItemCount = maxItemCount;
            this.continuationToken = continuationToken;
        }

        /// <summary>
        /// The query options for the result set
        /// </summary>
        protected readonly StandByFeedIteratorRequestOptions changeFeedOptions;

        public override bool HasMoreResults => true;

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ReadNextAsync(NoOpTrace.Singleton, cancellationToken);
        }

        public override async Task<ResponseMessage> ReadNextAsync(ITrace trace, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            string firstNotModifiedKeyRangeId = null;
            string currentKeyRangeId;
            string nextKeyRangeId;
            ResponseMessage response;
            do
            {
                (currentKeyRangeId, response) = await this.ReadNextInternalAsync(trace, cancellationToken);
                // Read only one range at a time - Breath first
                this.compositeContinuationToken.MoveToNextToken();
                (_, nextKeyRangeId) = await this.compositeContinuationToken.GetCurrentTokenAsync();
                if (response.StatusCode != HttpStatusCode.NotModified)
                {
                    break;
                }

                // HttpStatusCode.NotModified
                if (string.IsNullOrEmpty(firstNotModifiedKeyRangeId))
                {
                    // First NotModified Response
                    firstNotModifiedKeyRangeId = currentKeyRangeId;
                }
            }
            // We need to keep checking across all ranges until one of them returns OK or we circle back to the start
            while (!firstNotModifiedKeyRangeId.Equals(nextKeyRangeId, StringComparison.InvariantCultureIgnoreCase));

            // Send to the user the composite state for all ranges
            response.Headers.ContinuationToken = this.compositeContinuationToken.ToString();
            return response;
        }

        internal async Task<Tuple<string, ResponseMessage>> ReadNextInternalAsync(ITrace trace, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            if (this.compositeContinuationToken == null)
            {
                PartitionKeyRangeCache pkRangeCache = await this.clientContext.DocumentClient.GetPartitionKeyRangeCacheAsync(trace);
                this.containerRid = await this.container.GetCachedRIDAsync(
                    forceRefresh: false, 
                    trace, 
                    cancellationToken: cancellationToken);
                this.compositeContinuationToken = await StandByFeedContinuationToken.CreateAsync(
                    this.containerRid, 
                    this.continuationToken, 
                    pkRangeCache.TryGetOverlappingRangesAsync);
            }

            (CompositeContinuationToken currentRangeToken, string rangeId) = await this.compositeContinuationToken.GetCurrentTokenAsync();
            string partitionKeyRangeId = rangeId;
            this.continuationToken = currentRangeToken.Token;
            ResponseMessage response = await this.NextResultSetDelegateAsync(this.continuationToken, partitionKeyRangeId, this.maxItemCount, this.changeFeedOptions, trace, cancellationToken);
            if (await this.ShouldRetryFailureAsync(response, cancellationToken))
            {
                return await this.ReadNextInternalAsync(trace, cancellationToken);
            }

            if (response.IsSuccessStatusCode
                || response.StatusCode == HttpStatusCode.NotModified)
            {
                // Change Feed read uses Etag for continuation
                currentRangeToken.Token = response.Headers.ETag;
            }

            return new Tuple<string, ResponseMessage>(partitionKeyRangeId, response);
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

            return false;
        }

        internal virtual Task<ResponseMessage> NextResultSetDelegateAsync(
            string continuationToken,
            string partitionKeyRangeId,
            int? maxItemCount,
            StandByFeedIteratorRequestOptions options,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            string resourceUri = this.container.LinkUri;
            return this.clientContext.ProcessResourceOperationAsync<ResponseMessage>(
                resourceUri: resourceUri,
                resourceType: Documents.ResourceType.Document,
                operationType: Documents.OperationType.ReadFeed,
                requestOptions: options,
                containerInternal: this.container,
                requestEnricher: request =>
                {
                    if (!string.IsNullOrWhiteSpace(continuationToken))
                    {
                        // On REST level, change feed is using IfNoneMatch/ETag instead of continuation
                        request.Headers.IfNoneMatch = continuationToken;
                    }

                    if (maxItemCount.HasValue)
                    {
                        request.Headers.PageSize = maxItemCount.Value.ToString(CultureInfo.InvariantCulture);
                    }

                    if (!string.IsNullOrEmpty(partitionKeyRangeId))
                    {
                        request.PartitionKeyRangeId = new PartitionKeyRangeIdentity(partitionKeyRangeId);
                    }
                },
                responseCreator: response => response,
                feedRange: null,
                streamPayload: null,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            throw new NotImplementedException();
        }
    }
}