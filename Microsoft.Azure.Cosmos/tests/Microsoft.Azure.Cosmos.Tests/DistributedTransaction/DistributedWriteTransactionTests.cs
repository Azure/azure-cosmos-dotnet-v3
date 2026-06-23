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
                .CommitTransactionAsync(CancellationToken.None);

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
                .CommitTransactionAsync(CancellationToken.None);

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
                .CommitTransactionAsync(CancellationToken.None);

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
                .CommitTransactionAsync(CancellationToken.None);

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
                .CommitTransactionAsync(CancellationToken.None);

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
                .CommitTransactionAsync(CancellationToken.None);

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
                .CommitTransactionAsync(CancellationToken.None);

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
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
            Assert.IsFalse(response.IsSuccessStatusCode);
        }

        // Zero-operations guard

        [TestMethod]
        public async Task CommitAsync_ZeroOperations_ThrowsInvalidOperationException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();

            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => tx.CommitTransactionAsync(CancellationToken.None));

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
            DistributedTransactionResponse response = await tx.CommitTransactionAsync(CancellationToken.None);
            Assert.IsTrue(response.IsSuccessStatusCode);

            // Second commit must throw
            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => tx.CommitTransactionAsync(CancellationToken.None));
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
            DistributedTransactionResponse response = await tx.CommitTransactionAsync(CancellationToken.None);
            Assert.IsFalse(response.IsSuccessStatusCode);

            // Second commit must still throw — the token was already issued
            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => tx.CommitTransactionAsync(CancellationToken.None));
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
                () => tx.CommitTransactionAsync(CancellationToken.None));

            // Second commit attempt: must throw InvalidOperationException, NOT re-attempt the network call.
            // The SDK has no way to know whether the first attempt's request reached the server,
            // so a retry with a new idempotency token would risk a double-commit.
            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => tx.CommitTransactionAsync(CancellationToken.None));
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
                () => tx.CommitTransactionAsync(cts.Token));

            // Retry with a fresh CancellationToken should still be rejected.
            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => tx.CommitTransactionAsync(CancellationToken.None));
            Assert.AreEqual(DistributedWriteTransactionCore.CommitAlreadyCalledMessage, ex.Message);
            Assert.AreEqual(1, invocationCount, "Second call must not re-attempt the network operation.");
        }

        [TestMethod]
        [Description("Verifies that only one of N concurrent callers wins the Interlocked.CompareExchange gate. " +
                     "Uses Task.Run + ManualResetEventSlim so that all racers hit CommitTransactionAsync from " +
                     "separate thread-pool threads simultaneously, providing genuine concurrency coverage.")]
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
                    async (uri, resType, opType, opts, container, pk, itemId, stream, enricher, trace, ct) =>
                    {
                        Interlocked.Increment(ref invocationCount);
                        await Task.Delay(50, ct);
                        return BuildSuccessResponse(1);
                    });

            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(contextMock.Object)
                .CreateItem(BuildMockContainer(), new PartitionKey("pk"), "item-id", new TestItem());

            const int RacerCount = 16;
            using ManualResetEventSlim gate = new ManualResetEventSlim(initialState: false);

            // Spawn all racers on separate thread-pool threads, each blocked on the gate.
            Task<DistributedTransactionResponse>[] tasks = new Task<DistributedTransactionResponse>[RacerCount];
            for (int i = 0; i < RacerCount; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    gate.Wait();
                    return await tx.CommitTransactionAsync(CancellationToken.None);
                });
            }

            gate.Set(); // release all racers simultaneously

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
            // This assertion is the key atomicity proof: without Interlocked, two threads could
            // both read isCommitInvoked==CommitNotStarted before either writes CommitStarted,
            // and invocationCount would be >1.
            Assert.AreEqual(1, invocationCount, "The underlying commit pipeline must only fire once.");
        }

        [TestMethod]
        [Description("CommitTransactionAsync must route through OperationHelperAsync with the correct operation name, OperationType, TraceComponent, and OTel operation name for the write path — ensuring the write path is distinct from the read path.")]
        public async Task CommitTransactionAsync_RoutesThroughOperationHelper_WithExpectedWiring()
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
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual($"{nameof(DistributedWriteTransaction)}.{nameof(DistributedWriteTransaction.CommitTransactionAsync)}", capturedOperationName);
            Assert.AreEqual(OperationType.CommitDistributedTransaction, capturedOperationType);
            Assert.AreEqual(TraceComponent.Batch, capturedTraceComponent);
            Assert.AreEqual(OpenTelemetryConstants.Operations.CommitDistributedWriteTransaction, capturedOTelOperationName);
        }

        // Helpers

        /// <summary>
        /// Creates a transaction backed by a minimal context mock — suitable for
        /// validation-only tests that do not invoke <see cref="DistributedWriteTransaction.CommitTransactionAsync"/>.
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
        private Mock<CosmosClientContext> BuildContextSetup()
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
                    (operationName, containerName, databaseName, operationType, requestOptions, func, oTelFunc, resourceType, comp, level) => func(NoOpTrace.Singleton));

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
