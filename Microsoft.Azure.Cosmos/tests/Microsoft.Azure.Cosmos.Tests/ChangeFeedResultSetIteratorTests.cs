//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

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
            mockContext.Setup(x => x.SerializerCore).Returns(MockCosmosUtil.Serializer);
            mockContext.Setup(x => x.Client).Returns(client);
            mockContext.Setup(x => x.CreateLink(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(new Uri("/dbs/test/colls/test", UriKind.Relative));

            ResponseMessage firstResponse = new ResponseMessage(HttpStatusCode.NotModified);
            firstResponse.Headers.ETag = "FirstContinuation";
            ResponseMessage secondResponse = new ResponseMessage(
                statusCode: HttpStatusCode.NotFound,
                requestMessage: null,
                headers: new Headers()
                {
                    ETag = "ShouldNotContainThis"
                },
                cosmosException: CosmosExceptionFactory.CreateNotFoundException("something"),
                diagnostics: new CosmosDiagnosticsContextCore(userClientRequestId: null));

            mockContext.SetupSequence(x => x.ProcessResourceOperationAsync<ResponseMessage>(
                It.IsAny<Uri>(),
                It.IsAny<Documents.ResourceType>(),
                It.IsAny<Documents.OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<ContainerCore>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<Stream>(),
                It.IsAny<Action<RequestMessage>>(),
                It.IsAny<Func<ResponseMessage, ResponseMessage>>(),
                It.IsAny<CosmosDiagnosticsContext>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(firstResponse))
                .Returns(Task.FromResult(secondResponse));

            DatabaseCore databaseCore = new DatabaseCore(mockContext.Object, "mydb");

            StandByFeedIteratorCore iterator = new StandByFeedIteratorCore(
                mockContext.Object, new ContainerCore(mockContext.Object, databaseCore, "myColl"), null, 10, new ChangeFeedRequestOptions());
            ResponseMessage firstRequest = await iterator.ReadNextAsync();
            Assert.IsTrue(firstRequest.Headers.ContinuationToken.Contains(firstResponse.Headers.ETag), "Response should contain the first continuation");
            Assert.IsTrue(!firstRequest.Headers.ContinuationToken.Contains(secondResponse.Headers.ETag), "Response should not contain the second continuation");
            Assert.AreEqual(HttpStatusCode.NotFound, firstRequest.StatusCode);

            mockContext.Verify(x => x.ProcessResourceOperationAsync<ResponseMessage>(
                It.IsAny<Uri>(),
                It.IsAny<Documents.ResourceType>(),
                It.IsAny<Documents.OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<ContainerCore>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<Stream>(),
                It.IsAny<Action<RequestMessage>>(),
                It.IsAny<Func<ResponseMessage, ResponseMessage>>(),
                It.IsAny<CosmosDiagnosticsContext>(),
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
            mockContext.Setup(x => x.SerializerCore).Returns(MockCosmosUtil.Serializer);
            mockContext.Setup(x => x.Client).Returns(client);
            mockContext.Setup(x => x.CreateLink(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(new Uri("/dbs/test/colls/test", UriKind.Relative));

            ResponseMessage firstResponse = new ResponseMessage(HttpStatusCode.NotModified);
            firstResponse.Headers.ETag = "FirstContinuation";
            ResponseMessage secondResponse = new ResponseMessage(HttpStatusCode.OK);
            secondResponse.Headers.ETag = "SecondContinuation";

            mockContext.SetupSequence(x => x.ProcessResourceOperationAsync<ResponseMessage>(
                It.IsAny<Uri>(),
                It.IsAny<Documents.ResourceType>(),
                It.IsAny<Documents.OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<ContainerCore>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<Stream>(),
                It.IsAny<Action<RequestMessage>>(),
                It.IsAny<Func<ResponseMessage, ResponseMessage>>(),
                It.IsAny<CosmosDiagnosticsContext>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(firstResponse))
                .Returns(Task.FromResult(secondResponse));

            DatabaseCore databaseCore = new DatabaseCore(mockContext.Object, "mydb");

            StandByFeedIteratorCore iterator = new StandByFeedIteratorCore(
                mockContext.Object, new ContainerCore(mockContext.Object, databaseCore, "myColl"), null, 10, new ChangeFeedRequestOptions());
            ResponseMessage firstRequest = await iterator.ReadNextAsync();
            Assert.IsTrue(firstRequest.Headers.ContinuationToken.Contains(firstResponse.Headers.ETag), "Response should contain the first continuation");
            Assert.IsTrue(firstRequest.Headers.ContinuationToken.Contains(secondResponse.Headers.ETag), "Response should contain the second continuation");
            Assert.AreEqual(HttpStatusCode.OK, firstRequest.StatusCode);

            mockContext.Verify(x => x.ProcessResourceOperationAsync<ResponseMessage>(
                It.IsAny<Uri>(),
                It.IsAny<Documents.ResourceType>(),
                It.IsAny<Documents.OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<ContainerCore>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<Stream>(),
                It.IsAny<Action<RequestMessage>>(),
                It.IsAny<Func<ResponseMessage, ResponseMessage>>(),
                It.IsAny<CosmosDiagnosticsContext>(),
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
            mockContext.Setup(x => x.SerializerCore).Returns(MockCosmosUtil.Serializer);
            mockContext.Setup(x => x.Client).Returns(client);
            mockContext.Setup(x => x.CreateLink(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(new Uri("/dbs/test/colls/test", UriKind.Relative));

            ResponseMessage firstResponse = new ResponseMessage(HttpStatusCode.NotModified);
            firstResponse.Headers.ETag = "FirstContinuation";
            ResponseMessage secondResponse = new ResponseMessage(HttpStatusCode.NotModified);
            secondResponse.Headers.ETag = "SecondContinuation";
            ResponseMessage thirdResponse = new ResponseMessage(HttpStatusCode.NotModified);
            thirdResponse.Headers.ETag = "ThirdContinuation";

            mockContext.SetupSequence(x => x.ProcessResourceOperationAsync<ResponseMessage>(
                It.IsAny<Uri>(),
                It.IsAny<Documents.ResourceType>(),
                It.IsAny<Documents.OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<ContainerCore>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<Stream>(),
                It.IsAny<Action<RequestMessage>>(),
                It.IsAny<Func<ResponseMessage, ResponseMessage>>(),
                It.IsAny<CosmosDiagnosticsContext>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(firstResponse))
                .Returns(Task.FromResult(secondResponse))
                .Returns(Task.FromResult(thirdResponse));

            DatabaseCore databaseCore = new DatabaseCore(mockContext.Object, "mydb");

            StandByFeedIteratorCore iterator = new StandByFeedIteratorCore(
                mockContext.Object, new ContainerCore(mockContext.Object, databaseCore, "myColl"), null, 10, new ChangeFeedRequestOptions());
            ResponseMessage firstRequest = await iterator.ReadNextAsync();
            Assert.IsTrue(firstRequest.Headers.ContinuationToken.Contains(firstResponse.Headers.ETag), "Response should contain the first continuation");
            Assert.IsTrue(firstRequest.Headers.ContinuationToken.Contains(secondResponse.Headers.ETag), "Response should contain the second continuation");
            Assert.IsTrue(firstRequest.Headers.ContinuationToken.Contains(thirdResponse.Headers.ETag), "Response should contain the third continuation");
            Assert.AreEqual(HttpStatusCode.NotModified, firstRequest.StatusCode);

            mockContext.Verify(x => x.ProcessResourceOperationAsync<ResponseMessage>(
                It.IsAny<Uri>(),
                It.IsAny<Documents.ResourceType>(),
                It.IsAny<Documents.OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<ContainerCore>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<Stream>(),
                It.IsAny<Action<RequestMessage>>(),
                It.IsAny<Func<ResponseMessage, ResponseMessage>>(),
                It.IsAny<CosmosDiagnosticsContext>(),
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
            mockContext.Setup(x => x.SerializerCore).Returns(MockCosmosUtil.Serializer);
            mockContext.Setup(x => x.Client).Returns(client);
            mockContext.Setup(x => x.CreateLink(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(new Uri("/dbs/test/colls/test", UriKind.Relative));

            ResponseMessage firstResponse = new ResponseMessage(HttpStatusCode.NotModified);
            firstResponse.Headers.ETag = "FirstContinuation";

            mockContext.SetupSequence(x => x.ProcessResourceOperationAsync<ResponseMessage>(
                It.IsAny<Uri>(),
                It.IsAny<Documents.ResourceType>(),
                It.IsAny<Documents.OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<ContainerCore>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<Stream>(),
                It.IsAny<Action<RequestMessage>>(),
                It.IsAny<Func<ResponseMessage, ResponseMessage>>(),
                It.IsAny<CosmosDiagnosticsContext>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(firstResponse));

            DatabaseCore databaseCore = new DatabaseCore(mockContext.Object, "mydb");

            StandByFeedIteratorCore iterator = new StandByFeedIteratorCore(
                mockContext.Object, new ContainerCore(mockContext.Object, databaseCore, "myColl"), null, 10, new ChangeFeedRequestOptions());
            ResponseMessage firstRequest = await iterator.ReadNextAsync();
            Assert.IsTrue(firstRequest.Headers.ContinuationToken.Contains(firstResponse.Headers.ETag), "Response should contain the first continuation");
            Assert.AreEqual(HttpStatusCode.NotModified, firstRequest.StatusCode);

            mockContext.Verify(x => x.ProcessResourceOperationAsync<ResponseMessage>(
                It.IsAny<Uri>(),
                It.IsAny<Documents.ResourceType>(),
                It.IsAny<Documents.OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<ContainerCore>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<Stream>(),
                It.IsAny<Action<RequestMessage>>(),
                It.IsAny<Func<ResponseMessage, ResponseMessage>>(),
                It.IsAny<CosmosDiagnosticsContext>(),
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
            mockContext.Setup(x => x.SerializerCore).Returns(MockCosmosUtil.Serializer);
            mockContext.Setup(x => x.Client).Returns(client);
            mockContext.Setup(x => x.CreateLink(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(UriFactory.CreateDocumentCollectionUri("test", "test"));

            DatabaseCore db = new DatabaseCore(mockContext.Object, "test");
            ContainerCore container = new ContainerCore(mockContext.Object, db, "test");
            IEnumerable<string> tokens = await container.GetChangeFeedTokensAsync();
            Assert.AreEqual(3, tokens.Count());

            Routing.PartitionKeyRangeCache pkRangeCache = await documentClient.GetPartitionKeyRangeCacheAsync();
            foreach (string token in tokens)
            {
                // Validate that each token represents a StandByFeedContinuationToken with a single Range
                List<CompositeContinuationToken> deserialized = JsonConvert.DeserializeObject<List<CompositeContinuationToken>>(token);
                Assert.AreEqual(1, deserialized.Count);
                CompositeContinuationToken compositeToken = deserialized[0];

                IReadOnlyList<Documents.PartitionKeyRange> rangesForTheToken = await pkRangeCache.TryGetOverlappingRangesAsync("", compositeToken.Range);
                // Token represents one range
                Assert.AreEqual(1, rangesForTheToken.Count);
                Assert.AreEqual(rangesForTheToken[0].MinInclusive, compositeToken.Range.Min);
                Assert.AreEqual(rangesForTheToken[0].MaxExclusive, compositeToken.Range.Max);
            }
        }

        private class MultiRangeMockDocumentClient : MockDocumentClient
        {
            private List<Documents.PartitionKeyRange> availablePartitionKeyRanges = new List<Documents.PartitionKeyRange>() {
                new Documents.PartitionKeyRange() { MinInclusive = "", MaxExclusive = "AA", Id = "0" },
                new Documents.PartitionKeyRange() { MinInclusive = "AA", MaxExclusive = "BB", Id = "1" },
                new Documents.PartitionKeyRange() { MinInclusive = "BB", MaxExclusive = "FF", Id = "2" }
            };

            internal override IReadOnlyList<Documents.PartitionKeyRange> ResolveOverlapingPartitionKeyRanges(string collectionRid, Documents.Routing.Range<string> range, bool forceRefresh)
            {
                if (range.Min == Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey
                    && range.Max == Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey)
                {
                    return this.availablePartitionKeyRanges;
                }

                if (range.Min == this.availablePartitionKeyRanges[0].MinInclusive)
                {
                    return new List<Documents.PartitionKeyRange>() { this.availablePartitionKeyRanges[0] };
                }

                if (range.Min == this.availablePartitionKeyRanges[1].MinInclusive)
                {
                    return new List<Documents.PartitionKeyRange>() { this.availablePartitionKeyRanges[1] };
                }

                return new List<Documents.PartitionKeyRange>() { this.availablePartitionKeyRanges[2] };
            }
        }
    }
}
