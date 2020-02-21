//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Cosmos Change Feed Iterator for a particular Partition Key Range
    /// </summary>
    internal sealed class ChangeFeedPartitionKeyResultSetIteratorCore : FeedIteratorInternal
    {
        private readonly CosmosClientContext clientContext;
        private readonly ContainerCore container;
        private readonly ChangeFeedRequestOptions changeFeedOptions;
        private readonly FeedTokenInternal feedToken;
        private string continuationToken;
        private string partitionKeyRangeId;
        private bool hasMoreResultsInternal;

        internal ChangeFeedPartitionKeyResultSetIteratorCore(
            CosmosClientContext clientContext,
            ContainerCore container,
            string partitionKeyRangeId,
            string continuationToken,
            int? maxItemCount,
            ChangeFeedRequestOptions options)
        {
            if (container == null)
            {
                throw new ArgumentNullException(nameof(container));
            }

            if (partitionKeyRangeId == null)
            {
                throw new ArgumentNullException(nameof(partitionKeyRangeId));
            }

            this.clientContext = clientContext;
            this.container = container;
            this.changeFeedOptions = options;
            this.MaxItemCount = maxItemCount;
            this.continuationToken = continuationToken;
            this.partitionKeyRangeId = partitionKeyRangeId;
            this.feedToken = new FeedTokenPartitionKeyRange(this.partitionKeyRangeId);
            this.feedToken.UpdateContinuation(this.continuationToken);
        }

        /// <summary>
        /// Gets or sets the maximum number of items to be returned in the enumeration operation in the Azure Cosmos DB service.
        /// </summary>
        public int? MaxItemCount { get; set; }

        public override bool HasMoreResults => this.hasMoreResultsInternal;

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

            return this.NextResultSetDelegateAsync(this.continuationToken, this.partitionKeyRangeId, this.MaxItemCount, this.changeFeedOptions, cancellationToken)
                .ContinueWith(task =>
                {
                    ResponseMessage response = task.Result;
                    // Change Feed uses ETAG
                    this.continuationToken = response.Headers.ETag;
                    this.feedToken.UpdateContinuation(this.continuationToken);
                    this.hasMoreResultsInternal = response.StatusCode != HttpStatusCode.NotModified;
                    response.Headers.ContinuationToken = this.continuationToken;
                    return response;
                }, cancellationToken);
        }

        public override bool TryGetContinuationToken(out string continuationToken)
        {
            continuationToken = this.continuationToken;
            return true;
        }

        private Task<ResponseMessage> NextResultSetDelegateAsync(
            string continuationToken,
            string partitionKeyRangeId,
            int? maxItemCount,
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
               requestEnricher: request =>
               {
                   ChangeFeedRequestOptions.FillContinuationToken(request, continuationToken);
                   ChangeFeedRequestOptions.FillMaxItemCount(request, maxItemCount);
                   ChangeFeedRequestOptions.FillPartitionKeyRangeId(request, partitionKeyRangeId);
               },
               partitionKey: null,
               streamPayload: null,
               diagnosticsScope: null,
               cancellationToken: cancellationToken);

        }
    }
}