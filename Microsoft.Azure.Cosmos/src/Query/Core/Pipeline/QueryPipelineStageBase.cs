// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal abstract class QueryPipelineStageBase : IQueryPipelineStage
    {
        protected readonly IQueryPipelineStage inputStage;
        protected CancellationToken cancellationToken;
        private bool hasStarted;

        protected QueryPipelineStageBase(IQueryPipelineStage inputStage, CancellationToken cancellationToken)
        {
            this.inputStage = inputStage ?? throw new ArgumentNullException(nameof(inputStage));
            this.cancellationToken = cancellationToken;
        }

        public TryCatch<QueryPage> Current { get; private set; }

        public QueryState State { get; protected set; }

        public bool HasMoreResults => !this.hasStarted || (this.State != default);

        public ValueTask DisposeAsync() => this.inputStage.DisposeAsync();

        protected abstract Task<TryCatch<QueryPage>> GetNextPageAsync(CancellationToken cancellationToken);

        public async ValueTask<bool> MoveNextAsync()
        {
            if (!this.HasMoreResults)
            {
                return false;
            }

            this.hasStarted = true;

            this.Current = await this.GetNextPageAsync(this.cancellationToken);
            if (this.Current.Succeeded)
            {
                this.State = this.Current.Result.State;
            }

            return true;
        }

        public void SetCancellationToken(CancellationToken cancellationToken)
        {
            // Only here to support legacy query iterator and ExecuteNextAsync
            // can be removed only we only expose IAsyncEnumerable in v4 sdk.
            this.cancellationToken = cancellationToken;
            this.inputStage.SetCancellationToken(cancellationToken);
        }
    }
}
