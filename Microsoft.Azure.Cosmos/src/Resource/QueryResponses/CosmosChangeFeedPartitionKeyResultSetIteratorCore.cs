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
    internal class CosmosChangeFeedPartitionKeyResultSetIteratorCore<T> : CosmosResultSetIterator<T>
    {
        private readonly CosmosContainerCore cosmosContainer;
        private string continuationToken;
        private string partitionKeyRangeId;
        private int? maxItemCount;

        internal CosmosChangeFeedPartitionKeyResultSetIteratorCore(
            CosmosContainerCore cosmosContainer,
            string partitionKeyRangeId,
            string continuationToken,
            int? maxItemCount,
            CosmosChangeFeedRequestOptions options)
        {
            if (cosmosContainer == null) throw new ArgumentNullException(nameof(cosmosContainer));
            if (partitionKeyRangeId == null) throw new ArgumentNullException(nameof(partitionKeyRangeId));

            this.cosmosContainer = cosmosContainer;
            this.changeFeedOptions = options;
            this.maxItemCount = maxItemCount;
            this.continuationToken = continuationToken;
            this.partitionKeyRangeId = partitionKeyRangeId;
        }

        /// <summary>
        /// The query options for the result set
        /// </summary>
        protected readonly CosmosChangeFeedRequestOptions changeFeedOptions;

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override Task<CosmosQueryResponse<T>> FetchNextSetAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            return this.NextResultSetDelegate(this.continuationToken, this.partitionKeyRangeId, this.maxItemCount, this.changeFeedOptions, cancellationToken)
                .ContinueWith(task =>
                {
                    CosmosQueryResponse<T> response = task.Result;
                    this.continuationToken = response.ContinuationToken;
                    return response;
                }, cancellationToken);
        }

        internal virtual Task<CosmosQueryResponse<T>> NextResultSetDelegate(
            string continuationToken,
            string partitionKeyRangeId,
            int? maxItemCount,
            CosmosChangeFeedRequestOptions options,
            CancellationToken cancellationToken)
        {
            Uri resourceUri = this.cosmosContainer.LinkUri;
            return ExecUtils.ProcessResourceOperationAsync<CosmosQueryResponse<T>>(
                client: this.cosmosContainer.Database.Client,
                resourceUri: resourceUri,
                resourceType: Documents.ResourceType.Document,
                operationType: Documents.OperationType.ReadFeed,
                requestOptions: options,
                requestEnricher: request => {
                    CosmosChangeFeedRequestOptions.FillContinuationToken(request, continuationToken);
                    CosmosChangeFeedRequestOptions.FillMaxItemCount(request, maxItemCount);
                    CosmosChangeFeedRequestOptions.FillPartitionKeyRangeId(request, partitionKeyRangeId);
                },
                responseCreator: response =>
                {
                    // Change Feed uses ETAG
                    string responseContinuationToken = response.Headers.ETag;
                    bool hasMoreResults = response.StatusCode != HttpStatusCode.NotModified;
                    return CosmosQueryResponse<T>.CreateResponse<T>(response.Content, this.cosmosContainer.Database.Client.CosmosJsonSerializer, responseContinuationToken, hasMoreResults);
                },
                partitionKey: null,
                streamPayload: null,
                cancellationToken: cancellationToken);
        }
    }
}