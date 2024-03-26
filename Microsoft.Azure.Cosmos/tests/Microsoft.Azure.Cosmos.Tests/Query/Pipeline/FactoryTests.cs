//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class FactoryTests
    {
        [TestMethod]
        public void TestCreate()
        {
            Mock<IDocumentContainer> mockDocumentContainer = new Mock<IDocumentContainer>();

            TryCatch<IQueryPipelineStage> monadicCreatePipeline = PipelineFactory.MonadicCreate(
                ExecutionEnvironment.Compute,
                documentContainer: mockDocumentContainer.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c"),
                targetRanges: new List<FeedRangeEpk>() { FeedRangeEpk.FullRange },
                partitionKey: null,
                containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                queryInfo: new QueryInfo() { },
                queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: 10),
                maxConcurrency: 10,
                requestCancellationToken: default,
                requestContinuationToken: default); ;
            Assert.IsTrue(monadicCreatePipeline.Succeeded);
        }
    }
}
