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
    using global::Azure.Core;
    using global::Azure.Identity;
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

    /// <summary>
    /// A single comprehensive end-to-end test for the distributed READ transaction
    /// (<see cref="DistributedReadTransaction"/>) that exercises every positive and
    /// negative scenario in one run against a live DTX-enabled account.
    ///
    /// Document setup is performed exclusively with POINT WRITES (CreateItemAsync /
    /// ReplaceItemAsync). No distributed WRITE transaction is used anywhere in this test.
    ///
    /// Scenarios covered (each asserts both the transaction-level "outer" status and the
    /// per-operation "inner" results):
    ///   1. All items exist, no conditionals          -> 200 OK, every op 200 with body + ETag
    ///   2. Read of a missing document                 -> envelope 404; failing op 404, no body
    ///   3. Partial failure (one exists, one missing)  -> envelope 404; one 404 + 424 FailedDependency
    ///   4. Conditional read, matching IfNoneMatch     -> 207 MultiStatus; op 304 NotModified (no body)
    ///   5. Conditional read, stale IfNoneMatch        -> 200 OK with updated resource + new ETag
    ///   6. All ops matching IfNoneMatch               -> every op 304 NotModified
    ///   7. Per-op Index ordering across multiple reads-> Index values match submission order on success
    ///   8. Empty / no-body handling                   -> 304 ops expose null ResourceStream
    ///   9. Typed accessor                             -> GetOperationResultAtIndex&lt;T&gt; deserializes body
    ///
    /// The DTX endpoint is not available in the emulator, so this test is [Ignore]'d by default.
    /// To run locally, set COSMOS_DTX_ENDPOINT / COSMOS_DTX_KEY env vars (or the Endpoint / Key
    /// constants) and remove the [Ignore] attribute.
    /// SECURITY: never commit real key values. Scrub Endpoint / Key to "" before pushing.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    [TestCategory("DistributedTransaction")]
    public class DistributedReadTransactionComprehensiveE2ETests
    {
        // AAD (token) auth against the dtx-nocmk-1254 account — no account key required.
        private const string Endpoint = "https://dtx-nocmk-1254.documents-test.windows-int.net:443/";

        // A fresh, uniquely-named database/container is created for every test run so each run
        // starts from a clean slate (and parallel/repeat runs never collide).
        private const string DatabaseIdPrefix = "dtxreaddb1";
        private const string ContainerIdPrefix = "dtxreadcontainer1";
        private const string PartitionKeyPath = "/pk";

        private string databaseId;
        private string containerId;
        private CosmosClient client;
        private Container container;
        private readonly CancellationToken cancellationToken = CancellationToken.None;

        [TestInitialize]
        public async Task TestInitialize()
        {
            string endpoint = Environment.GetEnvironmentVariable("COSMOS_DTX_ENDPOINT") ?? Endpoint;

            if (string.IsNullOrWhiteSpace(endpoint) || endpoint.Contains("<your-account>"))
            {
                Assert.Fail("Set the Endpoint constant (or COSMOS_DTX_ENDPOINT env var) to run this test.");
            }

            // AAD token auth (no account key) via DefaultAzureCredential.
            TokenCredential tokenCredential = new DefaultAzureCredential();

            this.client = new CosmosClient(
                endpoint,
                tokenCredential,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    ConsistencyLevel = Cosmos.ConsistencyLevel.Session
                });

            // Fresh, unique database + container for this run.
            this.databaseId = $"{DatabaseIdPrefix}-{Guid.NewGuid():N}";
            this.containerId = $"{ContainerIdPrefix}-{Guid.NewGuid():N}";

            Cosmos.Database database = (await this.client.CreateDatabaseAsync(this.databaseId)).Database;
            ContainerResponse containerResponse = await database.CreateContainerAsync(
                new ContainerProperties(this.containerId, PartitionKeyPath));
            this.container = containerResponse.Container;
            Console.WriteLine($"Created fresh database '{this.databaseId}' and container '{this.containerId}'.");
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            if (this.client != null)
            {
                try
                {
                    if (!string.IsNullOrEmpty(this.databaseId))
                    {
                        await this.client.GetDatabase(this.databaseId).DeleteAsync();
                    }
                }
                catch
                {
                    // Ignore cleanup errors.
                }

                this.client.Dispose();
            }
        }

        [TestMethod]
        [Description("Comprehensive DTX read transaction: all positive and negative scenarios in one run, using point writes for setup.")]
        public async Task ReadDtx_AllScenarios_PositiveAndNegative()
        {
            // ================================================================
            // Scenario 1: All items exist, no conditionals -> 200 OK envelope,
            //             every op 200 with body + ETag, Index matches order.
            // ================================================================
            string pkA = $"s1-a-{Guid.NewGuid():N}";
            string pkB = $"s1-b-{Guid.NewGuid():N}";
            string idA = Guid.NewGuid().ToString();
            string idB = Guid.NewGuid().ToString();

            await this.container.CreateItemAsync(
                new TestDoc { id = idA, pk = pkA, value = "alpha", taskNum = 1 }, new PartitionKey(pkA), cancellationToken: this.cancellationToken);
            await this.container.CreateItemAsync(
                new TestDoc { id = idB, pk = pkB, value = "bravo", taskNum = 2 }, new PartitionKey(pkB), cancellationToken: this.cancellationToken);

            Console.WriteLine("=== Scenario 1: all exist, no conditionals ===");
            using (DistributedTransactionResponse resp = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(pkA), idA)
                .ReadItem(this.container, new PartitionKey(pkB), idB)
                .CommitTransactionAsync(this.cancellationToken))
            {
                // Outer
                Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode, "S1 envelope should be 200 OK");
                Assert.IsTrue(resp.IsSuccessStatusCode, "S1 envelope IsSuccessStatusCode should be true");
                Assert.AreEqual(2, resp.Count, "S1 should have 2 operation results");
                Assert.IsTrue(resp.RequestCharge > 0, "S1 transaction RequestCharge should be > 0");
                Assert.IsFalse(string.IsNullOrWhiteSpace(resp.ActivityId), "S1 ActivityId should be populated");

                // Inner (Scenario 7: Index ordering on success)
                for (int i = 0; i < resp.Count; i++)
                {
                    Assert.AreEqual(HttpStatusCode.OK, resp[i].StatusCode, $"S1 Op[{i}] should be 200 OK");
                    Assert.IsTrue(resp[i].IsSuccessStatusCode, $"S1 Op[{i}] IsSuccessStatusCode should be true");
                    Assert.AreEqual(i, resp[i].Index, $"S1 Op[{i}] Index should match submission order");
                    Assert.IsNotNull(resp[i].ResourceStream, $"S1 Op[{i}] should carry a resource body");
                    Assert.IsTrue(resp[i].ResourceStream.Length > 0, $"S1 Op[{i}] body should be non-empty");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(resp[i].ETag), $"S1 Op[{i}] should carry an ETag");
                    Assert.IsTrue(resp[i].RequestCharge > 0, $"S1 Op[{i}] RequestCharge should be > 0");
                }

                // Scenario 9: typed accessor deserializes the body.
                DistributedTransactionOperationResult<TestDoc> typed = resp.GetOperationResultAtIndex<TestDoc>(0);
                Assert.IsNotNull(typed.Resource, "S1 typed accessor should return a resource");
                Assert.AreEqual(idA, typed.Resource.id, "S1 typed resource id should match doc A");
                Assert.AreEqual("alpha", typed.Resource.value, "S1 typed resource value should match doc A");
            }

            // ================================================================
            // Scenario 2: Read of a SINGLE missing document -> envelope 404,
            //             the op is 404 NotFound with no body.
            // ================================================================
            string pkMissing = $"s2-missing-{Guid.NewGuid():N}";
            string idMissing = Guid.NewGuid().ToString();

            Console.WriteLine("=== Scenario 2: single missing doc ===");
            using (DistributedTransactionResponse resp = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(pkMissing), idMissing)
                .CommitTransactionAsync(this.cancellationToken))
            {
                Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode, "S2 envelope should be 404 NotFound");
                Assert.IsFalse(resp.IsSuccessStatusCode, "S2 envelope IsSuccessStatusCode should be false");
                Assert.AreEqual(1, resp.Count, "S2 should have 1 operation result");
                Assert.AreEqual(HttpStatusCode.NotFound, resp[0].StatusCode, "S2 Op[0] should be 404 NotFound");
                Assert.IsFalse(resp[0].IsSuccessStatusCode, "S2 Op[0] IsSuccessStatusCode should be false");
                Assert.IsNull(resp[0].ResourceStream, "S2 Op[0] (404) should have no resource body");
            }

            // ================================================================
            // Scenario 3: PARTIAL failure - one exists, one missing.
            // Observed behavior: a distributed READ transaction is atomic over its reads, so when
            // ANY targeted document is missing the whole transaction surfaces 404 and EVERY operation
            // reports a failure status (no bodies). The aborted (would-have-succeeded) read reports
            // either 404 NotFound or 424 FailedDependency depending on the deployment. Accept both.
            // ================================================================
            string pkExists = $"s3-exists-{Guid.NewGuid():N}";
            string idExists = Guid.NewGuid().ToString();
            string pkGone = $"s3-gone-{Guid.NewGuid():N}";
            string idGone = Guid.NewGuid().ToString();

            await this.container.CreateItemAsync(
                new TestDoc { id = idExists, pk = pkExists, value = "present", taskNum = 3 }, new PartitionKey(pkExists), cancellationToken: this.cancellationToken);

            Console.WriteLine("=== Scenario 3: partial failure (one exists, one missing) ===");
            using (DistributedTransactionResponse resp = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(pkExists), idExists)
                .ReadItem(this.container, new PartitionKey(pkGone), idGone)
                .CommitTransactionAsync(this.cancellationToken))
            {
                Assert.AreEqual(HttpStatusCode.NotFound, resp.StatusCode, "S3 envelope should be 404 NotFound");
                Assert.IsFalse(resp.IsSuccessStatusCode, "S3 envelope IsSuccessStatusCode should be false");
                Assert.AreEqual(2, resp.Count, "S3 should have 2 operation results (all present)");

                bool found404 = false;
                for (int i = 0; i < resp.Count; i++)
                {
                    Console.WriteLine($"S3 Op[{i}] StatusCode={(int)resp[i].StatusCode} ({resp[i].StatusCode}), HasBody={resp[i].ResourceStream != null}");
                    if (resp[i].StatusCode == HttpStatusCode.NotFound) { found404 = true; }

                    // Every op must be a failure (404 NotFound or 424 FailedDependency) with no body.
                    bool isFailureStatus = resp[i].StatusCode == HttpStatusCode.NotFound || (int)resp[i].StatusCode == 424;
                    Assert.IsTrue(isFailureStatus, $"S3 Op[{i}] should be 404 NotFound or 424 FailedDependency, but was {(int)resp[i].StatusCode}");
                    Assert.IsFalse(resp[i].IsSuccessStatusCode, $"S3 Op[{i}] should not be a success");
                    Assert.IsNull(resp[i].ResourceStream, $"S3 Op[{i}] (failed/aborted) should carry no body");
                }

                // The missing document must surface 404 somewhere in the result set.
                Assert.IsTrue(found404, "S3 should contain a 404 NotFound op for the missing document");
            }

            // ================================================================
            // Scenario 4: Conditional read with a MATCHING IfNoneMatch on one of two ops
            //             -> 207 MultiStatus; matching op is 304 NotModified (no body),
            //             the other op is 200 OK with body.
            // ================================================================
            string pkC1 = $"s4-a-{Guid.NewGuid():N}";
            string pkC2 = $"s4-b-{Guid.NewGuid():N}";
            string idC1 = Guid.NewGuid().ToString();
            string idC2 = Guid.NewGuid().ToString();

            await this.container.CreateItemAsync(
                new TestDoc { id = idC1, pk = pkC1, value = "cond1", taskNum = 41 }, new PartitionKey(pkC1), cancellationToken: this.cancellationToken);
            ItemResponse<TestDoc> createC2 = await this.container.CreateItemAsync(
                new TestDoc { id = idC2, pk = pkC2, value = "cond2", taskNum = 42 }, new PartitionKey(pkC2), cancellationToken: this.cancellationToken);
            string matchingEtagC2 = createC2.ETag;

            Console.WriteLine("=== Scenario 4: matching IfNoneMatch -> 207 + 304 ===");
            using (DistributedTransactionResponse resp = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(pkC1), idC1)
                .ReadItem(this.container, new PartitionKey(pkC2), idC2,
                    new DistributedTransactionRequestOptions { IfNoneMatchEtag = matchingEtagC2 })
                .CommitTransactionAsync(this.cancellationToken))
            {
                Assert.AreEqual((HttpStatusCode)207, resp.StatusCode, "S4 envelope should be 207 MultiStatus");
                Assert.AreEqual(2, resp.Count, "S4 should have 2 operation results");

                Assert.AreEqual(HttpStatusCode.OK, resp[0].StatusCode, "S4 Op[0] (no condition) should be 200 OK");
                Assert.IsNotNull(resp[0].ResourceStream, "S4 Op[0] should carry a body");

                // Scenario 8: empty / no-body handling on 304.
                Assert.AreEqual(HttpStatusCode.NotModified, resp[1].StatusCode, "S4 Op[1] (matching IfNoneMatch) should be 304");
                Assert.IsFalse(resp[1].IsSuccessStatusCode, "S4 Op[1] 304 should not be a success");
                Assert.IsNull(resp[1].ResourceStream, "S4 Op[1] 304 should have no resource body");
            }

            // ================================================================
            // Scenario 5: Conditional read with a STALE IfNoneMatch (item modified
            //             after the ETag was captured) -> 200 OK with updated body + new ETag.
            // ================================================================
            string pkStale = $"s5-{Guid.NewGuid():N}";
            string idStale = Guid.NewGuid().ToString();

            ItemResponse<TestDoc> createStale = await this.container.CreateItemAsync(
                new TestDoc { id = idStale, pk = pkStale, value = "original", taskNum = 50 }, new PartitionKey(pkStale), cancellationToken: this.cancellationToken);
            string staleEtag = createStale.ETag;

            // Point-write replace to change the ETag.
            await this.container.ReplaceItemAsync(
                new TestDoc { id = idStale, pk = pkStale, value = "updated", taskNum = 51 }, idStale, new PartitionKey(pkStale), cancellationToken: this.cancellationToken);

            Console.WriteLine("=== Scenario 5: stale IfNoneMatch -> 200 with updated resource ===");
            using (DistributedTransactionResponse resp = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(pkStale), idStale,
                    new DistributedTransactionRequestOptions { IfNoneMatchEtag = staleEtag })
                .CommitTransactionAsync(this.cancellationToken))
            {
                Assert.IsTrue(resp.IsSuccessStatusCode, "S5 envelope should be a success (stale IfNoneMatch -> 200)");
                Assert.AreEqual(1, resp.Count, "S5 should have 1 operation result");
                Assert.AreEqual(HttpStatusCode.OK, resp[0].StatusCode, "S5 Op[0] should be 200 OK");
                Assert.IsNotNull(resp[0].ResourceStream, "S5 Op[0] should carry the updated body");
                Assert.IsFalse(string.IsNullOrWhiteSpace(resp[0].ETag), "S5 Op[0] should carry the new ETag");
                Assert.AreNotEqual(staleEtag, resp[0].ETag, "S5 Op[0] ETag should differ from the stale one");

                DistributedTransactionOperationResult<TestDoc> typed = resp.GetOperationResultAtIndex<TestDoc>(0);
                Assert.AreEqual("updated", typed.Resource.value, "S5 should return the updated value");
            }

            // ================================================================
            // Scenario 6: ALL ops have matching IfNoneMatch -> every op 304 NotModified.
            // ================================================================
            string pkD1 = $"s6-a-{Guid.NewGuid():N}";
            string pkD2 = $"s6-b-{Guid.NewGuid():N}";
            string idD1 = Guid.NewGuid().ToString();
            string idD2 = Guid.NewGuid().ToString();

            ItemResponse<TestDoc> createD1 = await this.container.CreateItemAsync(
                new TestDoc { id = idD1, pk = pkD1, value = "d1", taskNum = 61 }, new PartitionKey(pkD1), cancellationToken: this.cancellationToken);
            ItemResponse<TestDoc> createD2 = await this.container.CreateItemAsync(
                new TestDoc { id = idD2, pk = pkD2, value = "d2", taskNum = 62 }, new PartitionKey(pkD2), cancellationToken: this.cancellationToken);

            Console.WriteLine("=== Scenario 6: all matching IfNoneMatch -> all 304 ===");
            using (DistributedTransactionResponse resp = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(pkD1), idD1,
                    new DistributedTransactionRequestOptions { IfNoneMatchEtag = createD1.ETag })
                .ReadItem(this.container, new PartitionKey(pkD2), idD2,
                    new DistributedTransactionRequestOptions { IfNoneMatchEtag = createD2.ETag })
                .CommitTransactionAsync(this.cancellationToken))
            {
                Assert.AreEqual(2, resp.Count, "S6 should have 2 operation results");
                for (int i = 0; i < resp.Count; i++)
                {
                    Assert.AreEqual(HttpStatusCode.NotModified, resp[i].StatusCode, $"S6 Op[{i}] should be 304 NotModified");
                    Assert.IsFalse(resp[i].IsSuccessStatusCode, $"S6 Op[{i}] 304 should not be a success");
                    Assert.IsNull(resp[i].ResourceStream, $"S6 Op[{i}] 304 should have no body");
                }
            }

            Console.WriteLine("\nAll comprehensive DTX read scenarios passed!");
        }

        [TestMethod]
        [Description("Single-document DTX read lifecycle: (1) read a missing doc -> 404 outer+inner; (2) point-write then DTX read -> 200 outer+inner with ETag and content; (3) DTX read with matching IfNoneMatch -> 304 outer+inner, no content; (4) DTX read with a random IfNoneMatch -> 200 outer+inner with content.")]
        public async Task ReadDtx_SingleExistingDocument_Returns200WithBody()
        {
            string pk = $"single-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();

            // ================================================================
            // Step 1: DTX read the document BEFORE it exists -> 404 outer + inner.
            // ================================================================
            Console.WriteLine($"=== Step 1: DTX read missing doc (expect 404) against {this.container.Database.Id}/{this.container.Id} ===");
            using (DistributedTransactionResponse missingResp = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(pk), id)
                .CommitTransactionAsync(this.cancellationToken))
            {
                Console.WriteLine($"Step1 Envelope StatusCode={missingResp.StatusCode}, Count={missingResp.Count}, Error={missingResp.ErrorMessage}");

                // Outer
                Assert.AreEqual(HttpStatusCode.NotFound, missingResp.StatusCode, $"Step1 envelope should be 404 NotFound. Error: {missingResp.ErrorMessage}");
                Assert.IsFalse(missingResp.IsSuccessStatusCode, "Step1 envelope IsSuccessStatusCode should be false");
                Assert.AreEqual(1, missingResp.Count, "Step1 should have exactly one operation result");

                // Inner
                DistributedTransactionOperationResult missingOp = missingResp[0];
                Assert.AreEqual(HttpStatusCode.NotFound, missingOp.StatusCode, "Step1 op should be 404 NotFound");
                Assert.IsFalse(missingOp.IsSuccessStatusCode, "Step1 op IsSuccessStatusCode should be false");
                Assert.IsNull(missingOp.ResourceStream, "Step1 op (404) should have no resource body");
            }

            // ================================================================
            // Step 2: Point-write the document, then DTX read -> 200 outer + inner,
            //         capture the ETag and verify the returned content.
            // ================================================================
            ItemResponse<TestDoc> create = await this.container.CreateItemAsync(
                new TestDoc { id = id, pk = pk, value = "solo", taskNum = 7 },
                new PartitionKey(pk),
                cancellationToken: this.cancellationToken);
            Assert.AreEqual(HttpStatusCode.Created, create.StatusCode, "Point-write setup should return 201 Created");

            string capturedEtag;
            Console.WriteLine("=== Step 2: DTX read existing doc (expect 200 + content) ===");
            using (DistributedTransactionResponse resp = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(pk), id)
                .CommitTransactionAsync(this.cancellationToken))
            {
                Console.WriteLine($"Step2 Envelope StatusCode={resp.StatusCode}, Count={resp.Count}, Charge={resp.RequestCharge}, Error={resp.ErrorMessage}");

                // Outer
                Assert.AreEqual(HttpStatusCode.OK, resp.StatusCode, $"Step2 envelope should be 200 OK. Error: {resp.ErrorMessage}");
                Assert.IsTrue(resp.IsSuccessStatusCode, "Step2 envelope IsSuccessStatusCode should be true");
                Assert.AreEqual(1, resp.Count, "Step2 should have exactly one operation result");
                Assert.IsTrue(resp.RequestCharge > 0, "Step2 transaction RequestCharge should be > 0");

                // Inner
                DistributedTransactionOperationResult op = resp[0];
                Assert.AreEqual(0, op.Index, "Step2 op Index should be 0");
                Assert.AreEqual(HttpStatusCode.OK, op.StatusCode, "Step2 op should be 200 OK");
                Assert.IsTrue(op.IsSuccessStatusCode, "Step2 op IsSuccessStatusCode should be true");
                Assert.IsNotNull(op.ResourceStream, "Step2 op should carry a resource body");
                Assert.IsTrue(op.ResourceStream.Length > 0, "Step2 op body should be non-empty");

                // ETag is present; capture it for the conditional reads below.
                Assert.IsFalse(string.IsNullOrWhiteSpace(op.ETag), "Step2 op should carry an ETag");
                capturedEtag = op.ETag;
                Console.WriteLine($"Step2 captured ETag={capturedEtag}");

                // Content check via the typed accessor.
                DistributedTransactionOperationResult<TestDoc> typed = resp.GetOperationResultAtIndex<TestDoc>(0);
                Assert.IsNotNull(typed.Resource, "Step2 typed accessor should return a resource");
                Assert.AreEqual(id, typed.Resource.id, "Step2 returned document id should match");
                Assert.AreEqual(pk, typed.Resource.pk, "Step2 returned document pk should match");
                Assert.AreEqual("solo", typed.Resource.value, "Step2 returned document value should match");
                Assert.AreEqual(7, typed.Resource.taskNum, "Step2 returned document taskNum should match");
            }

            // ================================================================
            // Step 3: DTX read with a MATCHING IfNoneMatch (the captured ETag)
            //         -> 304 outer + inner, no content returned.
            // ================================================================
            Console.WriteLine("=== Step 3: DTX read with matching IfNoneMatch (expect 304, no content) ===");
            using (DistributedTransactionResponse notModifiedResp = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(pk), id,
                    new DistributedTransactionRequestOptions { IfNoneMatchEtag = capturedEtag })
                .CommitTransactionAsync(this.cancellationToken))
            {
                Console.WriteLine($"Step3 Envelope StatusCode={notModifiedResp.StatusCode}, Count={notModifiedResp.Count}, Error={notModifiedResp.ErrorMessage}");

                // Outer
                Assert.AreEqual(HttpStatusCode.NotModified, notModifiedResp.StatusCode, $"Step3 envelope should be 304 NotModified. Error: {notModifiedResp.ErrorMessage}");
                Assert.IsFalse(notModifiedResp.IsSuccessStatusCode, "Step3 envelope IsSuccessStatusCode should be false (304 is not 2xx)");
                Assert.AreEqual(1, notModifiedResp.Count, "Step3 should have exactly one operation result");

                // Inner
                DistributedTransactionOperationResult notModifiedOp = notModifiedResp[0];
                Assert.AreEqual(HttpStatusCode.NotModified, notModifiedOp.StatusCode, "Step3 op should be 304 NotModified");
                Assert.IsFalse(notModifiedOp.IsSuccessStatusCode, "Step3 op IsSuccessStatusCode should be false");
                Assert.IsNull(notModifiedOp.ResourceStream, "Step3 op (304) should NOT return any content");
            }

            // ================================================================
            // Step 4: DTX read with a RANDOM (non-matching) IfNoneMatch
            //         -> 200 outer + inner, document content returned.
            // ================================================================
            string randomEtag = $"\"{Guid.NewGuid()}\"";
            Console.WriteLine($"=== Step 4: DTX read with random IfNoneMatch={randomEtag} (expect 200 + content) ===");
            using (DistributedTransactionResponse randomResp = await this.client
                .CreateDistributedReadTransaction()
                .ReadItem(this.container, new PartitionKey(pk), id,
                    new DistributedTransactionRequestOptions { IfNoneMatchEtag = randomEtag })
                .CommitTransactionAsync(this.cancellationToken))
            {
                Console.WriteLine($"Step4 Envelope StatusCode={randomResp.StatusCode}, Count={randomResp.Count}, Error={randomResp.ErrorMessage}");

                // Outer
                Assert.AreEqual(HttpStatusCode.OK, randomResp.StatusCode, $"Step4 envelope should be 200 OK. Error: {randomResp.ErrorMessage}");
                Assert.IsTrue(randomResp.IsSuccessStatusCode, "Step4 envelope IsSuccessStatusCode should be true");
                Assert.AreEqual(1, randomResp.Count, "Step4 should have exactly one operation result");

                // Inner
                DistributedTransactionOperationResult randomOp = randomResp[0];
                Assert.AreEqual(HttpStatusCode.OK, randomOp.StatusCode, "Step4 op should be 200 OK (random ETag does not match)");
                Assert.IsTrue(randomOp.IsSuccessStatusCode, "Step4 op IsSuccessStatusCode should be true");
                Assert.IsNotNull(randomOp.ResourceStream, "Step4 op should return the document content");
                Assert.IsTrue(randomOp.ResourceStream.Length > 0, "Step4 op body should be non-empty");

                DistributedTransactionOperationResult<TestDoc> typedRandom = randomResp.GetOperationResultAtIndex<TestDoc>(0);
                Assert.IsNotNull(typedRandom.Resource, "Step4 typed accessor should return a resource");
                Assert.AreEqual(id, typedRandom.Resource.id, "Step4 returned document id should match");
                Assert.AreEqual("solo", typedRandom.Resource.value, "Step4 returned document value should match");
            }

            Console.WriteLine("Single-doc DTX read lifecycle (404 -> 200 -> 304 -> 200) verified!");
        }

        [TestMethod]
        [Description("Minimal DTX write: a single-operation distributed write transaction that creates one document, then verifies it via a point read. Confirms the write envelope/op are successful and the document persisted.")]
        public async Task WriteDtx_SingleCreate_SucceedsAndPersists()
        {
            string pk = $"w-single-{Guid.NewGuid():N}";
            string id = Guid.NewGuid().ToString();

            Console.WriteLine($"=== Single-op DTX write against {this.container.Database.Id}/{this.container.Id} ===");
            using (DistributedTransactionResponse resp = await this.client
                .CreateDistributedWriteTransaction()
                .CreateItem(this.container, new PartitionKey(pk), id, new TestDoc { id = id, pk = pk, value = "dtx-write", taskNum = 77 })
                .CommitTransactionAsync(this.cancellationToken))
            {
                Console.WriteLine($"Envelope StatusCode={resp.StatusCode}, Count={resp.Count}, Charge={resp.RequestCharge}, Error={resp.ErrorMessage}");

                // Outer
                Assert.IsTrue(resp.IsSuccessStatusCode, $"Write envelope should be a success. Error: {resp.ErrorMessage}");
                Assert.AreEqual(1, resp.Count, "Should have exactly one operation result");
                Assert.IsTrue(resp.RequestCharge > 0, "Transaction RequestCharge should be > 0");
                Assert.IsFalse(string.IsNullOrWhiteSpace(resp.ActivityId), "ActivityId should be populated");

                // Inner
                DistributedTransactionOperationResult op = resp[0];
                Assert.AreEqual(0, op.Index, "Op Index should be 0");
                Assert.AreEqual(HttpStatusCode.Created, op.StatusCode, "Create op should be 201 Created");
                Assert.IsTrue(op.IsSuccessStatusCode, "Op IsSuccessStatusCode should be true");
                Assert.IsFalse(string.IsNullOrWhiteSpace(op.ETag), "Op should carry an ETag");
                Assert.IsTrue(op.RequestCharge > 0, "Op RequestCharge should be > 0");
            }

            // Verify persistence with a point read (no DTX).
            ItemResponse<TestDoc> readBack = await this.container.ReadItemAsync<TestDoc>(
                id, new PartitionKey(pk), cancellationToken: this.cancellationToken);
            Console.WriteLine($"Point read-back StatusCode={readBack.StatusCode}, value={readBack.Resource?.value}");
            Assert.AreEqual(HttpStatusCode.OK, readBack.StatusCode, "Document created via DTX write should be readable");
            Assert.AreEqual("dtx-write", readBack.Resource.value, "Persisted document value should match");

            Console.WriteLine("Single-op DTX write verified!");
        }

        private sealed class TestDoc
        {
            public string id { get; set; }

            public string pk { get; set; }

            public string value { get; set; }

            public int taskNum { get; set; }
        }
    }

    /// <summary>
    /// E2E test that authenticates the current signed-in user (absa) against a real Cosmos DB
    /// account using AAD (Entra ID) token auth via <see cref="DefaultAzureCredential"/>,
    /// creates a database and container named 'absadb'/'absacoll', and inserts 10,000
    /// documents using point writes.
    ///
    /// The database and container are intentionally NOT deleted after the run.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    [TestCategory("DistributedTransaction")]
    public class AbsaPointWriteE2ETests
    {
        // AAD (token) auth against the absa test account — no account key required.
        private const string Endpoint = "https://absa24july2026.documents.azure.com:443/";
        private const string DatabaseId = "absadb";
        private const string ContainerId = "absacoll";
        private const string PartitionKeyPath = "/pk";
        private const int DocumentCount = 10000;

        private CosmosClient client;
        private Container container;
        private readonly CancellationToken cancellationToken = CancellationToken.None;

        [TestInitialize]
        public void TestInitialize()
        {
            string endpoint = Environment.GetEnvironmentVariable("COSMOS_ABSA_ENDPOINT") ?? Endpoint;

            // AAD token auth for the current signed-in user (absa) via DefaultAzureCredential.
            // This resolves the identity from az login / Visual Studio / environment credentials.
            TokenCredential tokenCredential = new DefaultAzureCredential();

            this.client = new CosmosClient(
                endpoint,
                tokenCredential,
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    ConsistencyLevel = Cosmos.ConsistencyLevel.Session
                });
        }

        [TestCleanup]
        public void TestCleanup()
        {
            // Intentionally do NOT delete the database or container.
            this.client?.Dispose();
        }

        [TestMethod]
        [Description("Creates absadb/absacoll (if not present) and inserts 10,000 documents via point writes. Does not delete the database or container.")]
        public async Task Absa_CreateDbAndCollection_Insert10000Documents_ByPointWrite()
        {
            Cosmos.Database database = (await this.client.CreateDatabaseIfNotExistsAsync(
                DatabaseId,
                cancellationToken: this.cancellationToken)).Database;
            Console.WriteLine($"Ensured database '{DatabaseId}' exists.");

            ContainerResponse containerResponse = await database.CreateContainerIfNotExistsAsync(
                new ContainerProperties(ContainerId, PartitionKeyPath),
                cancellationToken: this.cancellationToken);
            this.container = containerResponse.Container;
            Console.WriteLine($"Ensured container '{ContainerId}' exists.");

            int inserted = 0;
            for (int i = 0; i < DocumentCount; i++)
            {
                AbsaDoc doc = new AbsaDoc
                {
                    id = Guid.NewGuid().ToString(),
                    pk = $"pk-{i % 100}",
                    value = $"absa-value-{i}",
                    taskNum = i
                };

                ItemResponse<AbsaDoc> response = await this.container.CreateItemAsync(
                    doc,
                    new PartitionKey(doc.pk),
                    cancellationToken: this.cancellationToken);

                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode, $"Point write for document {i} should return 201 Created.");
                inserted++;

                if (inserted % 1000 == 0)
                {
                    Console.WriteLine($"Inserted {inserted}/{DocumentCount} documents...");
                }
            }

            Console.WriteLine($"Successfully inserted {inserted} documents into '{DatabaseId}'/'{ContainerId}' via point writes.");
            Assert.AreEqual(DocumentCount, inserted, "All documents should have been inserted.");
        }

        private sealed class AbsaDoc
        {
            public string id { get; set; }

            public string pk { get; set; }

            public string value { get; set; }

            public int taskNum { get; set; }
        }
    }
}
