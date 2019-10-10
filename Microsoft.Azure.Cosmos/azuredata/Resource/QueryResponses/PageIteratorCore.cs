//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents;
    using static Microsoft.Azure.Documents.RuntimeConstants;

    internal class PageIteratorCore<T> : PageIterator<T>
    {
        private readonly CosmosClientContext clientContext;
        private readonly Uri resourceLink;
        private readonly ResourceType resourceType;
        private readonly SqlQuerySpec querySpec;
        private readonly bool usePropertySerializer;
        private readonly Func<Response, IReadOnlyList<T>> responseCreator;

        internal PageIteratorCore(
            CosmosClientContext clientContext,
            Uri resourceLink,
            ResourceType resourceType,
            QueryDefinition queryDefinition,
            QueryRequestOptions options,
            Func<Response, IReadOnlyList<T>> responseCreator,
            bool usePropertySerializer = false)
        {
            this.resourceLink = resourceLink;
            this.clientContext = clientContext;
            this.resourceType = resourceType;
            this.querySpec = queryDefinition?.ToSqlQuerySpec();
            this.requestOptions = options;
            this.responseCreator = responseCreator;
            this.usePropertySerializer = usePropertySerializer;
        }

        /// <summary>
        /// The query options for the result set
        /// </summary>
        protected QueryRequestOptions requestOptions { get; }

        public override async Task<Page<T>> GetPageAsync(string continuation = null, CancellationToken cancellationToken = default)
        {
            Stream stream = null;
            OperationType operation = OperationType.ReadFeed;
            if (this.querySpec != null)
            {
                // Use property serializer is for internal query operations like throughput
                // that should not use custom serializer
                CosmosSerializer serializer = this.usePropertySerializer ?
                    this.clientContext.PropertiesSerializer :
                    this.clientContext.SqlQuerySpecSerializer;

                stream = serializer.ToStream(this.querySpec);
                operation = OperationType.Query;
            }

            Response response = await this.clientContext.ProcessResourceOperationStreamAsync(
               resourceUri: this.resourceLink,
               resourceType: this.resourceType,
               operationType: operation,
               requestOptions: this.requestOptions,
               cosmosContainerCore: null,
               partitionKey: this.requestOptions?.PartitionKey,
               streamPayload: stream,
               requestEnricher: request =>
               {
                   QueryRequestOptions.FillContinuationToken(request, continuation);
                   if (this.querySpec != null)
                   {
                       request.Headers.Add(HttpConstants.HttpHeaders.ContentType, MediaTypes.QueryJson);
                       request.Headers.Add(HttpConstants.HttpHeaders.IsQuery, bool.TrueString);
                   }
               },
               cancellationToken: cancellationToken);

            return new Page<T>(this.responseCreator(response), response.Headers.GetContinuationToken(), response);
        }
    }
}