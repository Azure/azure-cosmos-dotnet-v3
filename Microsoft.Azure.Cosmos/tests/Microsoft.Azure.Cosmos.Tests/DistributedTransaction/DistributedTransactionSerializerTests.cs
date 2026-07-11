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
        private const string DatabaseName = "testDb";
        private const string ContainerName = "testContainer";

        // id field presence per operation type

        [TestMethod]
        [Description("CreateItem sets id explicitly; 'id' must be present in the serialized JSON with the correct value.")]
        public async Task CreateItem_SerializedBody_HasResourceBody_AndIdField()
        {
            const string itemId = "create-item";
            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.CreateItem(BuildMockContainer(), new PartitionKey("pk"), itemId, new TestItem(itemId)));

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
                tx.ReplaceItem(BuildMockContainer(), new PartitionKey("pk"), itemId, new TestItem(itemId)));

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
                tx.DeleteItem(BuildMockContainer(), new PartitionKey("pk"), itemId));

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
                tx.UpsertItem(BuildMockContainer(), new PartitionKey("pk"), itemId, new TestItem(itemId)));

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
                tx.ReplaceItem(BuildMockContainer(), new PartitionKey("pk"), expectedId, new TestItem(expectedId)));

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
                tx.DeleteItem(BuildMockContainer(), new PartitionKey("pk"), expectedId));

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
                tx.CreateItem(BuildMockContainer(), new PartitionKey("pk"), "json-check", new TestItem("json-check")));

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
                tx.CreateItem(BuildMockContainer(), new PartitionKey("pk"), "c", new TestItem("c"))
                  .ReplaceItem(BuildMockContainer(), new PartitionKey("pk"), "r", new TestItem("r"))
                  .DeleteItem(BuildMockContainer(), new PartitionKey("pk"), "d")
                  .UpsertItem(BuildMockContainer(), new PartitionKey("pk"), "u", new TestItem("u"))
                  .PatchItem(BuildMockContainer(), new PartitionKey("pk"), "p", patchOps),
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
                tx.CreateItem(BuildMockContainer(), new PartitionKey("pk"), "c", new TestItem("c"))
                  .ReplaceItem(BuildMockContainer(), new PartitionKey("pk"), "r", new TestItem("r"))
                  .DeleteItem(BuildMockContainer(), new PartitionKey("pk"), "d")
                  .UpsertItem(BuildMockContainer(), new PartitionKey("pk"), "u", new TestItem("u"))
                  .PatchItem(BuildMockContainer(), new PartitionKey("pk"), "p", patchOps),
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
                tx.ReplaceItem(BuildMockContainer(), new PartitionKey("pk"), itemId, new TestItem(itemId)));

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
                tx => tx.CreateItemStream(BuildMockContainer(), new PartitionKey("pk"), itemId, stream));

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
                tx => tx.ReplaceItemStream(BuildMockContainer(), new PartitionKey("pk"), itemId, stream));

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
                tx => tx.PatchItemStream(BuildMockContainer(), new PartitionKey("pk"), itemId, stream));

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
                tx => tx.UpsertItemStream(BuildMockContainer(), new PartitionKey("pk"), itemId, stream));

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
        [Description("ReadItem with IfNoneMatchEtag set must serialize an 'ifNoneMatch' field in the operation JSON.")]
        public async Task ReadItem_WithIfNoneMatchEtag_SerializesEtagField()
        {
            const string etag = "\"test-etag\"";
            const string itemId = "etag-read-id";

            string capturedJson = await this.CaptureReadCommitBodyAsync(tx =>
                tx.ReadItem(BuildMockContainer(), new PartitionKey("pk"), itemId,
                    new DistributedTransactionRequestOptions { IfNoneMatchEtag = etag }));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.IfNoneMatch, out JsonElement etagElement),
                "Read operation with IfNoneMatchEtag must include an 'ifNoneMatch' field.");
            Assert.AreEqual(etag, etagElement.GetString());
            Assert.IsFalse(op.TryGetProperty(DistributedTransactionSerializer.IfMatch, out _),
                "Read operation with only IfNoneMatchEtag must not include an 'ifMatch' field.");
        }

        [TestMethod]
        [Description("ReadItem without any etag options must not include either conditional field in the serialized JSON.")]
        public async Task ReadItem_WithoutEtag_DoesNotIncludeEtagField()
        {
            string capturedJson = await this.CaptureReadCommitBodyAsync(tx =>
                tx.ReadItem(BuildMockContainer(), new PartitionKey("pk"), "read-no-etag"));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsFalse(op.TryGetProperty(DistributedTransactionSerializer.IfMatch, out _),
                "Read operation without etag options must NOT include an 'ifMatch' field.");
            Assert.IsFalse(op.TryGetProperty(DistributedTransactionSerializer.IfNoneMatch, out _),
                "Read operation without etag options must NOT include an 'ifNoneMatch' field.");
        }

        [TestMethod]
        [Description("ReadItem with IfMatchEtag set must serialize an 'ifMatch' field in the operation JSON.")]
        public async Task ReadItem_WithIfMatchEtag_SerializesIfMatchField()
        {
            const string etag = "\"write-etag\"";
            const string itemId = "read-ifmatch-id";

            string capturedJson = await this.CaptureReadCommitBodyAsync(tx =>
                tx.ReadItem(BuildMockContainer(), new PartitionKey("pk"), itemId,
                    new DistributedTransactionRequestOptions { IfMatchEtag = etag }));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.IfMatch, out JsonElement etagElement),
                "Read operation with IfMatchEtag must include an 'ifMatch' field.");
            Assert.AreEqual(etag, etagElement.GetString());
            Assert.IsFalse(op.TryGetProperty(DistributedTransactionSerializer.IfNoneMatch, out _),
                "Read operation with only IfMatchEtag must not include an 'ifNoneMatch' field.");
        }

        // IfMatchEtag

        [TestMethod]
        [Description("ReplaceItem with IfMatchEtag set must serialize an 'ifMatch' field in the operation JSON.")]
        public async Task ReplaceItem_WithIfMatchEtag_SerializesEtagField()
        {
            const string etag = "\"test-etag\"";
            const string itemId = "etag-replace-id";

            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.ReplaceItem(BuildMockContainer(), new PartitionKey("pk"), itemId, new TestItem(itemId),
                    new DistributedTransactionRequestOptions { IfMatchEtag = etag }));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.IfMatch, out JsonElement etagElement),
                "Replace operation with IfMatchEtag must include an 'ifMatch' field.");
            Assert.AreEqual(etag, etagElement.GetString());
            Assert.IsFalse(op.TryGetProperty(DistributedTransactionSerializer.IfNoneMatch, out _),
                "Replace operation with only IfMatchEtag must not include an 'ifNoneMatch' field.");
        }

        [TestMethod]
        [Description("DeleteItem with IfMatchEtag set must serialize an 'ifMatch' field in the operation JSON.")]
        public async Task DeleteItem_WithIfMatchEtag_SerializesEtagField()
        {
            const string etag = "\"test-etag\"";
            const string itemId = "etag-delete-id";

            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.DeleteItem(BuildMockContainer(), new PartitionKey("pk"), itemId,
                    new DistributedTransactionRequestOptions { IfMatchEtag = etag }));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.IfMatch, out JsonElement etagElement),
                "Delete operation with IfMatchEtag must include an 'ifMatch' field.");
            Assert.AreEqual(etag, etagElement.GetString());
            Assert.IsFalse(op.TryGetProperty(DistributedTransactionSerializer.IfNoneMatch, out _),
                "Delete operation with only IfMatchEtag must not include an 'ifNoneMatch' field.");
        }

        [TestMethod]
        [Description("PatchItem with IfMatchEtag set must serialize an 'ifMatch' field in the operation JSON.")]
        public async Task PatchItem_WithIfMatchEtag_SerializesEtagField()
        {
            const string etag = "\"test-etag\"";
            const string itemId = "etag-patch-id";

            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.PatchItem(BuildMockContainer(), new PartitionKey("pk"), itemId,
                    new[] { PatchOperation.Add("/value", "v") },
                    new DistributedTransactionPatchItemRequestOptions { IfMatchEtag = etag }));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.IfMatch, out JsonElement etagElement),
                "Patch operation with IfMatchEtag must include an 'ifMatch' field.");
            Assert.AreEqual(etag, etagElement.GetString());
            Assert.IsFalse(op.TryGetProperty(DistributedTransactionSerializer.IfNoneMatch, out _),
                "Patch operation with only IfMatchEtag must not include an 'ifNoneMatch' field.");
        }

        [TestMethod]
        [Description("PatchItem with FilterPredicate set must serialize a 'condition' field inside the patch resourceBody.")]
        public async Task PatchItem_WithFilterPredicate_SerializesConditionInResourceBody()
        {
            const string itemId = "filter-patch-id";
            const string predicate = "from c where c.taskNum = 3";

            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.PatchItem(BuildMockContainer(), new PartitionKey("pk"), itemId,
                    new[] { PatchOperation.Add("/value", "v") },
                    new DistributedTransactionPatchItemRequestOptions { FilterPredicate = predicate }));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.ResourceBody, out JsonElement resourceBody),
                "Patch operation must include a 'resourceBody' field.");
            Assert.IsTrue(resourceBody.TryGetProperty(PatchConstants.PatchSpecAttributes.Condition, out JsonElement conditionElement),
                "Patch resourceBody must include a 'condition' field when FilterPredicate is set.");
            Assert.AreEqual(predicate, conditionElement.GetString(),
                "The 'condition' field must match the FilterPredicate passed to PatchItem.");
        }

        [TestMethod]
        [Description("PatchItem without a FilterPredicate must not serialize a 'condition' field inside the patch resourceBody.")]
        public async Task PatchItem_WithoutFilterPredicate_DoesNotSerializeConditionInResourceBody()
        {
            const string itemId = "no-filter-patch-id";

            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.PatchItem(BuildMockContainer(), new PartitionKey("pk"), itemId,
                    new[] { PatchOperation.Add("/value", "v") }));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.ResourceBody, out JsonElement resourceBody),
                "Patch operation must include a 'resourceBody' field.");
            Assert.IsFalse(resourceBody.TryGetProperty(PatchConstants.PatchSpecAttributes.Condition, out _),
                "Patch resourceBody must NOT include a 'condition' field when no FilterPredicate is set.");
        }

        [TestMethod]
        [Description("PatchItem with both FilterPredicate and IfMatchEtag set must serialize the operation-level 'ifMatch' field AND the 'condition' field inside the patch resourceBody.")]
        public async Task PatchItem_WithFilterPredicateAndIfMatchEtag_SerializesBoth()
        {
            const string itemId = "filter-and-etag-patch-id";
            const string predicate = "from c where c.status = 'pending'";
            const string etag = "\"test-etag\"";

            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.PatchItem(BuildMockContainer(), new PartitionKey("pk"), itemId,
                    new[] { PatchOperation.Replace("/status", "done") },
                    new DistributedTransactionPatchItemRequestOptions
                    {
                        FilterPredicate = predicate,
                        IfMatchEtag = etag
                    }));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            // Operation-level ifMatch (inherited from the base RequestOptions.IfMatchEtag).
            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.IfMatch, out JsonElement etagElement),
                "Patch operation with IfMatchEtag must include an 'ifMatch' field.");
            Assert.AreEqual(etag, etagElement.GetString());

            // Body-level condition (from the patch-specific FilterPredicate) coexists on the same operation.
            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.ResourceBody, out JsonElement resourceBody),
                "Patch operation must include a 'resourceBody' field.");
            Assert.IsTrue(resourceBody.TryGetProperty(PatchConstants.PatchSpecAttributes.Condition, out JsonElement conditionElement),
                "Patch resourceBody must include a 'condition' field when FilterPredicate is set.");
            Assert.AreEqual(predicate, conditionElement.GetString());
        }

        [TestMethod]
        [Description("Operations without conditional ETags must not include 'ifMatch' or 'ifNoneMatch' fields in serialized JSON.")]
        public async Task Operations_WithoutConditionalEtags_DoNotIncludeConditionalFields()
        {
            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.CreateItem(BuildMockContainer(), new PartitionKey("pk"), "create-no-etag", new TestItem("create-no-etag"))
                  .ReplaceItem(BuildMockContainer(), new PartitionKey("pk"), "replace-no-etag", new TestItem("replace-no-etag")),
                expectedResultCount: 2);

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement ops = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations);

            for (int i = 0; i < ops.GetArrayLength(); i++)
            {
                Assert.IsFalse(ops[i].TryGetProperty(DistributedTransactionSerializer.IfMatch, out _),
                    $"Operation[{i}] without conditional etags must NOT include an 'ifMatch' field.");
                Assert.IsFalse(ops[i].TryGetProperty(DistributedTransactionSerializer.IfNoneMatch, out _),
                    $"Operation[{i}] without conditional etags must NOT include an 'ifNoneMatch' field.");
            }
        }

        [TestMethod]
        [Description("An operation with both IfMatchEtag and IfNoneMatchEtag must serialize both wire fields.")]
        public async Task ReadItem_WithBothConditionalEtags_SerializesBothFields()
        {
            const string ifMatch = "\"if-match-etag\"";
            const string ifNoneMatch = "\"if-none-match-etag\"";

            string capturedJson = await this.CaptureReadCommitBodyAsync(tx =>
                tx.ReadItem(BuildMockContainer(), new PartitionKey("pk"), "read-both-etags",
                    new DistributedTransactionRequestOptions
                    {
                        IfMatchEtag = ifMatch,
                        IfNoneMatchEtag = ifNoneMatch
                    }));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.IfMatch, out JsonElement ifMatchElement),
                "Read operation with IfMatchEtag must include an 'ifMatch' field.");
            Assert.AreEqual(ifMatch, ifMatchElement.GetString());

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.IfNoneMatch, out JsonElement ifNoneMatchElement),
                "Read operation with IfNoneMatchEtag must include an 'ifNoneMatch' field.");
            Assert.AreEqual(ifNoneMatch, ifNoneMatchElement.GetString());
        }

        [TestMethod]
        [Description("A write operation (ReplaceItem) with IfNoneMatchEtag set must serialize an 'ifNoneMatch' field in the operation JSON.")]
        public async Task ReplaceItem_WithIfNoneMatchEtag_SerializesIfNoneMatchField()
        {
            const string etag = "\"test-etag\"";
            const string itemId = "etag-replace-ifnonematch-id";

            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.ReplaceItem(BuildMockContainer(), new PartitionKey("pk"), itemId, new TestItem(itemId),
                    new DistributedTransactionRequestOptions { IfNoneMatchEtag = etag }));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.IfNoneMatch, out JsonElement etagElement),
                "Replace operation with IfNoneMatchEtag must include an 'ifNoneMatch' field.");
            Assert.AreEqual(etag, etagElement.GetString());
            Assert.IsFalse(op.TryGetProperty(DistributedTransactionSerializer.IfMatch, out _),
                "Replace operation with only IfNoneMatchEtag must not include an 'ifMatch' field.");
        }

        // partitionKey wire serialization (PartitionKey#Json)

        [DataTestMethod]
        [DataRow("string")]
        [DataRow("number")]
        [DataRow("bool")]
        [Description("The 'partitionKey' field carries the partition key as the single-element array wire form, with the component value preserved per JSON value kind.")]
        public async Task CreateItem_SerializedBody_PartitionKey_SerializedAsArrayWithValue(string pkKind)
        {
            PartitionKey pk = pkKind switch
            {
                "string" => new PartitionKey("pk-value"),
                "number" => new PartitionKey(42.5),
                "bool" => new PartitionKey(true),
                _ => throw new ArgumentOutOfRangeException(nameof(pkKind)),
            };

            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.CreateItem(BuildMockContainer(), pk, "pk-json-id", new TestItem("pk-json-id")));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.PartitionKey, out JsonElement pkElement),
                "Operation must include a 'partitionKey' field carrying the routing key.");
            Assert.AreEqual(JsonValueKind.Array, pkElement.ValueKind,
                "The DTX wire partition key is a JSON array.");
            Assert.AreEqual(1, pkElement.GetArrayLength(),
                "A single-path partition key serializes to a one-element array.");

            JsonElement component = pkElement[0];
            switch (pkKind)
            {
                case "string":
                    Assert.AreEqual(JsonValueKind.String, component.ValueKind);
                    Assert.AreEqual("pk-value", component.GetString());
                    break;
                case "number":
                    Assert.AreEqual(JsonValueKind.Number, component.ValueKind);
                    Assert.AreEqual(42.5, component.GetDouble());
                    break;
                case "bool":
                    Assert.AreEqual(JsonValueKind.True, component.ValueKind);
                    Assert.IsTrue(component.GetBoolean());
                    break;
            }
        }

        [TestMethod]
        [Description("PartitionKey.Null serializes the 'partitionKey' field as a one-element array containing a JSON null.")]
        public async Task CreateItem_SerializedBody_NullPartitionKey_SerializedAsArrayWithJsonNull()
        {
            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.CreateItem(BuildMockContainer(), PartitionKey.Null, "pk-null-id", new TestItem("pk-null-id")));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.PartitionKey, out JsonElement pkElement),
                "Operation must include a 'partitionKey' field for PartitionKey.Null.");
            Assert.AreEqual(JsonValueKind.Array, pkElement.ValueKind);
            Assert.AreEqual(1, pkElement.GetArrayLength());
            Assert.AreEqual(JsonValueKind.Null, pkElement[0].ValueKind,
                "PartitionKey.Null serializes as a one-element array holding a JSON null.");
        }

        [TestMethod]
        [Description("DeleteItem has no resource body but must still serialize the routing 'partitionKey' field with the correct value.")]
        public async Task DeleteItem_SerializedBody_IncludesPartitionKey()
        {
            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.DeleteItem(BuildMockContainer(), new PartitionKey("del-pk"), "del-id"));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.PartitionKey, out JsonElement pkElement),
                "Delete operation must still carry a 'partitionKey' for routing.");
            Assert.AreEqual(JsonValueKind.Array, pkElement.ValueKind);
            Assert.AreEqual("del-pk", pkElement[0].GetString());
        }

        [TestMethod]
        [Description("CreateItem with PartitionKey.None still emits a 'partitionKey' field carrying the None JSON sentinel array (it is NOT omitted).")]
        public async Task CreateItem_SerializedBody_NonePartitionKey_EmitsSentinelArray()
        {
            string capturedJson = await this.CaptureCommitBodyAsync(tx =>
                tx.CreateItem(BuildMockContainer(), PartitionKey.None, "pk-none-id", new TestItem("pk-none-id")));

            using JsonDocument doc = JsonDocument.Parse(capturedJson);
            JsonElement op = doc.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.IsTrue(op.TryGetProperty(DistributedTransactionSerializer.PartitionKey, out JsonElement pkElement),
                "PartitionKey.None is serialized via its JSON sentinel, so the 'partitionKey' field is present (not omitted).");
            Assert.AreEqual(JsonValueKind.Array, pkElement.ValueKind,
                "The None partition key serializes as a JSON array sentinel.");
        }

        [TestMethod]
        [Description("CreateItem with a null id must throw ArgumentNullException at the call site.")]
        public void CreateItem_NullId_ThrowsArgumentNullException()
        {
            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(this.BuildContextSetup().Object);
            Assert.ThrowsException<ArgumentNullException>(() =>
                tx.CreateItem(BuildMockContainer(), new PartitionKey("pk"), id: null, new TestItem("body-id")));
        }

        [TestMethod]
        [Description("UpsertItem with an empty id must throw ArgumentNullException at the call site.")]
        public void UpsertItem_EmptyId_ThrowsArgumentNullException()
        {
            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(this.BuildContextSetup().Object);
            Assert.ThrowsException<ArgumentNullException>(() =>
                tx.UpsertItem(BuildMockContainer(), new PartitionKey("pk"), id: string.Empty, new TestItem("body-id")));
        }

        [TestMethod]
        [Description("CreateItemStream with a null id must throw ArgumentNullException at the call site.")]
        public void CreateItemStream_NullId_ThrowsArgumentNullException()
        {
            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(this.BuildContextSetup().Object);
            using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(@"{""value"":1}"));
            Assert.ThrowsException<ArgumentNullException>(() =>
                tx.CreateItemStream(BuildMockContainer(), new PartitionKey("pk"), id: null, stream));
        }

        [TestMethod]
        [Description("UpsertItemStream with a whitespace id must throw ArgumentNullException at the call site.")]
        public void UpsertItemStream_WhitespaceId_ThrowsArgumentNullException()
        {
            DistributedWriteTransaction tx = new DistributedWriteTransactionCore(this.BuildContextSetup().Object);
            using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(@"{""value"":1}"));
            Assert.ThrowsException<ArgumentNullException>(() =>
                tx.UpsertItemStream(BuildMockContainer(), new PartitionKey("pk"), id: "   ", stream));
        }

        // Helpers

        /// <summary>
        /// Builds a mock <see cref="Cosmos.Container"/> that returns <see cref="DatabaseName"/>
        /// and <see cref="ContainerName"/> from its <see cref="Cosmos.Container.Database"/>/<see cref="Cosmos.Container.Id"/>
        /// accessors.
        /// </summary>
        private static Cosmos.Container BuildMockContainer(
            string databaseId = DatabaseName,
            string containerId = ContainerName)
        {
            Mock<Cosmos.Database> databaseMock = new Mock<Cosmos.Database>();
            databaseMock.Setup(d => d.Id).Returns(databaseId);

            Mock<Cosmos.Container> containerMock = new Mock<Cosmos.Container>();
            containerMock.Setup(c => c.Id).Returns(containerId);
            containerMock.Setup(c => c.Database).Returns(databaseMock.Object);

            return containerMock.Object;
        }

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
