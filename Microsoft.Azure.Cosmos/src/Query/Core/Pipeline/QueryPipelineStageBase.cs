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

        protected QueryPipelineStageBase(IQueryPipelineStage inputStage, CancellationToken cancellationToken)
        {
            this.inputStage = inputStage ?? throw new ArgumentNullException(nameof(inputStage));
            this.cancellationToken = cancellationToken;
        }

        public TryCatch<QueryPage> Current { get; protected set; }

        public ValueTask DisposeAsync() => this.inputStage.DisposeAsync();

        public abstract ValueTask<bool> MoveNextAsync();

        public void SetCancellationToken(CancellationToken cancellationToken)
        {
            // Only here to support legacy query iterator and ExecuteNextAsync
            // can be removed only we only expose IAsyncEnumerable in v4 sdk.
            this.cancellationToken = cancellationToken;
            this.inputStage.SetCancellationToken(cancellationToken);
        }
    }
}
