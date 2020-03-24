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

    internal sealed class CosmosQueryExecutionContextWithNameCacheStaleRetry : CosmosQueryExecutionContext
    {
        private readonly CosmosQueryContext cosmosQueryContext;
        private readonly Func<CosmosQueryExecutionContext> cosmosQueryExecutionContextFactory;
        private CosmosQueryExecutionContext currentCosmosQueryExecutionContext;
        private bool alreadyRetried;

        public CosmosQueryExecutionContextWithNameCacheStaleRetry(
            CosmosQueryContext cosmosQueryContext,
            Func<CosmosQueryExecutionContext> cosmosQueryExecutionContextFactory)
        {
            this.cosmosQueryContext = cosmosQueryContext ?? throw new ArgumentNullException(nameof(cosmosQueryContext));
            this.cosmosQueryExecutionContextFactory = cosmosQueryExecutionContextFactory ?? throw new ArgumentNullException(nameof(cosmosQueryExecutionContextFactory));
            this.currentCosmosQueryExecutionContext = cosmosQueryExecutionContextFactory();
        }

        public override bool IsDone => this.currentCosmosQueryExecutionContext.IsDone;

        public override void Dispose()
        {
            this.currentCosmosQueryExecutionContext.Dispose();
        }

        public override async Task<QueryResponseCore> ExecuteNextAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // If the cache is stale the entire execute context has incorrect values and should be recreated.
            // This should only be done for the first execution.
            // If results have already been pulled,
            // then an error should be returned to the user,
            // since it's not possible to combine query results from multiple containers.
            QueryResponseCore queryResponse = await this.currentCosmosQueryExecutionContext.ExecuteNextAsync(cancellationToken);
            if (
                (queryResponse.StatusCode == System.Net.HttpStatusCode.Gone) &&
                (queryResponse.SubStatusCode == Documents.SubStatusCodes.NameCacheIsStale) &&
                !this.alreadyRetried)
            {
                await this.cosmosQueryContext.QueryClient.ForceRefreshCollectionCacheAsync(
                        this.cosmosQueryContext.ResourceLink.OriginalString,
                        cancellationToken);
                this.alreadyRetried = true;
                this.currentCosmosQueryExecutionContext.Dispose();
                this.currentCosmosQueryExecutionContext = this.cosmosQueryExecutionContextFactory();
                return await this.ExecuteNextAsync(cancellationToken);
            }

            return queryResponse;
        }

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            return this.currentCosmosQueryExecutionContext.GetCosmosElementContinuationToken();
        }

        public override bool TryGetFeedToken(
            string containerResourceId,
            SqlQuerySpec sqlQuerySpec,
            out QueryFeedTokenInternal feedToken)
        {
            return this.currentCosmosQueryExecutionContext.TryGetFeedToken(containerResourceId, sqlQuerySpec, out feedToken);
        }
    }
}
