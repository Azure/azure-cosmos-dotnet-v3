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
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SkipEmptyPageQueryPipelineStageTests
    {
        [TestMethod]
        public async Task StackOverflowTest()
        {
            EmptyPagePipelineStage emptyPagePipelineStage = new EmptyPagePipelineStage { EmptyPageCount = 2000 };
            SkipEmptyPageQueryPipelineStage skipEmptyPageStage = new SkipEmptyPageQueryPipelineStage(
                inputStage: emptyPagePipelineStage,
                cancellationToken: default);
            
            IQueryPipelineStage pipeline = new CatchAllQueryPipelineStage(inputStage: skipEmptyPageStage, cancellationToken: default);
            _ = await pipeline.MoveNextAsync(NoOpTrace.Singleton);
            TryCatch<QueryPage> result = pipeline.Current;
            Assert.IsFalse(result.Succeeded);
        }

        private class EmptyPagePipelineStage : IQueryPipelineStage
        {
            private static readonly TryCatch<QueryPage> Empty = TryCatch<QueryPage>.FromResult(new QueryPage(
                            documents: new List<CosmosElement>(),
                            requestCharge: 42,
                            activityId: Guid.NewGuid().ToString(),
                            responseLengthInBytes: "[]".Length,
                            cosmosQueryExecutionInfo: default,
                            disallowContinuationTokenMessage: default,
                            additionalHeaders: default,
                            state: new QueryState(CosmosString.Create("Started But Haven't Returned Any Documents Yet"))));

            public TryCatch<QueryPage> Current => Empty;

            public int EmptyPageCount { get; set; }

            public ValueTask DisposeAsync()
            {
                return new ValueTask();
            }

            public ValueTask<bool> MoveNextAsync(ITrace trace)
            {
                if (this.EmptyPageCount > 0)
                {
                    --this.EmptyPageCount;
                    return new ValueTask<bool>(true);
                }

                throw new CosmosException(
                    message:"Injected failure",
                    statusCode: System.Net.HttpStatusCode.InternalServerError,
                    subStatusCode: 0,
                    activityId: Guid.Empty.ToString(),
                    requestCharge: 0);
            }

            public void SetCancellationToken(CancellationToken cancellationToken)
            {
            }
        }
    }
}