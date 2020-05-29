//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Serializer;
    using Microsoft.Azure.Documents;
    using static Microsoft.Azure.Documents.RuntimeConstants;

    /// <summary>
    /// Cosmos feed stream iterator. This is used to get the query responses with a Stream content for non-partitioned results
    /// </summary>
    internal sealed class FeedIteratorCore : FeedIteratorBase
    {
        private readonly CosmosClientContext clientContext;
        private readonly Uri resourceLink;
        private readonly ResourceType resourceType;
        private readonly SqlQuerySpec querySpec;
        private readonly QueryRequestOptions queryRequestOptions;
        private bool hasMoreResultsInternal;

        public FeedIteratorCore(
            CosmosClientContext clientContext,
            Uri resourceLink,
            ResourceType resourceType,
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions options)
        {
            this.resourceLink = resourceLink;
            this.clientContext = clientContext;
            this.resourceType = resourceType;
            this.querySpec = queryDefinition?.ToSqlQuerySpec();
            this.ContinuationToken = continuationToken;
            this.queryRequestOptions = options;
            this.hasMoreResultsInternal = true;
        }

        public override bool HasMoreResults => this.hasMoreResultsInternal;

        /// <summary>
        /// The query options for the result set
        /// </summary>
        internal override RequestOptions RequestOptions => this.queryRequestOptions;

        /// <summary>
        /// The Continuation Token
        /// </summary>
        public string ContinuationToken { get; set; }

        internal async override Task<ResponseMessage> ReadNextAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Stream stream = null;
            OperationType operation = OperationType.ReadFeed;
            if (this.querySpec != null)
            {
                stream = this.clientContext.SerializerCore.ToStreamSqlQuerySpec(this.querySpec, this.resourceType);
                operation = OperationType.Query;
            }

            ResponseMessage responseMessage = await this.clientContext.ProcessResourceOperationStreamAsync(
               resourceUri: this.resourceLink,
               resourceType: this.resourceType,
               operationType: operation,
               requestOptions: this.RequestOptions,
               cosmosContainerCore: null,
               partitionKey: this.queryRequestOptions?.PartitionKey,
               streamPayload: stream,
               requestEnricher: request =>
               {
                   QueryRequestOptions.FillContinuationToken(request, this.ContinuationToken);
                   if (this.querySpec != null)
                   {
                       request.Headers.Add(HttpConstants.HttpHeaders.ContentType, MediaTypes.QueryJson);
                       request.Headers.Add(HttpConstants.HttpHeaders.IsQuery, bool.TrueString);
                   }
               },
               diagnosticsContext: diagnosticsContext,
               cancellationToken: cancellationToken);

            this.ContinuationToken = responseMessage.Headers.ContinuationToken;
            this.hasMoreResultsInternal = this.ContinuationToken != null && responseMessage.StatusCode != HttpStatusCode.NotModified;

            await CosmosElementSerializer.RewriteStreamAsTextAsync(responseMessage, this.queryRequestOptions);

            return responseMessage;
        }

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            throw new NotImplementedException();
        }
    }
}
