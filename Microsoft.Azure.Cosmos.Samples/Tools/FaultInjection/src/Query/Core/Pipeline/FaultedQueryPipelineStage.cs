﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Reactive;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class FaultedQueryPipelineStage : IQueryPipelineStage
    {
        private readonly JustAsyncEnumerator<TryCatch<QueryPage>> justAsyncEnumerator;

        public FaultedQueryPipelineStage(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            this.justAsyncEnumerator = new JustAsyncEnumerator<TryCatch<QueryPage>>(TryCatch<QueryPage>.FromException(exception));
        }

        public TryCatch<QueryPage> Current => this.justAsyncEnumerator.Current;

        public ValueTask DisposeAsync()
        {
            return this.justAsyncEnumerator.DisposeAsync();
        }

        public ValueTask<bool> MoveNextAsync(ITrace trace)
        {
            return this.justAsyncEnumerator.MoveNextAsync(trace);
        }

        public void SetCancellationToken(CancellationToken cancellationToken)
        {
            // No work to do with since this enumerator is fully sync.
        }
    }
}
