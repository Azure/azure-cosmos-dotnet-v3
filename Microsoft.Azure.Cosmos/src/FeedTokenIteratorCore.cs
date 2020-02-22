//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using static Microsoft.Azure.Documents.RuntimeConstants;

    /// <summary>
    /// Cosmos feed stream iterator. This is used to get the query responses with a Stream content
    /// </summary>
    internal class FeedTokenIteratorCore : FeedIteratorInternal
    {
        private readonly PartitionRoutingHelper partitionRoutingHelper = new PartitionRoutingHelper();
        private readonly ContainerCore containerCore;
        private readonly Uri resourceLink;
        private readonly ResourceType resourceType;
        private readonly SqlQuerySpec querySpec;
        private bool hasMoreResultsInternal;
        private FeedTokenInternal feedTokenInternal;

        internal FeedTokenIteratorCore(
            ContainerCore containerCore,
            Uri resourceLink,
            ResourceType resourceType,
            QueryDefinition queryDefinition,
            string continuationToken,
            FeedTokenInternal feedTokenInternal,
            QueryRequestOptions options)
        {
            this.resourceLink = resourceLink;
            this.containerCore = containerCore;
            this.resourceType = resourceType;
            this.querySpec = queryDefinition?.ToSqlQuerySpec();
            this.feedTokenInternal = feedTokenInternal;
            this.continuationToken = continuationToken ?? this.feedTokenInternal?.GetContinuation();
            this.requestOptions = options;
            this.hasMoreResultsInternal = true;
        }

        public override bool HasMoreResults => this.hasMoreResultsInternal;

#if PREVIEW
        public override
#else
        internal
#endif
        FeedToken FeedToken => this.feedTokenInternal;

        /// <summary>
        /// The query options for the result set
        /// </summary>
        protected QueryRequestOptions requestOptions { get; }

        /// <summary>
        /// The Continuation Token
        /// </summary>
        protected string continuationToken { get; set; }

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            Stream stream = null;
            OperationType operation = OperationType.ReadFeed;
            if (this.querySpec != null)
            {
                stream = this.containerCore.ClientContext.SerializerCore.ToStreamSqlQuerySpec(this.querySpec, this.resourceType);
                operation = OperationType.Query;
            }

            if (this.feedTokenInternal == null)
            {
                Routing.PartitionKeyRangeCache partitionKeyRangeCache = await this.containerCore.ClientContext.DocumentClient.GetPartitionKeyRangeCacheAsync();
                string containerRId = await this.containerCore.GetRIDAsync(cancellationToken);
                IReadOnlyList<PartitionKeyRange> partitionKeyRanges = await partitionKeyRangeCache.TryGetOverlappingRangesAsync(
                        containerRId,
                        new Documents.Routing.Range<string>(
                            Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                            Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                            isMinInclusive: true,
                            isMaxInclusive: false),
                        forceRefresh: true);
                // ReadAll scenario, initialize with one token for all
                this.feedTokenInternal = new FeedTokenEPKRange(containerRId, partitionKeyRanges);
            }

            ResponseMessage response = await this.containerCore.ClientContext.ProcessResourceOperationStreamAsync(
               resourceUri: this.resourceLink,
               resourceType: this.resourceType,
               operationType: operation,
               requestOptions: this.requestOptions,
               cosmosContainerCore: null,
               partitionKey: this.requestOptions?.PartitionKey,
               streamPayload: stream,
               requestEnricher: request =>
               {
                   QueryRequestOptions.FillContinuationToken(request, this.continuationToken);
                   if (this.querySpec != null)
                   {
                       request.Headers.Add(HttpConstants.HttpHeaders.ContentType, MediaTypes.QueryJson);
                       request.Headers.Add(HttpConstants.HttpHeaders.IsQuery, bool.TrueString);
                   }

                   this.feedTokenInternal?.EnrichRequest(request);
               },
               diagnosticsScope: null,
               cancellationToken: cancellationToken);

            // Retry in case of splits or other scenarios
            if (await this.feedTokenInternal.ShouldRetryAsync(this.containerCore, response, cancellationToken))
            {
                return await this.ReadNextAsync(cancellationToken);
            }

            if (response.IsSuccessStatusCode)
            {
                this.feedTokenInternal.UpdateContinuation(response.Headers.ContinuationToken);
            }

            this.continuationToken = this.feedTokenInternal.GetContinuation();
            this.hasMoreResultsInternal = !this.feedTokenInternal.IsDone;
            return response;
        }

        public override bool TryGetContinuationToken(out string continuationToken)
        {
            continuationToken = this.continuationToken;
            return true;
        }
    }
}