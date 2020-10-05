//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Routing;

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
        private ChangeFeedStartFrom changeFeedStartFrom;
        private string containerRid;

        internal StandByFeedIteratorCore(
            CosmosClientContext clientContext,
            ContainerCore container,
            ChangeFeedStartFrom changeFeedStartFrom,
            ChangeFeedRequestOptions options)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            this.clientContext = clientContext;
            this.container = container;
            this.changeFeedStartFrom = changeFeedStartFrom ?? throw new ArgumentNullException(nameof(changeFeedStartFrom));
            this.changeFeedOptions = options;
        }

        /// <summary>
        /// The query options for the result set
        /// </summary>
        protected readonly ChangeFeedRequestOptions changeFeedOptions;

        public override bool HasMoreResults => true;

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            string firstNotModifiedKeyRangeId = null;
            string currentKeyRangeId;
            string nextKeyRangeId;
            ResponseMessage response;
            do
            {
                (currentKeyRangeId, response) = await this.ReadNextInternalAsync(cancellationToken);
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

        internal async Task<(string, ResponseMessage)> ReadNextInternalAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (this.compositeContinuationToken == null)
            {
                PartitionKeyRangeCache pkRangeCache = await this.clientContext.DocumentClient.GetPartitionKeyRangeCacheAsync();
                this.containerRid = await this.container.GetRIDAsync(cancellationToken);

                if (this.changeFeedStartFrom is ChangeFeedStartFromContinuation startFromContinuation)
                {
                    this.compositeContinuationToken = await StandByFeedContinuationToken.CreateAsync(
                        this.containerRid,
                        startFromContinuation.Continuation,
                        pkRangeCache.TryGetOverlappingRangesAsync);
                    (CompositeContinuationToken token, string id) = await this.compositeContinuationToken.GetCurrentTokenAsync();

                    if (token.Token != null)
                    {
                        this.changeFeedStartFrom = ChangeFeedStartFrom.ContinuationToken(token.Token);
                    }
                    else
                    {
                        this.changeFeedStartFrom = ChangeFeedStartFrom.Beginning();
                    }
                }
                else
                {
                    this.compositeContinuationToken = await StandByFeedContinuationToken.CreateAsync(
                        this.containerRid,
                        initialStandByFeedContinuationToken: null,
                        pkRangeCache.TryGetOverlappingRangesAsync);
                }
            }

            (CompositeContinuationToken currentRangeToken, string rangeId) = await this.compositeContinuationToken.GetCurrentTokenAsync();
            FeedRange feedRange = new FeedRangePartitionKeyRange(rangeId);
            if (currentRangeToken.Token != null)
            {
                this.changeFeedStartFrom = new ChangeFeedStartFromContinuationAndFeedRange(currentRangeToken.Token, (FeedRangeInternal)feedRange);
            }
            else
            {
                this.changeFeedStartFrom = ChangeFeedStartFrom.Beginning(feedRange);
            }

            ResponseMessage response = await this.NextResultSetDelegateAsync(this.changeFeedOptions, cancellationToken);
            if (await this.ShouldRetryFailureAsync(response, cancellationToken))
            {
                return await this.ReadNextInternalAsync(cancellationToken);
            }

            if (response.IsSuccessStatusCode
                || response.StatusCode == HttpStatusCode.NotModified)
            {
                // Change Feed read uses Etag for continuation
                currentRangeToken.Token = response.Headers.ETag;
            }

            return (rangeId, response);
        }

        /// <summary>
        /// During Feed read, split can happen or Max Item count can go beyond the max response size
        /// </summary>
        internal async Task<bool> ShouldRetryFailureAsync(
            ResponseMessage response,
            CancellationToken cancellationToken = default)
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
            ChangeFeedRequestOptions options,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            throw new NotImplementedException();
        }
    }
}