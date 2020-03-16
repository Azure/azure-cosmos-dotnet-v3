// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    /// <summary>
    /// Implementation of <see cref="CosmosQueryExecutionContext"/> that composes another context and defers it's initialization until the first read.
    /// </summary>
    internal sealed class LazyCosmosQueryExecutionContext : CosmosQueryExecutionContext
    {
        private readonly AsyncLazy<(TryCatch<CosmosQueryExecutionContext> Context, IReadOnlyList<CosmosDiagnosticsInternal> diagnostics)> lazyTryCreateCosmosQueryExecutionContext;

        public LazyCosmosQueryExecutionContext(AsyncLazy<(TryCatch<CosmosQueryExecutionContext>, IReadOnlyList<CosmosDiagnosticsInternal>)> lazyTryCreateCosmosQueryExecutionContext)
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
                    TryCatch<CosmosQueryExecutionContext> tryCreateCosmosQueryExecutionContext = this.lazyTryCreateCosmosQueryExecutionContext.Result.Context;
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
                TryCatch<CosmosQueryExecutionContext> tryCreateCosmosQueryExecutionContext = this.lazyTryCreateCosmosQueryExecutionContext.Result.Context;
                if (tryCreateCosmosQueryExecutionContext.Succeeded)
                {
                    tryCreateCosmosQueryExecutionContext.Result.Dispose();
                }
            }
        }

        public override async Task<QueryResponseCore> ExecuteNextAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            (TryCatch<CosmosQueryExecutionContext> Context, IReadOnlyList<CosmosDiagnosticsInternal> diagnostics) tryCreateCosmosQueryExecutionContext = await this.lazyTryCreateCosmosQueryExecutionContext.GetValueAsync(cancellationToken);
            if (!tryCreateCosmosQueryExecutionContext.Context.Succeeded)
            {
                return QueryResponseFactory.CreateFromException(tryCreateCosmosQueryExecutionContext.Context.Exception);
            }

            CosmosQueryExecutionContext cosmosQueryExecutionContext = tryCreateCosmosQueryExecutionContext.Context.Result;
            QueryResponseCore queryResponseCore = await cosmosQueryExecutionContext.ExecuteNextAsync(cancellationToken);

            return queryResponseCore;
        }

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            if (!this.lazyTryCreateCosmosQueryExecutionContext.ValueInitialized)
            {
                throw new InvalidOperationException();
            }

            TryCatch<CosmosQueryExecutionContext> tryCreateCosmosQueryExecutionContext = this.lazyTryCreateCosmosQueryExecutionContext.Result.Context;
            if (!tryCreateCosmosQueryExecutionContext.Succeeded)
            {
                throw tryCreateCosmosQueryExecutionContext.Exception;
            }

            return tryCreateCosmosQueryExecutionContext.Result.GetCosmosElementContinuationToken();
        }
    }
}
