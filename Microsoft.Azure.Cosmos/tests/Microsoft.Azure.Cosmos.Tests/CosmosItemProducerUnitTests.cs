//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Client.Core.Tests;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Query.ExecutionComponent;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.Query;
    using Moq;
    using System.Threading;
    using Microsoft.Azure.Cosmos.CosmosElements;

    [TestClass]
    public class CosmosItemProducerUnitTests
    {
        [TestMethod]
        public async Task TestItemProducerTreeWithFailure()
        {
            int callBackCount = 0;
            Mock<CosmosQueryContext> mockQueryContext = new Mock<CosmosQueryContext>();
            
            SqlQuerySpec sqlQuerySpec = new SqlQuerySpec("Select * from t");
            PartitionKeyRange partitionKeyRange = new PartitionKeyRange { Id = "0", MinInclusive = "A", MaxExclusive = "B" };
            Action<ItemProducerTree, int, double, QueryMetrics, long, CancellationToken> produceAsyncCompleteCallback = (
                ItemProducerTree producer,
                int itemsBuffered,
                double resourceUnitUsage,
                QueryMetrics queryMetrics,
                long responseLengthBytes,
                CancellationToken token) => { callBackCount++; };

            Mock<IComparer<ItemProducerTree>> comparer = new Mock<IComparer<ItemProducerTree>>();
            Mock<IEqualityComparer<CosmosElement>> cosmosElementComparer = new Mock<IEqualityComparer<CosmosElement>>();
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            IEnumerable<CosmosElement> cosmosElements = new List<CosmosElement>()
            {
                new Mock<CosmosElement>(CosmosElementType.Object).Object
            };

            CosmosQueryResponseMessageHeaders headers = new CosmosQueryResponseMessageHeaders("TestToken", null)
            {
                ActivityId = "AA470D71-6DEF-4D61-9A08-272D8C9ABCFE",
                RequestCharge = 42
            };

            mockQueryContext.Setup(x => x.ExecuteQueryAsync(sqlQuerySpec, cancellationTokenSource.Token, It.IsAny<Action<CosmosRequestMessage>>())).Returns(
                Task.FromResult(CosmosQueryResponse.CreateSuccess(cosmosElements, 1, 500, headers)));

            ItemProducerTree itemProducerTree = new ItemProducerTree(
                queryContext: mockQueryContext.Object,
                querySpecForInit: sqlQuerySpec,
                partitionKeyRange: partitionKeyRange,
                produceAsyncCompleteCallback: produceAsyncCompleteCallback,
                itemProducerTreeComparer: comparer.Object,
                equalityComparer: cosmosElementComparer.Object,
                deferFirstPage: false,
                collectionRid: "collectionRid",
                initialContinuationToken: null,
                initialPageSize: 50);

            // Buffer to success responses
            await itemProducerTree.BufferMoreDocuments(cancellationTokenSource.Token);
            await itemProducerTree.BufferMoreDocuments(cancellationTokenSource.Token);

            // Buffer a failure
            mockQueryContext.Setup(x => x.ExecuteQueryAsync(sqlQuerySpec, cancellationTokenSource.Token, It.IsAny<Action<CosmosRequestMessage>>())).Returns(
                Task.FromResult(CosmosQueryResponse.CreateFailure(headers, HttpStatusCode.InternalServerError, null, "Error message", null)));

            await itemProducerTree.BufferMoreDocuments(cancellationTokenSource.Token);

            // First item should be a success
            var result = await itemProducerTree.MoveNextAsync(cancellationTokenSource.Token);
            Assert.IsTrue(result.successfullyMovedNext);
            Assert.IsNull(result.failureResponse);
            Assert.IsTrue(itemProducerTree.HasMoreResults);

            // Second item should be a success
            result = await itemProducerTree.MoveNextAsync(cancellationTokenSource.Token);
            Assert.IsTrue(result.successfullyMovedNext);
            Assert.IsNull(result.failureResponse);
            Assert.IsTrue(itemProducerTree.HasMoreResults);

            // Third item should be a failure
            result = await itemProducerTree.MoveNextAsync(cancellationTokenSource.Token);
            Assert.IsFalse(result.successfullyMovedNext);
            Assert.IsNotNull(result.failureResponse);
            Assert.IsFalse(itemProducerTree.HasMoreResults);

            // Try to buffer after failure. It should return the previous cached failure and not try to buffer again.
            mockQueryContext.Setup(x => x.ExecuteQueryAsync(sqlQuerySpec, cancellationTokenSource.Token, It.IsAny<Action<CosmosRequestMessage>>())).
                Throws(new Exception("Previous buffer failed. Operation should return original failure and not try again"));

            await itemProducerTree.BufferMoreDocuments(cancellationTokenSource.Token);
            Assert.IsFalse(result.successfullyMovedNext);
            Assert.IsNotNull(result.failureResponse);
            Assert.IsFalse(itemProducerTree.HasMoreResults);
        }
    }
}
