//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class MockQueryPipelineStage : QueryPipelineStageBase
    {
        private readonly IReadOnlyList<IReadOnlyList<CosmosElement>> pages;
        private int pageIndex;

        public MockQueryPipelineStage(IReadOnlyList<IReadOnlyList<CosmosElement>> pages)
            : base(EmptyQueryPipelineStage.Singleton, cancellationToken: default)
        {
            this.pages = pages ?? throw new ArgumentNullException(nameof(pages));
        }

        public override ValueTask<bool> MoveNextAsync(ITrace trace)
        {
            if (this.pageIndex == this.pages.Count)
            {
                this.Current = default;
                return new ValueTask<bool>(false);
            }

            IReadOnlyList<CosmosElement> documents = this.pages[this.pageIndex++];
            QueryState state = (this.pageIndex == this.pages.Count) ? null : new QueryState(CosmosString.Create(this.pageIndex.ToString()));
            QueryPage page = new QueryPage(
                documents: documents,
                requestCharge: default,
                activityId: Guid.NewGuid().ToString(),
                responseLengthInBytes: default,
                cosmosQueryExecutionInfo: default,
                disallowContinuationTokenMessage: default,
                state: state);
            this.Current = TryCatch<QueryPage>.FromResult(page);
            return new ValueTask<bool>(true);
        }
    }
}
