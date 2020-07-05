//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Remote;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class FactoryTests
    {
        [TestMethod]
        public void TestCreate()
        {
            Mock<IFeedRangeProvider> mockFeedRangeProvider = new Mock<IFeedRangeProvider>();
            Mock<IQueryDataSource> mockQueryDataSource = new Mock<IQueryDataSource>();

            TryCatch<IQueryPipelineStage> monadicCreatePipeline = PipelineFactory.MonadicCreate(
                ExecutionEnvironment.Compute,
                feedRangeProvider: mockFeedRangeProvider.Object,
                queryDataSource: mockQueryDataSource.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c"),
                queryInfo: new QueryInfo() { },
                pageSize: 10,
                requestContinuationToken: default);
            Assert.IsTrue(monadicCreatePipeline.Succeeded);

            IQueryPipelineStage pipelineStage = monadicCreatePipeline.Result;
            Assert.IsTrue(pipelineStage is ParallelCrossPartitionQueryPipelineStage);
        }
    }
}
