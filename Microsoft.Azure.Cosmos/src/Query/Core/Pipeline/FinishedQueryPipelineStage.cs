// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal sealed class FinishedQueryPipelineStage : IQueryPipelineStage
    {
        public static readonly FinishedQueryPipelineStage Value = new FinishedQueryPipelineStage();

        private FinishedQueryPipelineStage()
        {
        }

        public bool HasMoreResults => false;

        public TryCatch<QueryPage> Current => default;

        public ValueTask DisposeAsync() => default;

        public Task<TryCatch<QueryPage>> GetNextPageAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<bool> MoveNextAsync() => new ValueTask<bool>(false);
    }
}
