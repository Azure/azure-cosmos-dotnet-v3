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

    internal sealed class MockQueryPipelineStage : QueryPipelineStageBase
    {
        private readonly IReadOnlyList<IReadOnlyList<CosmosElement>> pages;
        private int pageIndex;

        public MockQueryPipelineStage(IReadOnlyList<IReadOnlyList<CosmosElement>> pages)
            : base(EmptyQueryPipelineStage.Singleton)
        {
            this.pages = pages ?? throw new ArgumentNullException(nameof(pages));
        }

        protected override Task<TryCatch<QueryPage>> GetNextPageAsync(CancellationToken cancellationToken)
        {
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
            return Task.FromResult(TryCatch<QueryPage>.FromResult(page));
        }
    }
}
