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
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using static Microsoft.Azure.Documents.RuntimeConstants;

    /// <summary>
    /// Cosmos feed stream iterator. This is used to get the query responses with a Stream content
    /// </summary>
    internal class FeedTokenIteratorCore : FeedIteratorInternal
    {
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
        FeedToken FeedToken => this.feedTokenInternal ?? throw new InvalidOperationException();

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

            if (this.feedTokenInternal == null
                && this.continuationToken == null)
            {
                // Only initialize on a ReadAll scenario with no previous Continuation Token
                string containerRId = await this.containerCore.GetRIDAsync(cancellationToken);
                PartitionKeyRangeCache partitionKeyRangeCache = await this.containerCore.ClientContext.DocumentClient.GetPartitionKeyRangeCacheAsync();
                IReadOnlyList<PartitionKeyRange> partitionKeyRanges = await partitionKeyRangeCache.TryGetOverlappingRangesAsync(
                        containerRId,
                        new Documents.Routing.Range<string>(
                            Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                            Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                            isMinInclusive: true,
                            isMaxInclusive: false),
                        forceRefresh: true);

                this.feedTokenInternal = new FeedTokenEPKRange(containerRId, partitionKeyRanges);
            }

            ResponseMessage response = await this.containerCore.ClientContext.ProcessResourceOperationStreamAsync(
               resourceUri: this.resourceLink,
               resourceType: this.resourceType,
               operationType: operation,
               requestOptions: this.requestOptions,
               cosmosContainerCore: null,
               partitionKey: null,
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

            if (this.feedTokenInternal != null)
            {
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
            }
            else
            {
                // Backward compatible with previously stored Continuation Token
                this.continuationToken = response.Headers.ContinuationToken;
                this.hasMoreResultsInternal = GetHasMoreResults(this.continuationToken, response.StatusCode);
            }

            return response;
        }

        public override bool TryGetContinuationToken(out string continuationToken)
        {
            continuationToken = this.continuationToken;
            return true;
        }

        internal static bool GetHasMoreResults(string continuationToken, HttpStatusCode statusCode)
        {
            // this logic might not be sufficient composite continuation token https://msdata.visualstudio.com/CosmosDB/SDK/_workitems/edit/269099
            // in the case where this is a result set iterator for a change feed, not modified indicates that
            // the enumeration is done for now.
            return continuationToken != null && statusCode != HttpStatusCode.NotModified;
        }
    }
}