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

    internal abstract class QueryPipelineStageBase : IQueryPipelineStage
    {
        protected readonly IQueryPipelineStage inputStage;

        protected QueryPipelineStageBase(IQueryPipelineStage inputStage)
        {
            this.inputStage = inputStage ?? throw new ArgumentNullException(nameof(inputStage));
        }

        public TryCatch<QueryPage> Current { get; protected set; }

        public ValueTask DisposeAsync()
        {
            return this.inputStage.DisposeAsync();
        }

        public abstract ValueTask<bool> MoveNextAsync(ITrace trace, CancellationToken cancellationToken);
    }
}
