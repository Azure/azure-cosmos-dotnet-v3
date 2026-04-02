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

    [TestClass]
    [DoNotParallelize]
    public class DistributedTransactionE2ETests : BaseCosmosClientHelper
    {   
        private const string IdempotencyTokenHeader = HttpConstants.HttpHeaders.IdempotencyToken;
        private const string PartitionKeyPath = "/pk";

        private Container container;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await this.TestInit();

            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKeyPath),
                cancellationToken: this.cancellationToken);

            this.container = response.Container;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task ValidateSessionTokenMergedIntoDtcClient()
        {
            ToDoActivity seedDoc = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> seedResponse = await this.container.CreateItemAsync(seedDoc, new PartitionKey(seedDoc.pk), cancellationToken: this.cancellationToken);

            string validSessionToken = seedResponse.Headers.Session;
            Assert.IsFalse(string.IsNullOrEmpty(validSessionToken), "A valid session token must be obtained from the emulator for this test to be meaningful.");

            string dtcMockResponse = $@"{{""operationResponses"":[{{""index"":0,""statuscode"":201,""sessionToken"":""{validSessionToken}""}}]}}";

            DistributedTransactionTestHandler handler = CreateMockHandler(HttpStatusCode.OK, dtcMockResponse);
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

        [TestMethod]
        public async Task ValidateHappyPathRequestAndResponse()
        {
            // Arrange
            ToDoActivity doc1 = ToDoActivity.CreateRandomToDoActivity();
            ToDoActivity doc2 = ToDoActivity.CreateRandomToDoActivity();
            
            DistributedTransactionTestHandler handler = CreateMockHandler(
                HttpStatusCode.OK, 
                CreateMockSuccessResponse(operationCount: 2));

            using CosmosClient client = TestCommon.CreateCosmosClient(
                clientOptions: new CosmosClientOptions
                {
                    CustomHandlers = { handler },
                    ConnectionMode = ConnectionMode.Gateway
                });

            // Act
            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(doc1.pk), doc1)
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(doc2.pk), doc2)
                .CommitTransactionAsync(CancellationToken.None);

            // Assert - Request
            Assert.IsNotNull(handler.CapturedRequest);
            Assert.IsNotNull(handler.CapturedRequest.Headers[IdempotencyTokenHeader]);
            ValidateRequestBody(handler.CapturedRequestBody, doc1, doc2);

            // Assert - Response
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.AreEqual(2, response.Count);

            response.Dispose();
        }

        [TestMethod]
        public async Task ValidateMixedOperationsRequestStructure()
        {
            // Arrange
            ToDoActivity createDoc = ToDoActivity.CreateRandomToDoActivity();
            ToDoActivity replaceDoc = ToDoActivity.CreateRandomToDoActivity();
            
            DistributedTransactionTestHandler handler = CreateMockHandler(
                HttpStatusCode.OK, 
                CreateMockSuccessResponse(operationCount: 3));

            using CosmosClient client = TestCommon.CreateCosmosClient(
                clientOptions: new CosmosClientOptions
                {
                    CustomHandlers = { handler },
                    ConnectionMode = ConnectionMode.Gateway
                });

            // Act
            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(createDoc.pk), createDoc)
                .ReplaceItem(this.database.Id, this.container.Id, new PartitionKey(replaceDoc.pk), replaceDoc.id, replaceDoc)
                .DeleteItem(this.database.Id, this.container.Id, new PartitionKey("delete-pk"), "delete-id")
                .CommitTransactionAsync(CancellationToken.None);

            // Assert
            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement operations = requestJson.RootElement.GetProperty("operations");
            
            Assert.AreEqual(3, operations.GetArrayLength());
            Assert.AreEqual(OperationType.Create.ToString(), operations[0].GetProperty("operationType").GetString()); // Create
            Assert.AreEqual(OperationType.Replace.ToString(), operations[1].GetProperty("operationType").GetString()); // Replace  
            Assert.AreEqual(OperationType.Delete.ToString(), operations[2].GetProperty("operationType").GetString()); // Delete

            response.Dispose();
        }

        [TestMethod]
        public async Task ValidateSerializedRequestFieldDataTypes()
        {
            // Arrange
            ToDoActivity createDoc = ToDoActivity.CreateRandomToDoActivity();
            ToDoActivity replaceDoc = ToDoActivity.CreateRandomToDoActivity();

            DistributedTransactionTestHandler handler = CreateMockHandler(
                HttpStatusCode.OK,
                CreateMockSuccessResponse(operationCount: 3));

            using CosmosClient client = TestCommon.CreateCosmosClient(
                clientOptions: new CosmosClientOptions
                {
                    CustomHandlers = { handler },
                    ConnectionMode = ConnectionMode.Gateway
                });

            // Act
            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(createDoc.pk), createDoc)
                .ReplaceItem(this.database.Id, this.container.Id, new PartitionKey(replaceDoc.pk), replaceDoc.id, replaceDoc)
                .DeleteItem(this.database.Id, this.container.Id, new PartitionKey("delete-pk"), "delete-id")
                .CommitTransactionAsync(CancellationToken.None);

            // Assert - Parse captured request
            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);

            // Verify root structure
            Assert.AreEqual(JsonValueKind.Object, requestJson.RootElement.ValueKind, "Root element should be an object");

            // Verify operations array
            Assert.IsTrue(requestJson.RootElement.TryGetProperty("operations", out JsonElement operations), "operations property should exist");
            Assert.AreEqual(JsonValueKind.Array, operations.ValueKind, "operations should be an array");
            Assert.AreEqual(3, operations.GetArrayLength(), "operations should have 3 elements");

            // Validate datatypes for each operation
            int operationIndex = 0;
            foreach (JsonElement operation in operations.EnumerateArray())
            {
                // Verify operation is an object
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

        [TestMethod]
        public async Task ValidateConflictResponseReturnsErrorStatus()
        {
            // Arrange
            string mockErrorResponse = @"{
                ""operationResponses"": [{
                    ""index"": 0,
                    ""statuscode"": 409,
                    ""substatuscode"": 0
                }]
            }";

            DistributedTransactionTestHandler handler = CreateMockHandler(HttpStatusCode.Conflict, mockErrorResponse);
            using CosmosClient client = TestCommon.CreateCosmosClient(
                clientOptions: new CosmosClientOptions
                {
                    CustomHandlers = { handler },
                    ConnectionMode = ConnectionMode.Gateway
                });

            ToDoActivity doc = ToDoActivity.CreateRandomToDoActivity();

            // Act
            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(doc.pk), doc)
                .CommitTransactionAsync(CancellationToken.None);

            // Assert
            Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
            Assert.IsFalse(response.IsSuccessStatusCode);
            Assert.AreEqual(1, response.Count);
            Assert.AreEqual(HttpStatusCode.Conflict, response[0].StatusCode);

            response.Dispose();
        }

        [TestMethod]
        public async Task ValidateResponseDeserializesCorrectly()
        {
            // Arrange
            ToDoActivity expectedDoc = ToDoActivity.CreateRandomToDoActivity();
            string base64Body = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(expectedDoc)));
            
            string mockResponse = $@"{{
                ""operationResponses"": [{{
                    ""index"": 0,
                    ""statuscode"": 201,
                    ""etag"": ""\""test-etag\"""",
                    ""resourcebody"": ""{base64Body}""
                }}]
            }}";

            DistributedTransactionTestHandler handler = CreateMockHandler(HttpStatusCode.OK, mockResponse);
            using CosmosClient client = TestCommon.CreateCosmosClient(
                clientOptions: new CosmosClientOptions
                {
                    CustomHandlers = { handler },
                    ConnectionMode = ConnectionMode.Gateway
                });

            // Act
            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(expectedDoc.pk), expectedDoc)
                .CommitTransactionAsync(CancellationToken.None);

            // Assert
            Assert.AreEqual(HttpStatusCode.Created, response[0].StatusCode);
            Assert.AreEqual("\"test-etag\"", response[0].ETag);
            Assert.IsNotNull(response[0].ResourceStream);

            using StreamReader reader = new StreamReader(response[0].ResourceStream);
            ToDoActivity returnedDoc = JsonSerializer.Deserialize<ToDoActivity>(await reader.ReadToEndAsync());
            
            Assert.AreEqual(expectedDoc.id, returnedDoc.id);
            Assert.AreEqual(expectedDoc.pk, returnedDoc.pk);

            response.Dispose();
        }

        [TestMethod]
        public async Task ValidateReplaceItemWithIfMatchEtagSerializedToRequest()
        {
            // Arrange
            ToDoActivity doc = ToDoActivity.CreateRandomToDoActivity();
            string expectedEtag = "\"test-etag-replace\"";

            DistributedTransactionTestHandler handler = CreateMockHandler(
                HttpStatusCode.OK,
                CreateMockSuccessResponse(operationCount: 1));

            using CosmosClient client = TestCommon.CreateCosmosClient(
                clientOptions: new CosmosClientOptions
                {
                    CustomHandlers = { handler },
                    ConnectionMode = ConnectionMode.Gateway
                });

            // Act
            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .ReplaceItem(
                    this.database.Id,
                    this.container.Id,
                    new PartitionKey(doc.pk),
                    doc.id,
                    doc,
                    new DistributedTransactionRequestOptions { IfMatchEtag = expectedEtag })
                .CommitTransactionAsync(CancellationToken.None);

            // Assert
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
        public async Task ValidateDeleteItemWithIfMatchEtagSerializedToRequest()
        {
            // Arrange
            string expectedEtag = "\"test-etag-delete\"";

            DistributedTransactionTestHandler handler = CreateMockHandler(
                HttpStatusCode.OK,
                CreateMockSuccessResponse(operationCount: 1));

            using CosmosClient client = TestCommon.CreateCosmosClient(
                clientOptions: new CosmosClientOptions
                {
                    CustomHandlers = { handler },
                    ConnectionMode = ConnectionMode.Gateway
                });

            // Act
            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .DeleteItem(
                    this.database.Id,
                    this.container.Id,
                    new PartitionKey("delete-pk"),
                    "delete-id",
                    new DistributedTransactionRequestOptions { IfMatchEtag = expectedEtag })
                .CommitTransactionAsync(CancellationToken.None);

            // Assert
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
        public async Task ValidatePatchItemWithIfMatchEtagSerializedToRequest()
        {
            // Arrange
            string expectedEtag = "\"test-etag-patch\"";
            IReadOnlyList<PatchOperation> patchOps = new[] { PatchOperation.Add("/description", "patched") };

            DistributedTransactionTestHandler handler = CreateMockHandler(
                HttpStatusCode.OK,
                CreateMockSuccessResponse(operationCount: 1));

            using CosmosClient client = TestCommon.CreateCosmosClient(
                clientOptions: new CosmosClientOptions
                {
                    CustomHandlers = { handler },
                    ConnectionMode = ConnectionMode.Gateway
                });

            // Act
            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .PatchItem(
                    this.database.Id,
                    this.container.Id,
                    new PartitionKey("patch-pk"),
                    "patch-id",
                    patchOps,
                    new DistributedTransactionRequestOptions { IfMatchEtag = expectedEtag })
                .CommitTransactionAsync(CancellationToken.None);

            // Assert
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
        public async Task ValidatePreconditionFailedResponse()
        {
            // Arrange
            string mockErrorResponse = @"{
                ""operationResponses"": [{
                    ""index"": 0,
                    ""statuscode"": 412,
                    ""substatuscode"": 0
                }]
            }";

            DistributedTransactionTestHandler handler = CreateMockHandler(HttpStatusCode.PreconditionFailed, mockErrorResponse);
            using CosmosClient client = TestCommon.CreateCosmosClient(
                clientOptions: new CosmosClientOptions
                {
                    CustomHandlers = { handler },
                    ConnectionMode = ConnectionMode.Gateway
                });

            ToDoActivity doc = ToDoActivity.CreateRandomToDoActivity();

            // Act
            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .ReplaceItem(
                    this.database.Id,
                    this.container.Id,
                    new PartitionKey(doc.pk),
                    doc.id,
                    doc,
                    new DistributedTransactionRequestOptions { IfMatchEtag = "\"stale-etag\"" })
                .CommitTransactionAsync(CancellationToken.None);

            // Assert
            Assert.AreEqual(HttpStatusCode.PreconditionFailed, response.StatusCode);
            Assert.IsFalse(response.IsSuccessStatusCode);
            Assert.AreEqual(1, response.Count);
            Assert.AreEqual(HttpStatusCode.PreconditionFailed, response[0].StatusCode);

            response.Dispose();
        }

        [TestMethod]
        public async Task ValidateOperationsWithoutIfMatchEtagDoNotSerializeEtagField()
        {
            // Arrange
            ToDoActivity createDoc = ToDoActivity.CreateRandomToDoActivity();
            ToDoActivity replaceDoc = ToDoActivity.CreateRandomToDoActivity();

            DistributedTransactionTestHandler handler = CreateMockHandler(
                HttpStatusCode.OK,
                CreateMockSuccessResponse(operationCount: 2));

            using CosmosClient client = TestCommon.CreateCosmosClient(
                clientOptions: new CosmosClientOptions
                {
                    CustomHandlers = { handler },
                    ConnectionMode = ConnectionMode.Gateway
                });

            // Act — no IfMatchEtag provided
            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .CreateItem(this.database.Id, this.container.Id, new PartitionKey(createDoc.pk), createDoc)
                .ReplaceItem(this.database.Id, this.container.Id, new PartitionKey(replaceDoc.pk), replaceDoc.id, replaceDoc)
                .CommitTransactionAsync(CancellationToken.None);

            // Assert — no etag field should be serialized when IfMatchEtag is not set
            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement operations = requestJson.RootElement.GetProperty("operations");
            foreach (JsonElement operation in operations.EnumerateArray())
            {
                Assert.IsFalse(operation.TryGetProperty("etag", out _), "etag field should not be present when IfMatchEtag is not set");
            }

            response.Dispose();
        }

        [TestMethod]
        public async Task ValidateCreateItemStreamOperation()
        {
            // Arrange
            ToDoActivity doc = ToDoActivity.CreateRandomToDoActivity();
            byte[] docBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(doc));

            DistributedTransactionTestHandler handler = CreateMockHandler(
                HttpStatusCode.OK,
                CreateMockSuccessResponse(operationCount: 1));

            using CosmosClient client = TestCommon.CreateCosmosClient(
                clientOptions: new CosmosClientOptions
                {
                    CustomHandlers = { handler },
                    ConnectionMode = ConnectionMode.Gateway
                });

            // Act
            using MemoryStream stream = new MemoryStream(docBytes);
            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .CreateItemStream(this.database.Id, this.container.Id, new PartitionKey(doc.pk), stream)
                .CommitTransactionAsync(CancellationToken.None);

            // Assert
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
        public async Task ValidateReplaceItemStreamOperation()
        {
            // Arrange
            ToDoActivity doc = ToDoActivity.CreateRandomToDoActivity();
            byte[] docBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(doc));

            DistributedTransactionTestHandler handler = CreateMockHandler(
                HttpStatusCode.OK,
                CreateMockSuccessResponse(operationCount: 1));

            using CosmosClient client = TestCommon.CreateCosmosClient(
                clientOptions: new CosmosClientOptions
                {
                    CustomHandlers = { handler },
                    ConnectionMode = ConnectionMode.Gateway
                });

            // Act
            using MemoryStream stream = new MemoryStream(docBytes);
            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .ReplaceItemStream(this.database.Id, this.container.Id, new PartitionKey(doc.pk), doc.id, stream)
                .CommitTransactionAsync(CancellationToken.None);

            // Assert
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
        public async Task ValidatePatchItemStreamOperation()
        {
            // Arrange
            string patchJson = @"{""operations"":[{""op"":""add"",""path"":""/description"",""value"":""patched""}]}";
            byte[] patchBytes = Encoding.UTF8.GetBytes(patchJson);

            DistributedTransactionTestHandler handler = CreateMockHandler(
                HttpStatusCode.OK,
                CreateMockSuccessResponse(operationCount: 1));

            using CosmosClient client = TestCommon.CreateCosmosClient(
                clientOptions: new CosmosClientOptions
                {
                    CustomHandlers = { handler },
                    ConnectionMode = ConnectionMode.Gateway
                });

            // Act
            using MemoryStream stream = new MemoryStream(patchBytes);
            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .PatchItemStream(this.database.Id, this.container.Id, new PartitionKey("patch-pk"), "patch-id", stream)
                .CommitTransactionAsync(CancellationToken.None);

            // Assert
            Assert.IsTrue(response.IsSuccessStatusCode);
            using JsonDocument requestJson = JsonDocument.Parse(handler.CapturedRequestBody);
            JsonElement operation = requestJson.RootElement.GetProperty("operations")[0];
            Assert.AreEqual(OperationType.Patch.ToString(), operation.GetProperty("operationType").GetString());
            Assert.AreEqual("patch-id", operation.GetProperty("id").GetString());

            response.Dispose();
        }

        [TestMethod]
        public async Task ValidateUpsertItemStreamOperation()
        {
            // Arrange
            ToDoActivity doc = ToDoActivity.CreateRandomToDoActivity();
            byte[] docBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(doc));

            DistributedTransactionTestHandler handler = CreateMockHandler(
                HttpStatusCode.OK,
                CreateMockSuccessResponse(operationCount: 1));

            using CosmosClient client = TestCommon.CreateCosmosClient(
                clientOptions: new CosmosClientOptions
                {
                    CustomHandlers = { handler },
                    ConnectionMode = ConnectionMode.Gateway
                });

            // Act
            using MemoryStream stream = new MemoryStream(docBytes);
            DistributedTransactionResponse response = await client.CreateDistributedWriteTransaction()
                .UpsertItemStream(this.database.Id, this.container.Id, new PartitionKey(doc.pk), stream)
                .CommitTransactionAsync(CancellationToken.None);

            // Assert
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

        #region Helper Methods

        private static DistributedTransactionTestHandler CreateMockHandler(HttpStatusCode statusCode, string responseBody)
        {
            return new DistributedTransactionTestHandler
            {
                MockResponseFactory = request =>
                {
                    ResponseMessage response = new ResponseMessage(statusCode, request, errorMessage: null)
                    {
                        Content = new MemoryStream(Encoding.UTF8.GetBytes(responseBody))
                    };
                    response.Headers["x-ms-activity-id"] = Guid.NewGuid().ToString();
                    response.Headers[IdempotencyTokenHeader] = request.Headers[IdempotencyTokenHeader] ?? Guid.NewGuid().ToString();
                    return Task.FromResult(response);
                }
            };
        }

        private static string CreateMockSuccessResponse(int operationCount)
        {
            List<string> responses = new();
            for (int i = 0; i < operationCount; i++)
            {
                responses.Add($@"{{""index"":{i},""statusCode"":201,""etag"":""\""etag-{i}\""""}}");
            }
            return $@"{{""operationResponses"":[{string.Join(",", responses)}]}}";
        }

        private static void ValidateRequestBody(string requestBody, params ToDoActivity[] expectedDocs)
        {
            using JsonDocument json = JsonDocument.Parse(requestBody);
            JsonElement operations = json.RootElement.GetProperty("operations");
            
            Assert.AreEqual(expectedDocs.Length, operations.GetArrayLength());

            for (int i = 0; i < expectedDocs.Length; i++)
            {
                JsonElement op = operations[i];
                
                Assert.AreEqual(i, op.GetProperty("index").GetInt32());
                Assert.IsTrue(op.TryGetProperty("databaseName", out _));
                Assert.IsTrue(op.TryGetProperty("collectionName", out _));
                Assert.IsTrue(op.TryGetProperty("operationType", out _));

                // resourceBody is now a nested JSON object, not a string
                JsonElement resourceBody = op.GetProperty("resourceBody");
                Assert.AreEqual(JsonValueKind.Object, resourceBody.ValueKind);
                
                ToDoActivity actualDoc = JsonSerializer.Deserialize<ToDoActivity>(resourceBody.GetRawText());
                ToDoActivity expectedDoc = expectedDocs[i];
                
                Assert.AreEqual(expectedDoc.id, actualDoc.id);
                Assert.AreEqual(expectedDoc.pk, actualDoc.pk);
                Assert.AreEqual(expectedDoc.taskNum, actualDoc.taskNum);
                Assert.AreEqual(expectedDoc.cost, actualDoc.cost);
                Assert.AreEqual(expectedDoc.description, actualDoc.description);
            }
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

        #endregion

        #region Test Handler

        private class DistributedTransactionTestHandler : RequestHandler
        {
            public RequestMessage CapturedRequest { get; private set; }
            public string CapturedRequestBody { get; private set; }
            public Func<RequestMessage, Task<ResponseMessage>> MockResponseFactory { get; set; }

            public override async Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
            {
                if (request.RequestUriString?.EndsWith("/dtc", StringComparison.OrdinalIgnoreCase) == true)
                {
                    this.CapturedRequest = request;

                    if (request.Content != null)
                    {
                        using MemoryStream ms = new();
                        await request.Content.CopyToAsync(ms);
                        this.CapturedRequestBody = Encoding.UTF8.GetString(ms.ToArray());
                        request.Content.Position = 0;
                    }

                    return this.MockResponseFactory != null 
                        ? await this.MockResponseFactory(request) 
                        : new ResponseMessage(HttpStatusCode.OK, request, errorMessage: null);
                }

                return await base.SendAsync(request, cancellationToken);
            }
        }

        #endregion
    }
}
