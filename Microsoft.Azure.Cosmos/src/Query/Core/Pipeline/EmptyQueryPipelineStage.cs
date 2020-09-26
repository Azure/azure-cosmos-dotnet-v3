// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Reactive;

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
    }
}
