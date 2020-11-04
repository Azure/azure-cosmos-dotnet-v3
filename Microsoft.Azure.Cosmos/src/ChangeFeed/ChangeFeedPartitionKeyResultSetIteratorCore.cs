//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Cosmos Change Feed Iterator for a particular Partition Key Range
    /// </summary>
    internal sealed class ChangeFeedPartitionKeyResultSetIteratorCore : FeedIteratorInternal
    {
        public static ChangeFeedPartitionKeyResultSetIteratorCore Create(
            DocumentServiceLease lease,
            string continuationToken,
            int? maxItemCount,
            ContainerInternal container,
            DateTime? startTime,
            bool startFromBeginning,
            Routing.PartitionKeyRangeCache partitionKeyRangeCache = null)
        {
            // Back compat with Partition based leases
            FeedRangeInternal feedRange = lease is DocumentServiceLeaseCoreEpk ? lease.FeedRange : new FeedRangePartitionKeyRange(lease.CurrentLeaseToken);

            ChangeFeedStartFrom startFrom;
            if (continuationToken != null)
            {
                // For continuation based feed range we need to manufactor a new continuation token that has the partition key range id in it.
                startFrom = new ChangeFeedStartFromContinuationAndFeedRange(continuationToken, feedRange);
            }
            else if (startTime.HasValue)
            {
                startFrom = ChangeFeedStartFrom.Time(startTime.Value, feedRange);
            }
            else if (startFromBeginning)
            {
                startFrom = ChangeFeedStartFrom.Beginning(feedRange);
            }
            else
            {
                startFrom = ChangeFeedStartFrom.Now(feedRange);
            }

            ChangeFeedRequestOptions requestOptions = new ChangeFeedRequestOptions()
            {
                PageSizeHint = maxItemCount,
            };

            return new ChangeFeedPartitionKeyResultSetIteratorCore(
                container: container,
                changeFeedStartFrom: startFrom,
                feedRangeInternal: feedRange,
                options: requestOptions,
                partitionKeyRangeCache: partitionKeyRangeCache);
        }

        private readonly CosmosClientContext clientContext;
        private readonly ContainerInternal container;
        private readonly ChangeFeedRequestOptions changeFeedOptions;
        private readonly FeedRangeInternal feedRangeInternal;
        private ChangeFeedStartFrom changeFeedStartFrom;
        private bool hasMoreResultsInternal;
        private Routing.PartitionKeyRangeCache partitionKeyRangeCache;

        private ChangeFeedPartitionKeyResultSetIteratorCore(
            FeedRangeInternal feedRangeInternal,
            ContainerInternal container,
            ChangeFeedStartFrom changeFeedStartFrom,
            ChangeFeedRequestOptions options,
            Routing.PartitionKeyRangeCache partitionKeyRangeCache)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.changeFeedStartFrom = changeFeedStartFrom ?? throw new ArgumentNullException(nameof(changeFeedStartFrom));
            this.feedRangeInternal = feedRangeInternal ?? throw new ArgumentNullException(nameof(feedRangeInternal));
            this.partitionKeyRangeCache = partitionKeyRangeCache;
            this.clientContext = this.container.ClientContext;
            this.changeFeedOptions = options;
        }

        public override bool HasMoreResults => this.hasMoreResultsInternal;

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A change feed response from cosmos service</returns>
        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            if (this.feedRangeInternal is FeedRangeEpk feedRangeEpk)
            {
                if (this.partitionKeyRangeCache == null)
                {
                    this.partitionKeyRangeCache = await this.clientContext.DocumentClient.GetPartitionKeyRangeCacheAsync();
                }

                IReadOnlyList<PartitionKeyRange> overlappingRanges = await this.partitionKeyRangeCache.TryGetOverlappingRangesAsync(
                    collectionRid: await this.container.GetRIDAsync(cancellationToken),
                    range: feedRangeEpk.Range);

                // Force lease to be handled for split or merge
                if ((overlappingRanges == null) || (overlappingRanges.Count != 1))
                {
                    ResponseMessage goneResponse = new ResponseMessage(System.Net.HttpStatusCode.Gone);
                    goneResponse.Headers.SubStatusCode = SubStatusCodes.PartitionKeyRangeGone;

                    return goneResponse;
                }
            }

            ResponseMessage responseMessage = await this.clientContext.ProcessResourceOperationStreamAsync(
                cosmosContainerCore: this.container,
                resourceUri: this.container.LinkUri,
                resourceType: ResourceType.Document,
                operationType: OperationType.ReadFeed,
                requestOptions: this.changeFeedOptions,
                requestEnricher: (requestMessage) =>
                {
                    ChangeFeedStartFromRequestOptionPopulator visitor = new ChangeFeedStartFromRequestOptionPopulator(requestMessage);
                    this.changeFeedStartFrom.Accept(visitor);
                },
                partitionKey: default,
                streamPayload: default,
                diagnosticsContext: default,
                cancellationToken: cancellationToken);

            // Change Feed uses etag as continuation token.
            string etag = responseMessage.Headers.ETag;
            this.hasMoreResultsInternal = responseMessage.IsSuccessStatusCode;
            responseMessage.Headers.ContinuationToken = etag;
            this.changeFeedStartFrom = new ChangeFeedStartFromContinuationAndFeedRange(etag, this.feedRangeInternal);

            return responseMessage;
        }
    }
}