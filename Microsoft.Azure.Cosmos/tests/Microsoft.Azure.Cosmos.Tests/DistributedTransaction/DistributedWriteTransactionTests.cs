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
        private const string Database = "testDb";
        private const string Container = "testContainer";

        // Argument validation

        [TestMethod]
        public void CreateItem_NullDatabase_ThrowsArgumentException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentNullException>(
                () => tx.CreateItem(null, Container, new PartitionKey("pk"), "item-id", new TestItem()));
        }

        [TestMethod]
        public void CreateItem_NullCollection_ThrowsArgumentException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentNullException>(
                () => tx.CreateItem(Database, null, new PartitionKey("pk"), "item-id", new TestItem()));
        }

        [TestMethod]
        public void CreateItem_NullResource_ThrowsArgumentException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentNullException>(
                () => tx.CreateItem<TestItem>(Database, Container, new PartitionKey("pk"), "item-id", null));
        }

        [TestMethod]
        public void ReplaceItem_NullId_ThrowsArgumentException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentNullException>(
                () => tx.ReplaceItem(Database, Container, new PartitionKey("pk"), null, new TestItem()));
        }

        [TestMethod]
        public void DeleteItem_EmptyId_ThrowsArgumentException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentNullException>(
                () => tx.DeleteItem(Database, Container, new PartitionKey("pk"), string.Empty));
        }

        [TestMethod]
        public void PatchItem_NullPatchOperations_ThrowsArgumentNullException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentNullException>(
                () => tx.PatchItem(Database, Container, new PartitionKey("pk"), "item-id", null));
        }

        [TestMethod]
        public void PatchItem_EmptyPatchOperations_ThrowsArgumentNullException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentNullException>(
                () => tx.PatchItem(Database, Container, new PartitionKey("pk"), "item-id", new List<PatchOperation>()));
        }

        [TestMethod]
        public void CreateItemStream_NullStream_ThrowsArgumentNullException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentNullException>(
                () => tx.CreateItemStream(Database, Container, new PartitionKey("pk"), "item-id", null));
        }

        [TestMethod]
        public void ReplaceItemStream_NullStream_ThrowsArgumentNullException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentNullException>(
                () => tx.ReplaceItemStream(Database, Container, new PartitionKey("pk"), "item-id", null));
        }

        [TestMethod]
        public void PatchItemStream_NullStream_ThrowsArgumentNullException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentNullException>(
                () => tx.PatchItemStream(Database, Container, new PartitionKey("pk"), "item-id", null));
        }

        [TestMethod]
        public void UpsertItemStream_NullStream_ThrowsArgumentNullException()
        {
            DistributedWriteTransaction tx = this.NewTransaction();
            Assert.ThrowsException<ArgumentNullException>(
                () => tx.UpsertItemStream(Database, Container, new PartitionKey("pk"), "item-id", null));
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
                .CreateItem(Database, Container, new PartitionKey("pk"), "item-id", new TestItem())
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
                .CreateItem(Database, Container, new PartitionKey("pk"), "item-id", new TestItem())
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
                .CreateItem(Database, Container, new PartitionKey("pk"), "item-id", new TestItem())
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
                .CreateItem(Database, Container, new PartitionKey("pk1"), "id1", new TestItem("id1"))
                .ReplaceItem(Database, Container, new PartitionKey("pk2"), "id2", new TestItem("id2"))
                .DeleteItem(Database, Container, new PartitionKey("pk3"), "id3")
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
                .CreateItem(Database, Container, new PartitionKey("pk"), "create", new TestItem("create"))
                .ReplaceItem(Database, Container, new PartitionKey("pk"), "replace", new TestItem("replace"))
                .DeleteItem(Database, Container, new PartitionKey("pk"), "delete")
                .PatchItem(Database, Container, new PartitionKey("pk"), "patch", new[] { PatchOperation.Add("/value", "v") })
                .UpsertItem(Database, Container, new PartitionKey("pk"), "upsert", new TestItem("upsert"))
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
                .CreateItem(Database, Container, new PartitionKey("pk1"), "id1", new TestItem("id1"))
                .CreateItem(Database, Container, new PartitionKey("pk2"), "id2", new TestItem("id2"))
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
                .CreateItem(Database, Container, new PartitionKey("pk"), "item-id", new TestItem())
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
            Assert.IsFalse(response.IsSuccessStatusCode);
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

            return contextMock;
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
