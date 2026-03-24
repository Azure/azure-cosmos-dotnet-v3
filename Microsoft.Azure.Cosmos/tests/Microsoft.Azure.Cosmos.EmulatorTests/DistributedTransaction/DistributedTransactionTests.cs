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

        // Helpers

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
