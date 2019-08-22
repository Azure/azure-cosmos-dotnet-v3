//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    /// <summary>
    /// Interface for all document query execution contexts
    /// </summary>
    internal class CosmosToDocumentQueryExecutionContext : IDocumentQueryExecutionContext
    {
        public bool IsDone => this.innerExecutionContext.IsDone;

        private CosmosQueryExecutionContext innerExecutionContext { get; }

        internal CosmosToDocumentQueryExecutionContext(CosmosQueryExecutionContext cosmosQueryExecutionContext)
        {
            this.innerExecutionContext = cosmosQueryExecutionContext;
        }

        public void Dispose()
        {
            this.innerExecutionContext.Dispose();
        }

        public async Task<DocumentFeedResponse<CosmosElement>> ExecuteNextFeedResponseAsync(CancellationToken token)
        {
            QueryResponse response = await this.innerExecutionContext.ExecuteNextAsync(token);
            INameValueCollection nameValueCollection = new DictionaryNameValueCollection();
            nameValueCollection.Add(HttpConstants.HttpHeaders.Continuation, response.QueryHeaders.InternalContinuationToken);
            nameValueCollection.Add(HttpConstants.HttpHeaders.RequestCharge, response.QueryHeaders.RequestCharge.ToString(CultureInfo.InvariantCulture));

            return new DocumentFeedResponse<CosmosElement>(
                result: response.CosmosElements,
                count: response.Count,
                responseHeaders: nameValueCollection,
                requestStats: null,
                useETagAsContinuation: false,
                queryMetrics: null,
                disallowContinuationTokenMessage: response.QueryHeaders.DisallowContinuationTokenMessage,
                continuationToken: response.QueryHeaders.InternalContinuationToken,
                responseLengthBytes: response.ResponseLengthBytes);
        }
    }
}
