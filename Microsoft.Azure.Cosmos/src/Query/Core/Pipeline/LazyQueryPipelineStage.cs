// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class LazyQueryPipelineStage : IQueryPipelineStage
    {
        private readonly AsyncLazy<TryCatch<IQueryPipelineStage>> lazyTryCreateStage;
        private CancellationToken cancellationToken;

        public LazyQueryPipelineStage(AsyncLazy<TryCatch<IQueryPipelineStage>> lazyTryCreateStage, CancellationToken cancellationToken)
        {
            this.lazyTryCreateStage = lazyTryCreateStage ?? throw new ArgumentNullException(nameof(lazyTryCreateStage));
            this.cancellationToken = cancellationToken;
        }

        public TryCatch<QueryPage> Current { get; private set; }

        public ValueTask DisposeAsync()
        {
            if (this.lazyTryCreateStage.ValueInitialized)
            {
                TryCatch<IQueryPipelineStage> tryCreatePipelineStage = this.lazyTryCreateStage.Result;
                if (tryCreatePipelineStage.Succeeded)
                {
                    return tryCreatePipelineStage.Result.DisposeAsync();
                }
            }

            return default;
        }

        public ValueTask<bool> MoveNextAsync()
        {
            return this.MoveNextAsync(NoOpTrace.Singleton);
        }

        public async ValueTask<bool> MoveNextAsync(ITrace trace)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            TryCatch<IQueryPipelineStage> tryCreateStage = await this.lazyTryCreateStage.GetValueAsync(trace, this.cancellationToken);
            if (tryCreateStage.Failed)
            {
                this.Current = TryCatch<QueryPage>.FromException(tryCreateStage.Exception);
                return true;
            }

            IQueryPipelineStage stage = tryCreateStage.Result;
            stage.SetCancellationToken(this.cancellationToken);
            if (!await stage.MoveNextAsync(trace))
            {
                this.Current = default;
                return false;
            }

            this.Current = stage.Current;
            return true;
        }

        public void SetCancellationToken(CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
        }
    }
}
