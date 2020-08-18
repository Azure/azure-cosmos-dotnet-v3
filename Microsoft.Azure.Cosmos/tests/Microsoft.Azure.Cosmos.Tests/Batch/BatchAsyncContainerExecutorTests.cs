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
    using System.Xml.Linq;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Routing;
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

            Mock<CosmosClientContext> mockedContext = new Mock<CosmosClientContext>();
            mockedContext.Setup(c => c.ClientOptions).Returns(new CosmosClientOptions());
            mockedContext
                .SetupSequence(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.PartitionKey?>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<CosmosDiagnosticsContext>(),
                    It.IsAny<CancellationToken>()))
                .Returns(this.GenerateSplitResponseAsync(itemBatchOperation))
                .Returns(this.GenerateOkResponseAsync(itemBatchOperation));

            mockedContext.Setup(c => c.SerializerCore).Returns(MockCosmosUtil.Serializer);

            string link = "/dbs/db/colls/colls";
            Mock<ContainerInternal> mockContainer = new Mock<ContainerInternal>();
            mockContainer.Setup(x => x.LinkUri).Returns(link);
            mockContainer.Setup(x => x.GetPartitionKeyDefinitionAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(new PartitionKeyDefinition() { Paths = new Collection<string>() { "/id" } }));

            CollectionRoutingMap routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                new[]
                    {
                        Tuple.Create(new PartitionKeyRange{ Id = "0", MinInclusive = "", MaxExclusive = "FF"}, (ServiceIdentity)null)
                    },
                string.Empty);
            mockContainer.Setup(x => x.GetRoutingMapAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(routingMap));
            BatchAsyncContainerExecutor executor = new BatchAsyncContainerExecutor(mockContainer.Object, mockedContext.Object, 20, BatchAsyncContainerExecutorCache.DefaultMaxBulkRequestBodySizeInBytes);
            TransactionalBatchOperationResult result = await executor.AddAsync(itemBatchOperation);

            Mock.Get(mockContainer.Object)
                .Verify(x => x.GetPartitionKeyDefinitionAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
            Mock.Get(mockedContext.Object)
                .Verify(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.PartitionKey?>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<CosmosDiagnosticsContext>(),
                    It.IsAny<CancellationToken>()), Times.Exactly(2));
            Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            Assert.IsNotNull(result.DiagnosticsContext);

            string diagnosticsString = result.DiagnosticsContext.ToString();
            Assert.IsTrue(diagnosticsString.Contains("PointOperationStatistics"), "Diagnostics might be missing");
        }

        [TestMethod]
        public async Task RetryOnNameStale()
        {
            ItemBatchOperation itemBatchOperation = CreateItem("test");

            Mock<CosmosClientContext> mockedContext = new Mock<CosmosClientContext>();
            mockedContext.Setup(c => c.ClientOptions).Returns(new CosmosClientOptions());
            mockedContext
                .SetupSequence(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.PartitionKey?>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<CosmosDiagnosticsContext>(),
                    It.IsAny<CancellationToken>()))
                .Returns(this.GenerateCacheStaleResponseAsync(itemBatchOperation))
                .Returns(this.GenerateOkResponseAsync(itemBatchOperation));

            mockedContext.Setup(c => c.SerializerCore).Returns(MockCosmosUtil.Serializer);

            string link = "/dbs/db/colls/colls";
            Mock<ContainerInternal> mockContainer = new Mock<ContainerInternal>();
            mockContainer.Setup(x => x.LinkUri).Returns(link);
            mockContainer.Setup(x => x.GetPartitionKeyDefinitionAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(new PartitionKeyDefinition() { Paths = new Collection<string>() { "/id" } }));

            CollectionRoutingMap routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                new[]
                    {
                        Tuple.Create(new PartitionKeyRange{ Id = "0", MinInclusive = "", MaxExclusive = "FF"}, (ServiceIdentity)null)
                    },
                string.Empty);
            mockContainer.Setup(x => x.GetRoutingMapAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(routingMap));
            BatchAsyncContainerExecutor executor = new BatchAsyncContainerExecutor(mockContainer.Object, mockedContext.Object, 20, BatchAsyncContainerExecutorCache.DefaultMaxBulkRequestBodySizeInBytes);
            TransactionalBatchOperationResult result = await executor.AddAsync(itemBatchOperation);

            Mock.Get(mockContainer.Object)
                .Verify(x => x.GetPartitionKeyDefinitionAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
            Mock.Get(mockedContext.Object)
                .Verify(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.PartitionKey?>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<CosmosDiagnosticsContext>(),
                    It.IsAny<CancellationToken>()), Times.Exactly(2));
            Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            Assert.IsNotNull(result.DiagnosticsContext);

            string diagnosticsString = result.DiagnosticsContext.ToString();
            Assert.IsTrue(diagnosticsString.Contains("PointOperationStatistics"), "Diagnostics might be missing");
        }

        [TestMethod]
        public async Task RetryOn429()
        {
            ItemBatchOperation itemBatchOperation = CreateItem("test");

            Mock<CosmosClientContext> mockedContext = new Mock<CosmosClientContext>();
            mockedContext.Setup(c => c.ClientOptions).Returns(new CosmosClientOptions());
            mockedContext
                .SetupSequence(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.PartitionKey?>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<CosmosDiagnosticsContext>(),
                    It.IsAny<CancellationToken>()))
                .Returns(this.Generate429ResponseAsync(itemBatchOperation))
                .Returns(this.GenerateOkResponseAsync(itemBatchOperation));

            mockedContext.Setup(c => c.SerializerCore).Returns(MockCosmosUtil.Serializer);

            string link = $"/dbs/db/colls/colls";
            Mock<ContainerInternal> mockContainer = new Mock<ContainerInternal>();
            mockContainer.Setup(x => x.LinkUri).Returns(link);
            mockContainer.Setup(x => x.GetPartitionKeyDefinitionAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(new PartitionKeyDefinition() { Paths = new Collection<string>() { "/id" } }));

            CollectionRoutingMap routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                new[]
                    {
                        Tuple.Create(new PartitionKeyRange{ Id = "0", MinInclusive = "", MaxExclusive = "FF"}, (ServiceIdentity)null)
                    },
                string.Empty);
            mockContainer.Setup(x => x.GetRoutingMapAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(routingMap));
            BatchAsyncContainerExecutor executor = new BatchAsyncContainerExecutor(mockContainer.Object, mockedContext.Object, 20, BatchAsyncContainerExecutorCache.DefaultMaxBulkRequestBodySizeInBytes);
            TransactionalBatchOperationResult result = await executor.AddAsync(itemBatchOperation);

            Mock.Get(mockContainer.Object)
                .Verify(x => x.GetPartitionKeyDefinitionAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
            Mock.Get(mockedContext.Object)
                .Verify(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.PartitionKey?>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<CosmosDiagnosticsContext>(),
                    It.IsAny<CancellationToken>()), Times.Exactly(2));
            Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            Assert.IsNotNull(result.DiagnosticsContext);

            string diagnosticsString = result.DiagnosticsContext.ToString();
            Assert.IsTrue(diagnosticsString.Contains("PointOperationStatistics"), "Diagnostics might be missing");
        }

        [TestMethod]
        public async Task DoesNotRecalculatePartitionKeyRangeOnNoSplits()
        {
            ItemBatchOperation itemBatchOperation = CreateItem("test");

            Mock<CosmosClientContext> mockedContext = new Mock<CosmosClientContext>();
            mockedContext.Setup(c => c.ClientOptions).Returns(new CosmosClientOptions());
            mockedContext
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.PartitionKey?>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<CosmosDiagnosticsContext>(),
                    It.IsAny<CancellationToken>()))
                .Returns(this.GenerateOkResponseAsync(itemBatchOperation));

            mockedContext.Setup(c => c.SerializerCore).Returns(MockCosmosUtil.Serializer);

            string link = "/dbs/db/colls/colls";
            Mock<ContainerInternal> mockContainer = new Mock<ContainerInternal>();
            mockContainer.Setup(x => x.LinkUri).Returns(link);
            mockContainer.Setup(x => x.GetPartitionKeyDefinitionAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(new PartitionKeyDefinition() { Paths = new Collection<string>() { "/id" } }));

            CollectionRoutingMap routingMap = CollectionRoutingMap.TryCreateCompleteRoutingMap(
                new[]
                    {
                        Tuple.Create(new PartitionKeyRange{ Id = "0", MinInclusive = "", MaxExclusive = "FF"}, (ServiceIdentity)null)
                    },
                string.Empty);
            mockContainer.Setup(x => x.GetRoutingMapAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(routingMap));
            BatchAsyncContainerExecutor executor = new BatchAsyncContainerExecutor(mockContainer.Object, mockedContext.Object, 20, BatchAsyncContainerExecutorCache.DefaultMaxBulkRequestBodySizeInBytes);
            TransactionalBatchOperationResult result = await executor.AddAsync(itemBatchOperation);

            Mock.Get(mockContainer.Object)
                .Verify(x => x.GetPartitionKeyDefinitionAsync(It.IsAny<CancellationToken>()), Times.Once);
            Mock.Get(mockedContext.Object)
                .Verify(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Cosmos.PartitionKey?>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<CosmosDiagnosticsContext>(),
                    It.IsAny<CancellationToken>()), Times.Once);
            Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
        }

        [DataTestMethod]
        [DataRow(true, null, true)]
        [DataRow(false, null, false)]
        [DataRow(false, false, false)]
        [DataRow(false, true, true)]
        [DataRow(true, true, true)]
        [DataRow(true, false, false)]
        public async Task ClientContext_UsesBulkWhenOptionsAreSet(bool clientOptionsAllowBulk, bool? itemRequestOptionsAllowBulk, bool expectedUseBulk)
        {
            MyDocument myDocument = new MyDocument() { id = "test", Status = "test" };
            ItemRequestOptions itemRequestOptions = null;
            if (itemRequestOptionsAllowBulk.HasValue)
            {
                itemRequestOptions = new ItemRequestOptions() { EnableBulkExecution = itemRequestOptionsAllowBulk.Value };
            }

            Mock<BatchAsyncContainerExecutor> mockExecutor = new Mock<BatchAsyncContainerExecutor>();
            mockExecutor
                .Setup(e => e.AddAsync(
                    It.IsAny<ItemBatchOperation>(),
                    It.IsAny<ItemRequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TransactionalBatchOperationResult(HttpStatusCode.OK));

            bool handlerCalled = false;
            TestHandler testHandler = new TestHandler((request, cancellationToken) => {
                handlerCalled = true;
                return TestHandler.ReturnSuccess();
            });

            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            CosmosClientContext clientContext = ClientContextCore.Create(
                client,
                new MockDocumentClient(),
                new CosmosClientOptions() { AllowBulkExecution = clientOptionsAllowBulk });
            RequestHandler handler = clientContext.RequestHandler.InnerHandler;
            while (handler != null)
            {
                if (handler.InnerHandler is RouterHandler)
                {
                    handler.InnerHandler = testHandler;
                    break;
                }

                handler = handler.InnerHandler;
            }

            Uri link = new Uri($"/dbs/db/colls/colls", UriKind.Relative);
            Mock<ContainerInternal> mockContainer = new Mock<ContainerInternal>();
            mockContainer.Setup(x => x.LinkUri).Returns(link);
            mockContainer.Setup(x => x.BatchExecutor).Returns(mockExecutor.Object);

            ResponseMessage responseMessage = await clientContext.ProcessResourceOperationStreamAsync(
                link,
                ResourceType.Document,
                OperationType.Delete,
                itemRequestOptions,
                mockContainer.Object,
                partitionKey: new Cosmos.PartitionKey("test"),
                itemId: "test",
                streamPayload: null,
                requestEnricher: null,
                diagnosticsContext: CosmosDiagnosticsContext.Create(itemRequestOptions),
                cancellationToken: default(CancellationToken));

            Assert.AreEqual(HttpStatusCode.OK, responseMessage.StatusCode);

            if (expectedUseBulk)
            {
                // Combination expected Bulk to be used
                Assert.IsFalse(handlerCalled, $"Expected Bulk pipeline to be used. Options.AllowBulk {clientOptionsAllowBulk} ItemRequestOptions.EnableBulk {itemRequestOptionsAllowBulk}");
                Mock.Get(mockExecutor.Object)
                    .Verify(e => e.AddAsync(
                        It.IsAny<ItemBatchOperation>(),
                        It.IsAny<ItemRequestOptions>(),
                        It.IsAny<CancellationToken>()), Times.Once, $"Expected Bulk pipeline to be used. Options.AllowBulk {clientOptionsAllowBulk} ItemRequestOptions.EnableBulk {itemRequestOptionsAllowBulk}");
            }
            else
            {
                // Combination did not expect Bulk to be used
                Assert.IsTrue(handlerCalled, $"Expected normal pipeline to be used. Options.AllowBulk {clientOptionsAllowBulk} ItemRequestOptions.EnableBulk {itemRequestOptionsAllowBulk}");
                Mock.Get(mockExecutor.Object)
                    .Verify(e => e.AddAsync(
                        It.IsAny<ItemBatchOperation>(),
                        It.IsAny<ItemRequestOptions>(),
                        It.IsAny<CancellationToken>()), Times.Never, $"Expected normal pipeline to be used. Options.AllowBulk {clientOptionsAllowBulk} ItemRequestOptions.EnableBulk {itemRequestOptionsAllowBulk}");
            }
        }

        private async Task<ResponseMessage> GenerateSplitResponseAsync(ItemBatchOperation itemBatchOperation)
        {
            List<TransactionalBatchOperationResult> results = new List<TransactionalBatchOperationResult>();
            ItemBatchOperation[] arrayOperations = new ItemBatchOperation[1];
            results.Add(
                new TransactionalBatchOperationResult(HttpStatusCode.Gone)
                {
                    ETag = itemBatchOperation.Id,
                    SubStatusCode = SubStatusCodes.PartitionKeyRangeGone
                });

            arrayOperations[0] = itemBatchOperation;

            MemoryStream responseContent = await new BatchResponsePayloadWriter(results).GeneratePayloadAsync();

            SinglePartitionKeyServerBatchRequest batchRequest = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                partitionKey: null,
                operations: new ArraySegment<ItemBatchOperation>(arrayOperations),
                serializerCore: MockCosmosUtil.Serializer,
            cancellationToken: CancellationToken.None);

            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.Gone)
            {
                Content = responseContent,
            };

            responseMessage.DiagnosticsContext.AddDiagnosticsInternal(new PointOperationStatistics(
                activityId: Guid.NewGuid().ToString(),
                statusCode: HttpStatusCode.Gone,
                subStatusCode: SubStatusCodes.Unknown,
                responseTimeUtc: DateTime.UtcNow,
                requestCharge: 0,
                errorMessage: string.Empty,
                method: HttpMethod.Get,
                requestUri: "http://localhost",
                requestSessionToken: null,
                responseSessionToken: null));

            responseMessage.Headers.SubStatusCode = SubStatusCodes.PartitionKeyRangeGone;
            return responseMessage;
        }

        private async Task<ResponseMessage> GenerateCacheStaleResponseAsync(ItemBatchOperation itemBatchOperation)
        {
            List<TransactionalBatchOperationResult> results = new List<TransactionalBatchOperationResult>();
            ItemBatchOperation[] arrayOperations = new ItemBatchOperation[1];
            results.Add(
                new TransactionalBatchOperationResult(HttpStatusCode.Gone)
                {
                    ETag = itemBatchOperation.Id,
                    SubStatusCode = SubStatusCodes.NameCacheIsStale
                });

            arrayOperations[0] = itemBatchOperation;

            MemoryStream responseContent = await new BatchResponsePayloadWriter(results).GeneratePayloadAsync();

            SinglePartitionKeyServerBatchRequest batchRequest = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                partitionKey: null,
                operations: new ArraySegment<ItemBatchOperation>(arrayOperations),
                serializerCore: MockCosmosUtil.Serializer,
            cancellationToken: CancellationToken.None);

            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.Gone)
            {
                Content = responseContent,
            };

            responseMessage.DiagnosticsContext.AddDiagnosticsInternal(new PointOperationStatistics(
                activityId: Guid.NewGuid().ToString(),
                statusCode: HttpStatusCode.Gone,
                subStatusCode: SubStatusCodes.Unknown,
                responseTimeUtc: DateTime.UtcNow,
                requestCharge: 0,
                errorMessage: string.Empty,
                method: HttpMethod.Get,
                requestUri: "http://localhost",
                requestSessionToken: null,
                responseSessionToken: null));

            responseMessage.Headers.SubStatusCode = SubStatusCodes.NameCacheIsStale;
            return responseMessage;
        }

        private async Task<ResponseMessage> Generate429ResponseAsync(ItemBatchOperation itemBatchOperation)
        {
            List<TransactionalBatchOperationResult> results = new List<TransactionalBatchOperationResult>();
            ItemBatchOperation[] arrayOperations = new ItemBatchOperation[1];
            results.Add(
                new TransactionalBatchOperationResult((HttpStatusCode) StatusCodes.TooManyRequests)
                {
                    ETag = itemBatchOperation.Id
                });

            arrayOperations[0] = itemBatchOperation;

            MemoryStream responseContent = await new BatchResponsePayloadWriter(results).GeneratePayloadAsync();

            SinglePartitionKeyServerBatchRequest batchRequest = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                partitionKey: null,
                operations: new ArraySegment<ItemBatchOperation>(arrayOperations),
                serializerCore: MockCosmosUtil.Serializer,
            cancellationToken: CancellationToken.None);

            ResponseMessage responseMessage = new ResponseMessage((HttpStatusCode)StatusCodes.TooManyRequests)
            {
                Content = responseContent,
            };

            responseMessage.DiagnosticsContext.AddDiagnosticsInternal(new PointOperationStatistics(
                activityId: Guid.NewGuid().ToString(),
                statusCode: (HttpStatusCode)StatusCodes.TooManyRequests,
                subStatusCode: SubStatusCodes.Unknown,
                responseTimeUtc: DateTime.UtcNow,
                requestCharge: 0,
                errorMessage: string.Empty,
                method: HttpMethod.Get,
                requestUri: "http://localhost",
                requestSessionToken: null,
                responseSessionToken: null));

            return responseMessage;
        }

        private async Task<ResponseMessage> GenerateOkResponseAsync(ItemBatchOperation itemBatchOperation)
        {
            List<TransactionalBatchOperationResult> results = new List<TransactionalBatchOperationResult>();
            ItemBatchOperation[] arrayOperations = new ItemBatchOperation[1];
            results.Add(
                new TransactionalBatchOperationResult(HttpStatusCode.OK)
                {
                    ETag = itemBatchOperation.Id
                });

            arrayOperations[0] = itemBatchOperation;

            MemoryStream responseContent = await new BatchResponsePayloadWriter(results).GeneratePayloadAsync();

            SinglePartitionKeyServerBatchRequest batchRequest = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                partitionKey: null,
                operations: new ArraySegment<ItemBatchOperation>(arrayOperations),
                serializerCore: MockCosmosUtil.Serializer,
            cancellationToken: CancellationToken.None);

            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK)
            {
                Content = responseContent,
            };

            responseMessage.DiagnosticsContext.AddDiagnosticsInternal(new PointOperationStatistics(
                activityId: Guid.NewGuid().ToString(),
                statusCode: HttpStatusCode.OK,
                subStatusCode: SubStatusCodes.Unknown,
                responseTimeUtc: DateTime.UtcNow,
                requestCharge: 0,
                errorMessage: string.Empty,
                method: HttpMethod.Get,
                requestUri: "http://localhost",
                requestSessionToken: null,
                responseSessionToken: null));

            return responseMessage;
        }

        private static ItemBatchOperation CreateItem(string id)
        {
            MyDocument myDocument = new MyDocument() { id = id, Status = id };
            return new ItemBatchOperation(
                operationType: OperationType.Create,
                operationIndex: 0,
                partitionKey: new Cosmos.PartitionKey(id),
                id: id,
                resourceStream: MockCosmosUtil.Serializer.ToStream(myDocument),
                diagnosticsContext: new CosmosDiagnosticsContextCore());
        }

        private class MyDocument
        {
            public string id { get; set; }

            public string Status { get; set; }

            public bool Updated { get; set; }
        }
    }
}
