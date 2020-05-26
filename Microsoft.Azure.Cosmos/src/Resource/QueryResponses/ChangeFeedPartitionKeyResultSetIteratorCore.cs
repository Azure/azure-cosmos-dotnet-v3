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
    using Microsoft.Azure.Cosmos.Json;

    /// <summary>
    /// Cosmos Change Feed Iterator for a particular Partition Key Range
    /// </summary>
    internal sealed class ChangeFeedPartitionKeyResultSetIteratorCore : FeedIteratorInternal
    {
        private readonly ContainerInternal container;
        private readonly ChangeFeedRequestOptions changeFeedOptions;
        private string continuationToken;
        private string partitionKeyRangeId;
        private bool hasMoreResultsInternal;

        internal ChangeFeedPartitionKeyResultSetIteratorCore(
            CosmosClientContext clientContext,
            ContainerInternal container,
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

            this.ClientContext = clientContext;
            this.container = container;
            this.changeFeedOptions = options;
            this.MaxItemCount = maxItemCount;
            this.continuationToken = continuationToken;
            this.partitionKeyRangeId = partitionKeyRangeId;
        }

        /// <summary>
        /// Gets or sets the maximum number of items to be returned in the enumeration operation in the Azure Cosmos DB service.
        /// </summary>
        public int? MaxItemCount { get; set; }

        public override bool HasMoreResults => this.hasMoreResultsInternal;

        public override CosmosClientContext ClientContext { get; }

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A change feed response from cosmos service</returns>
        public override Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return this.ClientContext.OperationHelperAsync(
               nameof(FeedIteratorCore),
               this.changeFeedOptions,
               async (diagnostics) =>
               {
                   return await this.ReadNextInternalAsync(diagnostics, cancellationToken);
               });
        }

        public override async Task<ResponseMessage> ReadNextInternalAsync(CosmosDiagnosticsContext diagnosticsContext, CancellationToken cancellationToken)
        {
            ResponseMessage responseMessage = await this.NextResultSetDelegateAsync(
                this.continuationToken,
                this.partitionKeyRangeId,
                this.MaxItemCount,
                this.changeFeedOptions,
                cancellationToken);

            // Change Feed uses ETAG
            this.continuationToken = responseMessage.Headers.ETag;
            this.hasMoreResultsInternal = responseMessage.StatusCode != HttpStatusCode.NotModified;
            responseMessage.Headers.ContinuationToken = this.continuationToken;
            return responseMessage;
        }

        private Task<ResponseMessage> NextResultSetDelegateAsync(
            string continuationToken,
            string partitionKeyRangeId,
            int? maxItemCount,
            ChangeFeedRequestOptions options,
            CancellationToken cancellationToken)
        {
            Uri resourceUri = this.container.LinkUri;
            return this.ClientContext.ProcessResourceOperationStreamAsync(
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
               diagnosticsContext: null,
               cancellationToken: cancellationToken);

        }
    }
}