﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Tests.Query.OfflineEngineTests;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class MockQueryPipelineStage : QueryPipelineStageBase
    {
        private readonly IReadOnlyList<IReadOnlyList<CosmosElement>> pages;

        public long PageIndex { get; private set; }

        public MockQueryPipelineStage(IReadOnlyList<IReadOnlyList<CosmosElement>> pages)
            : base(EmptyQueryPipelineStage.Singleton, cancellationToken: default)
        {
            this.pages = pages ?? throw new ArgumentNullException(nameof(pages));
        }

        public static MockQueryPipelineStage Create(
            IReadOnlyList<IReadOnlyList<CosmosElement>> pages,
            CosmosElement continuationToken)
        {
            MockQueryPipelineStage stage = new MockQueryPipelineStage(pages);
            
            if (continuationToken != null)
            {
                CosmosNumber index = continuationToken as CosmosNumber;
                stage.PageIndex = Number64.ToLong(index.Value);
            }

            return stage;
        }

        public override ValueTask<bool> MoveNextAsync(ITrace trace)
        {
            if (this.PageIndex == this.pages.Count)
            {
                this.Current = default;
                return new ValueTask<bool>(false);
            }

            IReadOnlyList<CosmosElement> documents = this.pages[(int)this.PageIndex++];
            QueryState state = this.PageIndex == this.pages.Count ? null : new QueryState(CosmosNumber64.Create(this.PageIndex));
            QueryPage page = new QueryPage(
                documents: documents,
                requestCharge: default,
                activityId: Guid.NewGuid().ToString(),
                responseLengthInBytes: default,
                cosmosQueryExecutionInfo: default,
                distributionPlanSpec: default,
                disallowContinuationTokenMessage: default,
                additionalHeaders: default,
                state: state);
            this.Current = TryCatch<QueryPage>.FromResult(page);
            return new ValueTask<bool>(true);
        }
    }
}
