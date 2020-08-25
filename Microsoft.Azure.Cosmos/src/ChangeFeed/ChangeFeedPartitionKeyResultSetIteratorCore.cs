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

    /// <summary>
    /// Cosmos Change Feed Iterator for a particular Partition Key Range
    /// </summary>
    internal sealed class ChangeFeedPartitionKeyResultSetIteratorCore : FeedIteratorInternal
    {
        private readonly CosmosClientContext clientContext;
        private readonly ContainerInternal container;
        private readonly ChangeFeedRequestOptions changeFeedOptions;
        private ChangeFeedStartFrom changeFeedStartFrom;
        private bool hasMoreResultsInternal;

        public ChangeFeedPartitionKeyResultSetIteratorCore(
            CosmosClientContext clientContext,
            ContainerInternal container,
            ChangeFeedStartFrom changeFeedStartFrom,
            ChangeFeedRequestOptions options)
        {
            this.clientContext = clientContext ?? throw new ArgumentNullException(nameof(clientContext));
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.changeFeedStartFrom = changeFeedStartFrom;
            this.changeFeedOptions = options;
        }

        public override bool HasMoreResults => this.hasMoreResultsInternal;

        public override CosmosElement GetCosmosElementContinuationToken() => throw new NotImplementedException();

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A change feed response from cosmos service</returns>
        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            ResponseMessage responseMessage = await this.clientContext.ProcessResourceOperationStreamAsync(
                cosmosContainerCore: this.container,
                resourceUri: this.container.LinkUri,
                resourceType: Documents.ResourceType.Document,
                operationType: Documents.OperationType.ReadFeed,
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

            FeedRangeInternal feedRange = (FeedRangeInternal)this.changeFeedStartFrom.Accept(ChangeFeedRangeExtractor.Singleton);
            this.changeFeedStartFrom = new ChangeFeedStartFromContinuationAndFeedRange(etag, feedRange);

            return responseMessage;
        }
    }
}