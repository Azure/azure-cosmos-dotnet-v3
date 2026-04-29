// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
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
        [Description("CreateItem sets id explicitly; 'id' must be present in the serialized JSON with the correct value.")]
        public async Task CreateItem_SerializedBody_HasResourceBody_AndIdField()
        {
            const string itemId = "create-item";
            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.CreateItem(Database, Container, new PartitionKey("pk"), itemId, new TestItem(itemId)));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.Id, out JsonElement idElement),
                "Create operation must include an 'id' field.");
            Assert.AreEqual(itemId, idElement.GetString(),
                "The 'id' field must match the id passed to CreateItem.");
            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.ResourceBody, out _),
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
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.Id, out _),
                "Replace operation must include an 'id' field.");
            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.ResourceBody, out _),
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
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.Id, out _),
                "Delete operation must include an 'id' field.");
            Assert.IsFalse(op.TryGetProperty(DistributedTransactionSerializer.ResourceBody, out _),
                "Delete operation must NOT include a 'resourceBody' field.");
        }

        [TestMethod]
        [Description("UpsertItem sets id explicitly; 'id' must be present in the serialized JSON with the correct value.")]
        public async Task UpsertItem_SerializedBody_HasResourceBody_AndIdField()
        {
            const string itemId = "upsert-item";
            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.UpsertItem(Database, Container, new PartitionKey("pk"), itemId, new TestItem(itemId)));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.Id, out JsonElement idElement),
                "Upsert operation must include an 'id' field.");
            Assert.AreEqual(itemId, idElement.GetString(),
                "The 'id' field must match the id passed to UpsertItem.");
            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.ResourceBody, out _),
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
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.Id, out JsonElement idElement));
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
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.Id, out JsonElement idElement));
            Assert.AreEqual(expectedId, idElement.GetString(),
                "The serialized 'id' field must equal the id passed to DeleteItem().");
        }

        // resourceBody content

        [TestMethod]
        [Description("The 'resourceBody' field for a CreateItem must be valid, parseable JSON.")]
        public async Task CreateItem_SerializedBody_ResourceBodyIsValidJson()
        {
            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.CreateItem(Database, Container, new PartitionKey("pk"), "json-check", new TestItem("json-check")));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.ResourceBody, out JsonElement resourceBodyElement),
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
                tx.CreateItem(Database, Container, new PartitionKey("pk"), "c", new TestItem("c"))
                  .ReplaceItem(Database, Container, new PartitionKey("pk"), "r", new TestItem("r"))
                  .DeleteItem(Database, Container, new PartitionKey("pk"), "d")
                  .UpsertItem(Database, Container, new PartitionKey("pk"), "u", new TestItem("u"))
                  .PatchItem(Database, Container, new PartitionKey("pk"), "p", patchOps),
                expectedResultCount: 5);

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement ops = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations);

            Assert.AreEqual(5, ops.GetArrayLength());
            Assert.AreEqual(OperationType.Create.ToString(), ops[0].GetProperty(DistributedTransactionSerializer.OperationType).GetString());
            Assert.AreEqual(OperationType.Replace.ToString(), ops[1].GetProperty(DistributedTransactionSerializer.OperationType).GetString());
            Assert.AreEqual(OperationType.Delete.ToString(), ops[2].GetProperty(DistributedTransactionSerializer.OperationType).GetString());
            Assert.AreEqual(OperationType.Upsert.ToString(), ops[3].GetProperty(DistributedTransactionSerializer.OperationType).GetString());
            Assert.AreEqual(OperationType.Patch.ToString(), ops[4].GetProperty(DistributedTransactionSerializer.OperationType).GetString());
        }

        [TestMethod]
        [Description("Every operation in the serialized body must have 'resourceType' set to 'Document' regardless of the operation type.")]
        public async Task AllOpTypes_SerializedBody_ResourceTypeIsAlwaysDocument()
        {
            IReadOnlyList<PatchOperation> patchOps = new[] { PatchOperation.Add("/value", "v") };

            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.CreateItem(Database, Container, new PartitionKey("pk"), "c", new TestItem("c"))
                  .ReplaceItem(Database, Container, new PartitionKey("pk"), "r", new TestItem("r"))
                  .DeleteItem(Database, Container, new PartitionKey("pk"), "d")
                  .UpsertItem(Database, Container, new PartitionKey("pk"), "u", new TestItem("u"))
                  .PatchItem(Database, Container, new PartitionKey("pk"), "p", patchOps),
                expectedResultCount: 5);

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement ops = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations);

            for (int i = 0; i < ops.GetArrayLength(); i++)
            {
                Assert.AreEqual(
                    ResourceType.Document.ToString(),
                    ops[i].GetProperty(DistributedTransactionSerializer.ResourceType).GetString(),
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
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.AreEqual(JsonValueKind.String, op.GetProperty(DistributedTransactionSerializer.DatabaseName).ValueKind, "'databaseName' must be a JSON string.");
            Assert.AreEqual(JsonValueKind.String, op.GetProperty(DistributedTransactionSerializer.CollectionName).ValueKind, "'collectionName' must be a JSON string.");
            Assert.AreEqual(JsonValueKind.String, op.GetProperty(DistributedTransactionSerializer.CollectionResourceId).ValueKind, "'collectionResourceId' must be a JSON string.");
            Assert.AreEqual(JsonValueKind.String, op.GetProperty(DistributedTransactionSerializer.DatabaseResourceId).ValueKind, "'databaseResourceId' must be a JSON string.");
            Assert.AreEqual(JsonValueKind.String, op.GetProperty(DistributedTransactionSerializer.Id).ValueKind, "'id' must be a JSON string.");
            Assert.AreEqual(JsonValueKind.Array,  op.GetProperty(DistributedTransactionSerializer.PartitionKey).ValueKind, "'partitionKey' must be a JSON array, not a quoted string.");
            Assert.AreEqual(JsonValueKind.Number, op.GetProperty(DistributedTransactionSerializer.Index).ValueKind, "'index' must be a JSON number, not a quoted string.");
            Assert.AreEqual(JsonValueKind.Object, op.GetProperty(DistributedTransactionSerializer.ResourceBody).ValueKind, "'resourceBody' must be a JSON object.");
            Assert.AreEqual(JsonValueKind.String, op.GetProperty(DistributedTransactionSerializer.OperationType).ValueKind, "'operationType' must be a JSON string.");
            Assert.AreEqual(JsonValueKind.String, op.GetProperty(DistributedTransactionSerializer.ResourceType).ValueKind, "'resourceType' must be a JSON string.");
        }

        // Stream operations

        [TestMethod]
        [Description("CreateItemStream sets id explicitly; 'id' must be present in the serialized JSON with the correct value.")]
        public async Task CreateItemStream_SerializedBody_HasResourceBody_AndIdField()
        {
            const string itemId = "test-id";
            byte[] docBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new TestItem(itemId)));
            using MemoryStream stream = new MemoryStream(docBytes);
            string capturedJson = await this.CaptureCommitBodyAsync(
                tx => tx.CreateItemStream(Database, Container, new PartitionKey("pk"), itemId, stream));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.Id, out JsonElement idElement),
                "CreateItemStream operation must include an 'id' field.");
            Assert.AreEqual(itemId, idElement.GetString(),
                "The 'id' field must match the id passed to CreateItemStream.");
            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.ResourceBody, out _),
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
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.Id, out _),
                "ReplaceItemStream operation must include an 'id' field.");
            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.ResourceBody, out _),
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
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.Id, out _),
                "PatchItemStream operation must include an 'id' field.");
            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.ResourceBody, out _),
                "PatchItemStream operation must include a 'resourceBody' field.");
        }

        [TestMethod]
        [Description("UpsertItemStream sets id explicitly; 'id' must be present in the serialized JSON with the correct value.")]
        public async Task UpsertItemStream_SerializedBody_HasResourceBody_AndIdField()
        {
            const string itemId = "upsert-stream-id";
            byte[] docBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new TestItem(itemId)));
            using MemoryStream stream = new MemoryStream(docBytes);
            string capturedJson = await this.CaptureCommitBodyAsync(
                tx => tx.UpsertItemStream(Database, Container, new PartitionKey("pk"), itemId, stream));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.Id, out JsonElement idElement),
                "UpsertItemStream operation must include an 'id' field.");
            Assert.AreEqual(itemId, idElement.GetString(),
                "The 'id' field must match the id passed to UpsertItemStream.");
            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.ResourceBody, out _),
                "UpsertItemStream operation must include a 'resourceBody' field.");
        }

        // IfNoneMatchEtag for ReadItem

        [TestMethod]
        [Description("ReadItem with IfNoneMatchEtag set must serialize an 'etag' field in the operation JSON.")]
        public async Task ReadItem_WithIfNoneMatchEtag_SerializesEtagField()
        {
            const string etag = "\"test-etag\"";
            const string itemId = "etag-read-id";

            string capturedJson = await this.CaptureReadCommitBodyAsync(tx =>
                tx.ReadItem(Database, Container, new PartitionKey("pk"), itemId,
                    new DistributedTransactionRequestOptions { IfNoneMatchEtag = etag }));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.ETag, out JsonElement etagElement),
                "Read operation with IfNoneMatchEtag must include an 'etag' field.");
            Assert.AreEqual(etag, etagElement.GetString());
        }

        [TestMethod]
        [Description("ReadItem without any etag option must not include an 'etag' field in the serialized JSON.")]
        public async Task ReadItem_WithoutEtag_DoesNotIncludeEtagField()
        {
            string capturedJson = await this.CaptureReadCommitBodyAsync(tx =>
                tx.ReadItem(Database, Container, new PartitionKey("pk"), "read-no-etag"));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsFalse(op.TryGetProperty(DistributedTransactionSerializer.ETag, out _),
                "Read operation without etag options must NOT include an 'etag' field.");
        }

        [TestMethod]
        [Description("ReadItem with IfMatchEtag (write-side etag) must NOT serialize an 'etag' field; reads use IfNoneMatchEtag only.")]
        public async Task ReadItem_WithIfMatchEtag_DoesNotSerializeEtagField()
        {
            const string etag = "\"write-etag\"";
            const string itemId = "read-ifmatch-id";

            string capturedJson = await this.CaptureReadCommitBodyAsync(tx =>
                tx.ReadItem(Database, Container, new PartitionKey("pk"), itemId,
                    new DistributedTransactionRequestOptions { IfMatchEtag = etag }));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsFalse(op.TryGetProperty(DistributedTransactionSerializer.ETag, out _),
                "Read operation must not use IfMatchEtag; only IfNoneMatchEtag is serialized for reads.");
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
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.ETag, out JsonElement etagElement),
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
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.ETag, out JsonElement etagElement),
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
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.ETag, out JsonElement etagElement),
                "Patch operation with IfMatchEtag must include an 'etag' field.");
            Assert.AreEqual(etag, etagElement.GetString());
        }

        [TestMethod]
        [Description("Operations without IfMatchEtag must not include an 'etag' field in the serialized JSON for any operation.")]
        public async Task Operations_WithoutIfMatchEtag_DoNotIncludeEtagField()
        {
            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.CreateItem(Database, Container, new PartitionKey("pk"), "create-no-etag", new TestItem("create-no-etag"))
                  .ReplaceItem(Database, Container, new PartitionKey("pk"), "replace-no-etag", new TestItem("replace-no-etag")),
                expectedResultCount: 2);

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement ops = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations);

            for (int i = 0; i < ops.GetArrayLength(); i++)
            {
                Assert.IsFalse(ops[i].TryGetProperty(DistributedTransactionSerializer.ETag, out _),
                    $"Operation[{i}] without IfMatchEtag must NOT include an 'etag' field.");
            }
        }

        [TestMethod]
        [Description("CreateItem with a null id must throw ArgumentNullException at the call site.")]
        public void CreateItem_NullId_ThrowsArgumentNullException()
        {
            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(this.BuildContextSetup().Object);
            Assert.ThrowsException<ArgumentNullException>(() =>
                tx.CreateItem(Database, Container, new PartitionKey("pk"), id: null, new TestItem("body-id")));
        }

        [TestMethod]
        [Description("UpsertItem with an empty id must throw ArgumentNullException at the call site.")]
        public void UpsertItem_EmptyId_ThrowsArgumentNullException()
        {
            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(this.BuildContextSetup().Object);
            Assert.ThrowsException<ArgumentNullException>(() =>
                tx.UpsertItem(Database, Container, new PartitionKey("pk"), id: string.Empty, new TestItem("body-id")));
        }

        [TestMethod]
        [Description("CreateItemStream with a null id must throw ArgumentNullException at the call site.")]
        public void CreateItemStream_NullId_ThrowsArgumentNullException()
        {
            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(this.BuildContextSetup().Object);
            using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(@"{""value"":1}"));
            Assert.ThrowsException<ArgumentNullException>(() =>
                tx.CreateItemStream(Database, Container, new PartitionKey("pk"), id: null, stream));
        }

        [TestMethod]
        [Description("UpsertItemStream with a whitespace id must throw ArgumentNullException at the call site.")]
        public void UpsertItemStream_WhitespaceId_ThrowsArgumentNullException()
        {
            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(this.BuildContextSetup().Object);
            using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(@"{""value"":1}"));
            Assert.ThrowsException<ArgumentNullException>(() =>
                tx.UpsertItemStream(Database, Container, new PartitionKey("pk"), id: "   ", stream));
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

        private async Task<string> CaptureReadCommitBodyAsync(
            Func<DistributedReadTransaction, DistributedReadTransaction> buildTransaction,
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

            DistributedReadTransaction tx = new DistributedReadTransactionCore(contextMock.Object);
            await buildTransaction(tx).CommitTransactionAsync(CancellationToken.None);

            Assert.IsNotNull(capturedJson, "The commit body was not captured — the mock was not invoked.");
            return capturedJson;
        }

        
        private Mock<CosmosClientContext> BuildContextSetup()
        {
            ContainerProperties containerProps = ContainerProperties.CreateWithResourceId("ccZ1ANCszwk=");
            containerProps.PartitionKeyPath = "/pk";

            MockDocumentClient documentClient = new MockDocumentClient
            {
                sessionContainer = new SessionContainer("testhost")
            };

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
