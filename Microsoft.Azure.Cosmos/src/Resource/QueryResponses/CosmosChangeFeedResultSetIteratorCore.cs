//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Routing;

    /// <summary>
    /// Cosmos Stand-By Feed iterator implementing Composite Continuation Token
    /// </summary>
    internal class CosmosChangeFeedResultSetIteratorCore : CosmosFeedResultSetIterator
    {
        private static readonly int DefaultMaxItemCount = 100;

        private readonly PartitionRoutingHelper partitionRoutingHelper = new PartitionRoutingHelper();
        private readonly CosmosContainerCore cosmosContainer;
        private StandByFeedContinuationToken compositeContinuationToken;
        private string containerRid;
        private int? originalMaxItemCount;

        internal CosmosChangeFeedResultSetIteratorCore(
            CosmosContainerCore cosmosContainer,
            CosmosChangeFeedRequestOptions options)
        {
            this.cosmosContainer = cosmosContainer;
            this.HasMoreResults = true;
            this.changeFeedOptions = options;
            this.originalMaxItemCount = options.MaxItemCount;
            this.compositeContinuationToken = new StandByFeedContinuationToken(options.RequestContinuation);
        }

        /// <summary>
        /// The Continuation Token
        /// </summary>
        protected string continuationToken => this.compositeContinuationToken?.NextToken;

        /// <summary>
        /// The query options for the result set
        /// </summary>
        protected readonly CosmosChangeFeedRequestOptions changeFeedOptions;

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override async Task<CosmosResponseMessage> FetchNextSetAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            PartitionKeyRangeCache pkRangeCache = await this.cosmosContainer.Client.DocumentClient.GetPartitionKeyRangeCacheAsync();
            if (this.containerRid == null)
            {
                this.containerRid = await this.cosmosContainer.GetRID(cancellationToken);
            }

            await this.compositeContinuationToken.InitializeCompositeTokens(this.containerRid, pkRangeCache);
            this.changeFeedOptions.PartitionKeyRangeId = await this.compositeContinuationToken.GetPartitionKeyRangeIdForCurrentState(this.containerRid, pkRangeCache);
            this.changeFeedOptions.RequestContinuation = this.continuationToken;

            CosmosResponseMessage response = await this.NextResultSetDelegate(this.changeFeedOptions, cancellationToken);
            if (await this.ShouldRetryFailure(pkRangeCache, response, cancellationToken))
            {
                this.HasMoreResults = true;
                CosmosResponseMessage retryMessage = new CosmosResponseMessage(HttpStatusCode.NoContent);
                retryMessage.Headers.Continuation = this.compositeContinuationToken.NextToken;
                return retryMessage;
            }

            // Change Feed read uses Etag for continuation
            string responseContinuationToken = response.Headers.ETag;
            this.HasMoreResults = GetHasMoreResults(responseContinuationToken, response.StatusCode);
            if (!this.HasMoreResults)
            {
                // Current Range is done, push it to the end
                response.Headers.Continuation = this.compositeContinuationToken.PushCurrentToBack();
                this.HasMoreResults = true;
            }
            else
            {
                response.Headers.Continuation = this.compositeContinuationToken.UpdateCurrentToken(responseContinuationToken);
            }

            return response;
        }

        internal static bool GetHasMoreResults(
            string continuationToken, 
            HttpStatusCode statusCode)
        {
            return continuationToken != null && statusCode != HttpStatusCode.NotModified;
        }

        /// <summary>
        /// During Feed read, split can happen or Max Item count can go beyond the max response size
        /// </summary>
        internal async Task<bool> ShouldRetryFailure(
            PartitionKeyRangeCache pkRangeCache, 
            CosmosResponseMessage response, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotModified)
            {
                if (this.changeFeedOptions.MaxItemCount != this.originalMaxItemCount)
                {
                    this.changeFeedOptions.MaxItemCount = this.originalMaxItemCount;   // Reset after successful execution.
                }

                return false;
            }

            bool partitionNotFound = response.StatusCode == HttpStatusCode.NotFound 
                && response.Headers.SubStatusCode != Documents.SubStatusCodes.ReadSessionNotAvailable;
            if (partitionNotFound)
            {
                this.containerRid = await this.cosmosContainer.GetRID(cancellationToken);
                await this.compositeContinuationToken.InitializeCompositeTokens(this.containerRid, pkRangeCache, true);
                return true;
            }

            bool partitionSplit = response.StatusCode == HttpStatusCode.Gone 
                && (response.Headers.SubStatusCode == Documents.SubStatusCodes.PartitionKeyRangeGone || response.Headers.SubStatusCode == Documents.SubStatusCodes.CompletingSplit);
            if (partitionSplit)
            {
                return await this.compositeContinuationToken.HandleRequestSplit(this.containerRid, pkRangeCache);
            }

            bool pageSizeError = response.ErrorMessage.Contains("Reduce page size and try again.");
            if (pageSizeError)
            {
                if (!this.changeFeedOptions.MaxItemCount.HasValue)
                {
                    this.changeFeedOptions.MaxItemCount = CosmosChangeFeedResultSetIteratorCore.DefaultMaxItemCount;
                }
                else if (this.changeFeedOptions.MaxItemCount <= 1)
                {
                    return false;
                }

                this.changeFeedOptions.MaxItemCount /= 2;
                return true;
            }

            return false;
        }

        private Task<CosmosResponseMessage> NextResultSetDelegate(
            CosmosChangeFeedRequestOptions options,
            CancellationToken cancellationToken)
        {
            Uri resourceUri = this.cosmosContainer.LinkUri;
            return ExecUtils.ProcessResourceOperationAsync<CosmosResponseMessage>(
                client: this.cosmosContainer.Database.Client,
                resourceUri: resourceUri,
                resourceType: Documents.ResourceType.Document,
                operationType: Documents.OperationType.ReadFeed,
                requestOptions: options,
                requestEnricher: request => { },
                responseCreator: response => response,
                partitionKey: null,
                streamPayload: null,
                cancellationToken: cancellationToken);
        }
    }
}