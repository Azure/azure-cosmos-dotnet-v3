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
        private bool hasStarted;

        protected QueryPipelineStageBase(IQueryPipelineStage inputStage)
        {
            this.inputStage = inputStage ?? throw new ArgumentNullException(nameof(inputStage));
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

            this.Current = await this.GetNextPageAsync(default);
            if (this.Current.Succeeded)
            {
                this.State = this.Current.Result.State;
            }

            return true;
        }
    }
}
