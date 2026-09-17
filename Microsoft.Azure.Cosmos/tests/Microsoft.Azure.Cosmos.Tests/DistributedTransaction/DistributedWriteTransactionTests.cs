// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using PartitionKey = Cosmos.PartitionKey;

    /// <summary>
    /// Unit tests for <see cref="DistributedWriteTransaction"/> covering argument validation,
    /// request structure, and response parsing. Uses a mocked <see cref="CosmosClientContext"/>
    /// so no emulator is required.
    /// </summary>
    [TestClass]
    public class DistributedWriteTransactionTests
    {
        private const string DatabaseName = "testDb";
        private const string ContainerName = "testContainer";

        // Argument validation

        [TestMethod]
        public void CreateItem_NullContainer_ThrowsArgumentException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentNullException>(
                () => tx.CreateItem(null, new PartitionKey("pk"), "item-id", new TestItem()));
        }

        [TestMethod]
        public void CreateItem_NullContainerId_ThrowsArgumentException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentException>(
                () => tx.CreateItem(BuildMockContainer(containerId: null), new PartitionKey("pk"), "item-id", new TestItem()));
        }

        [TestMethod]
        public void CreateItem_EmptyContainerId_ThrowsArgumentException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentException>(
                () => tx.CreateItem(BuildMockContainer(containerId: string.Empty), new PartitionKey("pk"), "item-id", new TestItem()));
        }

        [TestMethod]
        public void CreateItem_WhitespaceContainerId_ThrowsArgumentException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentException>(
                () => tx.CreateItem(BuildMockContainer(containerId: "   "), new PartitionKey("pk"), "item-id", new TestItem()));
        }

        [TestMethod]
        public void CreateItem_NullDatabase_ThrowsArgumentException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Mock<Cosmos.Container> containerMock = new Mock<Cosmos.Container>();
            containerMock.Setup(c => c.Id).Returns(ContainerName);
            containerMock.Setup(c => c.Database).Returns((Cosmos.Database)null);

            Assert.ThrowsException<ArgumentException>(
                () => tx.CreateItem(containerMock.Object, new PartitionKey("pk"), "item-id", new TestItem()));
        }

        [TestMethod]
        public void CreateItem_NullDatabaseId_ThrowsArgumentException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentException>(
                () => tx.CreateItem(BuildMockContainer(databaseId: null), new PartitionKey("pk"), "item-id", new TestItem()));
        }

        [TestMethod]
        public void CreateItem_EmptyDatabaseId_ThrowsArgumentException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentException>(
                () => tx.CreateItem(BuildMockContainer(databaseId: string.Empty), new PartitionKey("pk"), "item-id", new TestItem()));
        }

        [TestMethod]
        public void CreateItem_WhitespaceDatabaseId_ThrowsArgumentException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentException>(
                () => tx.CreateItem(BuildMockContainer(databaseId: "   "), new PartitionKey("pk"), "item-id", new TestItem()));
        }

        [TestMethod]
        public void CreateItem_DifferentCosmosClient_ThrowsArgumentException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            CosmosClient differentClient = new Mock<CosmosClient>().Object;
            Assert.ThrowsException<ArgumentException>(
                () => tx.CreateItem(BuildMockContainer(client: differentClient), new PartitionKey("pk"), "item-id", new TestItem()));
        }

        [TestMethod]
        public void CreateItem_SameCosmosClient_Succeeds()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            tx.CreateItem(BuildMockContainer(), new PartitionKey("pk"), "item-id", new TestItem());
        }

        [TestMethod]
        public void CreateItem_NullResource_ThrowsArgumentException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentNullException>(
                () => tx.CreateItem<TestItem>(BuildMockContainer(), new PartitionKey("pk"), "item-id", null));
        }

        [TestMethod]
        public void ReplaceItem_NullId_ThrowsArgumentException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentNullException>(
                () => tx.ReplaceItem(BuildMockContainer(), new PartitionKey("pk"), null, new TestItem()));
        }

        [TestMethod]
        public void DeleteItem_EmptyId_ThrowsArgumentException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentNullException>(
                () => tx.DeleteItem(BuildMockContainer(), new PartitionKey("pk"), string.Empty));
        }

        [TestMethod]
        public void PatchItem_NullPatchOperations_ThrowsArgumentNullException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentNullException>(
                () => tx.PatchItem(BuildMockContainer(), new PartitionKey("pk"), "item-id", null));
        }

        [TestMethod]
        public void PatchItem_EmptyPatchOperations_ThrowsArgumentNullException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentNullException>(
                () => tx.PatchItem(BuildMockContainer(), new PartitionKey("pk"), "item-id", new List<PatchOperation>()));
        }

        [TestMethod]
        public void CreateItemStream_NullStream_ThrowsArgumentNullException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentNullException>(
                () => tx.CreateItemStream(BuildMockContainer(), new PartitionKey("pk"), "item-id", null));
        }

        [TestMethod]
        public void ReplaceItemStream_NullStream_ThrowsArgumentNullException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentNullException>(
                () => tx.ReplaceItemStream(BuildMockContainer(), new PartitionKey("pk"), "item-id", null));
        }

        [TestMethod]
        public void PatchItemStream_NullStream_ThrowsArgumentNullException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentNullException>(
                () => tx.PatchItemStream(BuildMockContainer(), new PartitionKey("pk"), "item-id", null));
        }

        [TestMethod]
        public void UpsertItemStream_NullStream_ThrowsArgumentNullException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentNullException>(
                () => tx.UpsertItemStream(BuildMockContainer(), new PartitionKey("pk"), "item-id", null));
        }

        // Request structure

        [TestMethod]
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
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Returns<string, ResourceType, OperationType, RequestOptions, ContainerInternal, PartitionKey?, string, Stream, Action<RequestMessage>, ITrace, CancellationToken>(
                    (uri, resType, opType, opts, container, pk, itemId, stream, enricher, trace, ct) =>
                    {
                        capturedResourceType = resType;
                        capturedOperationType = opType;
                        return Task.FromResult(BuildSuccessResponse(1));
                    });

            await new DistributedWriteTransactionCore(contextMock.Object)
                .CreateItem(BuildMockContainer(), new PartitionKey("pk"), "item-id", new TestItem())
                .ExecuteTransactionAsync(CancellationToken.None);

            Assert.AreEqual(ResourceType.DistributedTransactionBatch, capturedResourceType);
            Assert.AreEqual(OperationType.CommitDistributedTransaction, capturedOperationType);
        }

        [TestMethod]
        public async Task CommitAsync_SetsIdempotencyTokenHeader()
        {
            string capturedToken = null;

            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            contextMock
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Returns<string, ResourceType, OperationType, RequestOptions, ContainerInternal, PartitionKey?, string, Stream, Action<RequestMessage>, ITrace, CancellationToken>(
                    (uri, resType, opType, opts, container, pk, itemId, stream, enricher, trace, ct) =>
                    {
                        RequestMessage req = new RequestMessage();
                        enricher?.Invoke(req);
                        capturedToken = req.Headers[HttpConstants.HttpHeaders.IdempotencyToken];
                        return Task.FromResult(BuildSuccessResponse(1));
                    });

            await new DistributedWriteTransactionCore(contextMock.Object)
                .CreateItem(BuildMockContainer(), new PartitionKey("pk"), "item-id", new TestItem())
                .ExecuteTransactionAsync(CancellationToken.None);

            Assert.IsNotNull(capturedToken, "Idempotency token header must be set.");
            Assert.IsTrue(Guid.TryParse(capturedToken, out _), "Idempotency token must be a valid GUID.");
        }

        [TestMethod]
        [Description("The idempotency token echoed back in the server response header is surfaced on the DistributedTransactionResponse.")]
        public async Task CommitAsync_ResponseContainsIdempotencyToken()
        {
            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            contextMock
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Returns<string, ResourceType, OperationType, RequestOptions, ContainerInternal, PartitionKey?, string, Stream, Action<RequestMessage>, ITrace, CancellationToken>(
                    (uri, resType, opType, opts, container, pk, itemId, stream, enricher, trace, ct) =>
                    {
                        // Capture the outgoing idempotency token and echo it back, simulating server behavior.
                        RequestMessage req = new RequestMessage();
                        enricher?.Invoke(req);
                        string token = req.Headers[HttpConstants.HttpHeaders.IdempotencyToken]
                            ?? Guid.NewGuid().ToString();

                        ResponseMessage response = BuildSuccessResponse(1);
                        response.Headers[HttpConstants.HttpHeaders.IdempotencyToken] = token;
                        return Task.FromResult(response);
                    });

            DistributedTransactionResponse response = await new DistributedWriteTransactionCore(contextMock.Object)
                .CreateItem(BuildMockContainer(), new PartitionKey("pk"), "item-id", new TestItem())
                .ExecuteTransactionAsync(CancellationToken.None);

            Assert.AreNotEqual(Guid.Empty, response.IdempotencyToken, "Response must carry the idempotency token.");
        }

        [TestMethod]
        public async Task CommitAsync_OperationIndexIsZeroBasedAndOrdered()
        {
            string capturedJson = null;

            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            contextMock
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Returns<string, ResourceType, OperationType, RequestOptions, ContainerInternal, PartitionKey?, string, Stream, Action<RequestMessage>, ITrace, CancellationToken>(
                    (uri, resType, opType, opts, container, pk, itemId, stream, enricher, trace, ct) =>
                    {
                        using MemoryStream ms = new MemoryStream();
                        stream.CopyTo(ms);
                        capturedJson = Encoding.UTF8.GetString(ms.ToArray());
                        return Task.FromResult(BuildSuccessResponse(3));
                    });

            await new DistributedWriteTransactionCore(contextMock.Object)
                .CreateItem(BuildMockContainer(), new PartitionKey("pk1"), "id1", new TestItem("id1"))
                .ReplaceItem(BuildMockContainer(), new PartitionKey("pk2"), "id2", new TestItem("id2"))
                .DeleteItem(BuildMockContainer(), new PartitionKey("pk3"), "id3")
                .ExecuteTransactionAsync(CancellationToken.None);

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement ops = doc.RootElement.GetProperty("operations");

            for (int i = 0; i < ops.GetArrayLength(); i++)
            {
                Assert.AreEqual(i, ops[i].GetProperty("index").GetInt32(),
                    $"Operation at position {i} should have index {i}.");
            }
        }

        [TestMethod]
        [Description("When server operationResponses arrive out-of-order, the SDK reorders them by index so response[i].Index == i.")]
        public async Task CommitAsync_OutOfOrderOperationResponses_SortedByIndex()
        {
            string capturedRequestJson = null;

            // Wire response has indices in order [3,4,1,2,0] — deliberately shuffled.
            const string mockResponseJson =
                @"{""operationResponses"":[{""index"":3,""statusCode"":201},{""index"":4,""statusCode"":201},{""index"":1,""statusCode"":201},{""index"":2,""statusCode"":201},{""index"":0,""statusCode"":201}]}";

            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            contextMock
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Returns<string, ResourceType, OperationType, RequestOptions, ContainerInternal, PartitionKey?, string, Stream, Action<RequestMessage>, ITrace, CancellationToken>(
                    (uri, resType, opType, opts, container, pk, itemId, stream, enricher, trace, ct) =>
                    {
                        using MemoryStream ms = new MemoryStream();
                        stream.CopyTo(ms);
                        capturedRequestJson = Encoding.UTF8.GetString(ms.ToArray());
                        return Task.FromResult(new ResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new MemoryStream(Encoding.UTF8.GetBytes(mockResponseJson))
                        });
                    });

            DistributedTransactionResponse response = await new DistributedWriteTransactionCore(contextMock.Object)
                .CreateItem(BuildMockContainer(), new PartitionKey("pk0"), "id0", new TestItem("id0"))
                .ReplaceItem(BuildMockContainer(), new PartitionKey("pk1"), "id1", new TestItem("id1"))
                .DeleteItem(BuildMockContainer(), new PartitionKey("pk2"), "id2")
                .PatchItem(BuildMockContainer(), new PartitionKey("pk3"), "id3", new[] { PatchOperation.Add("/value", "v3") })
                .UpsertItem(BuildMockContainer(), new PartitionKey("pk4"), "id4", new TestItem("id4"))
                .ExecuteTransactionAsync(CancellationToken.None);

            // Verify request indices are 0-based and ordered.
            using JsonDocument requestDoc = JsonDocument.Parse(capturedRequestJson);
            JsonElement requestOps = requestDoc.RootElement.GetProperty("operations");
            Assert.AreEqual(5, requestOps.GetArrayLength());
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(i, requestOps[i].GetProperty("index").GetInt32());
            }

            // After SDK reordering, response[i].Index must equal i.
            Assert.AreEqual(5, response.Count);
            for (int i = 0; i < response.Count; i++)
            {
                Assert.AreEqual(i, response[i].Index,
                    $"Response[{i}] must have Index {i} after SDK reordering.");
                Assert.AreEqual(HttpStatusCode.Created, response[i].StatusCode);
            }
        }

        [TestMethod]
        [Description("Smoke test: a commit with 100+ write operations whose server operationResponses arrive " +
                     "out-of-order is reordered by the SDK so response[i].Index == i for every operation.")]
        public async Task CommitAsync_ManyOperations_OutOfOrderResponses_SortedByIndex()
        {
            const int OperationCount = 128;

            // Server returns the per-operation responses in a deterministically-shuffled (out-of-order) sequence.
            int[] shuffledIndices = BuildShuffledIndices(OperationCount, seed: 7);
            Assert.IsTrue(
                IsOutOfOrder(shuffledIndices),
                "Wire order must be shuffled for this smoke test to be meaningful.");

            string responseJson = BuildOperationResponsesJson(shuffledIndices, statusCode: (int)HttpStatusCode.Created);

            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            contextMock
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResponseMessage(HttpStatusCode.OK)
                {
                    Content = new MemoryStream(Encoding.UTF8.GetBytes(responseJson))
                });

            DistributedWriteTransactionCore tx = new DistributedWriteTransactionCore(contextMock.Object);
            for (int i = 0; i < OperationCount; i++)
            {
                tx.CreateItem(BuildMockContainer(), new PartitionKey($"pk{i}"), $"id{i}", new TestItem($"id{i}"));
            }

            DistributedTransactionResponse response = await tx.ExecuteTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.AreEqual(OperationCount, response.Count);
            for (int i = 0; i < response.Count; i++)
            {
                Assert.AreEqual(i, response[i].Index,
                    $"Response[{i}] must have Index {i} after SDK reordering.");
                Assert.AreEqual(HttpStatusCode.Created, response[i].StatusCode);
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
            string responseJson = BuildOperationResponsesJson(indices, statusCode: (int)HttpStatusCode.Created);

            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            contextMock
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResponseMessage(HttpStatusCode.OK)
                {
                    Content = new MemoryStream(Encoding.UTF8.GetBytes(responseJson))
                });

            DistributedTransactionResponse response = await new DistributedWriteTransactionCore(contextMock.Object)
                .CreateItem(BuildMockContainer(), new PartitionKey("pk0"), "id0", new TestItem("id0"))
                .CreateItem(BuildMockContainer(), new PartitionKey("pk1"), "id1", new TestItem("id1"))
                .CreateItem(BuildMockContainer(), new PartitionKey("pk2"), "id2", new TestItem("id2"))
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
        public async Task CommitAsync_AllFiveOperationTypes_AreIncluded()
        {
            string capturedJson = null;

            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            contextMock
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Returns<string, ResourceType, OperationType, RequestOptions, ContainerInternal, PartitionKey?, string, Stream, Action<RequestMessage>, ITrace, CancellationToken>(
                    (uri, resType, opType, opts, container, pk, itemId, stream, enricher, trace, ct) =>
                    {
                        using MemoryStream ms = new MemoryStream();
                        stream.CopyTo(ms);
                        capturedJson = Encoding.UTF8.GetString(ms.ToArray());
                        return Task.FromResult(BuildSuccessResponse(5));
                    });

            await new DistributedWriteTransactionCore(contextMock.Object)
                .CreateItem(BuildMockContainer(), new PartitionKey("pk"), "create", new TestItem("create"))
                .ReplaceItem(BuildMockContainer(), new PartitionKey("pk"), "replace", new TestItem("replace"))
                .DeleteItem(BuildMockContainer(), new PartitionKey("pk"), "delete")
                .PatchItem(BuildMockContainer(), new PartitionKey("pk"), "patch", new[] { PatchOperation.Add("/value", "v") })
                .UpsertItem(BuildMockContainer(), new PartitionKey("pk"), "upsert", new TestItem("upsert"))
                .ExecuteTransactionAsync(CancellationToken.None);

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement ops = doc.RootElement.GetProperty("operations");

            Assert.AreEqual(5, ops.GetArrayLength());

            HashSet<string> opTypes = new HashSet<string>();
            foreach (JsonElement op in ops.EnumerateArray())
            {
                opTypes.Add(op.GetProperty("operationType").GetString());
            }

            Assert.IsTrue(opTypes.Contains(OperationType.Create.ToString()));
            Assert.IsTrue(opTypes.Contains(OperationType.Replace.ToString()));
            Assert.IsTrue(opTypes.Contains(OperationType.Delete.ToString()));
            Assert.IsTrue(opTypes.Contains(OperationType.Patch.ToString()));
            Assert.IsTrue(opTypes.Contains(OperationType.Upsert.ToString()));
        }

        [TestMethod]
        public async Task CommitAsync_SuccessResponse_ReturnsResultsForAllOperations()
        {
            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            contextMock
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildSuccessResponse(2));

            DistributedTransactionResponse response = await new DistributedWriteTransactionCore(contextMock.Object)
                .CreateItem(BuildMockContainer(), new PartitionKey("pk1"), "id1", new TestItem("id1"))
                .CreateItem(BuildMockContainer(), new PartitionKey("pk2"), "id2", new TestItem("id2"))
                .ExecuteTransactionAsync(CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.AreEqual(2, response.Count);
        }

        [TestMethod]
        public async Task CommitAsync_ErrorResponse_ReturnsIsSuccessStatusCodeFalse()
        {
            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            contextMock
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildErrorResponse(HttpStatusCode.Conflict));

            DistributedTransactionResponse response = await new DistributedWriteTransactionCore(contextMock.Object)
                .CreateItem(BuildMockContainer(), new PartitionKey("pk"), "item-id", new TestItem())
                .ExecuteTransactionAsync(CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
            Assert.IsFalse(response.IsSuccessStatusCode);
        }

        // Zero-operations guard

        [TestMethod]
        public async Task CommitAsync_ZeroOperations_ThrowsInvalidOperationException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();

            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => tx.ExecuteTransactionAsync(CancellationToken.None));

            Assert.IsTrue(ex.Message.Contains("zero operations"), $"Unexpected message: {ex.Message}");
        }

        // Double-commit guard

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
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildSuccessResponse(1));

            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(contextMock.Object)
                .CreateItem(BuildMockContainer(), new PartitionKey("pk"), "item-id", new TestItem());

            // First commit should succeed
            DistributedTransactionResponse response = await tx.ExecuteTransactionAsync(CancellationToken.None);
            Assert.IsTrue(response.IsSuccessStatusCode);

            // Second commit must throw
            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => tx.ExecuteTransactionAsync(CancellationToken.None));
            Assert.AreEqual(DistributedWriteTransactionCore.CommitAlreadyCalledMessage, ex.Message);
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
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(BuildErrorResponse(HttpStatusCode.Conflict));

            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(contextMock.Object)
                .CreateItem(BuildMockContainer(), new PartitionKey("pk"), "item-id", new TestItem());

            // First commit returns an error (but the call was made — idempotency token was consumed)
            DistributedTransactionResponse response = await tx.ExecuteTransactionAsync(CancellationToken.None);
            Assert.IsFalse(response.IsSuccessStatusCode);

            // Second commit must still throw — the token was already issued
            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => tx.ExecuteTransactionAsync(CancellationToken.None));
            Assert.AreEqual(DistributedWriteTransactionCore.CommitAlreadyCalledMessage, ex.Message);
        }

        [TestMethod]
        [Description("Verifies that a transient network exception during commit still consumes the transaction instance. " +
                     "Callers cannot distinguish 'request never sent' from 'request reached server, response lost', " +
                     "so retrying with a fresh token would risk a double-commit.")]
        public async Task CommitAsync_TransientExceptionFromNetwork_StillConsumesTransaction()
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
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Returns<string, ResourceType, OperationType, RequestOptions, ContainerInternal, PartitionKey?, string, Stream, Action<RequestMessage>, ITrace, CancellationToken>(
                    (uri, resType, opType, opts, container, pk, itemId, stream, enricher, trace, ct) =>
                    {
                        Interlocked.Increment(ref invocationCount);
                        throw new HttpRequestException("Simulated transient network failure");
                    });

            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(contextMock.Object)
                .CreateItem(BuildMockContainer(), new PartitionKey("pk"), "item-id", new TestItem());

            // First commit attempt: a transient network exception escapes to the caller.
            await Assert.ThrowsExceptionAsync<HttpRequestException>(
                () => tx.ExecuteTransactionAsync(CancellationToken.None));

            // Second commit attempt: must throw InvalidOperationException, NOT re-attempt the network call.
            // The SDK has no way to know whether the first attempt's request reached the server,
            // so a retry with a new idempotency token would risk a double-commit.
            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => tx.ExecuteTransactionAsync(CancellationToken.None));
            Assert.AreEqual(DistributedWriteTransactionCore.CommitAlreadyCalledMessage, ex.Message);
            Assert.AreEqual(1, invocationCount, "Second call must not re-attempt the network operation.");
        }

        [TestMethod]
        [Description("Verifies that user-initiated cancellation during commit still consumes the transaction instance.")]
        public async Task CommitAsync_CancelledDuringCommit_StillConsumesTransaction()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            int invocationCount = 0;

            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            contextMock
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Returns<string, ResourceType, OperationType, RequestOptions, ContainerInternal, PartitionKey?, string, Stream, Action<RequestMessage>, ITrace, CancellationToken>(
                    (uri, resType, opType, opts, container, pk, itemId, stream, enricher, trace, ct) =>
                    {
                        Interlocked.Increment(ref invocationCount);
                        cts.Cancel();
                        ct.ThrowIfCancellationRequested();
                        return Task.FromResult(BuildSuccessResponse(1));
                    });

            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(contextMock.Object)
                .CreateItem(BuildMockContainer(), new PartitionKey("pk"), "item-id", new TestItem());

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => tx.ExecuteTransactionAsync(cts.Token));

            // Retry with a fresh CancellationToken should still be rejected.
            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => tx.ExecuteTransactionAsync(CancellationToken.None));
            Assert.AreEqual(DistributedWriteTransactionCore.CommitAlreadyCalledMessage, ex.Message);
            Assert.AreEqual(1, invocationCount, "Second call must not re-attempt the network operation.");
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
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Returns<string, ResourceType, OperationType, RequestOptions, ContainerInternal, PartitionKey?, string, Stream, Action<RequestMessage>, ITrace, CancellationToken>(
                    (uri, resType, opType, opts, container, pk, itemId, stream, enricher, trace, ct) =>
                    {
                        Interlocked.Increment(ref invocationCount);
                        return Task.FromResult(BuildSuccessResponse(1));
                    });

            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(contextMock.Object)
                .CreateItem(BuildMockContainer(), new PartitionKey("pk"), "item-id", new TestItem());

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
                    Assert.AreEqual(DistributedWriteTransactionCore.CommitAlreadyCalledMessage, error.Message);
                    rejectedCount++;
                }
            }

            Assert.AreEqual(1, successCount, "Exactly one caller should execute the transaction.");
            Assert.AreEqual(RacerCount - 1, rejectedCount, "All other racers should be rejected by the guard.");
            Assert.AreEqual(1, invocationCount, "The underlying commit pipeline must only fire once.");
        }

        [TestMethod]
        [Description("ExecuteTransactionAsync must route through OperationHelperAsync with the correct operation name, OperationType, TraceComponent, and OTel operation name for the write path — ensuring the write path is distinct from the read path.")]
        public async Task ExecuteTransactionAsync_RoutesThroughOperationHelper_WithExpectedWiring()
        {
            string capturedOperationName = null;
            OperationType capturedOperationType = default;
            TraceComponent capturedTraceComponent = default;
            string capturedOTelOperationName = null;

            Mock<CosmosClientContext> contextMock = new Mock<CosmosClientContext>();
            contextMock
                .Setup(c => c.Client)
                .Returns(DistributedWriteTransactionTests.SharedMockClient);
            contextMock
                .Setup(c => c.OperationHelperAsync<DistributedTransactionResponse>(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<Func<ITrace, Task<DistributedTransactionResponse>>>(),
                    It.IsAny<(string OperationName, Func<DistributedTransactionResponse, Microsoft.Azure.Cosmos.Telemetry.OpenTelemetryAttributes> GetAttributes)?>(),
                    It.IsAny<ResourceType?>(),
                    It.IsAny<TraceComponent>(),
                    It.IsAny<TraceLevel>()))
                .Returns<string, string, string, OperationType, RequestOptions, Func<ITrace, Task<DistributedTransactionResponse>>, (string, Func<DistributedTransactionResponse, Microsoft.Azure.Cosmos.Telemetry.OpenTelemetryAttributes>)?, ResourceType?, TraceComponent, TraceLevel>(
                    (operationName, containerName, databaseName, operationType, requestOptions, func, oTelTuple, resourceType, comp, level) =>
                    {
                        capturedOperationName = operationName;
                        capturedOperationType = operationType;
                        capturedTraceComponent = comp;
                        capturedOTelOperationName = oTelTuple?.Item1;
                        return Task.FromResult<DistributedTransactionResponse>(null);
                    });

            await new DistributedWriteTransactionCore(contextMock.Object)
                .CreateItem(BuildMockContainer(), new PartitionKey("pk"), "id", new TestItem())
                .ExecuteTransactionAsync(CancellationToken.None);

            Assert.AreEqual($"{nameof(DistributedWriteTransaction)}.{nameof(DistributedWriteTransaction.ExecuteTransactionAsync)}", capturedOperationName);
            Assert.AreEqual(OperationType.CommitDistributedTransaction, capturedOperationType);
            Assert.AreEqual(TraceComponent.Batch, capturedTraceComponent);
            Assert.AreEqual(OpenTelemetryConstants.Operations.ExecuteDistributedWriteTransaction, capturedOTelOperationName);
        }

        // IdempotencyToken exposure (spec §4.4)

        [TestMethod]
        [Description("IdempotencyToken is Guid.Empty before ExecuteTransactionAsync is called — no attempt has reached dispatch yet.")]
        public void IdempotencyToken_BeforeCommit_IsEmpty()
        {
            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(this.BuildContextSetup().Object)
                .CreateItem(BuildMockContainer(), new PartitionKey("pk"), "item-id", new TestItem());

            Assert.AreEqual(Guid.Empty, tx.IdempotencyToken,
                "IdempotencyToken must be Guid.Empty before the first dispatch.");
        }

        [TestMethod]
        [Description("After a successful multi-attempt commit, DistributedWriteTransaction.IdempotencyToken equals the idempotency token stamped on the FINAL dispatched attempt (spec §4.4).")]
        public async Task IdempotencyToken_AfterMultiAttemptSuccess_EqualsFinalDispatchedToken()
        {
            int callCount = 0;
            List<string> capturedRequestTokens = new List<string>();
            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            contextMock
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, ResourceType, OperationType, RequestOptions, ContainerInternal, PartitionKey?, string, Stream, Action<RequestMessage>, ITrace, CancellationToken>(
                    (_, _, _, _, _, _, _, _, enricher, _, _) =>
                    {
                        RequestMessage request = new RequestMessage
                        {
                            ResourceType = ResourceType.DistributedTransactionBatch,
                            OperationType = OperationType.CommitDistributedTransaction,
                        };
                        enricher(request);
                        capturedRequestTokens.Add(request.Headers[HttpConstants.HttpHeaders.IdempotencyToken]);
                    })
                .Returns(() =>
                {
                    callCount++;
                    return callCount == 1
                        ? Task.FromResult(BuildRetriableAbortResponse())
                        : Task.FromResult(BuildSuccessResponse(1));
                });

            DistributedWriteTransactionCore tx = new DistributedWriteTransactionCore(contextMock.Object);
            tx.CreateItem(BuildMockContainer(), new PartitionKey("pk"), "item-id", new TestItem());

            DistributedTransactionResponse response = await tx.ExecuteTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.AreEqual(2, callCount, "Expected one retriable abort followed by a successful attempt.");
            Assert.AreEqual(2, capturedRequestTokens.Count);
            Assert.AreNotEqual(Guid.Empty, tx.IdempotencyToken);
            Assert.AreEqual(capturedRequestTokens[capturedRequestTokens.Count - 1], tx.IdempotencyToken.ToString(),
                "IdempotencyToken must equal the token stamped on the final dispatched attempt.");
        }

        [TestMethod]
        [Description("When cancellation fires between attempts, ExecuteTransactionAsync throws OperationCanceledException but IdempotencyToken still exposes the latest token that reached dispatch — never Guid.Empty (spec §4.4).")]
        public async Task IdempotencyToken_AfterCancellationBetweenAttempts_ExposesLatestDispatchedToken()
        {
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                List<string> capturedRequestTokens = new List<string>();
                Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
                contextMock
                    .Setup(c => c.ProcessResourceOperationStreamAsync(
                        It.IsAny<string>(),
                        It.IsAny<ResourceType>(),
                        It.IsAny<OperationType>(),
                        It.IsAny<RequestOptions>(),
                        It.IsAny<ContainerInternal>(),
                        It.IsAny<PartitionKey?>(),
                        It.IsAny<string>(),
                        It.IsAny<Stream>(),
                        It.IsAny<Action<RequestMessage>>(),
                        It.IsAny<ITrace>(),
                        It.IsAny<CancellationToken>()))
                    .Callback<string, ResourceType, OperationType, RequestOptions, ContainerInternal, PartitionKey?, string, Stream, Action<RequestMessage>, ITrace, CancellationToken>(
                        (_, _, _, _, _, _, _, _, enricher, _, _) =>
                        {
                            RequestMessage request = new RequestMessage
                            {
                                ResourceType = ResourceType.DistributedTransactionBatch,
                                OperationType = OperationType.CommitDistributedTransaction,
                            };
                            enricher(request);
                            capturedRequestTokens.Add(request.Headers[HttpConstants.HttpHeaders.IdempotencyToken]);
                        })
                    .Returns(() =>
                    {
                        // Cancel once this attempt has reached dispatch, so cancellation is observed at the
                        // next retry boundary (the backoff delay), never mid-dispatch.
                        cts.Cancel();
                        return Task.FromResult(BuildRetriableAbortResponse());
                    });

                DistributedWriteTransactionCore tx = new DistributedWriteTransactionCore(contextMock.Object);
                tx.CreateItem(BuildMockContainer(), new PartitionKey("pk"), "item-id", new TestItem());

                await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                    () => tx.ExecuteTransactionAsync(cts.Token));

                Assert.AreEqual(1, capturedRequestTokens.Count, "Exactly one attempt should reach dispatch before cancellation.");
                Assert.AreNotEqual(Guid.Empty, tx.IdempotencyToken, "IdempotencyToken must survive cancellation.");
                Assert.AreEqual(capturedRequestTokens[capturedRequestTokens.Count - 1], tx.IdempotencyToken.ToString(),
                    "IdempotencyToken must equal the last token that reached dispatch, even after cancellation.");
            }
        }

        [TestMethod]
        [Description("When cancellation fires during an in-flight dispatch, ExecuteTransactionAsync throws OperationCanceledException but IdempotencyToken still exposes the token published before the awaited dispatch — proving the publish happens before the await (spec §4.4).")]
        public async Task IdempotencyToken_AfterCancellationDuringInFlightDispatch_ExposesDispatchedToken()
        {
            using (CancellationTokenSource cts = new CancellationTokenSource())
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
                        It.IsAny<PartitionKey?>(),
                        It.IsAny<string>(),
                        It.IsAny<Stream>(),
                        It.IsAny<Action<RequestMessage>>(),
                        It.IsAny<ITrace>(),
                        It.IsAny<CancellationToken>()))
                    .Returns<string, ResourceType, OperationType, RequestOptions, ContainerInternal, PartitionKey?, string, Stream, Action<RequestMessage>, ITrace, CancellationToken>(
                        (uri, resType, opType, opts, container, pk, itemId, stream, enricher, trace, ct) =>
                        {
                            RequestMessage request = new RequestMessage
                            {
                                ResourceType = ResourceType.DistributedTransactionBatch,
                                OperationType = OperationType.CommitDistributedTransaction,
                            };
                            enricher(request);
                            capturedRequestToken = request.Headers[HttpConstants.HttpHeaders.IdempotencyToken];

                            // Cancellation observed while the dispatch is in flight: throw before any response.
                            cts.Cancel();
                            throw new OperationCanceledException(cts.Token);
                        });

                DistributedWriteTransactionCore tx = new DistributedWriteTransactionCore(contextMock.Object);
                tx.CreateItem(BuildMockContainer(), new PartitionKey("pk"), "item-id", new TestItem());

                await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                    () => tx.ExecuteTransactionAsync(cts.Token));

                Assert.IsNotNull(capturedRequestToken, "The attempt must reach dispatch before cancellation.");
                Assert.AreNotEqual(Guid.Empty, tx.IdempotencyToken,
                    "Token published before the await must survive an in-flight cancellation.");
                Assert.AreEqual(capturedRequestToken, tx.IdempotencyToken.ToString(),
                    "IdempotencyToken must equal the token published before the awaited dispatch.");
            }
        }

        [TestMethod]
        public async Task ExecuteTransactionAsync_ZeroOperations_ConsumesTransaction()
        {
            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            int dispatches = 0;
            SetupDispatch(contextMock, (stream, ct) =>
            {
                dispatches++;
                return Task.FromResult(BuildSuccessResponse(1));
            });
            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(contextMock.Object);
            InvalidOperationException empty = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => tx.ExecuteTransactionAsync(CancellationToken.None));
            StringAssert.Contains(empty.Message, "zero operations");
            StringAssert.Contains(empty.Message, "new");
            AssertMutationsRejected(tx);
            InvalidOperationException repeated = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => tx.ExecuteTransactionAsync(CancellationToken.None));
            Assert.AreEqual(DistributedWriteTransactionCore.CommitAlreadyCalledMessage, repeated.Message);
            Assert.AreEqual(Guid.Empty, tx.IdempotencyToken);
            Assert.AreEqual(0, dispatches);
        }

        [TestMethod]
        public async Task ExecuteTransactionAsync_PostSerializationMutation_PreservesResponse()
        {
            TaskCompletionSource<string> captured = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            SetupDispatch(contextMock, async (stream, ct) =>
            {
                using StreamReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
                captured.SetResult(await reader.ReadToEndAsync());
                await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
                return BuildSuccessResponse(1);
            });
            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(contextMock.Object)
                .CreateItem(BuildMockContainer(), new PartitionKey("pk"), "item-id", new TestItem());
            Task<DistributedTransactionResponse> execution = tx.ExecuteTransactionAsync();
            try
            {
                using JsonDocument body = JsonDocument.Parse(await captured.Task.WaitAsync(TimeSpan.FromSeconds(30)));
                Assert.AreEqual(1, body.RootElement.GetProperty("operations").GetArrayLength());
                Assert.AreEqual(0, body.RootElement.GetProperty("operations")[0].GetProperty("index").GetInt32());
                InvalidOperationException mutationError = null;
                try
                {
                    tx.DeleteItem(BuildMockContainer(), new PartitionKey("pk"), "late");
                }
                catch (InvalidOperationException error)
                {
                    mutationError = error;
                }

                if (mutationError != null)
                {
                    AssertMutationsRejected(tx);
                }

                release.TrySetResult(true);
                DistributedTransactionResponse response = await execution.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.IsTrue(response.IsSuccessStatusCode, $"One-operation success became {response.StatusCode} with {response.Count} results.");
                Assert.AreEqual(1, response.Count);
                Assert.AreEqual(0, response[0].Index);
                Assert.AreEqual(HttpStatusCode.Created, response[0].StatusCode);
                Assert.IsNotNull(mutationError, "The in-flight mutation must throw InvalidOperationException.");
                AssertMutationsRejected(tx);
                await AssertConsumedAsync(tx);
                Assert.AreEqual(1, response.Count);
                Assert.AreEqual(0, response[0].Index);
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
        public async Task ExecuteTransactionAsync_Failure_RejectsMutations(bool throwException)
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

                return Task.FromResult(BuildErrorResponse(HttpStatusCode.BadRequest));
            });
            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(contextMock.Object)
                .DeleteItem(BuildMockContainer(), new PartitionKey("pk"), "original");
            if (throwException)
            {
                await Assert.ThrowsExceptionAsync<IOException>(() => tx.ExecuteTransactionAsync());
            }
            else
            {
                using DistributedTransactionResponse response = await tx.ExecuteTransactionAsync();
                Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            }

            AssertMutationsRejected(tx);
            await AssertConsumedAsync(tx);
            Assert.AreEqual(1, dispatches);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ExecuteTransactionAsync_Cancellation_RejectsMutations(bool preCancelled)
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
                return BuildSuccessResponse(1);
            });
            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(contextMock.Object)
                .DeleteItem(BuildMockContainer(), new PartitionKey("pk"), "original");
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
                    AssertMutationsRejected(tx);
                    cancellation.Cancel();
                }

                release.TrySetResult(true);
                OperationCanceledException error = await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                    async () => await execution.WaitAsync(TimeSpan.FromSeconds(30)));
                Assert.AreEqual(cancellation.Token, error.CancellationToken);
                AssertMutationsRejected(tx);
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
        public async Task ExecuteTransactionAsync_BeforeHelperCallback_RejectsMutationsAndReexecution()
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
                return Task.FromResult(BuildSuccessResponse(1));
            });
            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(contextMock.Object)
                .DeleteItem(BuildMockContainer(), new PartitionKey("pk"), "original");
            Task<DistributedTransactionResponse> execution = tx.ExecuteTransactionAsync();
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.IsFalse(execution.IsCompleted);
                AssertMutationsRejected(tx);
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
                return Task.FromResult(BuildSuccessResponse(1));
            });
            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(contextMock.Object)
                .DeleteItem(BuildMockContainer(), new PartitionKey("pk"), "original");
            IOException error = await Assert.ThrowsExceptionAsync<IOException>(async () =>
            {
                using DistributedTransactionResponse response = await tx.ExecuteTransactionAsync().WaitAsync(TimeSpan.FromSeconds(30));
            });

            Assert.AreSame(expected, error);
            AssertMutationsRejected(tx);
            await AssertConsumedAsync(tx);
            Assert.AreEqual(1, helperCalls);
            Assert.AreEqual(0, dispatches);
            Assert.AreEqual(Guid.Empty, tx.IdempotencyToken);
            contextMock.Verify(c => c.GetCachedContainerPropertiesAsync(
                It.IsAny<string>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task ExecuteTransactionAsync_BeforeContainerResolution_RejectsMutations()
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
                return Task.FromResult(BuildSuccessResponse(1));
            });
            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(contextMock.Object)
                .DeleteItem(BuildMockContainer(), new PartitionKey("pk"), "original");
            Task<DistributedTransactionResponse> execution = tx.ExecuteTransactionAsync();
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
                Assert.AreEqual(0, dispatches);
                AssertMutationsRejected(tx);
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
        public async Task ExecuteTransactionAsync_ConcurrentEmptyCalls_OnlyOneReportsZeroOperations()
        {
            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            int dispatches = 0;
            SetupDispatch(contextMock, (stream, ct) =>
            {
                Interlocked.Increment(ref dispatches);
                return Task.FromResult(BuildSuccessResponse(1));
            });
            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(contextMock.Object);
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
            int emptyCount = 0;
            foreach (InvalidOperationException error in errors)
            {
                if (error.Message.Contains("zero operations"))
                {
                    emptyCount++;
                }
                else
                {
                    Assert.AreEqual(DistributedWriteTransactionCore.CommitAlreadyCalledMessage, error.Message);
                }
            }

            Assert.AreEqual(1, emptyCount);
            Assert.AreEqual(0, dispatches);
            Assert.AreEqual(Guid.Empty, tx.IdempotencyToken);
            AssertMutationsRejected(tx);
        }

        [TestMethod]
        public async Task ExecuteTransactionAsync_RacingAppend_IsIncludedOrRejected()
        {
            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            string captured = null;
            SetupDispatch(contextMock, async (stream, ct) =>
            {
                using StreamReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
                captured = await reader.ReadToEndAsync();
                using JsonDocument body = JsonDocument.Parse(captured);
                return BuildSuccessResponse(body.RootElement.GetProperty("operations").GetArrayLength());
            });
            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(contextMock.Object)
                .DeleteItem(BuildMockContainer(), new PartitionKey("pk"), "original");
            TaskCompletionSource<bool> start = NewGate();
            Task<bool> mutation = Task.Run(async () =>
            {
                await start.Task.WaitAsync(TimeSpan.FromSeconds(30));
                try
                {
                    tx.DeleteItem(BuildMockContainer(), new PartitionKey("pk"), "racing");
                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            });
            Task<DistributedTransactionResponse> execution = Task.Run(async () =>
            {
                await start.Task.WaitAsync(TimeSpan.FromSeconds(30));
                return await tx.ExecuteTransactionAsync();
            });
            start.SetResult(true);
            try
            {
                await Task.WhenAll(mutation, execution).WaitAsync(TimeSpan.FromSeconds(30));
                DistributedTransactionResponse response = await execution;
                using JsonDocument request = JsonDocument.Parse(captured);
                JsonElement operations = request.RootElement.GetProperty("operations");
                int expected = await mutation ? 2 : 1;
                Assert.AreEqual(expected, operations.GetArrayLength());
                Assert.AreEqual(expected, response.Count);
                Assert.IsTrue(response.IsSuccessStatusCode);
                for (int i = 0; i < expected; i++)
                {
                    Assert.AreEqual(i, operations[i].GetProperty("index").GetInt32());
                    Assert.AreEqual(i, response[i].Index);
                }
            }
            finally
            {
                using DistributedTransactionResponse cleanup = await execution.WaitAsync(TimeSpan.FromSeconds(30));
            }
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(5)]
        [DataRow(6)]
        [DataRow(7)]
        [DataRow(8)]
        public async Task ExecuteTransactionAsync_DuringMutationValidation_RejectsDelayedAppend(int mutationIndex)
        {
            Mock<CosmosClientContext> contextMock = this.BuildContextSetup();
            SetupDispatch(contextMock, (stream, ct) => Task.FromResult(BuildSuccessResponse(1)));
            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(contextMock.Object)
                .DeleteItem(BuildMockContainer(), new PartitionKey("pk"), "original");
            TaskCompletionSource<bool> entered = NewGate();
            using ManualResetEventSlim release = new ManualResetEventSlim();
            Cosmos.Container container = BuildMockContainer();
            Mock.Get(container).Setup(c => c.Id).Returns(() =>
            {
                entered.TrySetResult(true);
                Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(30)), "Container getter was not released.");
                return ContainerName;
            });
            using MemoryStream payload = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
            Action mutate = GetMutations(tx, container, payload)[mutationIndex];
            Task mutation = Task.Run(() => Assert.ThrowsException<InvalidOperationException>(mutate));
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
                using DistributedTransactionResponse response = await Task.Run(() => tx.ExecuteTransactionAsync())
                    .WaitAsync(TimeSpan.FromSeconds(30));
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.AreEqual(1, response.Count);
            }
            finally
            {
                release.Set();
                await mutation.WaitAsync(TimeSpan.FromSeconds(30));
            }

            AssertMutationsRejected(tx);
            await AssertConsumedAsync(tx);
        }

        // Helpers

        private static TaskCompletionSource<bool> NewGate() => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private static async Task AssertConsumedAsync(DistributedWriteTransaction tx)
        {
            InvalidOperationException error = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => tx.ExecuteTransactionAsync());
            Assert.AreEqual(DistributedWriteTransactionCore.CommitAlreadyCalledMessage, error.Message);
        }

        private static void AssertMutationsRejected(DistributedWriteTransaction tx)
        {
            foreach (Cosmos.Container container in new[] { BuildMockContainer(), null })
            {
                using MemoryStream payload = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
                foreach (Action mutation in GetMutations(tx, container, payload))
                {
                    InvalidOperationException error = Assert.ThrowsException<InvalidOperationException>(mutation);
                    Assert.AreEqual(DistributedWriteTransactionCore.CommitAlreadyCalledMessage, error.Message);
                    Assert.IsTrue(payload.CanRead, "Rejected mutations must not dispose caller streams.");
                    Assert.AreEqual(0L, payload.Position, "Rejected mutations must not consume caller streams.");
                }
            }
        }

        private static Action[] GetMutations(DistributedWriteTransaction tx, Cosmos.Container container, Stream payload)
        {
            PartitionKey pk = new PartitionKey("pk");
            return new Action[]
            {
                () => tx.CreateItem(container, pk, "late", new TestItem()),
                () => tx.CreateItemStream(container, pk, "late", payload),
                () => tx.ReplaceItem(container, pk, "late", new TestItem()),
                () => tx.ReplaceItemStream(container, pk, "late", payload),
                () => tx.DeleteItem(container, pk, "late"),
                () => tx.PatchItem(container, pk, "late", new[] { PatchOperation.Set("/value", 1) }),
                () => tx.PatchItemStream(container, pk, "late", payload),
                () => tx.UpsertItem(container, pk, "late", new TestItem()),
                () => tx.UpsertItemStream(container, pk, "late", payload),
            };
        }

        private static void SetupDispatch(Mock<CosmosClientContext> contextMock, Func<Stream, CancellationToken, Task<ResponseMessage>> dispatch)
        {
            contextMock.Setup(c => c.ProcessResourceOperationStreamAsync(
                It.IsAny<string>(), It.IsAny<ResourceType>(), It.IsAny<OperationType>(),
                It.IsAny<RequestOptions>(), It.IsAny<ContainerInternal>(), It.IsAny<PartitionKey?>(),
                It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<Action<RequestMessage>>(),
                It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .Returns<string, ResourceType, OperationType, RequestOptions, ContainerInternal, PartitionKey?, string, Stream, Action<RequestMessage>, ITrace, CancellationToken>(
                    (uri, resource, operation, options, container, pk, id, stream, enrich, trace, ct) => dispatch(stream, ct));
        }

        /// <summary>
        /// Creates a transaction backed by a minimal context mock — suitable for
        /// validation-only tests that do not invoke <see cref="DistributedWriteTransaction.ExecuteTransactionAsync"/>.
        /// </summary>
        private DistributedWriteTransaction NewTransaction()
        {
            return new DistributedWriteTransactionCore(this.BuildContextSetup().Object);
        }

        /// <summary>
        /// Builds a <see cref="Mock{CosmosClientContext}"/> with the common dependencies
        /// (<see cref="CosmosClientContext.SerializerCore"/> and
        /// <see cref="CosmosClientContext.GetCachedContainerPropertiesAsync"/>) already set up.
        /// Tests that intercept the outbound request add their own
        /// <see cref="CosmosClientContext.ProcessResourceOperationStreamAsync"/> setup on top.
        /// </summary>
        private Mock<CosmosClientContext> BuildContextSetup(
            Func<Func<ITrace, Task<DistributedTransactionResponse>>, Task<DistributedTransactionResponse>> operationHelper = null)
        {
            ContainerProperties containerProps = ContainerProperties.CreateWithResourceId("ccZ1ANCszwk=");
            containerProps.PartitionKeyPath = "/pk";

            MockDocumentClient documentClient = new MockDocumentClient();

            Mock<CosmosClientContext> contextMock = new Mock<CosmosClientContext>();

            contextMock
                .Setup(c => c.Client)
                .Returns(DistributedWriteTransactionTests.SharedMockClient);

            contextMock
                .Setup(c => c.DocumentClient)
                .Returns(documentClient);

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
                    It.IsAny<(string OperationName, Func<DistributedTransactionResponse, Microsoft.Azure.Cosmos.Telemetry.OpenTelemetryAttributes> GetAttributes)?>(),
                    It.IsAny<ResourceType?>(),
                    It.IsAny<TraceComponent>(),
                    It.IsAny<TraceLevel>()))
                .Returns<string, string, string, OperationType, RequestOptions, Func<ITrace, Task<DistributedTransactionResponse>>, (string, Func<DistributedTransactionResponse, Microsoft.Azure.Cosmos.Telemetry.OpenTelemetryAttributes>)?, ResourceType?, TraceComponent, TraceLevel>(
                    (operationName, containerName, databaseName, operationType, requestOptions, func, oTelFunc, resourceType, comp, level) =>
                        operationHelper != null ? operationHelper(func) : func(NoOpTrace.Singleton));

            return contextMock;
        }

        private static readonly CosmosClient SharedMockClient = new Mock<CosmosClient>().Object;

        /// <summary>
        /// Builds a mock <see cref="Cosmos.Container"/> that returns <see cref="DatabaseName"/>
        /// and <see cref="ContainerName"/> from its <see cref="Cosmos.Container.Database"/>/<see cref="Cosmos.Container.Id"/>
        /// accessors. The Container proxy makes no network calls, so a minimal mock is sufficient.
        /// </summary>
        private static Cosmos.Container BuildMockContainer(
            string databaseId = DatabaseName,
            string containerId = ContainerName,
            CosmosClient client = null)
        {
            Mock<Cosmos.Database> databaseMock = new Mock<Cosmos.Database>();
            databaseMock.Setup(d => d.Id).Returns(databaseId);
            databaseMock.Setup(d => d.Client).Returns(client ?? DistributedWriteTransactionTests.SharedMockClient);

            Mock<Cosmos.Container> containerMock = new Mock<Cosmos.Container>();
            containerMock.Setup(c => c.Id).Returns(containerId);
            containerMock.Setup(c => c.Database).Returns(databaseMock.Object);

            return containerMock.Object;
        }

        private static ResponseMessage BuildSuccessResponse(int operationCount)
        {
            List<string> results = new List<string>();
            for (int i = 0; i < operationCount; i++)
            {
                results.Add($@"{{""index"":{i},""statusCode"":201}}");
            }

            string json = $@"{{""operationResponses"":[{string.Join(",", results)}]}}";
            return new ResponseMessage(HttpStatusCode.OK)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(json))
            };
        }

        private static ResponseMessage BuildErrorResponse(HttpStatusCode statusCode)
        {
            string json = $@"{{""operationResponses"":[{{""index"":0,""statusCode"":{(int)statusCode}}}]}}";
            return new ResponseMessage(statusCode)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(json))
            };
        }

        /// <summary>
        /// Builds a retriable-abort response (HTTP 452 TransactionAborted marked isRetriable) that the
        /// committer's outer retry loop resubmits under a fresh idempotency token.
        /// </summary>
        private static ResponseMessage BuildRetriableAbortResponse()
        {
            string json = "{\"isRetriable\":true}";
            return new ResponseMessage((HttpStatusCode)StatusCodes.TransactionAborted)
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

        private sealed class TestItem
        {
            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public string Id { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("value")]
            public string Value { get; set; }

            public TestItem() : this(Guid.NewGuid().ToString()) { }

            public TestItem(string id)
            {
                this.Id = id;
                this.Value = "test-value";
            }
        }
    }
}
