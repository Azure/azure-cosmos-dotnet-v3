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
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using OperationType = Documents.OperationType;

    [TestClass]
    public class DistributedTransactionE2ETests : BaseCosmosClientHelper
    {   
        private const string IdempotencyTokenHeader = "x-ms-cosmos-idempotency-token";
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
                    ("partitionKey", JsonValueKind.String),
                    ("index", JsonValueKind.Number),
                    ("operationType", JsonValueKind.String),
                    ("resourceType", JsonValueKind.String),
                    ("sessionToken", JsonValueKind.String),
                    ("etag", JsonValueKind.String)
                };

                foreach ((string property, JsonValueKind expectedKind) in requiredFields)
                {
                    this.ValidateValueKind(operation, property, expectedKind, operationIndex, isRequired: true);
                }

                (string Property, JsonValueKind Kind)[] optionalFields =
                {
                    ("id", JsonValueKind.String),
                    ("resourceBody", JsonValueKind.Object),
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