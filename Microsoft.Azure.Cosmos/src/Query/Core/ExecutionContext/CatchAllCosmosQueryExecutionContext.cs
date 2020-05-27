// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    internal sealed class CatchAllCosmosQueryExecutionContext : CosmosQueryExecutionContext
    {
        private readonly CosmosQueryExecutionContext cosmosQueryExecutionContext;
        private bool hitException;

        public CatchAllCosmosQueryExecutionContext(
            CosmosQueryExecutionContext cosmosQueryExecutionContext)
        {
            this.cosmosQueryExecutionContext = cosmosQueryExecutionContext ?? throw new ArgumentNullException(nameof(cosmosQueryExecutionContext));
        }

        public override bool IsDone => this.hitException || this.cosmosQueryExecutionContext.IsDone;

        public override void Dispose()
        {
            this.cosmosQueryExecutionContext.Dispose();
        }

        public override async Task<QueryResponseCore> ExecuteNextAsync(CancellationToken cancellationToken)
        {
            if (this.IsDone)
            {
                throw new InvalidOperationException(
                    $"Can not {nameof(ExecuteNextAsync)} from a {nameof(CosmosQueryExecutionContext)} where {nameof(this.IsDone)}.");
            }

            QueryResponseCore queryResponseCore;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                queryResponseCore = await this.cosmosQueryExecutionContext.ExecuteNextAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                queryResponseCore = QueryResponseFactory.CreateFromException(ex);
            }

            if (!queryResponseCore.IsSuccess)
            {
                this.hitException = true;
            }

            return queryResponseCore;
        }

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            return this.cosmosQueryExecutionContext.GetCosmosElementContinuationToken();
        }
    }
}
