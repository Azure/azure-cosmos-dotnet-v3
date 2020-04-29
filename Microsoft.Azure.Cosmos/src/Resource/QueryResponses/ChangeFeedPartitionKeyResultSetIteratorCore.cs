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

            this.clientContext = clientContext;
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

        public override CosmosElement GetCosmsoElementContinuationToken()
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
            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(this.changeFeedOptions);
            using (diagnosticsContext.GetOverallScope())
            {
                cancellationToken.ThrowIfCancellationRequested();

                return this.NextResultSetDelegateAsync(
                    this.continuationToken,
                    this.partitionKeyRangeId,
                    this.MaxItemCount,
                    this.changeFeedOptions,
                    diagnosticsContext,
                    cancellationToken)
                    .ContinueWith(task =>
                    {
                        ResponseMessage response = task.Result;
                        // Change Feed uses ETAG
                        this.continuationToken = response.Headers.ETag;
                        this.hasMoreResultsInternal = response.StatusCode != HttpStatusCode.NotModified;
                        response.Headers.ContinuationToken = this.continuationToken;
                        return response;
                    }, cancellationToken);
            }
        }

        private async Task<ResponseMessage> NextResultSetDelegateAsync(
            string continuationToken,
            string partitionKeyRangeId,
            int? maxItemCount,
            ChangeFeedRequestOptions options,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Uri resourceUri = this.container.LinkUri;
            ResponseMessage responseMessage = await this.clientContext.ProcessResourceOperationStreamAsync(
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

            if (responseMessage.IsSuccessStatusCode &&
                this.changeFeedOptions.CosmosStreamTransformer != null &&
                responseMessage.Content != null)
            {
                responseMessage.Content = await FeedIteratorUtil.GetTransformedResponseMessageAsync(
                    responseMessage.Content,
                    this.clientContext.SerializerCore,
                    this.changeFeedOptions.CosmosStreamTransformer,
                    diagnosticsContext,
                    cancellationToken);

                return responseMessage;
            }

            return responseMessage;
        }
    }
}