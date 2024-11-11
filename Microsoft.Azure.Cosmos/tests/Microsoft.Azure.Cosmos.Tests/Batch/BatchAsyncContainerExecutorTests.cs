//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class BatchAsyncContainerExecutorTests
    {
        [TestMethod]
        public async Task RetryOnSplit()
        {
            ItemBatchOperation itemBatchOperation = CreateItem("test");

            Mock<CosmosClientContext> mockedContext = this.MockClientContext();
            mockedContext.Setup(c => c.ClientOptions).Returns(new CosmosClientOptions());
            mockedContext
                .SetupSequence(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.FeedRange>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Returns(GenerateSplitResponseAsync(itemBatchOperation))
                .Returns(GenerateOkResponseAsync(itemBatchOperation));

            mockedContext.Setup(c => c.SerializerCore).Returns(MockCosmosUtil.Serializer);

            string link = "/dbs/db/colls/colls";
            Mock<ContainerInternal> mockContainer = new Mock<ContainerInternal>();
            mockContainer.Setup(x => x.LinkUri).Returns(link);
            mockContainer.Setup(x => x.GetCachedContainerPropertiesAsync(It.IsAny<bool>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new ContainerProperties() { PartitionKey = new PartitionKeyDefinition() { Paths = new Collection<string>() { "/id" } } }));
            Mock<CosmosClientContext> context = this.MockClientContext();
            mockContainer.Setup(c => c.ClientContext).Returns(context.Object);
            context.Setup(c => c.DocumentClient).Returns(new ClientWithSplitDetection());


            CollectionRoutingMap routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                new[]
                    {
                        Tuple.Create(new PartitionKeyRange{ Id = "0", MinInclusive = "", MaxExclusive = "FF"}, (ServiceIdentity)null)
                    },
                string.Empty);
            mockContainer.Setup(x => x.GetRoutingMapAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(routingMap));
            BatchAsyncContainerExecutor executor = new BatchAsyncContainerExecutor(mockContainer.Object, mockedContext.Object, 20, BatchAsyncContainerExecutorCache.DefaultMaxBulkRequestBodySizeInBytes);
            TransactionalBatchOperationResult result = await executor.AddAsync(itemBatchOperation, NoOpTrace.Singleton);

            Mock.Get(mockContainer.Object)
                .Verify(x => x.GetCachedContainerPropertiesAsync(It.IsAny<bool>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            Mock.Get(mockedContext.Object)
                .Verify(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.FeedRange>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()), Times.Exactly(2));
            Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            Assert.IsNotNull(result.ToResponseMessage().Trace);
        }

        [TestMethod]
        public async Task RetryOnNameStale()
        {
            ItemBatchOperation itemBatchOperation = CreateItem("test");

            Mock<CosmosClientContext> mockedContext = this.MockClientContext();
            mockedContext.Setup(c => c.ClientOptions).Returns(new CosmosClientOptions());
            mockedContext
                .SetupSequence(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.FeedRange>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Returns(GenerateCacheStaleResponseAsync(itemBatchOperation))
                .Returns(GenerateOkResponseAsync(itemBatchOperation));

            mockedContext.Setup(c => c.SerializerCore).Returns(MockCosmosUtil.Serializer);

            string link = "/dbs/db/colls/colls";
            Mock<ContainerInternal> mockContainer = new Mock<ContainerInternal>();
            mockContainer.Setup(x => x.LinkUri).Returns(link);
            mockContainer.Setup(x => x.GetCachedContainerPropertiesAsync(It.IsAny<bool>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new ContainerProperties() { PartitionKey = new PartitionKeyDefinition() { Paths = new Collection<string>() { "/id" } } }));

            CollectionRoutingMap routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                new[]
                {
                    Tuple.Create(new PartitionKeyRange{ Id = "0", MinInclusive = "", MaxExclusive = "FF"}, (ServiceIdentity)null)
                },
                string.Empty);
            mockContainer.Setup(x => x.GetRoutingMapAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(routingMap));
            BatchAsyncContainerExecutor executor = new BatchAsyncContainerExecutor(mockContainer.Object, mockedContext.Object, 20, BatchAsyncContainerExecutorCache.DefaultMaxBulkRequestBodySizeInBytes);
            TransactionalBatchOperationResult result = await executor.AddAsync(itemBatchOperation, NoOpTrace.Singleton);

            Mock.Get(mockContainer.Object)
                .Verify(x => x.GetCachedContainerPropertiesAsync(It.IsAny<bool>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            Mock.Get(mockedContext.Object)
                .Verify(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.FeedRange>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()), Times.Exactly(2));
            Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            Assert.IsNotNull(result.ToResponseMessage().Trace);
        }

        [TestMethod]
        public async Task RetryOn429()
        {
            ItemBatchOperation itemBatchOperation = CreateItem("test");

            Mock<CosmosClientContext> mockedContext = this.MockClientContext();
            mockedContext.Setup(c => c.ClientOptions).Returns(new CosmosClientOptions());
            mockedContext
                .SetupSequence(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.FeedRange>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Generate429ResponseAsync(itemBatchOperation))
                .Returns(GenerateOkResponseAsync(itemBatchOperation));

            mockedContext.Setup(c => c.SerializerCore).Returns(MockCosmosUtil.Serializer);

            string link = $"/dbs/db/colls/colls";
            Mock<ContainerInternal> mockContainer = new Mock<ContainerInternal>();
            mockContainer.Setup(x => x.LinkUri).Returns(link);
            mockContainer.Setup(x => x.GetCachedContainerPropertiesAsync(It.IsAny<bool>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new ContainerProperties() { PartitionKey = new PartitionKeyDefinition() { Paths = new Collection<string>() { "/id" } } }));

            CollectionRoutingMap routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                new[]
                    {
                        Tuple.Create(new PartitionKeyRange{ Id = "0", MinInclusive = "", MaxExclusive = "FF"}, (ServiceIdentity)null)
                    },
                string.Empty);
            mockContainer.Setup(x => x.GetRoutingMapAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(routingMap));
            BatchAsyncContainerExecutor executor = new BatchAsyncContainerExecutor(mockContainer.Object, mockedContext.Object, 20, BatchAsyncContainerExecutorCache.DefaultMaxBulkRequestBodySizeInBytes);
            TransactionalBatchOperationResult result = await executor.AddAsync(itemBatchOperation, NoOpTrace.Singleton);

            Mock.Get(mockContainer.Object)
                .Verify(x => x.GetCachedContainerPropertiesAsync(It.IsAny<bool>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            Mock.Get(mockedContext.Object)
                .Verify(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.FeedRange>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()), Times.Exactly(2));
            Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            Assert.IsNotNull(result.ToResponseMessage().Trace);
        }

        [TestMethod]
        public async Task DoesNotRecalculatePartitionKeyRangeOnNoSplits()
        {
            ItemBatchOperation itemBatchOperation = CreateItem("test");

            Mock<CosmosClientContext> mockedContext = this.MockClientContext();
            mockedContext.Setup(c => c.ClientOptions).Returns(new CosmosClientOptions());
            mockedContext
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.FeedRange>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Returns(GenerateOkResponseAsync(itemBatchOperation));

            mockedContext.Setup(c => c.SerializerCore).Returns(MockCosmosUtil.Serializer);

            string link = "/dbs/db/colls/colls";
            Mock<ContainerInternal> mockContainer = new Mock<ContainerInternal>();
            mockContainer.Setup(x => x.LinkUri).Returns(link);
            mockContainer.Setup(x => x.GetCachedContainerPropertiesAsync(It.IsAny<bool>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new ContainerProperties() { PartitionKey = new PartitionKeyDefinition() { Paths = new Collection<string>() { "/id" } } }));

            CollectionRoutingMap routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                new[]
                    {
                        Tuple.Create(new PartitionKeyRange{ Id = "0", MinInclusive = "", MaxExclusive = "FF"}, (ServiceIdentity)null)
                    },
                string.Empty);
            mockContainer.Setup(x => x.GetRoutingMapAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(routingMap));
            BatchAsyncContainerExecutor executor = new BatchAsyncContainerExecutor(mockContainer.Object, mockedContext.Object, 20, BatchAsyncContainerExecutorCache.DefaultMaxBulkRequestBodySizeInBytes);
            TransactionalBatchOperationResult result = await executor.AddAsync(itemBatchOperation, NoOpTrace.Singleton);

            Mock.Get(mockContainer.Object)
                .Verify(x => x.GetCachedContainerPropertiesAsync(It.IsAny<bool>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>()), Times.Once);
            Mock.Get(mockedContext.Object)
                .Verify(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.FeedRange>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()), Times.Once);
            Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
        }

        private static async Task<ResponseMessage> GenerateResponseAsync(
            ItemBatchOperation itemBatchOperation,
            HttpStatusCode httpStatusCode,
            SubStatusCodes subStatusCode)
        {
            List<TransactionalBatchOperationResult> results = new List<TransactionalBatchOperationResult>();
            ItemBatchOperation[] arrayOperations = new ItemBatchOperation[1];
            results.Add(
                new TransactionalBatchOperationResult(httpStatusCode)
                {
                    ETag = itemBatchOperation.Id,
                    SubStatusCode = subStatusCode
                });

            arrayOperations[0] = itemBatchOperation;

            MemoryStream responseContent = await new BatchResponsePayloadWriter(results).GeneratePayloadAsync();

            _ = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                partitionKey: null,
                operations: new ArraySegment<ItemBatchOperation>(arrayOperations),
                serializerCore: MockCosmosUtil.Serializer,
                trace: NoOpTrace.Singleton,
                cancellationToken: CancellationToken.None);

            ResponseMessage responseMessage = new ResponseMessage(httpStatusCode)
            {
                Content = responseContent,
            };

            using (responseMessage.Trace = Trace.GetRootTrace("Test Trace"))
            {
                responseMessage.Trace.AddDatum(
                    "Point Operation Statistics",
                    new PointOperationStatisticsTraceDatum(
                    activityId: Guid.NewGuid().ToString(),
                    statusCode: httpStatusCode,
                    subStatusCode: subStatusCode,
                    responseTimeUtc: DateTime.UtcNow,
                    requestCharge: 0,
                    errorMessage: string.Empty,
                    method: HttpMethod.Get,
                    requestUri: "http://localhost",
                    requestSessionToken: null,
                    responseSessionToken: null,
                    beLatencyInMs: "0.42"));
            }

            responseMessage.Headers.SubStatusCode = subStatusCode;
            return responseMessage;
        }

        private static Task<ResponseMessage> GenerateSplitResponseAsync(ItemBatchOperation itemBatchOperation)
        {
            return GenerateResponseAsync(itemBatchOperation, HttpStatusCode.Gone, SubStatusCodes.PartitionKeyRangeGone);
        }

        private static Task<ResponseMessage> GenerateCacheStaleResponseAsync(ItemBatchOperation itemBatchOperation)
        {
            return GenerateResponseAsync(itemBatchOperation, HttpStatusCode.Gone, SubStatusCodes.NameCacheIsStale);
        }

        private static Task<ResponseMessage> Generate429ResponseAsync(ItemBatchOperation itemBatchOperation)
        {
            return GenerateResponseAsync(itemBatchOperation, (HttpStatusCode)429, SubStatusCodes.Unknown);
        }

        private static Task<ResponseMessage> GenerateOkResponseAsync(ItemBatchOperation itemBatchOperation)
        {
            return GenerateResponseAsync(itemBatchOperation, HttpStatusCode.OK, SubStatusCodes.Unknown);
        }

        private static ItemBatchOperation CreateItem(string id)
        {
            MyDocument myDocument = new MyDocument() { id = id, Status = id };
            return new ItemBatchOperation(
                operationType: OperationType.Create,
                operationIndex: 0,
                partitionKey: new Cosmos.PartitionKey(id),
                id: id,
                resourceStream: MockCosmosUtil.Serializer.ToStream(myDocument));
        }

        private Mock<CosmosClientContext> MockClientContext()
        {
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(x => x.OperationHelperAsync<object>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<OperationType>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<Func<ITrace, Task<object>>>(),
                It.IsAny<Tuple<string, Func<object, OpenTelemetryAttributes>>>(),
                It.IsAny<ResourceType?>(),
                It.IsAny<TraceComponent>(),
                It.IsAny<TraceLevel>()))
               .Returns<string, string, string, OperationType, RequestOptions, Func<ITrace, Task<object>>, Tuple<string, Func<object, OpenTelemetryAttributes>>, ResourceType?, TraceComponent, TraceLevel>(
                (operationName, containerName, databaseName, operationType, requestOptions, func, oTelFunc, resourceType, comp, level) => func(NoOpTrace.Singleton));

            mockContext.Setup(x => x.Client).Returns(MockCosmosUtil.CreateMockCosmosClient());

            return mockContext;
        }

        private class MyDocument
        {
            public string id { get; set; }

            public string Status { get; set; }

            public bool Updated { get; set; }
        }

        private class ClientWithSplitDetection : MockDocumentClient
        {
            private readonly Mock<PartitionKeyRangeCache> partitionKeyRangeCache;

            public ClientWithSplitDetection()
            {
                this.partitionKeyRangeCache = new Mock<PartitionKeyRangeCache>(MockBehavior.Strict, null, null, null, null);
                this.partitionKeyRangeCache.Setup(
                        m => m.TryGetOverlappingRangesAsync(
                            It.IsAny<string>(),
                            It.IsAny<Documents.Routing.Range<string>>(),
                            It.IsAny<ITrace>(),
                            It.Is<bool>(b => b == true) // Mocking only the refresh, if it doesn't get called, the test fails
                        )
                ).Returns((string collectionRid, Documents.Routing.Range<string> range, ITrace trace, bool forceRefresh) => Task.FromResult<IReadOnlyList<PartitionKeyRange>>(this.ResolveOverlapingPartitionKeyRanges(collectionRid, range, forceRefresh)));
            }

            internal override Task<PartitionKeyRangeCache> GetPartitionKeyRangeCacheAsync(ITrace trace)
            {
                return Task.FromResult(this.partitionKeyRangeCache.Object);
            }

        }
    }
}