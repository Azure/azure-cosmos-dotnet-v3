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
    using System.Text.Json;
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
        public async Task ReadItems_PreserveOperationMetadataOrderAndOptions()
        {
            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            string captured = null;
            SetupDispatch(contextMock, async (stream, ct) =>
            {
                using StreamReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
                captured = await reader.ReadToEndAsync();
                return BuildReadSuccessResponse(3);
            });
            DistributedReadTransaction tx = new DistributedReadTransactionCore(contextMock.Object);
            for (int i = 0; i < 3; i++)
            {
                DistributedTransactionRequestOptions options = i == 1 ? null : new DistributedTransactionRequestOptions
                {
                    IfMatchEtag = $"match-{i}",
                    IfNoneMatchEtag = $"nonmatch-{i}",
                    SessionToken = $"0:-1#{i + 1}",
                };
                Assert.AreSame(tx, tx.ReadItem(BuildMockContainer($"database-{i}", $"container-{i}"), new CosmosPK($"pk-{i}"), $"id-{i}", options));
            }

            using DistributedTransactionResponse response = await tx.ExecuteTransactionAsync();
            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.AreEqual(3, response.Count);
            using JsonDocument request = JsonDocument.Parse(captured);
            JsonElement operations = request.RootElement.GetProperty("operations");
            Assert.AreEqual(3, operations.GetArrayLength());
            for (int i = 0; i < 3; i++)
            {
                JsonElement operation = operations[i];
                Assert.AreEqual("Read", operation.GetProperty("operationType").GetString());
                Assert.AreEqual($"database-{i}", operation.GetProperty("databaseName").GetString());
                Assert.AreEqual($"container-{i}", operation.GetProperty("collectionName").GetString());
                Assert.AreEqual($"id-{i}", operation.GetProperty("id").GetString());
                Assert.AreEqual($"pk-{i}", operation.GetProperty("partitionKey")[0].GetString());
                Assert.AreEqual(i, operation.GetProperty("index").GetInt32());
                Assert.AreEqual(i, response[i].Index);
                Assert.IsFalse(operation.TryGetProperty("resourceBody", out _));
                if (i == 1)
                {
                    Assert.IsFalse(operation.TryGetProperty("ifMatch", out _));
                    Assert.IsFalse(operation.TryGetProperty("ifNoneMatch", out _));
                    Assert.IsFalse(operation.TryGetProperty("sessionToken", out _));
                }
                else
                {
                    Assert.AreEqual($"match-{i}", operation.GetProperty("ifMatch").GetString());
                    Assert.AreEqual($"nonmatch-{i}", operation.GetProperty("ifNoneMatch").GetString());
                    Assert.AreEqual($"0:-1#{i + 1}", operation.GetProperty("sessionToken").GetString());
                }

                string expectedPath = $"dbs/database-{i}/colls/container-{i}";
                contextMock.Verify(c => c.GetCachedContainerPropertiesAsync(
                    expectedPath, It.IsAny<ITrace>(), It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        #endregion

        #region Zero-operations guard

        [TestMethod]
        public async Task CommitAsync_ZeroOperations_ThrowsInvalidOperationException()
        {
            DistributedReadTransactionCore txn = this.CreateTransaction();

            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => txn.ExecuteTransactionAsync(CancellationToken.None));

            Assert.IsTrue(ex.Message.Contains("zero operations"), $"Unexpected message: {ex.Message}");
        }

        [TestMethod]
        public async Task ExecuteTransactionAsync_ZeroOperations_AllowsExecutionAfterAddingOperations()
        {
            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            int dispatches = 0;
            SetupDispatch(contextMock, (stream, ct) =>
            {
                dispatches++;
                return Task.FromResult(BuildReadSuccessResponse(1));
            });
            DistributedReadTransactionCore tx = new DistributedReadTransactionCore(contextMock.Object);
            InvalidOperationException empty = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => tx.ExecuteTransactionAsync(CancellationToken.None));
            StringAssert.Contains(empty.Message, "zero operations");
            InvalidOperationException repeated = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => tx.ExecuteTransactionAsync(CancellationToken.None));
            Assert.AreEqual(empty.Message, repeated.Message);
            Assert.AreEqual(Guid.Empty, tx.IdempotencyToken);
            Assert.AreEqual(0, dispatches);

            tx.ReadItem(BuildMockContainer(), TestPartitionKey, ItemId);
            using DistributedTransactionResponse response = await tx.ExecuteTransactionAsync();
            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.AreEqual(1, response.Count);
            await AssertConsumedAsync(tx);
            Assert.AreEqual(1, dispatches);
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

            DistributedTransactionResponse response = await tx.ExecuteTransactionAsync(CancellationToken.None);
            Assert.IsTrue(response.IsSuccessStatusCode);

            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => tx.ExecuteTransactionAsync(CancellationToken.None));
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
            DistributedTransactionResponse response = await tx.ExecuteTransactionAsync(CancellationToken.None);
            Assert.IsFalse(response.IsSuccessStatusCode);

            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => tx.ExecuteTransactionAsync(CancellationToken.None));
            Assert.AreEqual(DistributedReadTransactionCore.CommitAlreadyCalledMessage, ex.Message);
        }

        [TestMethod]
        [Description("Verifies that racing execution attempts cannot dispatch more than once.")]
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
                    (uri, resType, opType, opts, container, pk, itemId, stream, enricher, trace, ct) =>
                    {
                        Interlocked.Increment(ref invocationCount);
                        return Task.FromResult(BuildReadSuccessResponse(1));
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
                    Assert.IsTrue(gate.Wait(TimeSpan.FromSeconds(30)));
                    return await tx.ExecuteTransactionAsync(CancellationToken.None);
                });
            }

            gate.Set();

            int successCount = 0;
            int rejectedCount = 0;
            foreach (Task<DistributedTransactionResponse> t in tasks)
            {
                try
                {
                    using DistributedTransactionResponse response = await t.WaitAsync(TimeSpan.FromSeconds(30));
                    successCount++;
                }
                catch (InvalidOperationException error)
                {
                    Assert.AreEqual(DistributedReadTransactionCore.CommitAlreadyCalledMessage, error.Message);
                    rejectedCount++;
                }
            }

            Assert.AreEqual(1, successCount, "Exactly one caller should execute the transaction.");
            Assert.AreEqual(RacerCount - 1, rejectedCount, "All other racers should be rejected by the guard.");
            Assert.AreEqual(1, invocationCount, "The underlying commit pipeline must only fire once.");
        }

        #endregion

        #region OperationHelperAsync wiring

        [TestMethod]
        [Description("Verifies ExecuteTransactionAsync routes through OperationHelperAsync with the qualified operationName, the read-specific OTel constant, the Read operation type, and TraceComponent.Batch.")]
        public async Task ExecuteTransactionAsync_RoutesThroughOperationHelper_WithExpectedWiring()
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

            await txn.ExecuteTransactionAsync(CancellationToken.None);

            Assert.AreEqual($"{nameof(DistributedReadTransaction)}.{nameof(DistributedReadTransaction.ExecuteTransactionAsync)}", capturedOperationName);
            Assert.AreEqual(OperationType.Read, capturedOperationType);
            Assert.AreEqual(TraceComponent.Batch, capturedTraceComponent);
            Assert.AreEqual(OpenTelemetryConstants.Operations.ExecuteDistributedReadTransaction, capturedOTelOperationName);
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
                .ExecuteTransactionAsync(CancellationToken.None);

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

            DistributedTransactionResponse response = await tx.ExecuteTransactionAsync(CancellationToken.None);

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
                .ExecuteTransactionAsync(CancellationToken.None);

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

        [TestMethod]
        [Description("IdempotencyToken is Guid.Empty before ExecuteTransactionAsync is called on a read transaction — no attempt has reached dispatch yet.")]
        public void IdempotencyToken_BeforeCommit_IsEmpty()
        {
            DistributedReadTransaction tx = new DistributedReadTransactionCore(this.BuildContextSetup().Object)
                .ReadItem(BuildMockContainer(), TestPartitionKey, ItemId);

            Assert.AreEqual(Guid.Empty, tx.IdempotencyToken,
                "IdempotencyToken must be Guid.Empty before the first dispatch.");
        }

        [TestMethod]
        [Description("After a successful read commit, DistributedReadTransaction.IdempotencyToken equals the idempotency token stamped on the dispatched request (spec §4.4).")]
        public async Task IdempotencyToken_AfterCommit_ExposesDispatchedToken()
        {
            string capturedRequestToken = null;
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
                        RequestMessage request = new RequestMessage
                        {
                            ResourceType = ResourceType.DistributedTransactionBatch,
                            OperationType = OperationType.Read,
                        };
                        enricher(request);
                        capturedRequestToken = request.Headers[HttpConstants.HttpHeaders.IdempotencyToken];
                        return Task.FromResult(BuildReadSuccessResponse(1));
                    });

            DistributedReadTransactionCore tx = new DistributedReadTransactionCore(contextMock.Object);
            tx.ReadItem(BuildMockContainer(), TestPartitionKey, ItemId);

            DistributedTransactionResponse response = await tx.ExecuteTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.AreNotEqual(Guid.Empty, tx.IdempotencyToken);
            Assert.AreEqual(capturedRequestToken, tx.IdempotencyToken.ToString(),
                "IdempotencyToken must equal the token stamped on the dispatched request.");
        }

        #endregion

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ExecuteTransactionAsync_LateAdditionsAcrossRetries_PreserveRequestAndTokenPolicy(bool aborted)
        {
            TaskCompletionSource<bool> firstDispatched = NewGate();
            TaskCompletionSource<bool> release = NewGate();
            List<byte[]> payloads = new List<byte[]>();
            List<string> tokens = new List<string>();
            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            SetupDispatch(contextMock, async (stream, ct) =>
            {
                using MemoryStream payload = new MemoryStream();
                await stream.CopyToAsync(payload);
                payloads.Add(payload.ToArray());
                if (payloads.Count == 1)
                {
                    firstDispatched.TrySetResult(true);
                    await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    return new ResponseMessage(aborted ? (HttpStatusCode)StatusCodes.TransactionAborted : HttpStatusCode.ServiceUnavailable)
                    {
                        Content = new MemoryStream(Encoding.UTF8.GetBytes("{\"isRetriable\":true}"))
                    };
                }

                return BuildReadSuccessResponse(2);
            }, onRequest: request => tokens.Add(request.Headers[HttpConstants.HttpHeaders.IdempotencyToken]));
            DistributedReadTransaction tx = new DistributedReadTransactionCore(contextMock.Object)
                .ReadItem(BuildMockContainer(), TestPartitionKey, "first")
                .ReadItem(BuildMockContainer(), TestPartitionKey, "second");
            Task<DistributedTransactionResponse> execution = tx.ExecuteTransactionAsync();
            try
            {
                await firstDispatched.Task.WaitAsync(TimeSpan.FromSeconds(30));
                AssertAdditionAllowed(tx);
                release.TrySetResult(true);
                DistributedTransactionResponse response = await execution.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.AreEqual(2, response.Count);
                Assert.AreEqual(2, response.Operations.Count);
                Assert.AreEqual("first", response.Operations[0].Id);
                Assert.AreEqual("second", response.Operations[1].Id);
                Assert.AreEqual(0, response[0].Index);
                Assert.AreEqual(1, response[1].Index);
                await AssertConsumedAsync(tx);

                Assert.AreEqual(2, payloads.Count);
                CollectionAssert.AreEqual(payloads[0], payloads[1]);
                using JsonDocument body = JsonDocument.Parse(payloads[0]);
                JsonElement operations = body.RootElement.GetProperty("operations");
                Assert.AreEqual(2, operations.GetArrayLength());
                Assert.AreEqual("first", operations[0].GetProperty("id").GetString());
                Assert.AreEqual("second", operations[1].GetProperty("id").GetString());
                Assert.AreEqual(2, tokens.Count);
                Assert.AreNotEqual(Guid.Empty, Guid.Parse(tokens[0]));
                Assert.AreNotEqual(Guid.Empty, Guid.Parse(tokens[1]));
                Assert.AreEqual(aborted, tokens[0] != tokens[1], "Only a durable abort should rotate the token.");
                Assert.AreEqual(tokens[1], tx.IdempotencyToken.ToString());
                Assert.AreEqual(tx.IdempotencyToken, response.IdempotencyToken);
            }
            finally
            {
                release.TrySetResult(true);
                using DistributedTransactionResponse cleanup = await execution.WaitAsync(TimeSpan.FromSeconds(30));
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ExecuteTransactionAsync_LateAdditionsBeforeErrorResponse_PreserveSubmittedOperationCount(bool incompleteBody)
        {
            TaskCompletionSource<bool> dispatched = NewGate();
            TaskCompletionSource<bool> release = NewGate();
            int dispatches = 0;
            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            SetupDispatch(contextMock, async (stream, ct) =>
            {
                dispatches++;
                dispatched.TrySetResult(true);
                await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
                return incompleteBody
                    ? BuildReadErrorResponse(HttpStatusCode.BadRequest)
                    : new ResponseMessage(HttpStatusCode.BadRequest);
            });
            DistributedReadTransaction tx = new DistributedReadTransactionCore(contextMock.Object)
                .ReadItem(BuildMockContainer(), TestPartitionKey, "first")
                .ReadItem(BuildMockContainer(), TestPartitionKey, "second");
            Task<DistributedTransactionResponse> execution = tx.ExecuteTransactionAsync();
            try
            {
                await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(30));
                AssertAdditionAllowed(tx);
                release.TrySetResult(true);
                DistributedTransactionResponse response = await execution.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.IsFalse(response.IsSuccessStatusCode);
                Assert.AreEqual(2, response.Count);
                Assert.AreEqual(2, response.Operations.Count);
                Assert.AreEqual("first", response.Operations[0].Id);
                Assert.AreEqual("second", response.Operations[1].Id);
                Assert.AreEqual(HttpStatusCode.BadRequest, response[0].StatusCode);
                Assert.AreEqual(HttpStatusCode.BadRequest, response[1].StatusCode);
                await AssertConsumedAsync(tx);
                Assert.AreEqual(1, dispatches);
            }
            finally
            {
                release.TrySetResult(true);
                using DistributedTransactionResponse cleanup = await execution.WaitAsync(TimeSpan.FromSeconds(30));
            }
        }

        [DataTestMethod]
        [DataRow(1)]
        [DataRow(3)]
        public async Task ExecuteTransactionAsync_PostSerializationMutation_PreservesResponse(int operationCount)
        {
            TaskCompletionSource<string> captured = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            List<string> results = new List<string>();
            for (int i = operationCount - 1; i >= 0; i--)
            {
                results.Add($@"{{""index"":{i},""statusCode"":200,""Etag"":""etag-{i}""}}");
            }

            string responseJson = $@"{{""operationResponses"":[{string.Join(",", results)}]}}";
            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            SetupDispatch(contextMock, async (stream, ct) =>
            {
                using StreamReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
                captured.SetResult(await reader.ReadToEndAsync());
                await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
                return new ResponseMessage(HttpStatusCode.OK)
                {
                    Content = new MemoryStream(Encoding.UTF8.GetBytes(responseJson))
                };
            });
            DistributedReadTransaction tx = new DistributedReadTransactionCore(contextMock.Object);
            for (int i = 0; i < operationCount; i++)
            {
                tx.ReadItem(BuildMockContainer($"db-{i}", $"container-{i}"), new CosmosPK($"pk-{i}"), $"id-{i}");
            }

            Task<DistributedTransactionResponse> execution = tx.ExecuteTransactionAsync();
            try
            {
                using JsonDocument body = JsonDocument.Parse(await captured.Task.WaitAsync(TimeSpan.FromSeconds(30)));
                JsonElement operations = body.RootElement.GetProperty("operations");
                Assert.AreEqual(operationCount, operations.GetArrayLength());
                for (int i = 0; i < operationCount; i++)
                {
                    Assert.AreEqual(i, operations[i].GetProperty("index").GetInt32());
                    Assert.AreEqual($"id-{i}", operations[i].GetProperty("id").GetString());
                    Assert.AreEqual($"db-{i}", operations[i].GetProperty("databaseName").GetString());
                    Assert.AreEqual($"container-{i}", operations[i].GetProperty("collectionName").GetString());
                }

                AssertAdditionAllowed(tx);

                release.TrySetResult(true);
                DistributedTransactionResponse response = await execution.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.IsTrue(response.IsSuccessStatusCode, $"Success became {response.StatusCode} with {response.Count} results.");
                Guid token = tx.IdempotencyToken;
                Assert.AreNotEqual(Guid.Empty, token);
                AssertAdditionAllowed(tx);
                await AssertConsumedAsync(tx);
                Assert.AreEqual(operationCount, response.Count);
                Assert.AreEqual(operationCount, response.Operations.Count);
                for (int i = 0; i < operationCount; i++)
                {
                    Assert.AreEqual(i, response[i].Index);
                    Assert.AreEqual(HttpStatusCode.OK, response[i].StatusCode);
                    Assert.AreEqual($"etag-{i}", response[i].ETag);
                    Assert.AreEqual($"id-{i}", response.Operations[i].Id);
                    Assert.AreEqual($"db-{i}", response.Operations[i].Database);
                    Assert.AreEqual($"container-{i}", response.Operations[i].Container);
                }

                Assert.AreEqual(token, tx.IdempotencyToken);
            }
            finally
            {
                release.TrySetResult(true);
                using DistributedTransactionResponse cleanup = await execution.WaitAsync(TimeSpan.FromSeconds(30));
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ExecuteTransactionAsync_Failure_AllowsAdditionsButRemainsConsumed(bool throwException)
        {
            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            int dispatches = 0;
            SetupDispatch(contextMock, (stream, ct) =>
            {
                dispatches++;
                if (throwException)
                {
                    throw new IOException("dispatch failed");
                }

                return Task.FromResult(BuildReadErrorResponse(HttpStatusCode.BadRequest));
            });
            DistributedReadTransaction tx = new DistributedReadTransactionCore(contextMock.Object)
                .ReadItem(BuildMockContainer(), TestPartitionKey, ItemId);
            if (throwException)
            {
                await Assert.ThrowsExceptionAsync<IOException>(() => tx.ExecuteTransactionAsync());
            }
            else
            {
                using DistributedTransactionResponse response = await tx.ExecuteTransactionAsync();
                Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            }

            AssertAdditionAllowed(tx);
            await AssertConsumedAsync(tx);
            Assert.AreEqual(1, dispatches);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ExecuteTransactionAsync_Cancellation_AllowsAdditionsButRemainsConsumed(bool preCancelled)
        {
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            TaskCompletionSource<bool> entered = NewGate();
            TaskCompletionSource<bool> release = NewGate();
            int dispatches = 0;
            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            SetupDispatch(contextMock, async (stream, ct) =>
            {
                dispatches++;
                entered.TrySetResult(true);
                await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
                ct.ThrowIfCancellationRequested();
                return BuildReadSuccessResponse(1);
            });
            DistributedReadTransaction tx = new DistributedReadTransactionCore(contextMock.Object)
                .ReadItem(BuildMockContainer(), TestPartitionKey, ItemId);
            if (preCancelled)
            {
                cancellation.Cancel();
            }

            Task<DistributedTransactionResponse> execution = tx.ExecuteTransactionAsync(cancellation.Token);
            try
            {
                if (!preCancelled)
                {
                    await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    AssertAdditionAllowed(tx);
                    cancellation.Cancel();
                }

                release.TrySetResult(true);
                OperationCanceledException error = await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                    async () => await execution.WaitAsync(TimeSpan.FromSeconds(30)));
                Assert.AreEqual(cancellation.Token, error.CancellationToken);
                AssertAdditionAllowed(tx);
                await AssertConsumedAsync(tx);
                Assert.AreEqual(preCancelled ? 0 : 1, dispatches);
            }
            finally
            {
                cancellation.Cancel();
                release.TrySetResult(true);
                try
                {
                    using DistributedTransactionResponse cleanup = await execution.WaitAsync(TimeSpan.FromSeconds(30));
                }
                catch (OperationCanceledException error)
                {
                    Assert.AreEqual(cancellation.Token, error.CancellationToken);
                }
            }
        }

        [TestMethod]
        public async Task ExecuteTransactionAsync_BeforeHelperCallback_IsolatesAdditionsAndRejectsReexecution()
        {
            TaskCompletionSource<bool> entered = NewGate();
            TaskCompletionSource<bool> release = NewGate();
            int helperCalls = 0;
            Mock<CosmosClientContext> contextMock = this.BuildContextSetup(async callback =>
            {
                Assert.AreEqual(1, Interlocked.Increment(ref helperCalls), "Execution re-entered the operation helper.");
                entered.TrySetResult(true);
                await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
                return await callback(NoOpTrace.Singleton);
            });
            int dispatches = 0;
            SetupDispatch(contextMock, (stream, ct) =>
            {
                dispatches++;
                return Task.FromResult(BuildReadSuccessResponse(1));
            });
            DistributedReadTransaction tx = new DistributedReadTransactionCore(contextMock.Object)
                .ReadItem(BuildMockContainer(), TestPartitionKey, ItemId);
            Task<DistributedTransactionResponse> execution = tx.ExecuteTransactionAsync();
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.IsFalse(execution.IsCompleted);
                AssertAdditionAllowed(tx);
                await AssertConsumedAsync(tx);
                Assert.AreEqual(0, dispatches);
                Assert.AreEqual(Guid.Empty, tx.IdempotencyToken);
                contextMock.Verify(c => c.GetCachedContainerPropertiesAsync(
                    It.IsAny<string>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>()), Times.Never);
            }
            finally
            {
                release.TrySetResult(true);
                using DistributedTransactionResponse response = await execution.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.AreEqual(1, response.Count);
            }

            Assert.AreEqual(1, dispatches);
            Assert.AreEqual(1, helperCalls);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ExecuteTransactionAsync_HelperFailureBeforeCallback_RemainsConsumed(bool asynchronousFailure)
        {
            IOException expected = new IOException("operation helper failed");
            int helperCalls = 0;
            Mock<CosmosClientContext> contextMock = this.BuildContextSetup(callback =>
            {
                helperCalls++;
                if (asynchronousFailure)
                {
                    return Task.FromException<DistributedTransactionResponse>(expected);
                }

                throw expected;
            });
            int dispatches = 0;
            SetupDispatch(contextMock, (stream, ct) =>
            {
                dispatches++;
                return Task.FromResult(BuildReadSuccessResponse(1));
            });
            DistributedReadTransaction tx = new DistributedReadTransactionCore(contextMock.Object)
                .ReadItem(BuildMockContainer(), TestPartitionKey, ItemId);
            IOException error = await Assert.ThrowsExceptionAsync<IOException>(async () =>
            {
                using DistributedTransactionResponse response = await tx.ExecuteTransactionAsync().WaitAsync(TimeSpan.FromSeconds(30));
            });

            Assert.AreSame(expected, error);
            AssertAdditionAllowed(tx);
            await AssertConsumedAsync(tx);
            Assert.AreEqual(1, helperCalls);
            Assert.AreEqual(0, dispatches);
            Assert.AreEqual(Guid.Empty, tx.IdempotencyToken);
            contextMock.Verify(c => c.GetCachedContainerPropertiesAsync(
                It.IsAny<string>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task ExecuteTransactionAsync_BeforeContainerResolution_IsolatesAdditions()
        {
            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            TaskCompletionSource<bool> entered = NewGate();
            TaskCompletionSource<bool> release = NewGate();
            ContainerProperties properties = ContainerProperties.CreateWithResourceId("ccZ1ANCszwk=");
            properties.PartitionKeyPath = "/pk";
            contextMock.Setup(c => c.GetCachedContainerPropertiesAsync(
                It.IsAny<string>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    entered.TrySetResult(true);
                    await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    return properties;
                });
            int dispatches = 0;
            SetupDispatch(contextMock, (stream, ct) =>
            {
                dispatches++;
                return Task.FromResult(BuildReadSuccessResponse(1));
            });
            DistributedReadTransaction tx = new DistributedReadTransactionCore(contextMock.Object)
                .ReadItem(BuildMockContainer(), TestPartitionKey, ItemId);
            Task<DistributedTransactionResponse> execution = tx.ExecuteTransactionAsync();
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.AreEqual(0, dispatches);
                AssertAdditionAllowed(tx);
                Assert.AreEqual(Guid.Empty, tx.IdempotencyToken);
            }
            finally
            {
                release.TrySetResult(true);
                using DistributedTransactionResponse response = await execution.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.AreEqual(1, response.Count);
            }
        }

        [TestMethod]
        public async Task ExecuteTransactionAsync_ConcurrentEmptyCalls_DoNotConsumeTransaction()
        {
            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            int dispatches = 0;
            SetupDispatch(contextMock, (stream, ct) =>
            {
                Interlocked.Increment(ref dispatches);
                return Task.FromResult(BuildReadSuccessResponse(1));
            });
            DistributedReadTransaction tx = new DistributedReadTransactionCore(contextMock.Object);
            TaskCompletionSource<bool> start = NewGate();
            Task<InvalidOperationException>[] executions = new Task<InvalidOperationException>[8];
            for (int i = 0; i < executions.Length; i++)
            {
                executions[i] = Task.Run(async () =>
                {
                    await start.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    return await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => tx.ExecuteTransactionAsync());
                });
            }

            start.SetResult(true);
            InvalidOperationException[] errors = await Task.WhenAll(executions).WaitAsync(TimeSpan.FromSeconds(30));
            foreach (InvalidOperationException error in errors)
            {
                StringAssert.Contains(error.Message, "zero operations");
            }

            Assert.AreEqual(0, dispatches);
            Assert.AreEqual(Guid.Empty, tx.IdempotencyToken);
            tx.ReadItem(BuildMockContainer(), TestPartitionKey, ItemId);
            using DistributedTransactionResponse response = await tx.ExecuteTransactionAsync();
            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.AreEqual(1, response.Count);
            await AssertConsumedAsync(tx);
            Assert.AreEqual(1, dispatches);
        }

        [TestMethod]
        public async Task ExecuteTransactionAsync_ReusedOptions_ReadsValuesAtSerialization()
        {
            TaskCompletionSource<bool> release = NewGate();
            Mock<CosmosClientContext> contextMock = this.BuildContextSetup(async callback =>
            {
                await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
                return await callback(NoOpTrace.Singleton);
            });
            string captured = null;
            SetupDispatch(contextMock, async (stream, ct) =>
            {
                using StreamReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
                captured = await reader.ReadToEndAsync();
                return BuildReadSuccessResponse(2);
            });
            DistributedTransactionRequestOptions options = new DistributedTransactionRequestOptions();
            DistributedReadTransaction tx = new DistributedReadTransactionCore(contextMock.Object);
            for (int i = 0; i < 2; i++)
            {
                options.IfMatchEtag = $"match-{i}";
                options.IfNoneMatchEtag = $"nonmatch-{i}";
                options.SessionToken = $"0:-1#{i + 1}";
                tx.ReadItem(BuildMockContainer(), TestPartitionKey, $"item-{i}", options);
            }

            options.IfMatchEtag = "before-execute";
            options.IfNoneMatchEtag = "before-execute";
            options.SessionToken = "0:-1#100";
            Task<DistributedTransactionResponse> execution = tx.ExecuteTransactionAsync();
            try
            {
                Assert.IsFalse(execution.IsCompleted);
                options.IfMatchEtag = "after-execute";
                options.IfNoneMatchEtag = "after-execute";
                options.SessionToken = "0:-1#200";
                AssertAdditionAllowed(tx);
            }
            finally
            {
                release.TrySetResult(true);
                using DistributedTransactionResponse response = await execution.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.AreEqual(2, response.Count);
            }

            using JsonDocument body = JsonDocument.Parse(captured);
            JsonElement operations = body.RootElement.GetProperty("operations");
            Assert.AreEqual(2, operations.GetArrayLength());
            for (int i = 0; i < 2; i++)
            {
                Assert.AreEqual(i, operations[i].GetProperty("index").GetInt32());
                Assert.AreEqual($"item-{i}", operations[i].GetProperty("id").GetString());
                Assert.AreEqual("after-execute", operations[i].GetProperty("ifMatch").GetString());
                Assert.AreEqual("after-execute", operations[i].GetProperty("ifNoneMatch").GetString());
                Assert.AreEqual("0:-1#200", operations[i].GetProperty("sessionToken").GetString());
            }

            Assert.AreEqual("after-execute", options.IfMatchEtag);
            Assert.AreEqual("after-execute", options.IfNoneMatchEtag);
            Assert.AreEqual("0:-1#200", options.SessionToken);
            await AssertConsumedAsync(tx);
        }

        private static TaskCompletionSource<bool> NewGate() => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private static async Task AssertConsumedAsync(DistributedReadTransaction tx)
        {
            InvalidOperationException error = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => tx.ExecuteTransactionAsync());
            Assert.AreEqual(DistributedReadTransactionCore.CommitAlreadyCalledMessage, error.Message);
        }

        private static void AssertAdditionAllowed(DistributedReadTransaction tx)
        {
            Assert.AreSame(tx, tx.ReadItem(BuildMockContainer(), TestPartitionKey, "late"));
            Assert.ThrowsException<ArgumentNullException>(() => tx.ReadItem(null, TestPartitionKey, "late"));
            Assert.ThrowsException<ArgumentNullException>(() => tx.ReadItem(BuildMockContainer(), TestPartitionKey, null));
        }

        private static void SetupDispatch(
            Mock<CosmosClientContext> contextMock,
            Func<Stream, CancellationToken, Task<ResponseMessage>> dispatch,
            Action<RequestMessage> onRequest = null)
        {
            contextMock.Setup(c => c.ProcessResourceOperationStreamAsync(
                It.IsAny<string>(), It.IsAny<ResourceType>(), It.IsAny<OperationType>(),
                It.IsAny<RequestOptions>(), It.IsAny<ContainerInternal>(), It.IsAny<CosmosPK?>(),
                It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Action<RequestMessage>>(),
                It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .Returns<string, ResourceType, OperationType, RequestOptions, ContainerInternal, CosmosPK?, string, Stream, Action<RequestMessage>, ITrace, CancellationToken>(
                    (uri, resource, operation, options, container, pk, id, stream, enrich, trace, ct) =>
                    {
                        if (onRequest != null)
                        {
                            using RequestMessage request = new RequestMessage
                            {
                                ResourceType = resource,
                                OperationType = operation
                            };
                            enrich(request);
                            onRequest(request);
                        }

                        return dispatch(stream, ct);
                    });
        }

        private Mock<CosmosClientContext> BuildContextSetup(
            Func<Func<ITrace, Task<DistributedTransactionResponse>>, Task<DistributedTransactionResponse>> operationHelper = null)
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
                    (operationName, containerName, databaseName, operationType, requestOptions, func, oTelFunc, resourceType, comp, level) =>
                        operationHelper != null ? operationHelper(func) : func(NoOpTrace.Singleton));

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
