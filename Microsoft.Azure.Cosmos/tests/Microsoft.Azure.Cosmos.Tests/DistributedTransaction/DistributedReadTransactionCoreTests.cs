// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using CosmosPK = Microsoft.Azure.Cosmos.PartitionKey;

    [TestClass]
    public class DistributedReadTransactionCoreTests
    {
        private const string DatabaseName = "testDb";
        private const string ContainerName = "testColl";
        private static readonly CosmosPK TestPartitionKey = new CosmosPK("pk1");
        private static readonly string ItemId = "item-1";
        private static readonly CosmosClient SharedMockClient = new Mock<CosmosClient>().Object;

        private DistributedReadTransactionCore CreateTransaction()
        {
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(c => c.Client).Returns(DistributedReadTransactionCoreTests.SharedMockClient);
            return new DistributedReadTransactionCore(mockContext.Object);
        }

        private static Cosmos.Container BuildMockContainer(
            string databaseId = DatabaseName,
            string containerId = ContainerName,
            CosmosClient client = null)
        {
            Mock<Cosmos.Database> databaseMock = new Mock<Cosmos.Database>();
            databaseMock.Setup(d => d.Id).Returns(databaseId);
            databaseMock.Setup(d => d.Client).Returns(client ?? DistributedReadTransactionCoreTests.SharedMockClient);

            Mock<Cosmos.Container> containerMock = new Mock<Cosmos.Container>();
            containerMock.Setup(c => c.Id).Returns(containerId);
            containerMock.Setup(c => c.Database).Returns(databaseMock.Object);

            return containerMock.Object;
        }

        #region Constructor validation

        [TestMethod]
        public void Constructor_NullClientContext_ThrowsArgumentNullException()
        {
            Assert.ThrowsException<ArgumentNullException>(() => new DistributedReadTransactionCore(null));
        }

        #endregion

        #region ReadItem argument validation

        [TestMethod]
        public void ReadItem_NullContainer_ThrowsArgumentNullException()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            Assert.ThrowsException<ArgumentNullException>(() =>
                txn.ReadItem(null, TestPartitionKey, ItemId));
        }

        [TestMethod]
        public void ReadItem_NullContainerId_ThrowsArgumentException()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            Assert.ThrowsException<ArgumentException>(() =>
                txn.ReadItem(BuildMockContainer(containerId: null), TestPartitionKey, ItemId));
        }

        [TestMethod]
        public void ReadItem_EmptyContainerId_ThrowsArgumentException()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            Assert.ThrowsException<ArgumentException>(() =>
                txn.ReadItem(BuildMockContainer(containerId: string.Empty), TestPartitionKey, ItemId));
        }

        [TestMethod]
        public void ReadItem_WhitespaceContainerId_ThrowsArgumentException()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            Assert.ThrowsException<ArgumentException>(() =>
                txn.ReadItem(BuildMockContainer(containerId: "   "), TestPartitionKey, ItemId));
        }

        [TestMethod]
        public void ReadItem_NullDatabase_ThrowsArgumentException()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            Mock<Cosmos.Container> containerMock = new Mock<Cosmos.Container>();
            containerMock.Setup(c => c.Id).Returns(ContainerName);
            containerMock.Setup(c => c.Database).Returns((Cosmos.Database)null);

            Assert.ThrowsException<ArgumentException>(() =>
                txn.ReadItem(containerMock.Object, TestPartitionKey, ItemId));
        }

        [TestMethod]
        public void ReadItem_NullDatabaseId_ThrowsArgumentException()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            Assert.ThrowsException<ArgumentException>(() =>
                txn.ReadItem(BuildMockContainer(databaseId: null), TestPartitionKey, ItemId));
        }

        [TestMethod]
        public void ReadItem_EmptyDatabaseId_ThrowsArgumentException()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            Assert.ThrowsException<ArgumentException>(() =>
                txn.ReadItem(BuildMockContainer(databaseId: string.Empty), TestPartitionKey, ItemId));
        }

        [TestMethod]
        public void ReadItem_WhitespaceDatabaseId_ThrowsArgumentException()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            Assert.ThrowsException<ArgumentException>(() =>
                txn.ReadItem(BuildMockContainer(databaseId: "   "), TestPartitionKey, ItemId));
        }

        [TestMethod]
        public void ReadItem_DifferentCosmosClient_ThrowsArgumentException()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            CosmosClient differentClient = new Mock<CosmosClient>().Object;
            Assert.ThrowsException<ArgumentException>(() =>
                txn.ReadItem(BuildMockContainer(client: differentClient), TestPartitionKey, ItemId));
        }

        [TestMethod]
        public void ReadItem_SameCosmosClient_Succeeds()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            txn.ReadItem(BuildMockContainer(), TestPartitionKey, ItemId);
        }

        [TestMethod]
        public void ReadItem_NullId_ThrowsArgumentNullException()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            Assert.ThrowsException<ArgumentNullException>(() =>
                txn.ReadItem(BuildMockContainer(), TestPartitionKey, null));
        }

        [TestMethod]
        public void ReadItem_EmptyId_ThrowsArgumentNullException()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            Assert.ThrowsException<ArgumentNullException>(() =>
                txn.ReadItem(BuildMockContainer(), TestPartitionKey, string.Empty));
        }

        [TestMethod]
        public void ReadItem_WhitespaceId_ThrowsArgumentNullException()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            Assert.ThrowsException<ArgumentNullException>(() =>
                txn.ReadItem(BuildMockContainer(), TestPartitionKey, "   "));
        }

        #endregion

        #region Operation building

        [TestMethod]
        public void ReadItem_ValidArgs_ReturnsThisForChaining()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            DistributedReadTransaction result = txn.ReadItem(BuildMockContainer(), TestPartitionKey, ItemId);
            Assert.AreSame(txn, result);
        }

        [TestMethod]
        public void ReadItem_BuildsOperationWithReadType()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            txn.ReadItem(BuildMockContainer(), TestPartitionKey, ItemId);

            IReadOnlyList<DistributedTransactionOperation> ops = GetOperations(txn);
            Assert.AreEqual(1, ops.Count);
            Assert.AreEqual(OperationType.Read, ops[0].OperationType);
        }

        [TestMethod]
        public void ReadItem_BuildsOperationWithCorrectFields()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            txn.ReadItem(BuildMockContainer(), TestPartitionKey, ItemId);

            DistributedTransactionOperation op = GetOperations(txn)[0];
            Assert.AreEqual(DatabaseName, op.Database);
            Assert.AreEqual(ContainerName, op.Container);
            Assert.AreEqual(ItemId, op.Id);
            Assert.AreEqual(0, op.OperationIndex);
        }

        [TestMethod]
        public void ReadItem_HasNoResourceBody()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            txn.ReadItem(BuildMockContainer(), TestPartitionKey, ItemId);

            DistributedTransactionOperation op = GetOperations(txn)[0];
            Assert.IsTrue(op.ResourceBody.IsEmpty);
            Assert.IsNull(op.ResourceStream);
        }

        [TestMethod]
        public void MultipleReadItems_CorrectOperationIndices()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            txn.ReadItem(BuildMockContainer(), TestPartitionKey, "id-0")
                .ReadItem(BuildMockContainer(), TestPartitionKey, "id-1")
                .ReadItem(BuildMockContainer(), TestPartitionKey, "id-2");

            IReadOnlyList<DistributedTransactionOperation> ops = GetOperations(txn);
            Assert.AreEqual(3, ops.Count);
            Assert.AreEqual(0, ops[0].OperationIndex);
            Assert.AreEqual(1, ops[1].OperationIndex);
            Assert.AreEqual(2, ops[2].OperationIndex);
        }

        [TestMethod]
        public void ReadItem_WithRequestOptions_SetsOptionsOnOperation()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();
            DistributedTransactionRequestOptions options = new DistributedTransactionRequestOptions();
            txn.ReadItem(BuildMockContainer(), TestPartitionKey, ItemId, options);

            DistributedTransactionOperation op = GetOperations(txn)[0];
            Assert.AreSame(options, op.RequestOptions);
        }

        #endregion

        #region Double-commit guard

        [TestMethod]
        public async Task CommitAsync_CalledTwice_ThrowsInvalidOperationException()
        {
            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            contextMock
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<CosmosPK?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildReadSuccessResponse(1));

            DistributedReadTransaction tx = new DistributedReadTransactionCore(contextMock.Object)
                .ReadItem(BuildMockContainer(), TestPartitionKey, ItemId);

            DistributedTransactionResponse response = await tx.CommitTransactionAsync(CancellationToken.None);
            Assert.IsTrue(response.IsSuccessStatusCode);

            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => tx.CommitTransactionAsync(CancellationToken.None));
            Assert.AreEqual(DistributedReadTransactionCore.CommitAlreadyCalledMessage, ex.Message);
        }

        [TestMethod]
        public async Task CommitAsync_CalledAfterFailedCommit_ThrowsInvalidOperationException()
        {
            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            contextMock
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<CosmosPK?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildReadErrorResponse(HttpStatusCode.ServiceUnavailable));

            DistributedReadTransaction tx = new DistributedReadTransactionCore(contextMock.Object)
                .ReadItem(BuildMockContainer(), TestPartitionKey, ItemId);

            // First commit returns a server error — instance is still consumed.
            DistributedTransactionResponse response = await tx.CommitTransactionAsync(CancellationToken.None);
            Assert.IsFalse(response.IsSuccessStatusCode);

            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => tx.CommitTransactionAsync(CancellationToken.None));
            Assert.AreEqual(DistributedReadTransactionCore.CommitAlreadyCalledMessage, ex.Message);
        }

        [TestMethod]
        [Description("Verifies that only one of N concurrent callers wins the Interlocked.CompareExchange gate. " +
                     "Uses Task.Run + ManualResetEventSlim to provide genuine cross-thread concurrency.")]
        public async Task CommitAsync_ConcurrentCalls_OnlyOneSucceeds()
        {
            int invocationCount = 0;

            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            contextMock
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<CosmosPK?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Returns<string, ResourceType, OperationType, RequestOptions, ContainerInternal, CosmosPK?, string, Stream, Action<RequestMessage>, ITrace, CancellationToken>(
                    async (uri, resType, opType, opts, container, pk, itemId, stream, enricher, trace, ct) =>
                    {
                        Interlocked.Increment(ref invocationCount);
                        await Task.Delay(50, ct);
                        return BuildReadSuccessResponse(1);
                    });

            DistributedReadTransaction tx = new DistributedReadTransactionCore(contextMock.Object)
                .ReadItem(BuildMockContainer(), TestPartitionKey, ItemId);

            const int RacerCount = 16;
            using ManualResetEventSlim gate = new ManualResetEventSlim(initialState: false);

            Task<DistributedTransactionResponse>[] tasks = new Task<DistributedTransactionResponse>[RacerCount];
            for (int i = 0; i < RacerCount; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    gate.Wait();
                    return await tx.CommitTransactionAsync(CancellationToken.None);
                });
            }

            gate.Set();

            int successCount = 0;
            int rejectedCount = 0;
            foreach (Task<DistributedTransactionResponse> t in tasks)
            {
                try
                {
                    await t;
                    successCount++;
                }
                catch (InvalidOperationException)
                {
                    rejectedCount++;
                }
            }

            Assert.AreEqual(1, successCount, "Exactly one racer should win the CompareExchange.");
            Assert.AreEqual(RacerCount - 1, rejectedCount, "All other racers should be rejected by the guard.");
            Assert.AreEqual(1, invocationCount, "The underlying commit pipeline must only fire once.");
        }

        #endregion

        #region OperationHelperAsync wiring

        [TestMethod]
        [Description("Verifies CommitTransactionAsync routes through OperationHelperAsync with the qualified operationName, the read-specific OTel constant, the Read operation type, and TraceComponent.Batch.")]
        public async Task CommitTransactionAsync_RoutesThroughOperationHelper_WithExpectedWiring()
        {
            string capturedOperationName = null;
            OperationType capturedOperationType = default;
            TraceComponent capturedTraceComponent = default;
            string capturedOTelOperationName = null;

            Mock<CosmosClientContext> contextMock = new Mock<CosmosClientContext>();
            contextMock
                .Setup(c => c.Client)
                .Returns(DistributedReadTransactionCoreTests.SharedMockClient);
            contextMock
                .Setup(c => c.OperationHelperAsync<DistributedTransactionResponse>(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<Func<ITrace, Task<DistributedTransactionResponse>>>(),
                    It.IsAny<(string OperationName, Func<DistributedTransactionResponse, OpenTelemetryAttributes> GetAttributes)?>(),
                    It.IsAny<ResourceType?>(),
                    It.IsAny<TraceComponent>(),
                    It.IsAny<TraceLevel>()))
                .Returns<string, string, string, OperationType, RequestOptions, Func<ITrace, Task<DistributedTransactionResponse>>, (string, Func<DistributedTransactionResponse, OpenTelemetryAttributes>)?, ResourceType?, TraceComponent, TraceLevel>(
                    (operationName, containerName, databaseName, operationType, requestOptions, func, oTelTuple, resourceType, comp, level) =>
                    {
                        capturedOperationName = operationName;
                        capturedOperationType = operationType;
                        capturedTraceComponent = comp;
                        capturedOTelOperationName = oTelTuple?.Item1;
                        return Task.FromResult<DistributedTransactionResponse>(null);
                    });

            DistributedReadTransactionCore txn = new DistributedReadTransactionCore(contextMock.Object);
            txn.ReadItem(BuildMockContainer(), TestPartitionKey, ItemId);

            await txn.CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual($"{nameof(DistributedReadTransaction)}.{nameof(DistributedReadTransaction.CommitTransactionAsync)}", capturedOperationName);
            Assert.AreEqual(OperationType.Read, capturedOperationType);
            Assert.AreEqual(TraceComponent.Batch, capturedTraceComponent);
            Assert.AreEqual(OpenTelemetryConstants.Operations.CommitDistributedReadTransaction, capturedOTelOperationName);
        }

        [TestMethod]
        [Description("End-to-end: verifies the wire request issued by the DistributedTransactionCommitter for a read transaction uses ResourceType.DistributedTransactionBatch and OperationType.Read (not CommitDistributedTransaction).")]
        public async Task CommitAsync_SendsCorrectOperationAndResourceType()
        {
            ResourceType capturedResourceType = default;
            OperationType capturedOperationType = default;

            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            contextMock
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<CosmosPK?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Returns<string, ResourceType, OperationType, RequestOptions, ContainerInternal, CosmosPK?, string, Stream, Action<RequestMessage>, ITrace, CancellationToken>(
                    (uri, resType, opType, opts, container, pk, itemId, stream, enricher, trace, ct) =>
                    {
                        capturedResourceType = resType;
                        capturedOperationType = opType;
                        return Task.FromResult(BuildReadSuccessResponse(1));
                    });

            await new DistributedReadTransactionCore(contextMock.Object)
                .ReadItem(BuildMockContainer(), TestPartitionKey, ItemId)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual(ResourceType.DistributedTransactionBatch, capturedResourceType);
            Assert.AreEqual(OperationType.Read, capturedOperationType);
        }

        #endregion

        /// <summary>
        /// Reflective helper to access the private operations list for assertion purposes.
        /// </summary>
        private static IReadOnlyList<DistributedTransactionOperation> GetOperations(DistributedReadTransactionCore txn)
        {
            System.Reflection.FieldInfo field = typeof(DistributedReadTransactionCore)
                .GetField("operations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (IReadOnlyList<DistributedTransactionOperation>)field.GetValue(txn);
        }

        private Mock<CosmosClientContext> BuildContextSetup()
        {
            ContainerProperties containerProps = ContainerProperties.CreateWithResourceId("ccZ1ANCszwk=");
            containerProps.PartitionKeyPath = "/pk";

            Mock<CosmosClientContext> contextMock = new Mock<CosmosClientContext>();

            contextMock
                .Setup(c => c.Client)
                .Returns(DistributedReadTransactionCoreTests.SharedMockClient);

            contextMock
                .Setup(c => c.DocumentClient)
                .Returns(new MockDocumentClient());

            contextMock
                .Setup(c => c.SerializerCore)
                .Returns(MockCosmosUtil.Serializer);

            contextMock
                .Setup(c => c.GetCachedContainerPropertiesAsync(
                    It.IsAny<string>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(containerProps);

            contextMock
                .Setup(c => c.OperationHelperAsync<DistributedTransactionResponse>(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<Func<ITrace, Task<DistributedTransactionResponse>>>(),
                    It.IsAny<(string OperationName, Func<DistributedTransactionResponse, OpenTelemetryAttributes> GetAttributes)?>(),
                    It.IsAny<ResourceType?>(),
                    It.IsAny<TraceComponent>(),
                    It.IsAny<TraceLevel>()))
                .Returns<string, string, string, OperationType, RequestOptions, Func<ITrace, Task<DistributedTransactionResponse>>, (string, Func<DistributedTransactionResponse, OpenTelemetryAttributes>)?, ResourceType?, TraceComponent, TraceLevel>(
                    (operationName, containerName, databaseName, operationType, requestOptions, func, oTelFunc, resourceType, comp, level) => func(NoOpTrace.Singleton));

            return contextMock;
        }

        private static ResponseMessage BuildReadSuccessResponse(int operationCount)
        {
            List<string> results = new List<string>();
            for (int i = 0; i < operationCount; i++)
            {
                results.Add($@"{{""index"":{i},""statusCode"":200}}");
            }

            string json = $@"{{""operationResponses"":[{string.Join(",", results)}]}}";
            return new ResponseMessage(HttpStatusCode.OK)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(json))
            };
        }

        private static ResponseMessage BuildReadErrorResponse(HttpStatusCode statusCode)
        {
            string json = $@"{{""operationResponses"":[{{""index"":0,""statusCode"":{(int)statusCode}}}]}}";
            return new ResponseMessage(statusCode)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(json))
            };
        }
    }
}
