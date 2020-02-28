// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    internal sealed class QueryExecutionContextWithException : CosmosQueryExecutionContext
    {
        private readonly Exception exception;
        private bool returnedErrorResponse;

        public QueryExecutionContextWithException(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            this.exception = exception;
        }

        public override bool IsDone => this.returnedErrorResponse;

        public override void Dispose()
        {
        }

        public override Task<QueryResponseCore> ExecuteNextAsync(CancellationToken cancellationToken)
        {
            QueryResponseCore queryResponse = QueryResponseFactory.CreateFromException(this.exception);
            this.returnedErrorResponse = true;
            return Task.FromResult(queryResponse);
        }

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            throw new NotImplementedException();
        }
    }
}
