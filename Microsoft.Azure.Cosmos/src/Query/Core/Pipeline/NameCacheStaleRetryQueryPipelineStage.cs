// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class NameCacheStaleRetryQueryPipelineStage : IQueryPipelineStage
    {
        private readonly CosmosQueryContext cosmosQueryContext;
        private readonly Func<IQueryPipelineStage> queryPipelineStageFactory;
        private IQueryPipelineStage currentQueryPipelineStage;
        private bool alreadyRetried;

        public NameCacheStaleRetryQueryPipelineStage(
            CosmosQueryContext cosmosQueryContext,
            Func<IQueryPipelineStage> queryPipelineStageFactory)
        {
            this.cosmosQueryContext = cosmosQueryContext ?? throw new ArgumentNullException(nameof(cosmosQueryContext));
            this.queryPipelineStageFactory = queryPipelineStageFactory ?? throw new ArgumentNullException(nameof(queryPipelineStageFactory));
            this.currentQueryPipelineStage = queryPipelineStageFactory();
        }

        public TryCatch<QueryPage> Current { get; private set; }

        public ValueTask DisposeAsync() => this.currentQueryPipelineStage.DisposeAsync();

        public ValueTask<bool> MoveNextAsync()
        {
            return this.MoveNextAsync(NoOpTrace.Singleton);
        }

        public async ValueTask<bool> MoveNextAsync(ITrace trace)
        {
            if (!await this.currentQueryPipelineStage.MoveNextAsync(trace))
            {
                return false;
            }

            TryCatch<QueryPage> tryGetSourcePage = this.currentQueryPipelineStage.Current;
            this.Current = tryGetSourcePage;

            if (tryGetSourcePage.Failed)
            {
                Exception exception = tryGetSourcePage.InnerMostException;
                bool shouldRetry = (exception is CosmosException cosmosException)
                    && (cosmosException.StatusCode == HttpStatusCode.Gone)
                    && (cosmosException.SubStatusCode == (int)Documents.SubStatusCodes.NameCacheIsStale)
                    && !this.alreadyRetried;
                if (shouldRetry)
                {
                    await this.cosmosQueryContext.QueryClient.ForceRefreshCollectionCacheAsync(
                        this.cosmosQueryContext.ResourceLink,
                        default);
                    this.alreadyRetried = true;
                    await this.currentQueryPipelineStage.DisposeAsync();
                    this.currentQueryPipelineStage = this.queryPipelineStageFactory();
                    return await this.MoveNextAsync();
                }
            }

            return true;
        }

        public void SetCancellationToken(CancellationToken cancellationToken)
        {
            this.currentQueryPipelineStage.SetCancellationToken(cancellationToken);
        }
    }
}
