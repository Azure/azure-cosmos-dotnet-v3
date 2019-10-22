//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Microsoft.Azure.Cosmos.Routing;

    [TestClass]
    public class ChangeFeedResultSetIteratorTests
    {
        [TestMethod]
        public async Task ContinuationTokenIsNotUpdatedOnFails()
        {
            MultiRangeMockDocumentClient documentClient = new MultiRangeMockDocumentClient();
            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(x => x.ClientOptions).Returns(MockCosmosUtil.GetDefaultConfiguration());
            mockContext.Setup(x => x.DocumentClient).Returns(documentClient);
            mockContext.Setup(x => x.CosmosSerializer).Returns(MockCosmosUtil.Serializer);
            mockContext.Setup(x => x.Client).Returns(client);

            ResponseMessage firstResponse = new ResponseMessage(HttpStatusCode.NotModified);
            firstResponse.CosmosHeaders.ETag = "FirstContinuation";
            ResponseMessage secondResponse = new ResponseMessage(HttpStatusCode.NotFound);
            secondResponse.CosmosHeaders.ETag = "ShouldNotContainThis";
            secondResponse.ErrorMessage = "something";

            mockContext.SetupSequence(x => x.ProcessResourceOperationStreamAsync(
                It.IsAny<Uri>(),
                It.IsAny<ResourceType>(),
                It.IsAny<OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<ContainerCore>(),
                It.IsAny<Azure.Cosmos.PartitionKey?>(),
                It.IsAny<Stream>(),
                It.IsAny<Action<RequestMessage>>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((Response)firstResponse))
                .Returns(Task.FromResult((Response)secondResponse));

            ChangeFeedResultSetIteratorCore iterator = new ChangeFeedResultSetIteratorCore(
                mockContext.Object, (ContainerCore)client.GetContainer("myDb", "myColl"), null, 10, new ChangeFeedRequestOptions());
            Response firstRequest = await iterator.ReadNextAsync();
            Assert.IsTrue(firstRequest.Headers.GetContinuationToken().Contains(firstResponse.CosmosHeaders.ETag), "Response should contain the first continuation");
            Assert.IsTrue(!firstRequest.Headers.GetContinuationToken().Contains(secondResponse.CosmosHeaders.ETag), "Response should not contain the second continuation");
            Assert.AreEqual((int)HttpStatusCode.NotFound, firstRequest.Status);

            mockContext.Verify(x => x.ProcessResourceOperationStreamAsync(
                It.IsAny<Uri>(),
                It.IsAny<ResourceType>(),
                It.IsAny<OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<ContainerCore>(),
                It.IsAny<Azure.Cosmos.PartitionKey?>(),
                It.IsAny<Stream>(),
                It.IsAny<Action<RequestMessage>>(),
                It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [TestMethod]
        public async Task ShouldContinueUntilResponseOk()
        {
            // Setting 3 ranges, first one returns a 304, second returns Ok
            MultiRangeMockDocumentClient documentClient = new MultiRangeMockDocumentClient();
            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(x => x.ClientOptions).Returns(MockCosmosUtil.GetDefaultConfiguration());
            mockContext.Setup(x => x.DocumentClient).Returns(documentClient);
            mockContext.Setup(x => x.CosmosSerializer).Returns(MockCosmosUtil.Serializer);
            mockContext.Setup(x => x.Client).Returns(client);

            ResponseMessage firstResponse = new ResponseMessage(HttpStatusCode.NotModified);
            firstResponse.CosmosHeaders.ETag = "FirstContinuation";
            ResponseMessage secondResponse = new ResponseMessage(HttpStatusCode.OK);
            secondResponse.CosmosHeaders.ETag = "SecondContinuation";

            mockContext.SetupSequence(x => x.ProcessResourceOperationStreamAsync(
                It.IsAny<Uri>(),
                It.IsAny<ResourceType>(),
                It.IsAny<OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<ContainerCore>(),
                It.IsAny<Azure.Cosmos.PartitionKey?>(),
                It.IsAny<Stream>(),
                It.IsAny<Action<RequestMessage>>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((Response)firstResponse))
                .Returns(Task.FromResult((Response)secondResponse));

            ChangeFeedResultSetIteratorCore iterator = new ChangeFeedResultSetIteratorCore(
                mockContext.Object, (ContainerCore)client.GetContainer("myDb", "myColl"), null, 10, new ChangeFeedRequestOptions());
            Response firstRequest = await iterator.ReadNextAsync();
            Assert.IsTrue(firstRequest.Headers.GetContinuationToken().Contains(firstResponse.CosmosHeaders.ETag), "Response should contain the first continuation");
            Assert.IsTrue(firstRequest.Headers.GetContinuationToken().Contains(secondResponse.CosmosHeaders.ETag), "Response should contain the second continuation");
            Assert.AreEqual((int)HttpStatusCode.OK, firstRequest.Status);

            mockContext.Verify(x => x.ProcessResourceOperationStreamAsync(
                It.IsAny<Uri>(),
                It.IsAny<ResourceType>(),
                It.IsAny<OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<ContainerCore>(),
                It.IsAny<Azure.Cosmos.PartitionKey?>(),
                It.IsAny<Stream>(),
                It.IsAny<Action<RequestMessage>>(),
                It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [TestMethod]
        public async Task ShouldReturnNotModifiedAfterCyclingOnAllRanges()
        {
            // Setting mock to have 3 ranges, this test will get a 304 on all 3 ranges, do 3 backend requests, and return a 304
            MultiRangeMockDocumentClient documentClient = new MultiRangeMockDocumentClient();
            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(x => x.ClientOptions).Returns(MockCosmosUtil.GetDefaultConfiguration());
            mockContext.Setup(x => x.DocumentClient).Returns(documentClient);
            mockContext.Setup(x => x.CosmosSerializer).Returns(MockCosmosUtil.Serializer);
            mockContext.Setup(x => x.Client).Returns(client);

            ResponseMessage firstResponse = new ResponseMessage(HttpStatusCode.NotModified);
            firstResponse.CosmosHeaders.ETag = "FirstContinuation";
            ResponseMessage secondResponse = new ResponseMessage(HttpStatusCode.NotModified);
            secondResponse.CosmosHeaders.ETag = "SecondContinuation";
            ResponseMessage thirdResponse = new ResponseMessage(HttpStatusCode.NotModified);
            thirdResponse.CosmosHeaders.ETag = "ThirdContinuation";

            mockContext.SetupSequence(x => x.ProcessResourceOperationStreamAsync(
                It.IsAny<Uri>(),
                It.IsAny<ResourceType>(),
                It.IsAny<OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<ContainerCore>(),
                It.IsAny<Azure.Cosmos.PartitionKey?>(),
                It.IsAny<Stream>(),
                It.IsAny<Action<RequestMessage>>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((Response)firstResponse))
                .Returns(Task.FromResult((Response)secondResponse))
                .Returns(Task.FromResult((Response)thirdResponse));

            ChangeFeedResultSetIteratorCore iterator = new ChangeFeedResultSetIteratorCore(
                mockContext.Object, (ContainerCore)client.GetContainer("myDb", "myColl"), null, 10, new ChangeFeedRequestOptions());
            Response firstRequest = await iterator.ReadNextAsync();
            Assert.IsTrue(firstRequest.Headers.GetContinuationToken().Contains(firstResponse.CosmosHeaders.ETag), "Response should contain the first continuation");
            Assert.IsTrue(firstRequest.Headers.GetContinuationToken().Contains(secondResponse.CosmosHeaders.ETag), "Response should contain the second continuation");
            Assert.IsTrue(firstRequest.Headers.GetContinuationToken().Contains(thirdResponse.CosmosHeaders.ETag), "Response should contain the third continuation");
            Assert.AreEqual((int)HttpStatusCode.NotModified, firstRequest.Status);

            mockContext.Verify(x => x.ProcessResourceOperationStreamAsync(
                It.IsAny<Uri>(),
                It.IsAny<ResourceType>(),
                It.IsAny<OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<ContainerCore>(),
                It.IsAny<Azure.Cosmos.PartitionKey?>(),
                It.IsAny<Stream>(),
                It.IsAny<Action<RequestMessage>>(),
                It.IsAny<CancellationToken>()), Times.Exactly(3));
        }

        [TestMethod]
        public async Task ShouldReturnNotModifiedOnSingleRange()
        {
            // Default mock is 1 range
            MultiRangeMockDocumentClient documentClient = new MultiRangeMockDocumentClient();
            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(x => x.ClientOptions).Returns(MockCosmosUtil.GetDefaultConfiguration());
            mockContext.Setup(x => x.DocumentClient).Returns(new MockDocumentClient());
            mockContext.Setup(x => x.CosmosSerializer).Returns(MockCosmosUtil.Serializer);
            mockContext.Setup(x => x.Client).Returns(client);

            ResponseMessage firstResponse = new ResponseMessage(HttpStatusCode.NotModified);
            firstResponse.CosmosHeaders.ETag = "FirstContinuation";

            mockContext.SetupSequence(x => x.ProcessResourceOperationStreamAsync(
                It.IsAny<Uri>(),
                It.IsAny<ResourceType>(),
                It.IsAny<OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<ContainerCore>(),
                It.IsAny<Azure.Cosmos.PartitionKey?>(),
                It.IsAny<Stream>(),
                It.IsAny<Action<RequestMessage>>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((Response)firstResponse));

            ChangeFeedResultSetIteratorCore iterator = new ChangeFeedResultSetIteratorCore(
                mockContext.Object, (ContainerCore)client.GetContainer("myDb", "myColl"), null, 10, new ChangeFeedRequestOptions());
            Response firstRequest = await iterator.ReadNextAsync();
            Assert.IsTrue(firstRequest.Headers.GetContinuationToken().Contains(firstResponse.CosmosHeaders.ETag), "Response should contain the first continuation");
            Assert.AreEqual((int)HttpStatusCode.NotModified, firstRequest.Status);

            mockContext.Verify(x => x.ProcessResourceOperationStreamAsync(
                It.IsAny<Uri>(),
                It.IsAny<ResourceType>(),
                It.IsAny<OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<ContainerCore>(),
                It.IsAny<Azure.Cosmos.PartitionKey?>(),
                It.IsAny<Stream>(),
                It.IsAny<Action<RequestMessage>>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task GetChangeFeedTokensAsyncReturnsOnePerPartitionKeyRange()
        {
            // Setting mock to have 3 ranges, to generate 3 tokens
            MultiRangeMockDocumentClient documentClient = new MultiRangeMockDocumentClient();
            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(x => x.ClientOptions).Returns(MockCosmosUtil.GetDefaultConfiguration());
            mockContext.Setup(x => x.DocumentClient).Returns(documentClient);
            mockContext.Setup(x => x.CosmosSerializer).Returns(MockCosmosUtil.Serializer);
            mockContext.Setup(x => x.Client).Returns(client);
            mockContext.Setup(x => x.CreateLink(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(UriFactory.CreateDocumentCollectionUri("test", "test"));

            DatabaseCore db = new DatabaseCore(mockContext.Object, "test");
            ContainerCore container = new ContainerCore(mockContext.Object, db, "test");
            IEnumerable<string> tokens = await container.GetChangeFeedTokensAsync();
            Assert.AreEqual(3, tokens.Count());

            PartitionKeyRangeCache pkRangeCache = await documentClient.GetPartitionKeyRangeCacheAsync();
            foreach (string token in tokens)
            {
                // Validate that each token represents a StandByFeedContinuationToken with a single Range
                List<CompositeContinuationToken> deserialized = JsonConvert.DeserializeObject<List<CompositeContinuationToken>>(token);
                Assert.AreEqual(1, deserialized.Count);
                CompositeContinuationToken compositeToken = deserialized[0];

                IReadOnlyList<PartitionKeyRange> rangesForTheToken = await pkRangeCache.TryGetOverlappingRangesAsync("", compositeToken.Range);
                // Token represents one range
                Assert.AreEqual(1, rangesForTheToken.Count);
                Assert.AreEqual(rangesForTheToken[0].MinInclusive, compositeToken.Range.Min);
                Assert.AreEqual(rangesForTheToken[0].MaxExclusive, compositeToken.Range.Max);
            }
        }

        private class MultiRangeMockDocumentClient : MockDocumentClient
        {
            private List<PartitionKeyRange> availablePartitionKeyRanges = new List<PartitionKeyRange>() {
                new PartitionKeyRange() { MinInclusive = "", MaxExclusive = "AA", Id = "0" },
                new PartitionKeyRange() { MinInclusive = "AA", MaxExclusive = "BB", Id = "1" },
                new PartitionKeyRange() { MinInclusive = "BB", MaxExclusive = "FF", Id = "2" }
            };

            internal override IReadOnlyList<PartitionKeyRange> ResolveOverlapingPartitionKeyRanges(string collectionRid, Microsoft.Azure.Documents.Routing.Range<string> range, bool forceRefresh)
            {
                if (range.Min == Microsoft.Azure.Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey
                    && range.Max == Microsoft.Azure.Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey)
                {
                    return this.availablePartitionKeyRanges;
                }

                if (range.Min == this.availablePartitionKeyRanges[0].MinInclusive)
                {
                    return new List<PartitionKeyRange>() { this.availablePartitionKeyRanges[0] };
                }

                if (range.Min == this.availablePartitionKeyRanges[1].MinInclusive)
                {
                    return new List<PartitionKeyRange>() { this.availablePartitionKeyRanges[1] };
                }

                return new List<PartitionKeyRange>() { this.availablePartitionKeyRanges[2] };
            }
        }
    }
}
