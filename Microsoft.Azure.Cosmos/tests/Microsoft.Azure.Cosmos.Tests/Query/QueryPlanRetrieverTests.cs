//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.ObjectModel;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class QueryPlanRetrieverTests
    {
        [TestMethod]
        public async Task ServiceInterop_BadRequestContainsInnerException()
        {
            ExpectedQueryPartitionProviderException innerException = new ExpectedQueryPartitionProviderException("some parsing error");
            Mock<CosmosQueryClient> queryClient = new Mock<CosmosQueryClient>();

            queryClient.Setup(c => c.TryGetPartitionedQueryExecutionInfoAsync(
                It.IsAny<SqlQuerySpec>(),
                It.IsAny<Documents.PartitionKeyDefinition>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(TryCatch<PartitionedQueryExecutionInfo>.FromException(innerException));

            Mock<ITrace> trace = new Mock<ITrace>();
            CosmosException cosmosException = await Assert.ThrowsExceptionAsync<CosmosException>(() => QueryPlanRetriever.GetQueryPlanWithServiceInteropAsync(
                queryClient.Object,
                new SqlQuerySpec("selectttttt * from c"),
                new Documents.PartitionKeyDefinition() { Paths = new Collection<string>() { "/id" } },
                hasLogicalPartitionKey: false,
                trace.Object,
                default));

            Assert.AreEqual(HttpStatusCode.BadRequest, cosmosException.StatusCode);
            Assert.AreEqual(innerException, cosmosException.InnerException);
            Assert.IsNotNull(cosmosException.Trace);
            Assert.IsNotNull(cosmosException.Diagnostics);
        }

        [TestMethod]
        public async Task ServiceInterop_BadRequestContainsOriginalCosmosException()
        {
            CosmosException actualException = new CosmosException("Some message", (HttpStatusCode)429, (int)Documents.SubStatusCodes.Unknown, Guid.NewGuid().ToString(), 0);
            Mock<CosmosQueryClient> queryClient = new Mock<CosmosQueryClient>();

            queryClient.Setup(c => c.TryGetPartitionedQueryExecutionInfoAsync(
                It.IsAny<SqlQuerySpec>(),
                It.IsAny<Documents.PartitionKeyDefinition>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(TryCatch<PartitionedQueryExecutionInfo>.FromException(actualException));

            Mock<ITrace> trace = new Mock<ITrace>();
            CosmosException cosmosException = await Assert.ThrowsExceptionAsync<CosmosException>(() => QueryPlanRetriever.GetQueryPlanWithServiceInteropAsync(
                queryClient.Object,
                new SqlQuerySpec("selectttttt * from c"),
                new Documents.PartitionKeyDefinition() { Paths = new Collection<string>() { "/id" } },
                hasLogicalPartitionKey: false,
                trace.Object,
                default));

            Assert.AreEqual(actualException, cosmosException);
        }
    }
}
