// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    /// <summary>
    /// Implementation of <see cref="CosmosQueryExecutionContext"/> that composes another context and defers it's initialization until the first read.
    /// </summary>
    internal sealed class LazyCosmosQueryExecutionContext : CosmosQueryExecutionContext
    {
        private readonly AsyncLazy<TryCatch<CosmosQueryExecutionContext>> lazyTryCreateCosmosQueryExecutionContext;

        public LazyCosmosQueryExecutionContext(AsyncLazy<TryCatch<CosmosQueryExecutionContext>> lazyTryCreateCosmosQueryExecutionContext)
        {
            this.lazyTryCreateCosmosQueryExecutionContext = lazyTryCreateCosmosQueryExecutionContext ?? throw new ArgumentNullException(nameof(lazyTryCreateCosmosQueryExecutionContext));
        }

        public override bool IsDone
        {
            get
            {
                bool isDone;
                if (this.lazyTryCreateCosmosQueryExecutionContext.ValueInitialized)
                {
                    TryCatch<CosmosQueryExecutionContext> tryCreateCosmosQueryExecutionContext = this.lazyTryCreateCosmosQueryExecutionContext.Result;
                    if (tryCreateCosmosQueryExecutionContext.Succeeded)
                    {
                        isDone = tryCreateCosmosQueryExecutionContext.Result.IsDone;
                    }
                    else
                    {
                        isDone = true;
                    }
                }
                else
                {
                    isDone = false;
                }

                return isDone;
            }
        }

        public override void Dispose()
        {
            if (this.lazyTryCreateCosmosQueryExecutionContext.ValueInitialized)
            {
                TryCatch<CosmosQueryExecutionContext> tryCreateCosmosQueryExecutionContext = this.lazyTryCreateCosmosQueryExecutionContext.Result;
                if (tryCreateCosmosQueryExecutionContext.Succeeded)
                {
                    tryCreateCosmosQueryExecutionContext.Result.Dispose();
                }
            }
        }

        public override async Task<QueryResponseCore> ExecuteNextAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TryCatch<CosmosQueryExecutionContext> tryCreateCosmosQueryExecutionContext = await this.lazyTryCreateCosmosQueryExecutionContext.GetValueAsync(cancellationToken);
            if (!tryCreateCosmosQueryExecutionContext.Succeeded)
            {
                return QueryResponseFactory.CreateFromException(tryCreateCosmosQueryExecutionContext.Exception);
            }

            CosmosQueryExecutionContext cosmosQueryExecutionContext = tryCreateCosmosQueryExecutionContext.Result;
            QueryResponseCore queryResponseCore = await cosmosQueryExecutionContext.ExecuteNextAsync(cancellationToken);
            return queryResponseCore;
        }

        public override bool TryGetContinuationToken(out string state)
        {
            if (!this.lazyTryCreateCosmosQueryExecutionContext.ValueInitialized)
            {
                state = null;
                return false;
            }

            TryCatch<CosmosQueryExecutionContext> tryCreateCosmosQueryExecutionContext = this.lazyTryCreateCosmosQueryExecutionContext.Result;
            if (!tryCreateCosmosQueryExecutionContext.Succeeded)
            {
                state = null;
                return false;
            }

            return tryCreateCosmosQueryExecutionContext.Result.TryGetContinuationToken(out state);
        }

        public override bool TryGetFeedToken(out FeedToken feedToken)
        {
            if (!this.lazyTryCreateCosmosQueryExecutionContext.ValueInitialized)
            {
                feedToken = null;
                return false;
            }

            TryCatch<CosmosQueryExecutionContext> tryCreateCosmosQueryExecutionContext = this.lazyTryCreateCosmosQueryExecutionContext.Result;
            if (!tryCreateCosmosQueryExecutionContext.Succeeded)
            {
                feedToken = null;
                return false;
            }

            return tryCreateCosmosQueryExecutionContext.Result.TryGetFeedToken(out feedToken);
        }
    }
}
