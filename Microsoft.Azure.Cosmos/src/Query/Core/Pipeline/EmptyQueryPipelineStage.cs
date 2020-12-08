// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Reactive;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class EmptyQueryPipelineStage : IQueryPipelineStage
    {
        public static readonly EmptyQueryPipelineStage Singleton = new EmptyQueryPipelineStage();

        private readonly EmptyAsyncEnumerator<TryCatch<QueryPage>> emptyAsyncEnumerator;

        public EmptyQueryPipelineStage()
        {
            this.emptyAsyncEnumerator = new EmptyAsyncEnumerator<TryCatch<QueryPage>>();
        }

        public TryCatch<QueryPage> Current => this.emptyAsyncEnumerator.Current;

        public ValueTask DisposeAsync() => this.emptyAsyncEnumerator.DisposeAsync();

        public ValueTask<bool> MoveNextAsync() => this.emptyAsyncEnumerator.MoveNextAsync();

        public ValueTask<bool> MoveNextAsync(ITrace trace) => this.emptyAsyncEnumerator.MoveNextAsync(trace);

        public void SetCancellationToken(CancellationToken cancellationToken)
        {
            // No work to do since this enumerator is fully sync.
        }
    }
}
