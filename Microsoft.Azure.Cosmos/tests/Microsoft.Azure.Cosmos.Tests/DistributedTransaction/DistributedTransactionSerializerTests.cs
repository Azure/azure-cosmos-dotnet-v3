// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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
    /// Unit tests verifying which fields (<c>id</c>, <c>resourceBody</c>) are present or absent
    /// in the serialized request body for each distributed-transaction operation type.
    ///
    /// Tests go through <see cref="DistributedWriteTransactionCore"/> using the same mock-context
    /// intercept pattern as <see cref="DistributedWriteTransactionTests"/>, so the assertions
    /// cover the full chain: argument → operation → serialization.
    /// </summary>
    [TestClass]
    public class DistributedTransactionSerializerTests
    {
        private const string Database = "testDb";
        private const string Container = "testContainer";

        // id field presence per operation type

        [TestMethod]
        [Description("CreateItem does not set an explicit id field on the operation, so 'id' must be absent from the serialized JSON.")]
        public async Task CreateItem_SerializedBody_HasResourceBody_NoIdField()
        {
            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.CreateItem(Database, Container, new PartitionKey("pk"), new TestItem("create-item")));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty("operations")[0];

            Assert.IsFalse(op.TryGetProperty("id", out _),
                "Create operation must NOT include an 'id' field in the serialized body.");
            Assert.IsTrue(op.TryGetProperty("resourceBody", out _),
                "Create operation must include a 'resourceBody' field.");
        }

        [TestMethod]
        [Description("ReplaceItem sets both id and resource, so both 'id' and 'resourceBody' must appear in the serialized JSON.")]
        public async Task ReplaceItem_SerializedBody_HasIdField_AndResourceBody()
        {
            const string itemId = "replace-item-id";

            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.ReplaceItem(Database, Container, new PartitionKey("pk"), itemId, new TestItem(itemId)));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty("operations")[0];

            Assert.IsTrue(op.TryGetProperty("id", out _),
                "Replace operation must include an 'id' field.");
            Assert.IsTrue(op.TryGetProperty("resourceBody", out _),
                "Replace operation must include a 'resourceBody' field.");
        }

        [TestMethod]
        [Description("DeleteItem sets id but has no resource body, so 'id' must be present and 'resourceBody' must be absent.")]
        public async Task DeleteItem_SerializedBody_HasIdField_NoResourceBody()
        {
            const string itemId = "delete-item-id";

            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.DeleteItem(Database, Container, new PartitionKey("pk"), itemId));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty("operations")[0];

            Assert.IsTrue(op.TryGetProperty("id", out _),
                "Delete operation must include an 'id' field.");
            Assert.IsFalse(op.TryGetProperty("resourceBody", out _),
                "Delete operation must NOT include a 'resourceBody' field.");
        }

        [TestMethod]
        [Description("UpsertItem provides a resource but no explicit id, so 'resourceBody' must be present and 'id' must be absent.")]
        public async Task UpsertItem_SerializedBody_HasResourceBody_NoIdField()
        {
            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.UpsertItem(Database, Container, new PartitionKey("pk"), new TestItem("upsert-item")));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty("operations")[0];

            Assert.IsFalse(op.TryGetProperty("id", out _),
                "Upsert operation must NOT include an 'id' field.");
            Assert.IsTrue(op.TryGetProperty("resourceBody", out _),
                "Upsert operation must include a 'resourceBody' field.");
        }

        // id value correctness

        [TestMethod]
        [Description("The 'id' field in the serialized JSON for ReplaceItem must exactly match the id passed to ReplaceItem().")]
        public async Task ReplaceItem_SerializedBody_IdValueMatchesProvided()
        {
            const string expectedId = "exact-replace-id";

            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.ReplaceItem(Database, Container, new PartitionKey("pk"), expectedId, new TestItem(expectedId)));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty("operations")[0];

            Assert.IsTrue(op.TryGetProperty("id", out JsonElement idElement));
            Assert.AreEqual(expectedId, idElement.GetString(),
                "The serialized 'id' field must equal the id passed to ReplaceItem().");
        }

        [TestMethod]
        [Description("The 'id' field in the serialized JSON for DeleteItem must exactly match the id passed to DeleteItem().")]
        public async Task DeleteItem_SerializedBody_IdValueMatchesProvided()
        {
            const string expectedId = "exact-delete-id";

            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.DeleteItem(Database, Container, new PartitionKey("pk"), expectedId));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty("operations")[0];

            Assert.IsTrue(op.TryGetProperty("id", out JsonElement idElement));
            Assert.AreEqual(expectedId, idElement.GetString(),
                "The serialized 'id' field must equal the id passed to DeleteItem().");
        }

        // resourceBody content

        [TestMethod]
        [Description("The 'resourceBody' field for a CreateItem must be valid, parseable JSON.")]
        public async Task CreateItem_SerializedBody_ResourceBodyIsValidJson()
        {
            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.CreateItem(Database, Container, new PartitionKey("pk"), new TestItem("json-check")));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty("operations")[0];

            Assert.IsTrue(op.TryGetProperty("resourceBody", out JsonElement resourceBodyElement),
                "resourceBody must be present.");
            Assert.AreEqual(JsonValueKind.Object, resourceBodyElement.ValueKind,
                "resourceBody must be a valid JSON object.");
        }

        // operationType and resourceType contracts

        [TestMethod]
        [Description("Each of the five operation types must serialize the 'operationType' field with the matching enum name.")]
        public async Task AllOpTypes_SerializedBody_OperationTypeMatchesOpType()
        {
            IReadOnlyList<PatchOperation> patchOps = new[] { PatchOperation.Add("/value", "v") };

            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.CreateItem(Database, Container, new PartitionKey("pk"), new TestItem("c"))
                  .ReplaceItem(Database, Container, new PartitionKey("pk"), "r", new TestItem("r"))
                  .DeleteItem(Database, Container, new PartitionKey("pk"), "d")
                  .UpsertItem(Database, Container, new PartitionKey("pk"), new TestItem("u"))
                  .PatchItem(Database, Container, new PartitionKey("pk"), "p", patchOps),
                expectedResultCount: 5);

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement ops = doc.RootElement.GetProperty("operations");

            Assert.AreEqual(5, ops.GetArrayLength());
            Assert.AreEqual(OperationType.Create.ToString(), ops[0].GetProperty("operationType").GetString());
            Assert.AreEqual(OperationType.Replace.ToString(), ops[1].GetProperty("operationType").GetString());
            Assert.AreEqual(OperationType.Delete.ToString(), ops[2].GetProperty("operationType").GetString());
            Assert.AreEqual(OperationType.Upsert.ToString(), ops[3].GetProperty("operationType").GetString());
            Assert.AreEqual(OperationType.Patch.ToString(), ops[4].GetProperty("operationType").GetString());
        }

        [TestMethod]
        [Description("Every operation in the serialized body must have 'resourceType' set to 'Document' regardless of the operation type.")]
        public async Task AllOpTypes_SerializedBody_ResourceTypeIsAlwaysDocument()
        {
            IReadOnlyList<PatchOperation> patchOps = new[] { PatchOperation.Add("/value", "v") };

            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.CreateItem(Database, Container, new PartitionKey("pk"), new TestItem("c"))
                  .ReplaceItem(Database, Container, new PartitionKey("pk"), "r", new TestItem("r"))
                  .DeleteItem(Database, Container, new PartitionKey("pk"), "d")
                  .UpsertItem(Database, Container, new PartitionKey("pk"), new TestItem("u"))
                  .PatchItem(Database, Container, new PartitionKey("pk"), "p", patchOps),
                expectedResultCount: 5);

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement ops = doc.RootElement.GetProperty("operations");

            for (int i = 0; i < ops.GetArrayLength(); i++)
            {
                Assert.AreEqual(
                    ResourceType.Document.ToString(),
                    ops[i].GetProperty("resourceType").GetString(),
                    $"Operation[{i}] must have resourceType = 'Document'.");
            }
        }

        // field type contracts

        [TestMethod]
        [Description("Every field present in a serialized operation must carry the correct JSON value type")]
        public async Task SerializedOperation_AllPresentFields_HaveCorrectJsonValueKinds()
        {
            const string itemId = "type-check-id";

            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.ReplaceItem(Database, Container, new PartitionKey("pk"), itemId, new TestItem(itemId)));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty("operations")[0];

            Assert.AreEqual(JsonValueKind.String, op.GetProperty("databaseName").ValueKind, "'databaseName' must be a JSON string.");
            Assert.AreEqual(JsonValueKind.String, op.GetProperty("collectionName").ValueKind, "'collectionName' must be a JSON string.");
            Assert.AreEqual(JsonValueKind.String, op.GetProperty("collectionResourceId").ValueKind, "'collectionResourceId' must be a JSON string.");
            Assert.AreEqual(JsonValueKind.String, op.GetProperty("databaseResourceId").ValueKind, "'databaseResourceId' must be a JSON string.");
            Assert.AreEqual(JsonValueKind.String, op.GetProperty("id").ValueKind, "'id' must be a JSON string.");
            Assert.AreEqual(JsonValueKind.Array,  op.GetProperty("partitionKey").ValueKind, "'partitionKey' must be a JSON array, not a quoted string.");
            Assert.AreEqual(JsonValueKind.Number, op.GetProperty("index").ValueKind, "'index' must be a JSON number, not a quoted string.");
            Assert.AreEqual(JsonValueKind.Object, op.GetProperty("resourceBody").ValueKind, "'resourceBody' must be a JSON object.");
            Assert.AreEqual(JsonValueKind.String, op.GetProperty("operationType").ValueKind, "'operationType' must be a JSON string.");
            Assert.AreEqual(JsonValueKind.String, op.GetProperty("resourceType").ValueKind, "'resourceType' must be a JSON string.");
        }

        // Stream operations

        [TestMethod]
        [Description("CreateItemStream with a JSON stream sets resourceBody on the operation; no explicit 'id' field should appear.")]
        public async Task CreateItemStream_SerializedBody_HasResourceBody_NoIdField()
        {
            byte[] docBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new TestItem("test-id")));
            using MemoryStream stream = new MemoryStream(docBytes);
            string capturedJson = await this.CaptureCommitBodyAsync(
                tx => tx.CreateItemStream(Database, Container, new PartitionKey("pk"), stream));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty("operations")[0];

            Assert.IsFalse(op.TryGetProperty("id", out _),
                "CreateItemStream operation must NOT include an 'id' field in the serialized body.");
            Assert.IsTrue(op.TryGetProperty("resourceBody", out _),
                "CreateItemStream operation must include a 'resourceBody' field.");
        }

        [TestMethod]
        [Description("ReplaceItemStream sets both id and resource; 'id' and 'resourceBody' must appear in the serialized JSON.")]
        public async Task ReplaceItemStream_SerializedBody_HasIdAndResourceBody()
        {
            const string itemId = "replace-stream-id";
            byte[] docBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new TestItem(itemId)));
            using MemoryStream stream = new MemoryStream(docBytes);
            string capturedJson = await this.CaptureCommitBodyAsync(
                tx => tx.ReplaceItemStream(Database, Container, new PartitionKey("pk"), itemId, stream));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty("operations")[0];

            Assert.IsTrue(op.TryGetProperty("id", out _),
                "ReplaceItemStream operation must include an 'id' field.");
            Assert.IsTrue(op.TryGetProperty("resourceBody", out _),
                "ReplaceItemStream operation must include a 'resourceBody' field.");
        }

        [TestMethod]
        [Description("PatchItemStream sets id and resource; 'id' and 'resourceBody' must appear in the serialized JSON.")]
        public async Task PatchItemStream_SerializedBody_HasIdAndResourceBody()
        {
            const string itemId = "patch-stream-id";
            byte[] patchBytes = Encoding.UTF8.GetBytes(@"{""operations"":[{""op"":""add"",""path"":""/description"",""value"":""patched""}]}");
            using MemoryStream stream = new MemoryStream(patchBytes);
            string capturedJson = await this.CaptureCommitBodyAsync(
                tx => tx.PatchItemStream(Database, Container, new PartitionKey("pk"), itemId, stream));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty("operations")[0];

            Assert.IsTrue(op.TryGetProperty("id", out _),
                "PatchItemStream operation must include an 'id' field.");
            Assert.IsTrue(op.TryGetProperty("resourceBody", out _),
                "PatchItemStream operation must include a 'resourceBody' field.");
        }

        [TestMethod]
        [Description("UpsertItemStream sets resource but no explicit id; 'resourceBody' must be present and 'id' must be absent.")]
        public async Task UpsertItemStream_SerializedBody_HasResourceBody_NoIdField()
        {
            byte[] docBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new TestItem("upsert-stream-id")));
            using MemoryStream stream = new MemoryStream(docBytes);
            string capturedJson = await this.CaptureCommitBodyAsync(
                tx => tx.UpsertItemStream(Database, Container, new PartitionKey("pk"), stream));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty("operations")[0];

            Assert.IsFalse(op.TryGetProperty("id", out _),
                "UpsertItemStream operation must NOT include an 'id' field in the serialized body.");
            Assert.IsTrue(op.TryGetProperty("resourceBody", out _),
                "UpsertItemStream operation must include a 'resourceBody' field.");
        }

        // IfMatchEtag

        [TestMethod]
        [Description("ReplaceItem with IfMatchEtag set must serialize an 'etag' field in the operation JSON.")]
        public async Task ReplaceItem_WithIfMatchEtag_SerializesEtagField()
        {
            const string etag = "\"test-etag\"";
            const string itemId = "etag-replace-id";

            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.ReplaceItem(Database, Container, new PartitionKey("pk"), itemId, new TestItem(itemId),
                    new DistributedTransactionRequestOptions { IfMatchEtag = etag }));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty("operations")[0];

            Assert.IsTrue(op.TryGetProperty("etag", out JsonElement etagElement),
                "Replace operation with IfMatchEtag must include an 'etag' field.");
            Assert.AreEqual(etag, etagElement.GetString());
        }

        [TestMethod]
        [Description("DeleteItem with IfMatchEtag set must serialize an 'etag' field in the operation JSON.")]
        public async Task DeleteItem_WithIfMatchEtag_SerializesEtagField()
        {
            const string etag = "\"test-etag\"";
            const string itemId = "etag-delete-id";

            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.DeleteItem(Database, Container, new PartitionKey("pk"), itemId,
                    new DistributedTransactionRequestOptions { IfMatchEtag = etag }));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty("operations")[0];

            Assert.IsTrue(op.TryGetProperty("etag", out JsonElement etagElement),
                "Delete operation with IfMatchEtag must include an 'etag' field.");
            Assert.AreEqual(etag, etagElement.GetString());
        }

        [TestMethod]
        [Description("PatchItem with IfMatchEtag set must serialize an 'etag' field in the operation JSON.")]
        public async Task PatchItem_WithIfMatchEtag_SerializesEtagField()
        {
            const string etag = "\"test-etag\"";
            const string itemId = "etag-patch-id";

            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.PatchItem(Database, Container, new PartitionKey("pk"), itemId,
                    new[] { PatchOperation.Add("/value", "v") },
                    new DistributedTransactionRequestOptions { IfMatchEtag = etag }));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty("operations")[0];

            Assert.IsTrue(op.TryGetProperty("etag", out JsonElement etagElement),
                "Patch operation with IfMatchEtag must include an 'etag' field.");
            Assert.AreEqual(etag, etagElement.GetString());
        }

        [TestMethod]
        [Description("Operations without IfMatchEtag must not include an 'etag' field in the serialized JSON for any operation.")]
        public async Task Operations_WithoutIfMatchEtag_DoNotIncludeEtagField()
        {
            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.CreateItem(Database, Container, new PartitionKey("pk"), new TestItem("create-no-etag"))
                  .ReplaceItem(Database, Container, new PartitionKey("pk"), "replace-no-etag", new TestItem("replace-no-etag")),
                expectedResultCount: 2);

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement ops = doc.RootElement.GetProperty("operations");

            for (int i = 0; i < ops.GetArrayLength(); i++)
            {
                Assert.IsFalse(ops[i].TryGetProperty("etag", out _),
                    $"Operation[{i}] without IfMatchEtag must NOT include an 'etag' field.");
            }
        }

        // Helpers

        /// <summary>
        /// Builds a transaction using <paramref name="buildTransaction"/>, intercepts the HTTP
        /// commit call, captures the serialized JSON body, and returns it.
        /// </summary>
        private async Task<string> CaptureCommitBodyAsync(
            Func<DistributedWriteTransaction, DistributedWriteTransaction> buildTransaction,
            int expectedResultCount = 1)
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
                        return Task.FromResult(this.BuildSuccessResponse(expectedResultCount));
                    });

            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(contextMock.Object);
            await buildTransaction(tx).CommitTransactionAsync(CancellationToken.None);

            Assert.IsNotNull(capturedJson, "The commit body was not captured — the mock was not invoked.");
            return capturedJson;
        }

        private Mock<CosmosClientContext> BuildContextSetup()
        {
            ContainerProperties containerProps = ContainerProperties.CreateWithResourceId("ccZ1ANCszwk=");
            containerProps.PartitionKeyPath = "/pk";

            Mock<CosmosClientContext> contextMock = new Mock<CosmosClientContext>();

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

        private ResponseMessage BuildSuccessResponse(int operationCount)
        {
            List<string> results = new List<string>();
            for (int i = 0; i < operationCount; i++)
            {
                results.Add($@"{{""index"":{i},""statusCode"":201}}");
            }

            string json = $@"{{""operationResponses"":[{string.Join(",", results)}]}}";
            return new ResponseMessage(System.Net.HttpStatusCode.OK)
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

            public TestItem(string id)
            {
                this.Id = id;
                this.Value = "test-value";
            }
        }
    }
}
