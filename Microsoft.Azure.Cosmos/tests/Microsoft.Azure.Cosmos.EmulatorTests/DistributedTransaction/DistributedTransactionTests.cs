// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using PartitionKey = Cosmos.PartitionKey;

    /// <summary>
    /// Scenario tests for <see cref="DistributedWriteTransaction"/>.
    ///
    /// These tests use a <see cref="DistributedTransactionMockHandler"/> to intercept the DTC
    /// commit request at the handler level while letting all other requests (container creation,
    /// RID resolution) flow to the real emulator. This lets us verify the full request/response
    /// cycle — serialization, response parsing, idempotency semantics — without requiring the
    /// emulator to natively support distributed transactions.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    [TestCategory("DistributedTransaction")]
    public class DistributedTransactionTests : BaseCosmosClientHelper
    {
        private const string IdempotencyTokenHeader = HttpConstants.HttpHeaders.IdempotencyToken;
        private const string PartitionKeyPath = "/pk";

        private Container container;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await this.TestInit();

            ContainerResponse containerResponse = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKeyPath),
                cancellationToken: this.cancellationToken);

            this.container = containerResponse.Container;
        }

        [TestCleanup]
        public new async Task TestCleanup()
        {
            await base.TestCleanup();
        }

        // Happy path scenarios

        [TestMethod]
        [Description("Two creates against the same container both return 201 Created.")]
        public async Task CreateItems_SameContainer_AllReturnCreatedStatus()
        {
            ToDoActivity doc1 = ToDoActivity.CreateRandomToDoActivity();
            ToDoActivity doc2 = ToDoActivity.CreateRandomToDoActivity();

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.OK, BuildSuccessResponseJson(2))));

            using CosmosClient client = this.CreateMockClient(handler);

            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(doc1.pk), doc1)
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(doc2.pk), doc2)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.AreEqual(2, response.Count);
            Assert.AreEqual(HttpStatusCode.Created, response[0].StatusCode);
            Assert.AreEqual(HttpStatusCode.Created, response[1].StatusCode);

            response.Dispose();
        }

        [TestMethod]
        [Description("Create, Replace, and Delete operations are all serialized with the correct operationType values.")]
        public async Task MixedOperations_AllOperationsAreSerialized()
        {
            ToDoActivity createDoc = ToDoActivity.CreateRandomToDoActivity();
            ToDoActivity replaceDoc = ToDoActivity.CreateRandomToDoActivity();

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.OK, BuildSuccessResponseJson(3))));

            using CosmosClient client = this.CreateMockClient(handler);

            await client.CreateDistributedWriteTransaction()
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(createDoc.pk), createDoc)
                .ReplaceItem(this.database.Id, this.container.Id, new PartitionKey(replaceDoc.pk), replaceDoc.id, replaceDoc)
                .DeleteItem(this.database.Id, this.container.Id, new PartitionKey("delete-pk"), "delete-id")
                .CommitTransactionAsync(CancellationToken.None);

            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement ops = requestJson.RootElement.GetProperty("operations");

            Assert.AreEqual(3, ops.GetArrayLength());
            Assert.AreEqual(OperationType.Create.ToString(), ops[0].GetProperty("operationType").GetString());
            Assert.AreEqual(OperationType.Replace.ToString(), ops[1].GetProperty("operationType").GetString());
            Assert.AreEqual(OperationType.Delete.ToString(), ops[2].GetProperty("operationType").GetString());
        }

        [TestMethod]
        [Description("Upsert alongside a create is serialized as an Upsert operation.")]
        public async Task UpsertItem_IncludedInTransaction_SerializesAsUpsertOperation()
        {
            ToDoActivity createDoc = ToDoActivity.CreateRandomToDoActivity();
            ToDoActivity upsertDoc = ToDoActivity.CreateRandomToDoActivity();

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.OK, BuildSuccessResponseJson(2))));

            using CosmosClient client = this.CreateMockClient(handler);

            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(createDoc.pk), createDoc)
                .UpsertItem(this.database.Id, this.container.Id, new PartitionKey(upsertDoc.pk), upsertDoc)
                .CommitTransactionAsync(CancellationToken.None);

            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement ops = requestJson.RootElement.GetProperty("operations");

            Assert.AreEqual(2, ops.GetArrayLength());
            Assert.AreEqual(OperationType.Upsert.ToString(), ops[1].GetProperty("operationType").GetString());
            Assert.AreEqual(2, response.Count);

            response.Dispose();
        }

        [TestMethod]
        [Description("Patch operation is serialized and included in the transaction.")]
        public async Task PatchItem_WithAddOperation_IncludedInTransaction()
        {
            ToDoActivity createDoc = ToDoActivity.CreateRandomToDoActivity();
            IReadOnlyList<PatchOperation> patchOps = new[] { PatchOperation.Add("/description", "patched") };

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.OK, BuildSuccessResponseJson(2))));

            using CosmosClient client = this.CreateMockClient(handler);

            await client.CreateDistributedWriteTransaction()
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(createDoc.pk), createDoc)
                .PatchItem(this.database.Id, this.container.Id, new PartitionKey("patch-pk"), "item-to-patch", patchOps)
                .CommitTransactionAsync(CancellationToken.None);

            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement ops = requestJson.RootElement.GetProperty("operations");

            Assert.AreEqual(2, ops.GetArrayLength());
            Assert.AreEqual(OperationType.Patch.ToString(), ops[1].GetProperty("operationType").GetString());
        }

        [TestMethod]
        [Description("Operations targeting two different containers are both serialized with their respective container names.")]
        public async Task CrossContainer_TwoDifferentContainers_AllOperationsCommitted()
        {
            ContainerResponse secondContainerResponse = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKeyPath),
                cancellationToken: this.cancellationToken);

            Container secondContainer = secondContainerResponse.Container;

            ToDoActivity doc1 = ToDoActivity.CreateRandomToDoActivity();
            ToDoActivity doc2 = ToDoActivity.CreateRandomToDoActivity();

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.OK, BuildSuccessResponseJson(2))));

            using CosmosClient client = this.CreateMockClient(handler);

            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(doc1.pk), doc1)
                .CreateItem(this.database.Id, secondContainer.Id, new PartitionKey(doc2.pk), doc2)
                .CommitTransactionAsync(CancellationToken.None);

            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement ops = requestJson.RootElement.GetProperty("operations");

            Assert.AreEqual(2, ops.GetArrayLength());
            Assert.AreNotEqual(
                ops[0].GetProperty("collectionName").GetString(),
                ops[1].GetProperty("collectionName").GetString(),
                "Operations should reference different containers.");
            Assert.AreEqual(2, response.Count);

            response.Dispose();
        }

        // Response properties

        [TestMethod]
        [Description("The idempotency token sent in the request header is echoed back in the response.")]
        public async Task CommitAsync_ResponseContainsIdempotencyToken()
        {
            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(request =>
            {
                string token = request.Headers[IdempotencyTokenHeader] ?? Guid.NewGuid().ToString();
                ResponseMessage mockResponse = this.BuildMockResponse(HttpStatusCode.OK, BuildSuccessResponseJson(1));
                mockResponse.Headers[IdempotencyTokenHeader] = token;
                return Task.FromResult(mockResponse);
            });

            using CosmosClient client = this.CreateMockClient(handler);
            ToDoActivity doc = ToDoActivity.CreateRandomToDoActivity();

            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(doc.pk), doc)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreNotEqual(Guid.Empty, response.IdempotencyToken, "Response must carry the idempotency token.");

            response.Dispose();
        }

        [TestMethod]
        [Description("Each operation result's Index matches its position in the request operation list.")]
        public async Task EachResult_HasIndex_MatchingOperationOrder()
        {
            ToDoActivity doc1 = ToDoActivity.CreateRandomToDoActivity();
            ToDoActivity doc2 = ToDoActivity.CreateRandomToDoActivity();
            ToDoActivity doc3 = ToDoActivity.CreateRandomToDoActivity();

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.OK, BuildSuccessResponseJson(3))));

            using CosmosClient client = this.CreateMockClient(handler);

            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(doc1.pk), doc1)
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(doc2.pk), doc2)
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(doc3.pk), doc3)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual(3, response.Count);
            for (int i = 0; i < response.Count; i++)
            {
                Assert.AreEqual(i, response[i].Index, $"Result at position {i} should have Index = {i}.");
            }

            response.Dispose();
        }

        [TestMethod]
        [Description("When the server includes a resource body, it is accessible as a readable stream on the result.")]
        public async Task SuccessfulCreate_ResponseContainsResourceBody()
        {
            ToDoActivity expectedDoc = ToDoActivity.CreateRandomToDoActivity();
            string resourceBodyJson = JsonSerializer.Serialize(expectedDoc);

            string mockResponseJson = $@"{{
                ""operationResponses"": [{{
                    ""index"": 0,
                    ""statusCode"": 201,
                    ""etag"": ""\""test-etag\"""",
                    ""resourceBody"": {resourceBodyJson}
                }}]
            }}";

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.OK, mockResponseJson)));

            using CosmosClient client = this.CreateMockClient(handler);

            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(expectedDoc.pk), expectedDoc)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.Created, response[0].StatusCode);
            Assert.AreEqual("\"test-etag\"", response[0].ETag);
            Assert.IsNotNull(response[0].ResourceStream, "Resource stream should be populated when resourcebody is present.");

            using StreamReader reader = new StreamReader(response[0].ResourceStream);
            ToDoActivity returnedDoc = JsonSerializer.Deserialize<ToDoActivity>(await reader.ReadToEndAsync());

            Assert.AreEqual(expectedDoc.id, returnedDoc.id);
            Assert.AreEqual(expectedDoc.pk, returnedDoc.pk);

            response.Dispose();
        }

        // Error handling

        [TestMethod]
        [Description("A 409 Conflict response marks the transaction and the failing operation as not successful.")]
        public async Task ConflictResponse_ReturnsFailureStatus()
        {
            string mockErrorJson = @"{
                ""operationResponses"": [{
                    ""index"": 0,
                    ""statusCode"": 409,
                    ""subStatusCode"": 0
                }]
            }";

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.Conflict, mockErrorJson)));

            using CosmosClient client = this.CreateMockClient(handler);
            ToDoActivity doc = ToDoActivity.CreateRandomToDoActivity();

            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(doc.pk), doc)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
            Assert.IsFalse(response.IsSuccessStatusCode);
            Assert.AreEqual(1, response.Count);
            Assert.AreEqual(HttpStatusCode.Conflict, response[0].StatusCode);

            response.Dispose();
        }

        [TestMethod]
        [Description("A 404 Not Found on a replace operation marks the transaction as failed.")]
        public async Task NotFoundResponse_OnReplaceItem_ReturnsFailureStatus()
        {
            string mockErrorJson = @"{
                ""operationResponses"": [{
                    ""index"": 0,
                    ""statusCode"": 404,
                    ""subStatusCode"": 0
                }]
            }";

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.NotFound, mockErrorJson)));

            using CosmosClient client = this.CreateMockClient(handler);
            ToDoActivity doc = ToDoActivity.CreateRandomToDoActivity();

            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .ReplaceItem(this.database.Id, this.container.Id, new PartitionKey(doc.pk), doc.id, doc)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
            Assert.IsFalse(response.IsSuccessStatusCode);
            Assert.AreEqual(HttpStatusCode.NotFound, response[0].StatusCode);

            response.Dispose();
        }

        [TestMethod]
        [Description("A 207 MultiStatus response promotes the first failing operation's status code and all results are present.")]
        public async Task MultiStatusResponse_PartialFailure_AllResultsPresent()
        {
            // One success (index 0) and one failure (index 1) → MultiStatus 207
            string mockMultiStatusJson = @"{
                ""operationResponses"": [
                    { ""index"": 0, ""statusCode"": 201 },
                    { ""index"": 1, ""statusCode"": 409, ""subStatusCode"": 0 }
                ]
            }";

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse((HttpStatusCode)207, mockMultiStatusJson)));

            using CosmosClient client = this.CreateMockClient(handler);
            ToDoActivity doc1 = ToDoActivity.CreateRandomToDoActivity();
            ToDoActivity doc2 = ToDoActivity.CreateRandomToDoActivity();

            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(doc1.pk), doc1)
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(doc2.pk), doc2)
                .CommitTransactionAsync(CancellationToken.None);

            // All results must be present regardless of partial failure
            Assert.AreEqual(2, response.Count, "Response must contain a result for every operation.");
            Assert.IsFalse(response.IsSuccessStatusCode, "Partial failure should make the overall response unsuccessful.");

            response.Dispose();
        }

        // Serialization

        [TestMethod]
        [Description("All required fields are present with the correct JSON value kind across Create, Replace, and Delete operations; optional fields that appear also have the correct kind.")]
        public async Task SerializedRequest_AllOperations_CorrectFieldTypes()
        {
            ToDoActivity createDoc = ToDoActivity.CreateRandomToDoActivity();
            ToDoActivity replaceDoc = ToDoActivity.CreateRandomToDoActivity();

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.OK, BuildSuccessResponseJson(3))));

            using CosmosClient client = this.CreateMockClient(handler);

            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(createDoc.pk), createDoc)
                .ReplaceItem(this.database.Id, this.container.Id, new PartitionKey(replaceDoc.pk), replaceDoc.id, replaceDoc)
                .DeleteItem(this.database.Id, this.container.Id, new PartitionKey("delete-pk"), "delete-id")
                .CommitTransactionAsync(CancellationToken.None);

            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);

            Assert.AreEqual(JsonValueKind.Object, requestJson.RootElement.ValueKind, "Root element should be an object");
            Assert.IsTrue(requestJson.RootElement.TryGetProperty("operations", out JsonElement operations), "operations property should exist");
            Assert.AreEqual(JsonValueKind.Array, operations.ValueKind, "operations should be an array");
            Assert.AreEqual(3, operations.GetArrayLength(), "operations should have 3 elements");

            int operationIndex = 0;
            foreach (JsonElement operation in operations.EnumerateArray())
            {
                Assert.AreEqual(JsonValueKind.Object, operation.ValueKind, $"Operation {operationIndex} should be an object");

                (string Property, JsonValueKind Kind)[] requiredFields =
                {
                    ("databaseName", JsonValueKind.String),
                    ("collectionName", JsonValueKind.String),
                    ("collectionResourceId", JsonValueKind.String),
                    ("databaseResourceId", JsonValueKind.String),
                    ("partitionKey", JsonValueKind.Array),
                    ("index", JsonValueKind.Number),
                    ("operationType", JsonValueKind.String),
                    ("resourceType", JsonValueKind.String)
                };

                foreach ((string property, JsonValueKind expectedKind) in requiredFields)
                {
                    this.ValidateValueKind(operation, property, expectedKind, operationIndex, isRequired: true);
                }

                (string Property, JsonValueKind Kind)[] optionalFields =
                {
                    ("id", JsonValueKind.String),
                    ("resourceBody", JsonValueKind.Object),
                    ("sessionToken", JsonValueKind.String),
                    ("etag", JsonValueKind.String),
                };

                foreach ((string property, JsonValueKind expectedKind) in optionalFields)
                {
                    this.ValidateValueKind(operation, property, expectedKind, operationIndex, isRequired: false);
                }

                operationIndex++;
            }

            response.Dispose();
        }

        // ETag conditions

        [TestMethod]
        [Description("A replace operation with IfMatchEtag set serializes the etag field to the request.")]
        public async Task ReplaceItem_WithIfMatchEtag_EtagSerializedToRequest()
        {
            ToDoActivity doc = ToDoActivity.CreateRandomToDoActivity();
            string expectedEtag = "\"test-etag-replace\"";

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.OK, BuildSuccessResponseJson(1))));

            using CosmosClient client = this.CreateMockClient(handler);

            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .ReplaceItem(
                    this.database.Id,
                    this.container.Id,
                    new PartitionKey(doc.pk),
                    doc.id,
                    doc,
                    new DistributedTransactionRequestOptions { IfMatchEtag = expectedEtag })
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode);
            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement operation = requestJson.RootElement.GetProperty("operations")[0];
            Assert.IsTrue(operation.TryGetProperty("id", out JsonElement idElement), "id field should be present for replace operation");
            Assert.AreEqual(doc.id, idElement.GetString());
            Assert.IsTrue(operation.TryGetProperty("etag", out JsonElement etagElement), "etag field should be present when IfMatchEtag is set");
            Assert.AreEqual(expectedEtag, etagElement.GetString());

            response.Dispose();
        }

        [TestMethod]
        [Description("A delete operation with IfMatchEtag set serializes the etag field to the request.")]
        public async Task DeleteItem_WithIfMatchEtag_EtagSerializedToRequest()
        {
            string expectedEtag = "\"test-etag-delete\"";

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.OK, BuildSuccessResponseJson(1))));

            using CosmosClient client = this.CreateMockClient(handler);

            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .DeleteItem(
                    this.database.Id,
                    this.container.Id,
                    new PartitionKey("delete-pk"),
                    "delete-id",
                    new DistributedTransactionRequestOptions { IfMatchEtag = expectedEtag })
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode);
            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement operation = requestJson.RootElement.GetProperty("operations")[0];
            Assert.IsTrue(operation.TryGetProperty("id", out JsonElement idElement), "id field should be present for delete operation");
            Assert.AreEqual("delete-id", idElement.GetString());
            Assert.IsTrue(operation.TryGetProperty("etag", out JsonElement etagElement), "etag field should be present when IfMatchEtag is set");
            Assert.AreEqual(expectedEtag, etagElement.GetString());

            response.Dispose();
        }

        [TestMethod]
        [Description("A patch operation with IfMatchEtag set serializes the etag field to the request.")]
        public async Task PatchItem_WithIfMatchEtag_EtagSerializedToRequest()
        {
            string expectedEtag = "\"test-etag-patch\"";
            IReadOnlyList<PatchOperation> patchOps = new[] { PatchOperation.Add("/description", "patched") };

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.OK, BuildSuccessResponseJson(1))));

            using CosmosClient client = this.CreateMockClient(handler);

            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .PatchItem(
                    this.database.Id,
                    this.container.Id,
                    new PartitionKey("patch-pk"),
                    "patch-id",
                    patchOps,
                    new DistributedTransactionRequestOptions { IfMatchEtag = expectedEtag })
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode);
            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement operation = requestJson.RootElement.GetProperty("operations")[0];
            Assert.IsTrue(operation.TryGetProperty("id", out JsonElement idElement), "id field should be present for patch operation");
            Assert.AreEqual("patch-id", idElement.GetString());
            Assert.IsTrue(operation.TryGetProperty("etag", out JsonElement etagElement), "etag field should be present when IfMatchEtag is set");
            Assert.AreEqual(expectedEtag, etagElement.GetString());

            response.Dispose();
        }

        [TestMethod]
        [Description("A 412 Precondition Failed response marks the transaction and the failing operation as not successful.")]
        public async Task PreconditionFailedResponse_OnReplaceWithStaleEtag_ReturnsFailureStatus()
        {
            string mockErrorJson = @"{
                ""operationResponses"": [{
                    ""index"": 0,
                    ""statusCode"": 412,
                    ""subStatusCode"": 0
                }]
            }";

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.PreconditionFailed, mockErrorJson)));

            using CosmosClient client = this.CreateMockClient(handler);
            ToDoActivity doc = ToDoActivity.CreateRandomToDoActivity();

            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .ReplaceItem(
                    this.database.Id,
                    this.container.Id,
                    new PartitionKey(doc.pk),
                    doc.id,
                    doc,
                    new DistributedTransactionRequestOptions { IfMatchEtag = "\"stale-etag\"" })
                .CommitTransactionAsync(CancellationToken.None);

            Assert.AreEqual(HttpStatusCode.PreconditionFailed, response.StatusCode);
            Assert.IsFalse(response.IsSuccessStatusCode);
            Assert.AreEqual(1, response.Count);
            Assert.AreEqual(HttpStatusCode.PreconditionFailed, response[0].StatusCode);

            response.Dispose();
        }

        [TestMethod]
        [Description("Operations without IfMatchEtag set do not include an etag field in the serialized request.")]
        public async Task Operations_WithoutIfMatchEtag_NoEtagFieldSerialized()
        {
            ToDoActivity createDoc = ToDoActivity.CreateRandomToDoActivity();
            ToDoActivity replaceDoc = ToDoActivity.CreateRandomToDoActivity();

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.OK, BuildSuccessResponseJson(2))));

            using CosmosClient client = this.CreateMockClient(handler);

            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(createDoc.pk), createDoc)
                .ReplaceItem(this.database.Id, this.container.Id, new PartitionKey(replaceDoc.pk), replaceDoc.id, replaceDoc)
                .CommitTransactionAsync(CancellationToken.None);

            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement ops = requestJson.RootElement.GetProperty("operations");
            foreach (JsonElement operation in ops.EnumerateArray())
            {
                Assert.IsFalse(operation.TryGetProperty("etag", out _), "etag field should not be present when IfMatchEtag is not set");
            }

            response.Dispose();
        }

        // Stream operations

        [TestMethod]
        [Description("CreateItemStream serializes the stream payload as a JSON object resourceBody in the request.")]
        public async Task CreateItemStream_ValidDocument_SerializedAsCreateOperation()
        {
            ToDoActivity doc = ToDoActivity.CreateRandomToDoActivity();
            byte[] docBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(doc));

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.OK, BuildSuccessResponseJson(1))));

            using CosmosClient client = this.CreateMockClient(handler);

            using MemoryStream stream = new MemoryStream(docBytes);
            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .CreateItemStream(this.database.Id, this.container.Id, new PartitionKey(doc.pk), stream)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode);
            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement operation = requestJson.RootElement.GetProperty("operations")[0];
            Assert.AreEqual(OperationType.Create.ToString(), operation.GetProperty("operationType").GetString());
            JsonElement resourceBody = operation.GetProperty("resourceBody");
            Assert.AreEqual(JsonValueKind.Object, resourceBody.ValueKind);
            ToDoActivity actualDoc = JsonSerializer.Deserialize<ToDoActivity>(resourceBody.GetRawText());
            Assert.AreEqual(doc.id, actualDoc.id);
            Assert.AreEqual(doc.pk, actualDoc.pk);

            response.Dispose();
        }

        [TestMethod]
        [Description("ReplaceItemStream serializes the stream payload as a JSON object resourceBody and includes the item id in the request.")]
        public async Task ReplaceItemStream_ValidDocument_SerializedAsReplaceOperation()
        {
            ToDoActivity doc = ToDoActivity.CreateRandomToDoActivity();
            byte[] docBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(doc));

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.OK, BuildSuccessResponseJson(1))));

            using CosmosClient client = this.CreateMockClient(handler);

            using MemoryStream stream = new MemoryStream(docBytes);
            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .ReplaceItemStream(this.database.Id, this.container.Id, new PartitionKey(doc.pk), doc.id, stream)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode);
            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement operation = requestJson.RootElement.GetProperty("operations")[0];
            Assert.AreEqual(OperationType.Replace.ToString(), operation.GetProperty("operationType").GetString());
            Assert.AreEqual(doc.id, operation.GetProperty("id").GetString());
            JsonElement resourceBody = operation.GetProperty("resourceBody");
            Assert.AreEqual(JsonValueKind.Object, resourceBody.ValueKind);
            ToDoActivity actualDoc = JsonSerializer.Deserialize<ToDoActivity>(resourceBody.GetRawText());
            Assert.AreEqual(doc.id, actualDoc.id);
            Assert.AreEqual(doc.pk, actualDoc.pk);

            response.Dispose();
        }

        [TestMethod]
        [Description("PatchItemStream serializes the patch payload and includes the item id in the request.")]
        public async Task PatchItemStream_ValidPatch_SerializedAsPatchOperation()
        {
            string patchJson = @"{""operations"":[{""op"":""add"",""path"":""/description"",""value"":""patched""}]}";
            byte[] patchBytes = Encoding.UTF8.GetBytes(patchJson);

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.OK, BuildSuccessResponseJson(1))));

            using CosmosClient client = this.CreateMockClient(handler);

            using MemoryStream stream = new MemoryStream(patchBytes);
            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .PatchItemStream(this.database.Id, this.container.Id, new PartitionKey("patch-pk"), "patch-id", stream)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode);
            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement operation = requestJson.RootElement.GetProperty("operations")[0];
            Assert.AreEqual(OperationType.Patch.ToString(), operation.GetProperty("operationType").GetString());
            Assert.AreEqual("patch-id", operation.GetProperty("id").GetString());

            response.Dispose();
        }

        [TestMethod]
        [Description("UpsertItemStream serializes the stream payload as a JSON object resourceBody in the request.")]
        public async Task UpsertItemStream_ValidDocument_SerializedAsUpsertOperation()
        {
            ToDoActivity doc = ToDoActivity.CreateRandomToDoActivity();
            byte[] docBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(doc));

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.OK, BuildSuccessResponseJson(1))));

            using CosmosClient client = this.CreateMockClient(handler);

            using MemoryStream stream = new MemoryStream(docBytes);
            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .UpsertItemStream(this.database.Id, this.container.Id, new PartitionKey(doc.pk), stream)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode);
            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement operation = requestJson.RootElement.GetProperty("operations")[0];
            Assert.AreEqual(OperationType.Upsert.ToString(), operation.GetProperty("operationType").GetString());
            JsonElement resourceBody = operation.GetProperty("resourceBody");
            Assert.AreEqual(JsonValueKind.Object, resourceBody.ValueKind);
            ToDoActivity actualDoc = JsonSerializer.Deserialize<ToDoActivity>(resourceBody.GetRawText());
            Assert.AreEqual(doc.id, actualDoc.id);
            Assert.AreEqual(doc.pk, actualDoc.pk);

            response.Dispose();
        }

        // Session token handling

        [TestMethod]
        [Description("Session tokens returned in DTC operation responses are merged into the client's session container, preventing ReadSessionNotAvailable errors on subsequent reads.")]
        public async Task ValidateSessionTokenMergedIntoDtcClient()
        {
            ToDoActivity seedDoc = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> seedResponse = await this.container.CreateItemAsync(seedDoc, new PartitionKey(seedDoc.pk), cancellationToken: this.cancellationToken);

            string validSessionToken = seedResponse.Headers.Session;
            Assert.IsFalse(string.IsNullOrEmpty(validSessionToken), "A valid session token must be obtained from the emulator for this test to be meaningful.");

            string dtcMockResponse = $@"{{""operationResponses"":[{{""index"":0,""statusCode"":201,""sessionToken"":""{validSessionToken}""}}]}}";

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.OK, dtcMockResponse)));

            using CosmosClient dtcClient = TestCommon.CreateCosmosClient(
                clientOptions: new CosmosClientOptions
                {
                    CustomHandlers = { handler },
                    ConnectionMode = ConnectionMode.Gateway,
                    ConsistencyLevel = Cosmos.ConsistencyLevel.Session,
                });

            ToDoActivity newDoc = ToDoActivity.CreateRandomToDoActivity();
            DistributedTransactionResponse dtcResponse = await dtcClient
                .CreateDistributedWriteTransaction()
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(newDoc.pk), newDoc)
                .CommitTransactionAsync(this.cancellationToken);

            Assert.IsTrue(dtcResponse.IsSuccessStatusCode, "The simulated DTC commit should appear successful to the client.");

            Container dtcContainer = dtcClient.GetContainer(this.database.Id, this.container.Id);
            try
            {
                ItemResponse<ToDoActivity> readResponse = await dtcContainer.ReadItemAsync<ToDoActivity>(
                    seedDoc.id,
                    new PartitionKey(seedDoc.pk),
                    new ItemRequestOptions { ConsistencyLevel = Cosmos.ConsistencyLevel.Session },
                    cancellationToken: this.cancellationToken);

                Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode, "A Session-consistency read after a DTC commit should return 200 OK.");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                Assert.AreNotEqual(
                    (int)SubStatusCodes.ReadSessionNotAvailable,
                    ex.SubStatusCode,
                    "A Session-consistency read after a DTC commit must not fail with " +
                    "ReadSessionNotAvailable (404/1002). This indicates that session token " +
                    "merging in DistributedTransactionCommitter is broken.");
            }
        }

        // Helpers

        private void ValidateValueKind(JsonElement operation, string property, JsonValueKind expectedValueKind, int operationIndex, bool isRequired)
        {
            if (!operation.TryGetProperty(property, out JsonElement value))
            {
                Assert.IsFalse(isRequired, $"Operation {operationIndex}: required property '{property}' is missing");
                return;
            }

            Assert.AreEqual(expectedValueKind, value.ValueKind, $"Operation {operationIndex}: '{property}' should be {expectedValueKind}");
        }

        private CosmosClient CreateMockClient(DistributedTransactionMockHandler handler)
        {
            return TestCommon.CreateCosmosClient(clientOptions: new CosmosClientOptions
            {
                CustomHandlers = { handler },
                ConnectionMode = ConnectionMode.Gateway
            });
        }

        private ResponseMessage BuildMockResponse(HttpStatusCode statusCode, string responseBody)
        {
            ResponseMessage response = new ResponseMessage(statusCode)
            {
                Content = new MemoryStream(Encoding.UTF8.GetBytes(responseBody))
            };
            response.Headers["x-ms-activity-id"] = Guid.NewGuid().ToString();
            return response;
        }

        private static string BuildSuccessResponseJson(int operationCount)
        {
            List<string> results = new List<string>();
            for (int i = 0; i < operationCount; i++)
            {
                results.Add($@"{{""index"":{i},""statusCode"":201,""etag"":""\""etag-{i}\""""}}");
            }

            return $@"{{""operationResponses"":[{string.Join(",", results)}]}}";
        }

        // Mock handler

        /// <summary>
        /// Intercepts DTC commit requests (URLs ending in "/dtc"), captures the serialized
        /// request body, and returns the response produced by <see cref="MockResponseFactory"/>.
        /// All other requests are forwarded to the next handler in the pipeline (the emulator).
        /// </summary>
        private class DistributedTransactionMockHandler : RequestHandler
        {
            private readonly Func<RequestMessage, Task<ResponseMessage>> mockResponseFactory;

            public string CapturedRequestBody { get; private set; }

            public DistributedTransactionMockHandler(Func<RequestMessage, Task<ResponseMessage>> mockResponseFactory)
            {
                this.mockResponseFactory = mockResponseFactory;
            }

            public override async Task<ResponseMessage> SendAsync(
                RequestMessage request,
                CancellationToken cancellationToken)
            {
                if (request.RequestUriString?.EndsWith("/dtc", StringComparison.OrdinalIgnoreCase) == true)
                {
                    if (request.Content != null)
                    {
                        using MemoryStream ms = new MemoryStream();
                        await request.Content.CopyToAsync(ms);
                        this.CapturedRequestBody = Encoding.UTF8.GetString(ms.ToArray());
                        request.Content.Position = 0;
                    }

                    return await this.mockResponseFactory(request);
                }

                return await base.SendAsync(request, cancellationToken);
            }
        }
    }
}
