//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
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
        private readonly ContainerCore container;
        private readonly ChangeFeedRequestOptions changeFeedOptions;
        private readonly FeedTokenInternal feedToken;
        private bool hasMoreResultsInternal;

        public ChangeFeedPartitionKeyResultSetIteratorCore(
            CosmosClientContext clientContext,
            ContainerCore container,
            ChangeFeedRequestOptions options)
        {
            this.clientContext = clientContext ?? throw new ArgumentNullException(nameof(clientContext));
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.changeFeedOptions = options;
            this.feedToken = new FeedTokenPartitionKeyRange(options?.PartitionKeyRangeId);
            if (options?.From is ChangeFeedRequestOptions.StartFromContinuation startFromContinuation)
            {
                this.feedToken.UpdateContinuation(startFromContinuation.Continuation);
            }
        }

        public override bool HasMoreResults => this.hasMoreResultsInternal;

        public override CosmosElement GetCosmsoElementContinuationToken()
        {
            throw new NotImplementedException();
        }

#if PREVIEW
        public override
#else
        internal
#endif
        FeedToken FeedToken => this.feedToken;

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A change feed response from cosmos service</returns>
        public override Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            return this.NextResultSetDelegateAsync(
                this.changeFeedOptions,
                cancellationToken)
                .ContinueWith(task =>
                {
                    ResponseMessage response = task.Result;
                    // Change Feed uses ETAG
                    this.feedToken.UpdateContinuation(response.Headers.ETag);
                    this.hasMoreResultsInternal = response.StatusCode != HttpStatusCode.NotModified;
                    response.Headers.ContinuationToken = response.Headers.ETag;
                    return response;
                }, cancellationToken);
        }

        private Task<ResponseMessage> NextResultSetDelegateAsync(
            ChangeFeedRequestOptions options,
            CancellationToken cancellationToken)
        {
            Uri resourceUri = this.container.LinkUri;
            return this.clientContext.ProcessResourceOperationStreamAsync(
               cosmosContainerCore: this.container,
               resourceUri: resourceUri,
               resourceType: Documents.ResourceType.Document,
               operationType: Documents.OperationType.ReadFeed,
               requestOptions: options,
               requestEnricher: default,
               partitionKey: default,
               streamPayload: default,
               diagnosticsContext: default,
               cancellationToken: cancellationToken);

        }
    }
}