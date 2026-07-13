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

        #region Zero-operations guard

        [TestMethod]
        public async Task CommitAsync_ZeroOperations_ThrowsInvalidOperationException()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();

            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => txn.CommitTransactionAsync(CancellationToken.None));

            Assert.IsTrue(ex.Message.Contains("zero operations"), $"Unexpected message: {ex.Message}");
        }

        [TestMethod]
        public async Task CommitAsync_ZeroOperations_DoesNotConsumeTransaction()
        {
            // The zero-operations guard runs before the single-use commit latch is set, so a caller
            // can follow the error message's advice (add an operation) and commit on the same instance.
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

            DistributedReadTransactionCore tx = new DistributedReadTransactionCore(contextMock.Object);

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => tx.CommitTransactionAsync(CancellationToken.None));

            // Instance is not consumed: adding an operation and committing now succeeds.
            tx.ReadItem(BuildMockContainer(), TestPartitionKey, ItemId);
            DistributedTransactionResponse response = await tx.CommitTransactionAsync(CancellationToken.None);
            Assert.IsTrue(response.IsSuccessStatusCode);
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

        [TestMethod]
        [Description("Smoke test: a commit with 100+ read operations whose server operationResponses arrive " +
                     "out-of-order is reordered by the SDK so response[i].Index == i for every operation.")]
        public async Task CommitAsync_ManyOperations_OutOfOrderResponses_SortedByIndex()
        {
            const int OperationCount = 128;

            // Server returns the per-operation responses in a deterministically-shuffled (out-of-order) sequence.
            int[] shuffledIndices = BuildShuffledIndices(OperationCount, seed: 7);
            Assert.IsTrue(
                IsOutOfOrder(shuffledIndices),
                "Wire order must be shuffled for this smoke test to be meaningful.");

            string responseJson = BuildOperationResponsesJson(shuffledIndices, statusCode: (int)HttpStatusCode.OK);

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
                .ReturnsAsync(new ResponseMessage(HttpStatusCode.OK)
                {
                    Content = new MemoryStream(Encoding.UTF8.GetBytes(responseJson))
                });

            DistributedReadTransactionCore tx = new DistributedReadTransactionCore(contextMock.Object);
            for (int i = 0; i < OperationCount; i++)
            {
                tx.ReadItem(BuildMockContainer(), new CosmosPK($"pk{i}"), $"id{i}");
            }

            DistributedTransactionResponse response = await tx.CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.AreEqual(OperationCount, response.Count);
            for (int i = 0; i < response.Count; i++)
            {
                Assert.AreEqual(i, response[i].Index,
                    $"Response[{i}] must have Index {i} after SDK reordering.");
                Assert.AreEqual(HttpStatusCode.OK, response[i].StatusCode);
            }
        }

        [TestMethod]
        [Description("Fail-closed: when the server response repeats an operation index (not a complete permutation " +
                     "of 0..n-1), the SDK cannot map results back to requests and returns HTTP 500 instead of " +
                     "surfacing misaligned data.")]
        public async Task CommitAsync_DuplicateOperationIndex_FailsClosed()
        {
            // Three operations, but the response repeats index 1 and omits index 2 — an invalid permutation.
            int[] indices = new[] { 0, 1, 1 };
            string responseJson = BuildOperationResponsesJson(indices, statusCode: (int)HttpStatusCode.OK);

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
                .ReturnsAsync(new ResponseMessage(HttpStatusCode.OK)
                {
                    Content = new MemoryStream(Encoding.UTF8.GetBytes(responseJson))
                });

            DistributedTransactionResponse response = await new DistributedReadTransactionCore(contextMock.Object)
                .ReadItem(BuildMockContainer(), new CosmosPK("pk0"), "id0")
                .ReadItem(BuildMockContainer(), new CosmosPK("pk1"), "id1")
                .ReadItem(BuildMockContainer(), new CosmosPK("pk2"), "id2")
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode,
                "A duplicate operation index must fail closed with HTTP 500.");
            Assert.IsFalse(response.IsSuccessStatusCode);

            // The unmappable payload is discarded and replaced with uniform fail-closed placeholders,
            // one per submitted operation.
            Assert.AreEqual(3, response.Count);
            for (int i = 0; i < response.Count; i++)
            {
                Assert.AreEqual(HttpStatusCode.InternalServerError, response[i].StatusCode);
            }
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

        /// <summary>
        /// Builds an <c>operationResponses</c> JSON envelope from an explicit, ordered list of
        /// per-operation indices (the wire order), each assigned the supplied HTTP status code.
        /// </summary>
        private static string BuildOperationResponsesJson(IReadOnlyList<int> indices, int statusCode)
        {
            List<string> results = new List<string>(indices.Count);
            foreach (int index in indices)
            {
                results.Add($@"{{""index"":{index},""statusCode"":{statusCode}}}");
            }

            return $@"{{""operationResponses"":[{string.Join(",", results)}]}}";
        }

        /// <summary>
        /// Returns the indices <c>0..count-1</c> in a deterministically-shuffled order so the
        /// produced wire order is reproducibly out-of-order across test runs.
        /// </summary>
        private static int[] BuildShuffledIndices(int count, int seed)
        {
            int[] indices = new int[count];
            for (int i = 0; i < count; i++)
            {
                indices[i] = i;
            }

            Random rng = new Random(seed);
            for (int i = count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int temp = indices[i];
                indices[i] = indices[j];
                indices[j] = temp;
            }

            return indices;
        }

        /// <summary>
        /// Returns <c>true</c> when at least one element is not already in its sorted position.
        /// </summary>
        private static bool IsOutOfOrder(IReadOnlyList<int> indices)
        {
            for (int i = 0; i < indices.Count; i++)
            {
                if (indices[i] != i)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
