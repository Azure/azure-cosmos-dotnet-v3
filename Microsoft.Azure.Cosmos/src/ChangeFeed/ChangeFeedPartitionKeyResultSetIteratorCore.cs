//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;

    /// <summary>
    /// Cosmos Change Feed Iterator for a particular Partition Key Range
    /// </summary>
    internal sealed class ChangeFeedPartitionKeyResultSetIteratorCore : FeedIteratorInternal
    {
        private readonly FeedIterator feedIterator;
        private bool hasMoreResultsInternal;

        public ChangeFeedPartitionKeyResultSetIteratorCore(
            CosmosClientContext clientContext,
            ContainerInternal container,
            ChangeFeedStartFrom changeFeedStartFrom,
            ChangeFeedRequestOptions options)
        {
            this.feedIterator = container.GetChangeFeedStreamIterator(changeFeedStartFrom, options);
        }

        public override bool HasMoreResults => this.hasMoreResultsInternal;

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A change feed response from cosmos service</returns>
        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            ResponseMessage responseMessage = await this.feedIterator.ReadNextAsync(cancellationToken);
            if ((responseMessage.StatusCode != System.Net.HttpStatusCode.OK) && (responseMessage.StatusCode != System.Net.HttpStatusCode.NotModified))
            {
                this.hasMoreResultsInternal = false;
                return responseMessage;
            }

            // Parse out the single partition continuation token
            string versionedContinuationToken = responseMessage.ContinuationToken;
            CosmosObject parsedVersionedContinuationToken = CosmosObject.Parse(versionedContinuationToken);
            CosmosArray cosmosArray = (CosmosArray)parsedVersionedContinuationToken["Continuation"];
            CosmosObject changeFeedContinuationToken = (CosmosObject)cosmosArray[0];
            string etag = ((CosmosString)((CosmosObject)changeFeedContinuationToken["State"])["value"]).Value;

            // Change Feed uses etag as continuation token.
            this.hasMoreResultsInternal = responseMessage.IsSuccessStatusCode;
            responseMessage.Headers.ContinuationToken = etag;

            return responseMessage;
        }
    }
}