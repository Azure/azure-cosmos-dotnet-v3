//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.ParallelQuery;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

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
                CancellationToken token) =>
            { callBackCount++; };

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
            (bool successfullyMovedNext, CosmosQueryResponse failureResponse) result = await itemProducerTree.MoveNextAsync(cancellationTokenSource.Token);
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


            // Third item should be a failure
            result = await itemProducerTree.MoveNextAsync(cancellationTokenSource.Token);
            Assert.IsFalse(result.successfullyMovedNext);
            Assert.IsNotNull(result.failureResponse);
            Assert.IsFalse(itemProducerTree.HasMoreResults);
            CosmosElement forhtElement = itemProducerTree.Current;

            // Try to buffer after failure. It should return the previous cached failure and not try to buffer again.
            mockQueryContext.Setup(x => x.ExecuteQueryAsync(sqlQuerySpec, cancellationTokenSource.Token, It.IsAny<Action<CosmosRequestMessage>>())).
                Throws(new Exception("Previous buffer failed. Operation should return original failure and not try again"));

            await itemProducerTree.BufferMoreDocuments(cancellationTokenSource.Token);
            Assert.IsFalse(result.successfullyMovedNext);
            Assert.IsNotNull(result.failureResponse);
            Assert.IsFalse(itemProducerTree.HasMoreResults);
        }

        [TestMethod]
        public async Task TestItemProducerTreeWithSplit()
        {
            int callBackCount = 0;
            string collectionRid = "collectionRid";
            Mock<CosmosQueryContext> mockQueryContext = new Mock<CosmosQueryContext>();
            mockQueryContext.Setup(x => x.ResourceTypeEnum).Returns(ResourceType.Document);

            SqlQuerySpec sqlQuerySpec = new SqlQuerySpec("Select * from t");
            PartitionKeyRange partitionKeyRange = new PartitionKeyRange { Id = "0", MinInclusive = "A", MaxExclusive = "C" };

            Action<ItemProducerTree, int, double, QueryMetrics, long, CancellationToken> produceAsyncCompleteCallback = (
                ItemProducerTree producer,
                int itemsBuffered,
                double resourceUnitUsage,
                QueryMetrics queryMetrics,
                long responseLengthBytes,
                CancellationToken token) =>
            { callBackCount++; };

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            IEnumerable<CosmosElement> cosmosElements = new List<CosmosElement>()
            {
                new Mock<CosmosElement>(CosmosElementType.Object).Object
            };

            CosmosQueryResponseMessageHeaders headers = new CosmosQueryResponseMessageHeaders(null, null)
            {
                ActivityId = "AA470D71-6DEF-4D61-9A08-272D8C9ABCFE",
                RequestCharge = 42
            };

            IReadOnlyList<PartitionKeyRange> partitionKeyRangeAfterSplit = new List<PartitionKeyRange>()
            {
                new PartitionKeyRange { Id = "1", MinInclusive = "A", MaxExclusive = "B" },
                new PartitionKeyRange { Id = "2", MinInclusive = "B", MaxExclusive = "C" }
            };

            Mock<IRoutingMapProvider> mockRoutingMapProvider = new Mock<IRoutingMapProvider>();
            mockRoutingMapProvider.Setup(x => x.TryGetOverlappingRangesAsync(
                It.Is<string>(
                    s => string.Equals(s, collectionRid)),
                It.Is<Documents.Routing.Range<string>>(r => r.Min == partitionKeyRange.MinInclusive && r.IsMinInclusive && r.Max == partitionKeyRange.MaxExclusive && !r.IsMaxInclusive),
                true))
                .Returns(Task.FromResult(partitionKeyRangeAfterSplit));

            Mock<CosmosQueryClient> mockQueryClient = new Mock<CosmosQueryClient>();
            mockQueryClient.Setup(x => x.GetRoutingMapProviderAsync()).Returns(Task.FromResult(mockRoutingMapProvider.Object));
            mockQueryContext.Setup(x => x.QueryClient).Returns(mockQueryClient.Object);

            ItemProducerTree itemProducerTree = new ItemProducerTree(
                queryContext: mockQueryContext.Object,
                querySpecForInit: sqlQuerySpec,
                partitionKeyRange: partitionKeyRange,
                produceAsyncCompleteCallback: produceAsyncCompleteCallback,
                itemProducerTreeComparer: new ParllelTreeComparer(),
                equalityComparer: new MockElementComparer(),
                deferFirstPage: true,
                collectionRid: collectionRid,
                initialContinuationToken: null,
                initialPageSize: 50);

            //// Setup the first response to be a success with 1 item
            //mockQueryContext.Setup(x => x.ExecuteQueryAsync(
            //    sqlQuerySpec,
            //    cancellationTokenSource.Token,
            //    It.Is<Action<CosmosRequestMessage>>(action => VerifySamePkRange(action, partitionKeyRange))))
            //    .Returns(Task.FromResult(CosmosQueryResponse.CreateSuccess(cosmosElements, 1, 500, headers)));

            //// Buffer to success responses
            //await itemProducerTree.BufferMoreDocuments(cancellationTokenSource.Token);

            // Buffer a split
            mockQueryContext.Setup(x => x.ExecuteQueryAsync(
               sqlQuerySpec,
               cancellationTokenSource.Token,
               It.Is<Action<CosmosRequestMessage>>(action => VerifySamePkRange(action, partitionKeyRange))))
               .Returns(Task.FromResult(CreateSplitFailure()));

            // After split setup a valid response for both new partition key range
            mockQueryContext.SetupSequence(x => x.ExecuteQueryAsync(
               sqlQuerySpec,
               cancellationTokenSource.Token,
               It.Is<Action<CosmosRequestMessage>>(action => VerifySamePkRange(action, partitionKeyRangeAfterSplit[0]))))
               .Returns(Task.FromResult(CosmosQueryResponse.CreateSuccess(GetMockCosmosElements(), 1, 500, headers)))
               .Throws(new InvalidOperationException($"It should only pull the message once for partition key {partitionKeyRangeAfterSplit[0]}"));

            mockQueryContext.SetupSequence(x => x.ExecuteQueryAsync(
               sqlQuerySpec,
               cancellationTokenSource.Token,
               It.Is<Action<CosmosRequestMessage>>(action => VerifySamePkRange(action, partitionKeyRangeAfterSplit[1]))))
               .Returns(Task.FromResult(CosmosQueryResponse.CreateSuccess(GetMockCosmosElements(), 1, 500, headers)))
               .Throws(new InvalidOperationException($"It should only pull the message once for partition key {partitionKeyRangeAfterSplit[0]}"));

            // Buffer more will hit the split. It will update the new ranges.
            //await itemProducerTree.BufferMoreDocuments(cancellationTokenSource.Token);

            // First item should be a success
            (bool successfullyMovedNext, CosmosQueryResponse failureResponse) result = await itemProducerTree.MoveNextAsync(cancellationTokenSource.Token);
            Assert.IsTrue(result.successfullyMovedNext);
            Assert.IsNull(result.failureResponse);
            Assert.IsTrue(itemProducerTree.HasMoreResults);
            CosmosElement firstElement = itemProducerTree.Current;

            // Second item should be a success
            result = await itemProducerTree.MoveNextAsync(cancellationTokenSource.Token);
            Assert.IsTrue(result.successfullyMovedNext);
            Assert.IsNull(result.failureResponse);
            Assert.IsTrue(itemProducerTree.HasMoreResults);
            CosmosElement secondElement = itemProducerTree.Current;

            // Third item should be a success
            result = await itemProducerTree.MoveNextAsync(cancellationTokenSource.Token);
            Assert.IsTrue(result.successfullyMovedNext);
            Assert.IsNull(result.failureResponse);
            Assert.IsTrue(itemProducerTree.HasMoreResults);
            CosmosElement thirdElement = itemProducerTree.Current;

            result = await itemProducerTree.MoveNextAsync(cancellationTokenSource.Token);
            Assert.IsTrue(itemProducerTree.HasMoreResults);
            CosmosElement forthElement = itemProducerTree.Current;

            // Try to buffer after failure. It should return the previous cached failure and not try to buffer again.
            mockQueryContext.Setup(x => x.ExecuteQueryAsync(sqlQuerySpec, cancellationTokenSource.Token, It.IsAny<Action<CosmosRequestMessage>>())).
                Throws(new Exception("Previous buffer failed. Operation should return original failure and not try again"));

            await itemProducerTree.BufferMoreDocuments(cancellationTokenSource.Token);
            Assert.IsFalse(result.successfullyMovedNext);
            Assert.IsNotNull(result.failureResponse);
            Assert.IsFalse(itemProducerTree.HasMoreResults);
        }

        private class MockElementComparer : IEqualityComparer<CosmosElement>
        {
            public bool Equals(CosmosElement x, CosmosElement y)
            {
                return x.Equals(y);
            }

            public int GetHashCode(CosmosElement obj)
            {
                throw new NotImplementedException();
            }
        }

        private static IEnumerable<CosmosElement> GetMockCosmosElements()
        {
            return new List<CosmosElement>()
            {
                new Mock<CosmosElement>(CosmosElementType.Object).Object
            };
        }

        private static bool VerifySamePkRange(Action<CosmosRequestMessage> action, PartitionKeyRange partitionKeyRange)
        {
            CosmosRequestMessage requestMessage = new CosmosRequestMessage();
            action(requestMessage);
            string requestPkRange = requestMessage.PartitionKeyRangeId;

            return requestPkRange != null &&
                string.Equals(requestPkRange, partitionKeyRange.Id);
        }

        private static CosmosQueryResponse CreateSplitFailure()
        {
            CosmosQueryResponseMessageHeaders headersFailure = new CosmosQueryResponseMessageHeaders(null, null)
            {
                ActivityId = "47D27BC3-A756-457D-BB73-854E24CA5D7F",
                RequestCharge = 2,
                SubStatusCode = SubStatusCodes.PartitionKeyRangeGone
            };

            return CosmosQueryResponse.CreateFailure(headersFailure, HttpStatusCode.Gone, null, "Split Error message", null);
        }
    }
}
