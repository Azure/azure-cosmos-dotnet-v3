// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Json;
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

        public override void SerializeState(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException(nameof(jsonWriter));
            }

            if (!this.lazyTryCreateCosmosQueryExecutionContext.ValueInitialized)
            {
                throw new InvalidOperationException();
            }

            TryCatch<CosmosQueryExecutionContext> tryCreateCosmosQueryExecutionContext = this.lazyTryCreateCosmosQueryExecutionContext.Result;
            if (!tryCreateCosmosQueryExecutionContext.Succeeded)
            {
                throw tryCreateCosmosQueryExecutionContext.Exception;
            }

            tryCreateCosmosQueryExecutionContext.Result.SerializeState(jsonWriter);
        }
    }
}
