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
                .CreateItem(this.GetContainerForClient(client, this.container), new PartitionKey(doc1.pk), doc1.id, doc1)
                .CreateItem(this.GetContainerForClient(client, this.container), new PartitionKey(doc2.pk), doc2.id, doc2)
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
                .CreateItem(this.GetContainerForClient(client, this.container), new PartitionKey(createDoc.pk), createDoc.id, createDoc)
                .ReplaceItem(this.GetContainerForClient(client, this.container), new PartitionKey(replaceDoc.pk), replaceDoc.id, replaceDoc)
                .DeleteItem(this.GetContainerForClient(client, this.container), new PartitionKey("delete-pk"), "delete-id")
                .CommitTransactionAsync(CancellationToken.None);

            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement ops = requestJson.RootElement.GetProperty(DistributedTransactionSerializer.Operations);

            Assert.AreEqual(3, ops.GetArrayLength());
            Assert.AreEqual(OperationType.Create.ToString(), ops[0].GetProperty(DistributedTransactionSerializer.OperationType).GetString());
            Assert.AreEqual(OperationType.Replace.ToString(), ops[1].GetProperty(DistributedTransactionSerializer.OperationType).GetString());
            Assert.AreEqual(OperationType.Delete.ToString(), ops[2].GetProperty(DistributedTransactionSerializer.OperationType).GetString());
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
                .CreateItem(this.GetContainerForClient(client, this.container), new PartitionKey(createDoc.pk), createDoc.id, createDoc)
                .UpsertItem(this.GetContainerForClient(client, this.container), new PartitionKey(upsertDoc.pk), upsertDoc.id, upsertDoc)
                .CommitTransactionAsync(CancellationToken.None);

            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement ops = requestJson.RootElement.GetProperty(DistributedTransactionSerializer.Operations);

            Assert.AreEqual(2, ops.GetArrayLength());
            Assert.AreEqual(OperationType.Upsert.ToString(), ops[1].GetProperty(DistributedTransactionSerializer.OperationType).GetString());
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
                .CreateItem(this.GetContainerForClient(client, this.container), new PartitionKey(createDoc.pk), createDoc.id, createDoc)
                .PatchItem(this.GetContainerForClient(client, this.container), new PartitionKey("patch-pk"), "item-to-patch", patchOps)
                .CommitTransactionAsync(CancellationToken.None);

            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement ops = requestJson.RootElement.GetProperty(DistributedTransactionSerializer.Operations);

            Assert.AreEqual(2, ops.GetArrayLength());
            Assert.AreEqual(OperationType.Patch.ToString(), ops[1].GetProperty(DistributedTransactionSerializer.OperationType).GetString());
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
                .CreateItem(this.GetContainerForClient(client, this.container), new PartitionKey(doc1.pk), doc1.id, doc1)
                .CreateItem(this.GetContainerForClient(client, secondContainer), new PartitionKey(doc2.pk), doc2.id, doc2)
                .CommitTransactionAsync(CancellationToken.None);

            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement ops = requestJson.RootElement.GetProperty(DistributedTransactionSerializer.Operations);

            Assert.AreEqual(2, ops.GetArrayLength());
            Assert.AreNotEqual(
                ops[0].GetProperty(DistributedTransactionSerializer.CollectionName).GetString(),
                ops[1].GetProperty(DistributedTransactionSerializer.CollectionName).GetString(),
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
                .CreateItem(this.GetContainerForClient(client, this.container), new PartitionKey(doc.pk), doc.id, doc)
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
                .CreateItem(this.GetContainerForClient(client, this.container), new PartitionKey(doc1.pk), doc1.id, doc1)
                .CreateItem(this.GetContainerForClient(client, this.container), new PartitionKey(doc2.pk), doc2.id, doc2)
                .CreateItem(this.GetContainerForClient(client, this.container), new PartitionKey(doc3.pk), doc3.id, doc3)
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
                .CreateItem(this.GetContainerForClient(client, this.container), new PartitionKey(expectedDoc.pk), expectedDoc.id, expectedDoc)
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
                .CreateItem(this.GetContainerForClient(client, this.container), new PartitionKey(doc.pk), doc.id, doc)
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
                .ReplaceItem(this.GetContainerForClient(client, this.container), new PartitionKey(doc.pk), doc.id, doc)
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
                .CreateItem(this.GetContainerForClient(client, this.container), new PartitionKey(doc1.pk), doc1.id, doc1)
                .CreateItem(this.GetContainerForClient(client, this.container), new PartitionKey(doc2.pk), doc2.id, doc2)
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
                .CreateItem(this.GetContainerForClient(client, this.container), new PartitionKey(createDoc.pk), createDoc.id, createDoc)
                .ReplaceItem(this.GetContainerForClient(client, this.container), new PartitionKey(replaceDoc.pk), replaceDoc.id, replaceDoc)
                .DeleteItem(this.GetContainerForClient(client, this.container), new PartitionKey("delete-pk"), "delete-id")
                .CommitTransactionAsync(CancellationToken.None);

            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);

            Assert.AreEqual(JsonValueKind.Object, requestJson.RootElement.ValueKind, "Root element should be an object");
            Assert.IsTrue(requestJson.RootElement.TryGetProperty(DistributedTransactionSerializer.Operations, out JsonElement operations), "operations property should exist");
            Assert.AreEqual(JsonValueKind.Array, operations.ValueKind, "operations should be an array");
            Assert.AreEqual(3, operations.GetArrayLength(), "operations should have 3 elements");

            int operationIndex = 0;
            foreach (JsonElement operation in operations.EnumerateArray())
            {
                Assert.AreEqual(JsonValueKind.Object, operation.ValueKind, $"Operation {operationIndex} should be an object");

                (string Property, JsonValueKind Kind)[] requiredFields =
                {
                    (DistributedTransactionSerializer.DatabaseName, JsonValueKind.String),
                    (DistributedTransactionSerializer.CollectionName, JsonValueKind.String),
                    (DistributedTransactionSerializer.CollectionResourceId, JsonValueKind.String),
                    (DistributedTransactionSerializer.DatabaseResourceId, JsonValueKind.String),
                    (DistributedTransactionSerializer.PartitionKey, JsonValueKind.Array),
                    (DistributedTransactionSerializer.Index, JsonValueKind.Number),
                    (DistributedTransactionSerializer.OperationType, JsonValueKind.String),
                    (DistributedTransactionSerializer.ResourceType, JsonValueKind.String)
                };

                foreach ((string property, JsonValueKind expectedKind) in requiredFields)
                {
                    this.ValidateValueKind(operation, property, expectedKind, operationIndex, isRequired: true);
                }

                (string Property, JsonValueKind Kind)[] optionalFields =
                {
                    (DistributedTransactionSerializer.Id, JsonValueKind.String),
                    (DistributedTransactionSerializer.ResourceBody, JsonValueKind.Object),
                    (DistributedTransactionSerializer.SessionToken, JsonValueKind.String),
                    (DistributedTransactionSerializer.IfMatch, JsonValueKind.String),
                    (DistributedTransactionSerializer.IfNoneMatch, JsonValueKind.String),
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
        [Description("A replace operation with IfMatchEtag set serializes the ifMatch field to the request.")]
        public async Task ReplaceItem_WithIfMatchEtag_EtagSerializedToRequest()
        {
            ToDoActivity doc = ToDoActivity.CreateRandomToDoActivity();
            string expectedEtag = "\"test-etag-replace\"";

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.OK, BuildSuccessResponseJson(1))));

            using CosmosClient client = this.CreateMockClient(handler);

            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .ReplaceItem(
                    this.GetContainerForClient(client, this.container),
                    new PartitionKey(doc.pk),
                    doc.id,
                    doc,
                    new DistributedTransactionRequestOptions { IfMatchEtag = expectedEtag })
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode);
            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement operation = requestJson.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];
            Assert.IsTrue(operation.TryGetProperty(DistributedTransactionSerializer.Id, out JsonElement idElement), "id field should be present for replace operation");
            Assert.AreEqual(doc.id, idElement.GetString());
            Assert.IsTrue(operation.TryGetProperty(DistributedTransactionSerializer.IfMatch, out JsonElement etagElement), "ifMatch field should be present when IfMatchEtag is set");
            Assert.AreEqual(expectedEtag, etagElement.GetString());

            response.Dispose();
        }

        [TestMethod]
        [Description("A delete operation with IfMatchEtag set serializes the ifMatch field to the request.")]
        public async Task DeleteItem_WithIfMatchEtag_EtagSerializedToRequest()
        {
            string expectedEtag = "\"test-etag-delete\"";

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.OK, BuildSuccessResponseJson(1))));

            using CosmosClient client = this.CreateMockClient(handler);

            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .DeleteItem(
                    this.GetContainerForClient(client, this.container),
                    new PartitionKey("delete-pk"),
                    "delete-id",
                    new DistributedTransactionRequestOptions { IfMatchEtag = expectedEtag })
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode);
            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement operation = requestJson.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];
            Assert.IsTrue(operation.TryGetProperty(DistributedTransactionSerializer.Id, out JsonElement idElement), "id field should be present for delete operation");
            Assert.AreEqual("delete-id", idElement.GetString());
            Assert.IsTrue(operation.TryGetProperty(DistributedTransactionSerializer.IfMatch, out JsonElement etagElement), "ifMatch field should be present when IfMatchEtag is set");
            Assert.AreEqual(expectedEtag, etagElement.GetString());

            response.Dispose();
        }

        [TestMethod]
        [Description("A patch operation with IfMatchEtag set serializes the ifMatch field to the request.")]
        public async Task PatchItem_WithIfMatchEtag_EtagSerializedToRequest()
        {
            string expectedEtag = "\"test-etag-patch\"";
            IReadOnlyList<PatchOperation> patchOps = new[] { PatchOperation.Add("/description", "patched") };

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.OK, BuildSuccessResponseJson(1))));

            using CosmosClient client = this.CreateMockClient(handler);

            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .PatchItem(
                    this.GetContainerForClient(client, this.container),
                    new PartitionKey("patch-pk"),
                    "patch-id",
                    patchOps,
                    new DistributedTransactionRequestOptions { IfMatchEtag = expectedEtag })
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode);
            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement operation = requestJson.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];
            Assert.IsTrue(operation.TryGetProperty(DistributedTransactionSerializer.Id, out JsonElement idElement), "id field should be present for patch operation");
            Assert.AreEqual("patch-id", idElement.GetString());
            Assert.IsTrue(operation.TryGetProperty(DistributedTransactionSerializer.IfMatch, out JsonElement etagElement), "ifMatch field should be present when IfMatchEtag is set");
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
                    this.GetContainerForClient(client, this.container),
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
        [Description("Operations without conditional ETags set do not include ifMatch or ifNoneMatch fields in the serialized request.")]
        public async Task Operations_WithoutIfMatchEtag_NoEtagFieldSerialized()
        {
            ToDoActivity createDoc = ToDoActivity.CreateRandomToDoActivity();
            ToDoActivity replaceDoc = ToDoActivity.CreateRandomToDoActivity();

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.OK, BuildSuccessResponseJson(2))));

            using CosmosClient client = this.CreateMockClient(handler);

            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .CreateItem(this.GetContainerForClient(client, this.container), new PartitionKey(createDoc.pk), createDoc.id, createDoc)
                .ReplaceItem(this.GetContainerForClient(client, this.container), new PartitionKey(replaceDoc.pk), replaceDoc.id, replaceDoc)
                .CommitTransactionAsync(CancellationToken.None);

            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement ops = requestJson.RootElement.GetProperty(DistributedTransactionSerializer.Operations);
            foreach (JsonElement operation in ops.EnumerateArray())
            {
                Assert.IsFalse(operation.TryGetProperty(DistributedTransactionSerializer.IfMatch, out _), "ifMatch field should not be present when IfMatchEtag is not set");
                Assert.IsFalse(operation.TryGetProperty(DistributedTransactionSerializer.IfNoneMatch, out _), "ifNoneMatch field should not be present when IfNoneMatchEtag is not set");
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
                .CreateItemStream(this.GetContainerForClient(client, this.container), new PartitionKey(doc.pk), doc.id, stream)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode);
            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement operation = requestJson.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];
            Assert.AreEqual(OperationType.Create.ToString(), operation.GetProperty(DistributedTransactionSerializer.OperationType).GetString());
            JsonElement resourceBody = operation.GetProperty(DistributedTransactionSerializer.ResourceBody);
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
                .ReplaceItemStream(this.GetContainerForClient(client, this.container), new PartitionKey(doc.pk), doc.id, stream)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode);
            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement operation = requestJson.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];
            Assert.AreEqual(OperationType.Replace.ToString(), operation.GetProperty(DistributedTransactionSerializer.OperationType).GetString());
            Assert.AreEqual(doc.id, operation.GetProperty(DistributedTransactionSerializer.Id).GetString());
            JsonElement resourceBody = operation.GetProperty(DistributedTransactionSerializer.ResourceBody);
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
                .PatchItemStream(this.GetContainerForClient(client, this.container), new PartitionKey("patch-pk"), "patch-id", stream)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode);
            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement operation = requestJson.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];
            Assert.AreEqual(OperationType.Patch.ToString(), operation.GetProperty(DistributedTransactionSerializer.OperationType).GetString());
            Assert.AreEqual("patch-id", operation.GetProperty(DistributedTransactionSerializer.Id).GetString());

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
                .UpsertItemStream(this.GetContainerForClient(client, this.container), new PartitionKey(doc.pk), doc.id, stream)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode);
            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement operation = requestJson.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];
            Assert.AreEqual(OperationType.Upsert.ToString(), operation.GetProperty(DistributedTransactionSerializer.OperationType).GetString());
            JsonElement resourceBody = operation.GetProperty(DistributedTransactionSerializer.ResourceBody);
            Assert.AreEqual(JsonValueKind.Object, resourceBody.ValueKind);
            ToDoActivity actualDoc = JsonSerializer.Deserialize<ToDoActivity>(resourceBody.GetRawText());
            Assert.AreEqual(doc.id, actualDoc.id);
            Assert.AreEqual(doc.pk, actualDoc.pk);

            response.Dispose();
        }

        // Session token handling

        [TestMethod]
        [Description("When DTC response carries a session token in the new wire format (LSN-only sessionToken + " +
            "separate partitionKeyRangeId), the SDK assembles the canonical {pkRangeId}:{lsn} token and merges it " +
            "into the session container so that subsequent Session-consistency reads succeed.")]
        public async Task ValidateSessionTokenMergedIntoDtcClient()
        {
            ToDoActivity seedDoc = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> seedResponse = await this.container.CreateItemAsync(seedDoc, new PartitionKey(seedDoc.pk), cancellationToken: this.cancellationToken);

            string canonicalToken = seedResponse.Headers.Session;
            Assert.IsFalse(string.IsNullOrEmpty(canonicalToken), "A valid session token must be obtained from the emulator for this test to be meaningful.");

            // Split the canonical {pkRangeId}:{lsn} token into the two fields the DTC endpoint sends.
            int colonIndex = canonicalToken.IndexOf(':');
            Assert.IsTrue(colonIndex > 0, $"Emulator session token '{canonicalToken}' must be in {{pkRangeId}}:{{lsn}} format.");
            string pkRangeId = canonicalToken.Substring(0, colonIndex);
            string lsnOnly = canonicalToken.Substring(colonIndex + 1);

            // Build a DTC mock response using the new wire contract: LSN-only in sessionToken,
            // pkRangeId in a separate partitionKeyRangeId field.
            string dtcMockResponse = $@"{{""operationResponses"":[{{""index"":0,""statusCode"":201,""sessionToken"":""{lsnOnly}"",""partitionKeyRangeId"":""{pkRangeId}""}}]}}";

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.OK, dtcMockResponse)));

            using CosmosClient dtcClient = TestCommon.CreateCosmosClient(
                clientOptions: new CosmosClientOptions
                {
                    CustomHandlers = { handler },
                    ConnectionMode = ConnectionMode.Gateway,
                    ConsistencyLevel = Cosmos.ConsistencyLevel.Session,
                });

            // Use the same partition key as seedDoc so the DTC operation targets the same physical
            // partition whose session token is carried in the mock response.
            ToDoActivity newDoc = ToDoActivity.CreateRandomToDoActivity(pk: seedDoc.pk);
            DistributedTransactionResponse dtcResponse = await dtcClient
                .CreateDistributedWriteTransaction()
                .CreateItem(this.GetContainerForClient(dtcClient, this.container), new PartitionKey(newDoc.pk), newDoc.id, newDoc)
                .CommitTransactionAsync(this.cancellationToken);

            Assert.IsTrue(dtcResponse.IsSuccessStatusCode, "The simulated DTC commit should appear successful to the client.");
            Assert.AreEqual(canonicalToken, dtcResponse[0].SessionToken,
                "SessionToken must be assembled as {pkRangeId}:{lsn} from the two separate wire fields.");

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

        [TestMethod]
        [Description("When DTC response carries only an LSN-only sessionToken with no partitionKeyRangeId " +
            "(current server behavior before coordinator update), the commit must succeed without throwing " +
            "and the SDK silently skips merging the session token rather than crashing.")]
        // TODO(issue#5857): Remove this test once the coordinator is updated to emit partitionKeyRangeId and the SDK no longer needs to handle its absence.
        public async Task ValidateSessionTokenSkipped_WhenPartitionKeyRangeIdAbsent()
        {
            ToDoActivity seedDoc = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> seedResponse = await this.container.CreateItemAsync(seedDoc, new PartitionKey(seedDoc.pk), cancellationToken: this.cancellationToken);

            string canonicalToken = seedResponse.Headers.Session;
            Assert.IsFalse(string.IsNullOrEmpty(canonicalToken), "A valid session token must be obtained from the emulator.");
            int colonIndex = canonicalToken.IndexOf(':');
            Assert.IsTrue(colonIndex > 0, $"Emulator session token '{canonicalToken}' must be in {{pkRangeId}}:{{lsn}} format.");
            string lsnOnly = canonicalToken.Substring(colonIndex + 1);

            // Current server behavior: LSN-only token, no partitionKeyRangeId field.
            string dtcMockResponse = $@"{{""operationResponses"":[{{""index"":0,""statusCode"":201,""sessionToken"":""{lsnOnly}""}}]}}";

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.OK, dtcMockResponse)));

            using CosmosClient dtcClient = TestCommon.CreateCosmosClient(
                clientOptions: new CosmosClientOptions
                {
                    CustomHandlers = { handler },
                    ConnectionMode = ConnectionMode.Gateway,
                    ConsistencyLevel = Cosmos.ConsistencyLevel.Session,
                });

            // Use the same partition key as seedDoc for consistency.
            ToDoActivity newDoc = ToDoActivity.CreateRandomToDoActivity(pk: seedDoc.pk);
            DistributedTransactionResponse dtcResponse = await dtcClient
                .CreateDistributedWriteTransaction()
                .CreateItem(this.GetContainerForClient(dtcClient, this.container), new PartitionKey(newDoc.pk), newDoc.id, newDoc)
                .CommitTransactionAsync(this.cancellationToken);

            // Commit must succeed — this was the crash point before the fix (IndexOutOfRangeException
            // in SessionContainer.SetSessionToken when it tried tokenParts[1] on an LSN-only token).
            Assert.IsTrue(dtcResponse.IsSuccessStatusCode, "Commit must succeed even when partitionKeyRangeId is absent.");

            // Session token must be null — FromJson nulls it out when pkRangeId is absent so that
            // MergeSessionTokens skips the operation rather than passing a bad token to SetSessionToken.
            Assert.IsNull(dtcResponse[0].SessionToken,
                "SessionToken must be null when partitionKeyRangeId is absent; the SDK silently skips merging.");
        }

        // Read Transaction Tests

        [TestMethod]
        public async Task ValidateReadTransactionHappyPath()
        {
            // Arrange
            ToDoActivity doc1 = ToDoActivity.CreateRandomToDoActivity();
            ToDoActivity doc2 = ToDoActivity.CreateRandomToDoActivity();

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(
                    HttpStatusCode.OK,
                    BuildReadSuccessResponseJson(2, JsonSerializer.Serialize(doc1), JsonSerializer.Serialize(doc2)))));

            using CosmosClient client = this.CreateMockClient(handler);

            // Act
            DistributedTransactionResponse response = await client
                .CreateDistributedReadTransaction()
                .ReadItem(this.GetContainerForClient(client, this.container), new PartitionKey(doc1.pk), doc1.id)
                .ReadItem(this.GetContainerForClient(client, this.container), new PartitionKey(doc2.pk), doc2.id)
                .CommitTransactionAsync(CancellationToken.None);

            // Assert
            Assert.IsNotNull(handler.CapturedRequestBody);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.AreEqual(2, response.Count);

            response.Dispose();
        }

        [TestMethod]
        public async Task ValidateReadTransactionRequestStructure()
        {
            // Arrange
            ToDoActivity doc = ToDoActivity.CreateRandomToDoActivity();

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(
                    HttpStatusCode.OK,
                    BuildReadSuccessResponseJson(1, JsonSerializer.Serialize(doc)))));

            using CosmosClient client = this.CreateMockClient(handler);

            // Act
            DistributedTransactionResponse response = await client
                .CreateDistributedReadTransaction()
                .ReadItem(this.GetContainerForClient(client, this.container), new PartitionKey(doc.pk), doc.id)
                .CommitTransactionAsync(CancellationToken.None);

            // Assert – request structure
            Assert.IsNotNull(handler.CapturedRequestBody);
            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement operation = requestJson.RootElement.GetProperty(DistributedTransactionSerializer.Operations)[0];

            Assert.AreEqual(OperationType.Read.ToString(), operation.GetProperty(DistributedTransactionSerializer.OperationType).GetString());
            Assert.AreEqual(doc.id, operation.GetProperty(DistributedTransactionSerializer.Id).GetString());
            Assert.IsTrue(operation.TryGetProperty(DistributedTransactionSerializer.DatabaseName, out _), "databaseName should be present");
            Assert.IsTrue(operation.TryGetProperty(DistributedTransactionSerializer.CollectionName, out _), "collectionName should be present");
            Assert.IsTrue(operation.TryGetProperty(DistributedTransactionSerializer.PartitionKey, out _), "partitionKey should be present");
            Assert.IsFalse(operation.TryGetProperty(DistributedTransactionSerializer.ResourceBody, out _), "resourceBody must NOT be present for read operations");

            response.Dispose();
        }

        [TestMethod]
        public async Task ValidateReadTransactionResponseDeserialization()
        {
            // Arrange
            ToDoActivity expectedDoc = ToDoActivity.CreateRandomToDoActivity();

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(
                    HttpStatusCode.OK,
                    BuildReadSuccessResponseJson(1, JsonSerializer.Serialize(expectedDoc)))));

            using CosmosClient client = this.CreateMockClient(handler);

            // Act
            DistributedTransactionResponse response = await client
                .CreateDistributedReadTransaction()
                .ReadItem(this.GetContainerForClient(client, this.container), new PartitionKey(expectedDoc.pk), expectedDoc.id)
                .CommitTransactionAsync(CancellationToken.None);

            // Assert
            Assert.IsTrue(response.IsSuccessStatusCode);
            ToDoActivity actualDoc = JsonSerializer.Deserialize<ToDoActivity>(response[0].ResourceStream);
            Assert.IsNotNull(actualDoc);
            Assert.AreEqual(expectedDoc.id, actualDoc.id);
            Assert.AreEqual(expectedDoc.pk, actualDoc.pk);
            Assert.AreEqual(expectedDoc.taskNum, actualDoc.taskNum);

            response.Dispose();
        }

        [TestMethod]
        public async Task ValidateReadTransactionResourceStream()
        {
            // Arrange
            ToDoActivity expectedDoc = ToDoActivity.CreateRandomToDoActivity();

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(
                    HttpStatusCode.OK,
                    BuildReadSuccessResponseJson(1, JsonSerializer.Serialize(expectedDoc)))));

            using CosmosClient client = this.CreateMockClient(handler);

            // Act
            DistributedTransactionResponse response = await client
                .CreateDistributedReadTransaction()
                .ReadItem(this.GetContainerForClient(client, this.container), new PartitionKey(expectedDoc.pk), expectedDoc.id)
                .CommitTransactionAsync(CancellationToken.None);

            // Assert – raw stream access
            Stream stream = response[0].ResourceStream;
            Assert.IsNotNull(stream);
            ToDoActivity actualDoc = JsonSerializer.Deserialize<ToDoActivity>(stream);
            Assert.AreEqual(expectedDoc.id, actualDoc.id);
            Assert.AreEqual(expectedDoc.pk, actualDoc.pk);

            response.Dispose();
        }

        [TestMethod]
        public void ValidateReadTransactionMissingIdThrows()
        {
            using CosmosClient client = this.CreateMockClient(new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.OK, BuildSuccessResponseJson(1)))));

            Assert.ThrowsException<ArgumentNullException>(() =>
                client.CreateDistributedReadTransaction()
                    .ReadItem(this.GetContainerForClient(client, this.container), new PartitionKey("pk"), id: null));
        }

        [TestMethod]
        public void ValidateReadTransactionMissingContainerThrows()
        {
            using CosmosClient client = this.CreateMockClient(new DistributedTransactionMockHandler(
                request => Task.FromResult(this.BuildMockResponse(HttpStatusCode.OK, BuildSuccessResponseJson(1)))));

            Assert.ThrowsException<ArgumentNullException>(() =>
                client.CreateDistributedReadTransaction()
                    .ReadItem(null, new PartitionKey("pk"), "item-id"));
        }

        // Fault injection: wire-contract + retry-flow coverage for the full DTX SDK response catalog.

        [TestMethod]
        [Description("A1+A5 (core): A retriable error body (isRetriable:true) drives the committer outer loop to retry; " +
            "after N failures it succeeds, and the wire contract (idempotency token, operation type, resource type/URI, " +
            "request body) is byte-identical across every attempt.")]
        public async Task WriteTransaction_RetriableFault_RetriesAndPreservesWireContract()
        {
            const int failuresBeforeSuccess = 2;
            int attempt = 0;

            DistributedTransactionMockHandler handler = new DistributedTransactionMockHandler(request =>
            {
                int current = Interlocked.Increment(ref attempt);
                return Task.FromResult(current <= failuresBeforeSuccess
                    ? this.BuildMockResponse((HttpStatusCode)449, BuildRetriableErrorJson(), (SubStatusCodes)5352)
                    : this.BuildMockResponse(HttpStatusCode.OK, BuildSuccessResponseJson(1)));
            });

            using CosmosClient client = this.CreateMockClient(handler);
            ToDoActivity doc = ToDoActivity.CreateRandomToDoActivity();

            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .CreateItem(this.GetContainerForClient(client, this.container), new PartitionKey(doc.pk), doc.id, doc)
                .CommitTransactionAsync(CancellationToken.None);

            Assert.IsTrue(response.IsSuccessStatusCode, "Committer should retry the retriable fault and ultimately succeed.");
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            AssertWireContractStableAcrossRequests(
                handler,
                OperationType.CommitDistributedTransaction.ToOperationTypeString(),
                failuresBeforeSuccess + 1);

            response.Dispose();
        }

        // Helpers

        private Container GetContainerForClient(CosmosClient client, Container sourceContainer)
        {
            return client.GetContainer(sourceContainer.Database.Id, sourceContainer.Id);
        }

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

        private static string BuildReadSuccessResponseJson(int operationCount, params string[] itemJsonBodies)
        {
            List<string> results = new List<string>();
            for (int i = 0; i < operationCount; i++)
            {
                string body = i < itemJsonBodies.Length ? itemJsonBodies[i] : "{}";
                results.Add($@"{{""index"":{i},""statusCode"":200,""etag"":""\""etag-{i}\"""",""resourceBody"":{body}}}");
            }

            return $@"{{""operationResponses"":[{string.Join(",", results)}]}}";
        }

        // Fault-injection helpers

        /// <summary>
        /// Builds a mock DTC <see cref="ResponseMessage"/> and, optionally, stamps the wire
        /// sub-status (<c>x-ms-substatus</c>) and <c>Retry-After</c> headers the SDK reads from the
        /// gateway response. The committer derives the surfaced sub-status from
        /// <see cref="Headers.SubStatusCode"/> and the retry delay hint from
        /// <see cref="Headers.RetryAfter"/>, so simulating those wire codes requires setting them here.
        /// </summary>
        private ResponseMessage BuildMockResponse(
            HttpStatusCode statusCode,
            string responseBody,
            SubStatusCodes? subStatusCode,
            TimeSpan? retryAfter = null)
        {
            ResponseMessage response = this.BuildMockResponse(statusCode, responseBody);

            if (subStatusCode.HasValue)
            {
                response.Headers.SubStatusCode = subStatusCode.Value;
            }

            if (retryAfter.HasValue)
            {
                response.Headers.RetryAfter = retryAfter.Value;
            }

            return response;
        }

        /// <summary>
        /// Builds the minimal JSON body the coordinator returns to mark a transaction outcome as
        /// retriable. The committer's outer loop retries iff the response carries
        /// <c>isRetriable:true</c> in its body (see <c>DistributedTransactionResponse</c>).
        /// </summary>
        private static string BuildRetriableErrorJson(string diagnosticString = "injected-retriable-fault")
        {
            return $@"{{""{DistributedTransactionSerializer.IsRetriable}"":true,""{DistributedTransactionSerializer.DiagnosticString}"":""{diagnosticString}""}}";
        }

        /// <summary>
        /// Asserts that every DTC request the committer issued is byte-identical on the wire:
        /// same idempotency token, operation type, resource type, resource URI and request body.
        /// This is the core retry-flow invariant — a retry must replay the exact same transaction.
        /// </summary>
        private static void AssertWireContractStableAcrossRequests(
            DistributedTransactionMockHandler handler,
            string expectedOperationType,
            int expectedRequestCount)
        {
            Assert.AreEqual(expectedRequestCount, handler.RequestCount,
                $"Committer should have issued exactly {expectedRequestCount} wire request(s).");
            Assert.IsTrue(handler.RequestCount > 0, "At least one DTC request must have been captured.");

            CapturedDtcRequest first = handler.CapturedRequests[0];

            Assert.IsFalse(string.IsNullOrEmpty(first.IdempotencyToken), "Idempotency token header must be present on the wire.");
            Assert.IsTrue(Guid.TryParse(first.IdempotencyToken, out _), "Idempotency token must be a valid GUID.");
            Assert.AreEqual(expectedOperationType, first.OperationType, "Operation type header mismatch.");
            Assert.AreEqual(ResourceType.DistributedTransactionBatch.ToResourceTypeString(), first.ResourceType, "Resource type header mismatch.");
            Assert.IsTrue(first.RequestUri?.EndsWith("/dtc", StringComparison.OrdinalIgnoreCase) == true,
                $"Resource URI must target the '/dtc' endpoint but was '{first.RequestUri}'.");

            for (int i = 1; i < handler.RequestCount; i++)
            {
                CapturedDtcRequest current = handler.CapturedRequests[i];
                Assert.AreEqual(first.IdempotencyToken, current.IdempotencyToken,
                    $"Idempotency token changed on retry attempt {i}: '{first.IdempotencyToken}' -> '{current.IdempotencyToken}'.");
                Assert.AreEqual(first.OperationType, current.OperationType,
                    $"Operation type changed on retry attempt {i}.");
                Assert.AreEqual(first.ResourceType, current.ResourceType,
                    $"Resource type changed on retry attempt {i}.");
                Assert.AreEqual(first.RequestUri, current.RequestUri,
                    $"Resource URI changed on retry attempt {i}.");
                Assert.AreEqual(first.Body, current.Body,
                    $"Request body changed on retry attempt {i}; the wire contract must be replayed byte-identically.");
            }
        }

        // Mock handler

        /// <summary>
        /// Immutable snapshot of a single DTC request as it appeared on the wire, captured by
        /// <see cref="DistributedTransactionMockHandler"/>. Used by the fault-injection tests to
        /// assert that the wire contract (idempotency token, operation type, resource type,
        /// resource URI and request body) is preserved byte-identically across committer retries.
        /// </summary>
        private sealed class CapturedDtcRequest
        {
            public string Body { get; set; }

            public string IdempotencyToken { get; set; }

            public string OperationType { get; set; }

            public string ResourceType { get; set; }

            public string RequestUri { get; set; }
        }

        /// <summary>
        /// Intercepts DTC commit requests (URLs ending in "/dtc"), captures the serialized
        /// request body, and returns the response produced by <see cref="MockResponseFactory"/>.
        /// All other requests are forwarded to the next handler in the pipeline (the emulator).
        ///
        /// In addition to <see cref="CapturedRequestBody"/> (the body of the most recent request,
        /// preserved for existing tests), the handler records a per-request <see cref="CapturedDtcRequest"/>
        /// snapshot in <see cref="CapturedRequests"/> so retry-flow tests can assert wire-contract
        /// stability across every attempt the committer issues.
        /// </summary>
        private class DistributedTransactionMockHandler : RequestHandler
        {
            private readonly Func<RequestMessage, Task<ResponseMessage>> mockResponseFactory;
            private readonly List<CapturedDtcRequest> capturedRequests = new List<CapturedDtcRequest>();

            public string CapturedRequestBody { get; private set; }

            /// <summary>
            /// The full ordered list of DTC requests intercepted by this handler, one entry per
            /// attempt. <see cref="RequestCount"/> therefore equals the number of wire requests the
            /// committer issued (1 = no retry, N+1 = N retries).
            /// </summary>
            public IReadOnlyList<CapturedDtcRequest> CapturedRequests => this.capturedRequests;

            public int RequestCount => this.capturedRequests.Count;

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
                    string body = null;
                    if (request.Content != null)
                    {
                        using MemoryStream ms = new MemoryStream();
                        await request.Content.CopyToAsync(ms);
                        body = Encoding.UTF8.GetString(ms.ToArray());
                        request.Content.Position = 0;
                    }

                    this.CapturedRequestBody = body;
                    this.capturedRequests.Add(new CapturedDtcRequest
                    {
                        Body = body,
                        IdempotencyToken = request.Headers[HttpConstants.HttpHeaders.IdempotencyToken],
                        OperationType = request.Headers[HttpConstants.HttpHeaders.OperationType],
                        ResourceType = request.Headers[HttpConstants.HttpHeaders.ResourceType],
                        RequestUri = request.RequestUriString,
                    });

                    return await this.mockResponseFactory(request);
                }

                return await base.SendAsync(request, cancellationToken);
            }
        }
    }
}
