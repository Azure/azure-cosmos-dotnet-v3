//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.CosmosElements;

    /// <summary>
    /// Interface for all document query execution contexts
    /// </summary>
    internal class CosmosToDocumentQueryExecutionContext : IDocumentQueryExecutionContext
    {
        public bool IsDone => this.cosmosQueryExecutionContext.IsDone;

        private CosmosQueryExecutionContext cosmosQueryExecutionContext { get; }

        internal CosmosToDocumentQueryExecutionContext(CosmosQueryExecutionContext cosmosQueryExecutionContext)
        {
            this.cosmosQueryExecutionContext = cosmosQueryExecutionContext;
        }

        public void Dispose()
        {
            this.cosmosQueryExecutionContext.Dispose();
        }

        public async Task<DocumentFeedResponse<CosmosElement>> ExecuteNextFeedResponseAsync(CancellationToken token)
        {
            QueryResponse response = await this.cosmosQueryExecutionContext.ExecuteNextAsync(token);

            return new DocumentFeedResponse<CosmosElement>(
                result: response.CosmosElements,
                count: response.Count,
                responseHeaders: response.QueryHeaders.CosmosMessageHeaders,
                requestStats: null,
                useETagAsContinuation: false,
                queryMetrics: null,
                disallowContinuationTokenMessage: response.QueryHeaders.DisallowContinuationTokenMessage,
                responseLengthBytes: response.ResponseLengthBytes);
        }
    }
}
