// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Reactive;

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

        public ValueTask DisposeAsync() => this.justAsyncEnumerator.DisposeAsync();

        public ValueTask<bool> MoveNextAsync() => this.justAsyncEnumerator.MoveNextAsync();
    }
}
