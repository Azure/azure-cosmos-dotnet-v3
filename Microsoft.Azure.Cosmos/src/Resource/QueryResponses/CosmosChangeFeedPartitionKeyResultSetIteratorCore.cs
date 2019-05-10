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
    internal class CosmosChangeFeedPartitionKeyResultSetIteratorCore : FeedIterator
    {
        private readonly CosmosClientContext clientContext;
        private readonly CosmosContainerCore cosmosContainer;
        private readonly CosmosChangeFeedRequestOptions changeFeedOptions;
        private string continuationToken;
        private string partitionKeyRangeId;

        internal CosmosChangeFeedPartitionKeyResultSetIteratorCore(
            CosmosClientContext clientContext,
            CosmosContainerCore cosmosContainer,
            string partitionKeyRangeId,
            string continuationToken,
            int? maxItemCount,
            CosmosChangeFeedRequestOptions options)
        {
            if (cosmosContainer == null) throw new ArgumentNullException(nameof(cosmosContainer));
            if (partitionKeyRangeId == null) throw new ArgumentNullException(nameof(partitionKeyRangeId));

            this.clientContext = clientContext;
            this.cosmosContainer = cosmosContainer;
            this.changeFeedOptions = options;
            this.MaxItemCount = maxItemCount;
            this.continuationToken = continuationToken;
            this.partitionKeyRangeId = partitionKeyRangeId;
        }

        /// <summary>
        /// Gets or sets the maximum number of items to be returned in the enumeration operation in the Azure Cosmos DB service.
        /// </summary>
        public int? MaxItemCount { get; set; }

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A change feed response from cosmos service</returns>
        public override Task<CosmosResponseMessage> FetchNextSetAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            return this.NextResultSetDelegate(this.continuationToken, this.partitionKeyRangeId, this.MaxItemCount, this.changeFeedOptions, cancellationToken)
                .ContinueWith(task =>
                {
                    CosmosResponseMessage response = task.Result;
                    // Change Feed uses ETAG
                    this.continuationToken = response.Headers.ETag;
                    this.HasMoreResults = response.StatusCode != HttpStatusCode.NotModified;
                    response.Headers.Continuation = this.continuationToken;
                    return response;
                }, cancellationToken);
        }

        private Task<CosmosResponseMessage> NextResultSetDelegate(
            string continuationToken,
            string partitionKeyRangeId,
            int? maxItemCount,
            CosmosChangeFeedRequestOptions options,
            CancellationToken cancellationToken)
        {
            Uri resourceUri = this.cosmosContainer.LinkUri;
            return this.clientContext.ProcessResourceOperationAsync<CosmosResponseMessage>(
                cosmosContainerCore: this.cosmosContainer,
                resourceUri: resourceUri,
                resourceType: Documents.ResourceType.Document,
                operationType: Documents.OperationType.ReadFeed,
                requestOptions: options,
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