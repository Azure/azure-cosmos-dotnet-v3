//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using JsonReader = Json.JsonReader;
    using JsonWriter = Json.JsonWriter;
    using PartitionKey = Documents.PartitionKey;
    using static Microsoft.Azure.Cosmos.SDK.EmulatorTests.TransportClientHelper;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.Cosmos.Diagnostics;

    [TestClass]
    public class CosmosItemTests : BaseCosmosClientHelper
    {
        private Container Container = null;
        private ContainerProperties containerSettings = null;

        private static readonly string nonPartitionItemId = "fixed-Container-Item";
        private static readonly string undefinedPartitionItemId = "undefined-partition-Item";

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit(validateSinglePartitionKeyRangeCacheCall: true);
            string PartitionKey = "/pk";
            this.containerSettings = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey);
            ContainerResponse response = await this.database.CreateContainerAsync(
                this.containerSettings,
                throughput: 15000,
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public void ParentResourceTest()
        {
            Assert.AreEqual(this.database, this.Container.Database);
            Assert.AreEqual(this.GetClient(), this.Container.Database.Client);
        }

        [TestMethod]
        [DataRow(false, DisplayName = "Test scenario when binary encoding is disabled at client level.")]
        [DataRow(true, DisplayName = "Test scenario when binary encoding is enabled at client level.")]
        public async Task CreateDropItemWithInvalidIdCharactersTest(bool binaryEncodingEnabledInClient)
        {
            try
            {
                if (binaryEncodingEnabledInClient)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");
                }

                ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
                testItem.id = "Invalid#/\\?Id";
                await this.Container.CreateItemAsync(testItem, new Cosmos.PartitionKey(testItem.pk));

                try
                {
                    await this.Container.ReadItemAsync<JObject>(testItem.id, new Cosmos.PartitionKey(testItem.pk));
                    Assert.Fail("Read item should fail because id has invalid characters");
                }
                catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.NotFound)
                {
                    string message = ce.ToString();
                    Assert.IsNotNull(message);
                    CosmosItemTests.ValidateCosmosException(ce);
                }

                // Get a container reference that use RID values
                ContainerProperties containerProperties = await this.Container.ReadContainerAsync();
                string[] selfLinkSegments = containerProperties.SelfLink.Split('/');
                string databaseRid = selfLinkSegments[1];
                string containerRid = selfLinkSegments[3];
                Container containerByRid = this.GetClient().GetContainer(databaseRid, containerRid);

                // List of invalid characters are listed here.
                //https://docs.microsoft.com/dotnet/api/microsoft.azure.documents.resource.id?view=azure-dotnet#remarks
                FeedIterator<JObject> invalidItemsIterator = this.Container.GetItemQueryIterator<JObject>(
                    @"select * from t where CONTAINS(t.id, ""/"") or CONTAINS(t.id, ""#"") or CONTAINS(t.id, ""?"") or CONTAINS(t.id, ""\\"") ");
                while (invalidItemsIterator.HasMoreResults)
                {
                    foreach (JObject itemWithInvalidId in await invalidItemsIterator.ReadNextAsync())
                    {
                        // It recommend to chose a new id that does not contain special characters, but
                        // if that is not possible then it can be Base64 encoded to escape the special characters
                        byte[] plainTextBytes = Encoding.UTF8.GetBytes(itemWithInvalidId["id"].ToString());
                        itemWithInvalidId["id"] = Convert.ToBase64String(plainTextBytes);

                        // Update the item with the new id value using the rid based container reference
                        JObject item = await containerByRid.ReplaceItemAsync<JObject>(
                            item: itemWithInvalidId,
                            id: itemWithInvalidId["_rid"].ToString(),
                            partitionKey: new Cosmos.PartitionKey(itemWithInvalidId["pk"].ToString()));

                        // Validate the new id can be read using the original name based contianer reference
                        await this.Container.ReadItemAsync<ToDoActivity>(
                            item["id"].ToString(),
                            new Cosmos.PartitionKey(item["pk"].ToString())); ;
                    }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, null);
            }
        }

        [TestMethod]
        [DataRow(false, DisplayName = "Test scenario when binary encoding is disabled at client level.")]
        [DataRow(true, DisplayName = "Test scenario when binary encoding is enabled at client level.")]
        public async Task CreateDropItemTest(bool binaryEncodingEnabledInClient)
        {
            try
            {
                if (binaryEncodingEnabledInClient)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");
                }

                ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
                ItemResponse<ToDoActivity> response = await this.Container.CreateItemAsync<ToDoActivity>(item: testItem);
                Assert.IsNotNull(response);
                Assert.IsNotNull(response.Resource);
                Assert.IsNotNull(response.Diagnostics);
                CosmosTraceDiagnostics diagnostics = (CosmosTraceDiagnostics)response.Diagnostics;
                Assert.IsFalse(diagnostics.IsGoneExceptionHit());
                string diagnosticString = response.Diagnostics.ToString();
                Assert.IsTrue(diagnosticString.Contains("Response Serialization"));

                Assert.IsFalse(string.IsNullOrEmpty(diagnostics.ToString()));
                Assert.IsTrue(diagnostics.GetClientElapsedTime() > TimeSpan.Zero);
                Assert.AreEqual(0, response.Diagnostics.GetFailedRequestCount());
                Assert.IsNull(response.Diagnostics.GetQueryMetrics());

                response = await this.Container.ReadItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.pk));
                Assert.IsNotNull(response);
                Assert.IsNotNull(response.Resource);
                Assert.IsNotNull(response.Diagnostics);
                Assert.IsFalse(string.IsNullOrEmpty(response.Diagnostics.ToString()));
                Assert.IsTrue(response.Diagnostics.GetClientElapsedTime() > TimeSpan.Zero);
                Assert.AreEqual(0, response.Diagnostics.GetFailedRequestCount());
                Assert.IsNotNull(response.Diagnostics.GetStartTimeUtc());

                Assert.IsNotNull(response.Headers.GetHeaderValue<string>(Documents.HttpConstants.HttpHeaders.MaxResourceQuota));
                Assert.IsNotNull(response.Headers.GetHeaderValue<string>(Documents.HttpConstants.HttpHeaders.CurrentResourceQuotaUsage));
                ItemResponse<ToDoActivity> deleteResponse = await this.Container.DeleteItemAsync<ToDoActivity>(partitionKey: new Cosmos.PartitionKey(testItem.pk), id: testItem.id);
                Assert.IsNotNull(deleteResponse);
                Assert.IsNotNull(response.Diagnostics);
                Assert.IsFalse(string.IsNullOrEmpty(response.Diagnostics.ToString()));
                Assert.IsTrue(response.Diagnostics.GetClientElapsedTime() > TimeSpan.Zero);
                Assert.IsNull(response.Diagnostics.GetQueryMetrics());
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, null);
            }
        }

        [TestMethod]
        [DataRow(false, DisplayName = "Test scenario when binary encoding is disabled at client level.")]
        [DataRow(true, DisplayName = "Test scenario when binary encoding is enabled at client level.")]
        public async Task ClientConsistencyTestAsync(bool binaryEncodingEnabledInClient)
        {
            try
            {
                if (binaryEncodingEnabledInClient)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");
                }

                List<Cosmos.ConsistencyLevel> cosmosLevels = Enum.GetValues(typeof(Cosmos.ConsistencyLevel)).Cast<Cosmos.ConsistencyLevel>().ToList();

                foreach (Cosmos.ConsistencyLevel consistencyLevel in cosmosLevels)
                {
                    RequestHandlerHelper handlerHelper = new RequestHandlerHelper();
                    using CosmosClient cosmosClient = TestCommon.CreateCosmosClient(x =>
                        x.WithConsistencyLevel(consistencyLevel).AddCustomHandlers(handlerHelper));
                    Container consistencyContainer = cosmosClient.GetContainer(this.database.Id, this.Container.Id);

                    int requestCount = 0;
                    handlerHelper.UpdateRequestMessage = (request) =>
                    {
                        Assert.AreEqual(consistencyLevel.ToString(), request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel]);
                        requestCount++;
                    };

                    ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
                    ItemResponse<ToDoActivity> response = await consistencyContainer.CreateItemAsync<ToDoActivity>(item: testItem);
                    response = await consistencyContainer.ReadItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.pk));

                    Assert.AreEqual(2, requestCount);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, null);
            }
        }

        [TestMethod]
        [DataRow(true, DisplayName = "Test scenario when binary encoding is enabled at client level.")]
        [DataRow(false, DisplayName = "Test scenario when binary encoding is disabled at client level.")]
        public async Task NegativeCreateItemTest(bool binaryEncodingEnabledInClient)
        {
            try
            {
                if (binaryEncodingEnabledInClient)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");
                }

                HttpClientHandlerHelper httpHandler = new HttpClientHandlerHelper();
                HttpClient httpClient = new HttpClient(httpHandler);
                using CosmosClient client = TestCommon.CreateCosmosClient(x => x.WithHttpClientFactory(() => httpClient));

                httpHandler.RequestCallBack = (request, cancellation) =>
                {
                    if (request.Method == HttpMethod.Get &&
                        request.RequestUri.AbsolutePath == "//addresses/")
                    {
                        HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.Forbidden);

                        // Add a substatus code that is not part of the enum. 
                        // This ensures that if the backend adds a enum the status code is not lost.
                        result.Headers.Add(WFConstants.BackendHeaders.SubStatus, 999999.ToString(CultureInfo.InvariantCulture));
                        string payload = JsonConvert.SerializeObject(new Error() { Message = "test message" });
                        result.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                        return Task.FromResult(result);
                    }

                    return null;
                };

                try
                {
                    ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
                    await client.GetContainer(this.database.Id, this.Container.Id).CreateItemAsync<ToDoActivity>(item: testItem);
                    Assert.Fail("Request should throw exception.");
                }
                catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.Forbidden)
                {
                    Assert.AreEqual(999999, ce.SubStatusCode);
                    string exception = ce.ToString();
                    Assert.IsTrue(exception.StartsWith("Microsoft.Azure.Cosmos.CosmosException : Response status code does not indicate success: Forbidden (403); Substatus: 999999; "));
                    string diagnostics = ce.Diagnostics.ToString();
                    Assert.IsTrue(diagnostics.Contains("999999"));
                    CosmosItemTests.ValidateCosmosException(ce);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, null);
            }
        }

        [TestMethod]
        [DataRow(true, DisplayName = "Test scenario when binary encoding is enabled at client level.")]
        [DataRow(false, DisplayName = "Test scenario when binary encoding is disabled at client level.")]
        public async Task NegativeCreateDropItemTest(bool binaryEncodingEnabledInClient)
        {
            try
            {
                if (binaryEncodingEnabledInClient)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");
                }

                ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
                ResponseMessage response = await this.Container.CreateItemStreamAsync(streamPayload: TestCommon.SerializerCore.ToStream(testItem), partitionKey: new Cosmos.PartitionKey("BadKey"));
                Assert.IsNotNull(response);
                Assert.IsNull(response.Content);
                Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.AreNotEqual(0, response.Diagnostics.GetFailedRequestCount());
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, null);
            }
        }

        [TestMethod]
        [DataRow(true, DisplayName = "Test scenario when binary encoding is enabled at client level.")]
        [DataRow(false, DisplayName = "Test scenario when binary encoding is disabled at client level.")]
        public async Task MemoryStreamBufferIsAccessibleOnResponse(bool binaryEncodingEnabledInClient)
        {
            try
            {
                if (binaryEncodingEnabledInClient)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");
                }

                ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
                ResponseMessage response = await this.Container.CreateItemStreamAsync(streamPayload: TestCommon.SerializerCore.ToStream(testItem), partitionKey: new Cosmos.PartitionKey(testItem.pk));
                Assert.IsNotNull(response);
                Assert.IsTrue((response.Content as MemoryStream).TryGetBuffer(out _));
                FeedIterator feedIteratorQuery = this.Container.GetItemQueryStreamIterator(queryText: "SELECT * FROM c");

                while (feedIteratorQuery.HasMoreResults)
                {
                    ResponseMessage feedResponseQuery = await feedIteratorQuery.ReadNextAsync();
                    Assert.IsTrue((feedResponseQuery.Content as MemoryStream).TryGetBuffer(out _));
                }

                FeedIterator feedIterator = this.Container.GetItemQueryStreamIterator(requestOptions: new QueryRequestOptions()
                {
                    PartitionKey = new Cosmos.PartitionKey(testItem.pk)
                });

                while (feedIterator.HasMoreResults)
                {
                    ResponseMessage feedResponse = await feedIterator.ReadNextAsync();
                    Assert.IsTrue((feedResponse.Content as MemoryStream).TryGetBuffer(out _));
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, null);
            }
        }

        [TestMethod]
        [DataRow(true, DisplayName = "Test scenario when binary encoding is enabled at client level.")]
        [DataRow(false, DisplayName = "Test scenario when binary encoding is disabled at client level.")]
        public async Task CustomSerilizerTest(bool binaryEncodingEnabledInClient)
        {
            try
            {
                if (binaryEncodingEnabledInClient)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");
                }

                string id1 = "MyCustomSerilizerTestId1";
                string id2 = "MyCustomSerilizerTestId2";
                string pk = "MyTestPk";

                // Delete the item to prevent create conflicts if test is run multiple times
                using (await this.Container.DeleteItemStreamAsync(id1, new Cosmos.PartitionKey(pk)))
                { }
                using (await this.Container.DeleteItemStreamAsync(id2, new Cosmos.PartitionKey(pk)))
                { }

                // Both items have null description
                dynamic testItem = new { id = id1, status = pk, description = (string)null };
                dynamic testItem2 = new { id = id2, status = pk, description = (string)null };

                // Create a client that ignore null
                CosmosClientOptions clientOptions = new CosmosClientOptions()
                {
                    Serializer = new CosmosJsonDotNetSerializer(
                        new JsonSerializerSettings()
                        {
                            NullValueHandling = NullValueHandling.Ignore
                        })
                };

                CosmosClient ignoreNullClient = TestCommon.CreateCosmosClient(clientOptions);
                Container ignoreContainer = ignoreNullClient.GetContainer(this.database.Id, this.Container.Id);

                ItemResponse<dynamic> ignoreNullResponse = await ignoreContainer.CreateItemAsync<dynamic>(item: testItem);
                Assert.IsNotNull(ignoreNullResponse);
                Assert.IsNotNull(ignoreNullResponse.Resource);
                Assert.IsNull(ignoreNullResponse.Resource["description"]);

                ItemResponse<dynamic> keepNullResponse = await this.Container.CreateItemAsync<dynamic>(item: testItem2);
                Assert.IsNotNull(keepNullResponse);
                Assert.IsNotNull(keepNullResponse.Resource);
                Assert.IsNotNull(keepNullResponse.Resource["description"]);

                using (await this.Container.DeleteItemStreamAsync(id1, new Cosmos.PartitionKey(pk)))
                { }
                using (await this.Container.DeleteItemStreamAsync(id2, new Cosmos.PartitionKey(pk)))
                { }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, null);
            }
        }

        [TestMethod]
        [DataRow(true, DisplayName = "Test scenario when binary encoding is enabled at client level.")]
        [DataRow(false, DisplayName = "Test scenario when binary encoding is disabled at client level.")]
        public async Task CreateDropItemUndefinedPartitionKeyTest(bool binaryEncodingEnabledInClient)
        {
            try
            {
                if (binaryEncodingEnabledInClient)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");
                }

                dynamic testItem = new
                {
                    id = Guid.NewGuid().ToString()
                };

                ItemResponse<dynamic> response = await this.Container.CreateItemAsync<dynamic>(item: testItem, partitionKey: new Cosmos.PartitionKey(Undefined.Value));
                Assert.IsNotNull(response);
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                Assert.IsNotNull(response.Headers.GetHeaderValue<string>(Documents.HttpConstants.HttpHeaders.MaxResourceQuota));
                Assert.IsNotNull(response.Headers.GetHeaderValue<string>(Documents.HttpConstants.HttpHeaders.CurrentResourceQuotaUsage));

                ItemResponse<dynamic> deleteResponse = await this.Container.DeleteItemAsync<dynamic>(id: testItem.id, partitionKey: new Cosmos.PartitionKey(Undefined.Value));
                Assert.IsNotNull(deleteResponse);
                Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, null);
            }
        }

        [TestMethod]
        [DataRow(true, DisplayName = "Test scenario when binary encoding is enabled at client level.")]
        [DataRow(false, DisplayName = "Test scenario when binary encoding is disabled at client level.")]
        public async Task CreateDropItemPartitionKeyNotInTypeTest(bool binaryEncodingEnabledInClient)
        {
            try
            {
                if (binaryEncodingEnabledInClient)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");
                }

                dynamic testItem = new
                {
                    id = Guid.NewGuid().ToString()
                };

                ItemResponse<dynamic> response = await this.Container.CreateItemAsync<dynamic>(item: testItem);
                Assert.IsNotNull(response);
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                Assert.IsNotNull(response.Headers.GetHeaderValue<string>(Documents.HttpConstants.HttpHeaders.MaxResourceQuota));
                Assert.IsNotNull(response.Headers.GetHeaderValue<string>(Documents.HttpConstants.HttpHeaders.CurrentResourceQuotaUsage));

                ItemResponse<dynamic> readResponse = await this.Container.ReadItemAsync<dynamic>(id: testItem.id, partitionKey: Cosmos.PartitionKey.None);
                Assert.IsNotNull(readResponse);
                Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);

                ItemResponse<dynamic> deleteResponse = await this.Container.DeleteItemAsync<dynamic>(id: testItem.id, partitionKey: Cosmos.PartitionKey.None);
                Assert.IsNotNull(deleteResponse);
                Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);

                try
                {
                    readResponse = await this.Container.ReadItemAsync<dynamic>(id: testItem.id, partitionKey: Cosmos.PartitionKey.None);
                    Assert.Fail("Should throw exception.");
                }
                catch (CosmosException ex)
                {
                    Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
                    CosmosItemTests.ValidateCosmosException(ex);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, null);
            }
        }

        [TestMethod]
        [DataRow(true, DisplayName = "Test scenario when binary encoding is enabled at client level.")]
        [DataRow(false, DisplayName = "Test scenario when binary encoding is disabled at client level.")]
        public async Task CreateDropItemMultiPartPartitionKeyTest(bool binaryEncodingEnabledInClient)
        {
            try
            {
                if (binaryEncodingEnabledInClient)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");
                }

                Container multiPartPkContainer = await this.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/a/b/c");

                dynamic testItem = new
                {
                    id = Guid.NewGuid().ToString(),
                    a = new
                    {
                        b = new
                        {
                            c = "pk1",
                        }
                    }
                };

                ItemResponse<dynamic> response = await multiPartPkContainer.CreateItemAsync<dynamic>(item: testItem);
                Assert.IsNotNull(response);
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                Assert.IsNull(response.Diagnostics.GetQueryMetrics());

                ItemResponse<dynamic> readResponse = await multiPartPkContainer.ReadItemAsync<dynamic>(id: testItem.id, partitionKey: new Cosmos.PartitionKey("pk1"));
                Assert.IsNotNull(readResponse);
                Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);

                ItemResponse<dynamic> deleteResponse = await multiPartPkContainer.DeleteItemAsync<dynamic>(id: testItem.id, partitionKey: new Cosmos.PartitionKey("pk1"));
                Assert.IsNotNull(deleteResponse);
                Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);

                try
                {
                    readResponse = await multiPartPkContainer.ReadItemAsync<dynamic>(id: testItem.id, partitionKey: new Cosmos.PartitionKey("pk1"));
                    Assert.Fail("Should throw exception.");
                }
                catch (CosmosException ex)
                {
                    Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
                    CosmosItemTests.ValidateCosmosException(ex);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, null);
            }
        }

        [TestMethod]
        public async Task ReadCollectionNotExists()
        {
            string collectionName = Guid.NewGuid().ToString();
            Container testContainer = this.database.GetContainer(collectionName);
            await CosmosItemTests.TestNonePKForNonExistingContainer(testContainer);

            // Item -> Container -> Database contract 
            string dbName = Guid.NewGuid().ToString();
            testContainer = this.GetClient().GetDatabase(dbName).GetContainer(collectionName);
            await CosmosItemTests.TestNonePKForNonExistingContainer(testContainer);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "Test scenario when binary encoding is enabled at client level.")]
        [DataRow(false, DisplayName = "Test scenario when binary encoding is disabled at client level.")]
        public async Task NonPartitionKeyLookupCacheTest(bool binaryEncodingEnabledInClient)
        {
            try
            {
                if (binaryEncodingEnabledInClient)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");
                }

                int count = 0;
                using CosmosClient client = TestCommon.CreateCosmosClient(builder =>
                {
                    builder.WithConnectionModeDirect();
                    builder.WithSendingRequestEventArgs((sender, e) =>
                    {
                        if (e.DocumentServiceRequest != null)
                        {
                            System.Diagnostics.Trace.TraceInformation($"{e.DocumentServiceRequest.ToString()}");
                        }

                        if (e.HttpRequest != null)
                        {
                            System.Diagnostics.Trace.TraceInformation($"{e.HttpRequest.ToString()}");
                        }

                        if (e.IsHttpRequest()
                            && e.HttpRequest.RequestUri.AbsolutePath.Contains("/colls/"))
                        {
                            count++;
                        }

                        if (e.IsHttpRequest()
                            && e.HttpRequest.RequestUri.AbsolutePath.Contains("/pkranges"))
                        {
                            Debugger.Break();
                        }
                    });
                },
                validatePartitionKeyRangeCalls: false);

                string dbName = Guid.NewGuid().ToString();
                string containerName = Guid.NewGuid().ToString();
                ContainerInternal testContainer = (ContainerInlineCore)client.GetContainer(dbName, containerName);

                int loopCount = 2;
                for (int i = 0; i < loopCount; i++)
                {
                    try
                    {
                        await testContainer.GetNonePartitionKeyValueAsync(NoOpTrace.Singleton, default(CancellationToken));
                        Assert.Fail();
                    }
                    catch (CosmosException dce) when (dce.StatusCode == HttpStatusCode.NotFound)
                    {
                    }
                }

                Assert.AreEqual(loopCount, count);

                // Create real container and address 
                Cosmos.Database db = await client.CreateDatabaseAsync(dbName);
                Container container = await db.CreateContainerAsync(containerName, "/id");

                // reset counter
                count = 0;
                for (int i = 0; i < loopCount; i++)
                {
                    await testContainer.GetNonePartitionKeyValueAsync(NoOpTrace.Singleton, default);
                }

                // expected once post create 
                Assert.AreEqual(1, count);

                // reset counter
                count = 0;
                for (int i = 0; i < loopCount; i++)
                {
                    await testContainer.GetCachedRIDAsync(forceRefresh: false, NoOpTrace.Singleton, cancellationToken: default);
                }

                // Already cached by GetNonePartitionKeyValueAsync before
                Assert.AreEqual(0, count);

                // reset counter
                count = 0;
                int expected = 0;
                for (int i = 0; i < loopCount; i++)
                {
                    await testContainer.GetRoutingMapAsync(default);
                    expected = count;
                }

                // OkRagnes should be fetched only once. 
                // Possible to make multiple calls for ranges
                Assert.AreEqual(expected, count);

                await db.DeleteStreamAsync();
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, null);
            }
        }

        [TestMethod]
        [DataRow(true, true, DisplayName = "Test scenario when binary encoding is enabled at client level and expected stream response type is binary.")]
        [DataRow(true, false, DisplayName = "Test scenario when binary encoding is enabled at client level and expected stream response type is text.")]
        [DataRow(false, true, DisplayName = "Test scenario when binary encoding is disabled at client level and expected stream response type is binary.")]
        [DataRow(false, false, DisplayName = "Test scenario when binary encoding is disabled at client level and expected stream response type is text.")]
        public async Task CreateDropItemStreamTest(bool binaryEncodingEnabledInClient, bool shouldExpectBinaryOnResponse)
        {
            try
            {
                if (binaryEncodingEnabledInClient)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");
                }

                ItemRequestOptions requestOptions = new()
                {
                    EnableBinaryResponseOnPointOperations = binaryEncodingEnabledInClient && shouldExpectBinaryOnResponse,
                };

                ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
                using (Stream stream = TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem))
                {
                    using (ResponseMessage response = await this.Container.CreateItemStreamAsync(
                        streamPayload: stream,
                        partitionKey: new Cosmos.PartitionKey(testItem.pk),
                        requestOptions: requestOptions))
                    {
                        Assert.IsNotNull(response);
                        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                        Assert.IsTrue(response.Headers.RequestCharge > 0);
                        Assert.IsNotNull(response.Headers.ActivityId);
                        Assert.IsNotNull(response.Headers.ETag);
                        Assert.IsNotNull(response.Diagnostics);
                        Assert.IsTrue(!string.IsNullOrEmpty(response.Diagnostics.ToString()));
                        Assert.IsTrue(response.Diagnostics.GetClientElapsedTime() > TimeSpan.Zero);

                        if (requestOptions.EnableBinaryResponseOnPointOperations)
                        {
                            AssertOnResponseSerializationBinaryType(response.Content);
                        }
                        else
                        {
                            AssertOnResponseSerializationTextType(response.Content);
                        }
                    }
                }

                using (ResponseMessage response = await this.Container.ReadItemStreamAsync(
                    id: testItem.id,
                    partitionKey: new Cosmos.PartitionKey(testItem.pk),
                    requestOptions: requestOptions))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                    Assert.IsTrue(response.Headers.RequestCharge > 0);
                    Assert.IsNotNull(response.Headers.ActivityId);
                    Assert.IsNotNull(response.Headers.ETag);
                    Assert.IsNotNull(response.Diagnostics);
                    Assert.IsTrue(!string.IsNullOrEmpty(response.Diagnostics.ToString()));
                    Assert.IsTrue(response.Diagnostics.GetClientElapsedTime() > TimeSpan.Zero);

                    if (requestOptions.EnableBinaryResponseOnPointOperations)
                    {
                        AssertOnResponseSerializationBinaryType(response.Content);
                    }
                    else
                    {
                        AssertOnResponseSerializationTextType(response.Content);
                    }
                }

                using (ResponseMessage deleteResponse = await this.Container.DeleteItemStreamAsync(
                    id: testItem.id,
                    partitionKey: new Cosmos.PartitionKey(testItem.pk),
                    requestOptions: requestOptions))
                {
                    Assert.IsNotNull(deleteResponse);
                    Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
                    Assert.IsTrue(deleteResponse.Headers.RequestCharge > 0);
                    Assert.IsNotNull(deleteResponse.Headers.ActivityId);
                    Assert.IsNotNull(deleteResponse.Diagnostics);
                    Assert.IsTrue(!string.IsNullOrEmpty(deleteResponse.Diagnostics.ToString()));
                    Assert.IsTrue(deleteResponse.Diagnostics.GetClientElapsedTime() > TimeSpan.Zero);

                    if (requestOptions.EnableBinaryResponseOnPointOperations)
                    {
                        AssertOnResponseSerializationBinaryType(deleteResponse.Content);
                    }
                    else
                    {
                        AssertOnResponseSerializationTextType(deleteResponse.Content);
                    }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, null);
            }
        }

        [TestMethod]
        [DataRow(false, DisplayName = "Test scenario when binary encoding is disabled at client level.")]
        [DataRow(true, DisplayName = "Test scenario when binary encoding is enabled at client level.")]
        public async Task UpsertItemStreamTest(bool binaryEncodingEnabledInClient)
        {
            try
            {
                if (binaryEncodingEnabledInClient)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");
                }

                ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
                using (Stream stream = TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem))
                {
                    //Create the object
                    using (ResponseMessage response = await this.Container.UpsertItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.pk), streamPayload: stream))
                    {
                        Assert.IsNotNull(response);
                        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                        Assert.IsNotNull(response.Headers.Session);
                        using (StreamReader str = new StreamReader(response.Content))
                        {
                            string responseContentAsString = await str.ReadToEndAsync();
                        }
                    }
                }

                //Updated the taskNum field
                testItem.taskNum = 9001;
                using (Stream stream = TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem))
                {
                    using (ResponseMessage response = await this.Container.UpsertItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.pk), streamPayload: stream))
                    {
                        Assert.IsNotNull(response);
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                        Assert.IsNotNull(response.Headers.Session);
                    }
                }
                using (ResponseMessage deleteResponse = await this.Container.DeleteItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.pk), id: testItem.id))
                {
                    Assert.IsNotNull(deleteResponse);
                    Assert.AreEqual(deleteResponse.StatusCode, HttpStatusCode.NoContent);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, null);
            }
        }

        [TestMethod]
        [DataRow(false, DisplayName = "Test scenario when binary encoding is disabled at client level.")]
        [DataRow(true, DisplayName = "Test scenario when binary encoding is enabled at client level.")]
        public async Task UpsertItemTest(bool binaryEncodingEnabledInClient)
        {
            try
            {
                if (binaryEncodingEnabledInClient)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");
                }

                ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();

                {
                    ItemResponse<ToDoActivity> response = await this.Container.UpsertItemAsync(testItem, partitionKey: new Cosmos.PartitionKey(testItem.pk));
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                    Assert.IsNotNull(response.Headers.Session);
                    Assert.IsNull(response.Diagnostics.GetQueryMetrics());
                }

                {
                    //Updated the taskNum field
                    testItem.taskNum = 9001;
                    ItemResponse<ToDoActivity> response = await this.Container.UpsertItemAsync(testItem, partitionKey: new Cosmos.PartitionKey(testItem.pk));

                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                    Assert.IsNotNull(response.Headers.Session);
                    Assert.IsNull(response.Diagnostics.GetQueryMetrics());
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, null);
            }
        }

        [TestMethod]
        [DataRow(false, DisplayName = "Test scenario when binary encoding is disabled at client level.")]
        [DataRow(true, DisplayName = "Test scenario when binary encoding is enabled at client level.")]
        public async Task ReplaceItemStreamTest(bool binaryEncodingEnabledInClient)
        {
            try
            {
                if (binaryEncodingEnabledInClient)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");
                }

                ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
                using (Stream stream = TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem))
                {
                    //Replace a non-existing item. It should fail, and not throw an exception.
                    using (ResponseMessage response = await this.Container.ReplaceItemStreamAsync(
                        partitionKey: new Cosmos.PartitionKey(testItem.pk),
                        id: testItem.id,
                        streamPayload: stream))
                    {
                        Assert.IsFalse(response.IsSuccessStatusCode);
                        Assert.IsNotNull(response);
                        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, response.ErrorMessage);
                    }
                }

                using (Stream stream = TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem))
                {
                    //Create the item
                    using (ResponseMessage response = await this.Container.CreateItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.pk), streamPayload: stream))
                    {
                        Assert.IsNotNull(response);
                        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                    }
                }

                //Updated the taskNum field
                testItem.taskNum = 9001;
                using (Stream stream = TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem))
                {
                    using (ResponseMessage response = await this.Container.ReplaceItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.pk), id: testItem.id, streamPayload: stream))
                    {
                        Assert.IsNotNull(response);
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                    }

                    using (ResponseMessage deleteResponse = await this.Container.DeleteItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.pk), id: testItem.id))
                    {
                        Assert.IsNotNull(deleteResponse);
                        Assert.AreEqual(deleteResponse.StatusCode, HttpStatusCode.NoContent);
                    }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, null);
            }
        }

        [DataRow(false)]
        [DataRow(true)]
        [DataTestMethod]
        public async Task ItemStreamIterator(bool useStatelessIterator)
        {
            IList<ToDoActivity> deleteList = await ToDoActivity.CreateRandomItems(this.Container, 3, randomPartitionKey: true);
            HashSet<string> itemIds = deleteList.Select(x => x.id).ToHashSet<string>();

            string lastContinuationToken = null;
            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                MaxItemCount = 1
            };

            FeedIterator feedIterator = this.Container.GetItemQueryStreamIterator(
                continuationToken: lastContinuationToken,
                requestOptions: requestOptions);

            while (feedIterator.HasMoreResults)
            {
                if (useStatelessIterator)
                {
                    feedIterator = this.Container.GetItemQueryStreamIterator(
                        continuationToken: lastContinuationToken,
                        requestOptions: requestOptions);
                }

                using (ResponseMessage responseMessage =
                    await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    lastContinuationToken = responseMessage.Headers.ContinuationToken;
                    Assert.AreEqual(responseMessage.ContinuationToken, responseMessage.Headers.ContinuationToken);
                    Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                    foreach (ToDoActivity toDoActivity in response)
                    {
                        if (itemIds.Contains(toDoActivity.id))
                        {
                            itemIds.Remove(toDoActivity.id);
                        }
                    }

                    Assert.IsNull(responseMessage.Diagnostics.GetQueryMetrics());
                }

            }

            Assert.IsNull(lastContinuationToken);
            Assert.AreEqual(itemIds.Count, 0);
        }

        [TestMethod]
        [DataRow(true, DisplayName = "Test scenario when binary encoding is enabled at client level.")]
        [DataRow(false, DisplayName = "Test scenario when binary encoding is disabled at client level.")]
        public async Task PartitionKeyDeleteTest(bool binaryEncodingEnabledInClient)
        {
            try
            {
                if (binaryEncodingEnabledInClient)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");
                }

                string pKString = "PK1";
                string pKString2 = "PK2";
                dynamic testItem1 = new
                {
                    id = "item1",
                    pk = pKString
                };

                dynamic testItem2 = new
                {
                    id = "item2",
                    pk = pKString
                };

                dynamic testItem3 = new
                {
                    id = "item3",
                    pk = pKString2
                };

                ContainerInternal containerInternal = (ContainerInternal)this.Container;
                await this.Container.CreateItemAsync<dynamic>(testItem1);
                await this.Container.CreateItemAsync<dynamic>(testItem2);
                await this.Container.CreateItemAsync<dynamic>(testItem3);
                Cosmos.PartitionKey partitionKey1 = new Cosmos.PartitionKey(pKString);
                Cosmos.PartitionKey partitionKey2 = new Cosmos.PartitionKey(pKString2);
                using (ResponseMessage pKDeleteResponse = await containerInternal.DeleteAllItemsByPartitionKeyStreamAsync(partitionKey1))
                {
                    Assert.AreEqual(pKDeleteResponse.StatusCode, HttpStatusCode.OK);
                }

                using (ResponseMessage readResponse = await this.Container.ReadItemStreamAsync("item1", partitionKey1))
                {
                    Assert.AreEqual(readResponse.StatusCode, HttpStatusCode.NotFound);
                    Assert.AreEqual(readResponse.Headers.SubStatusCode, SubStatusCodes.Unknown);
                }

                using (ResponseMessage readResponse = await this.Container.ReadItemStreamAsync("item2", partitionKey1))
                {
                    Assert.AreEqual(readResponse.StatusCode, HttpStatusCode.NotFound);
                    Assert.AreEqual(readResponse.Headers.SubStatusCode, SubStatusCodes.Unknown);
                }

                //verify item with the other Partition Key is not deleted
                using (ResponseMessage readResponse = await this.Container.ReadItemStreamAsync("item3", partitionKey2))
                {
                    Assert.AreEqual(readResponse.StatusCode, HttpStatusCode.OK);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, null);
            }
        }

        [TestMethod]
        [DataRow(false, DisplayName = "Test scenario when binary encoding is disabled at client level.")]
        [DataRow(true, DisplayName = "Test scenario when binary encoding is enabled at client level.")]
        public async Task PartitionKeyDeleteTestForSubpartitionedContainer(bool binaryEncodingEnabledInClient)
        {
            try
            {
                if (binaryEncodingEnabledInClient)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");
                }

                string currentVersion = HttpConstants.Versions.CurrentVersion;
                HttpConstants.Versions.CurrentVersion = "2020-07-15";
                using CosmosClient client = TestCommon.CreateCosmosClient(true);
                Cosmos.Database database = null;
                try
                {
                    database = await client.CreateDatabaseIfNotExistsAsync("mydb");

                    ContainerProperties containerProperties = new ContainerProperties("subpartitionedcontainer", new List<string> { "/Country", "/City" });
                    Container container = await database.CreateContainerAsync(containerProperties);
                    ContainerInternal containerInternal = (ContainerInternal)container;

                    //Document create.
                    ItemResponse<Document>[] documents = new ItemResponse<Document>[5];
                    Document doc1 = new Document { Id = "document1" };
                    doc1.SetValue("Country", "USA");
                    doc1.SetValue("City", "Redmond");
                    documents[0] = await container.CreateItemAsync<Document>(doc1);

                    doc1 = new Document { Id = "document2" };
                    doc1.SetValue("Country", "USA");
                    doc1.SetValue("City", "Pittsburgh");
                    documents[1] = await container.CreateItemAsync<Document>(doc1);

                    doc1 = new Document { Id = "document3" };
                    doc1.SetValue("Country", "USA");
                    doc1.SetValue("City", "Stonybrook");
                    documents[2] = await container.CreateItemAsync<Document>(doc1);

                    doc1 = new Document { Id = "document4" };
                    doc1.SetValue("Country", "USA");
                    doc1.SetValue("City", "Stonybrook");
                    documents[3] = await container.CreateItemAsync<Document>(doc1);

                    doc1 = new Document { Id = "document5" };
                    doc1.SetValue("Country", "USA");
                    doc1.SetValue("City", "Stonybrook");
                    documents[4] = await container.CreateItemAsync<Document>(doc1);

                    Cosmos.PartitionKey partitionKey1 = new PartitionKeyBuilder().Add("USA").Add("Stonybrook").Build();

                    using (ResponseMessage pKDeleteResponse = await containerInternal.DeleteAllItemsByPartitionKeyStreamAsync(partitionKey1))
                    {
                        Assert.AreEqual(pKDeleteResponse.StatusCode, HttpStatusCode.OK);
                    }
                    using (ResponseMessage readResponse = await containerInternal.ReadItemStreamAsync("document5", partitionKey1))
                    {
                        Assert.AreEqual(readResponse.StatusCode, HttpStatusCode.NotFound);
                        Assert.AreEqual(readResponse.Headers.SubStatusCode, SubStatusCodes.Unknown);
                    }

                    Cosmos.PartitionKey partitionKey2 = new PartitionKeyBuilder().Add("USA").Add("Pittsburgh").Build();
                    using (ResponseMessage readResponse = await containerInternal.ReadItemStreamAsync("document2", partitionKey2))
                    {
                        Assert.AreEqual(readResponse.StatusCode, HttpStatusCode.OK);
                    }


                    //Specifying a partial partition key should fail
                    Cosmos.PartitionKey partialPartitionKey = new PartitionKeyBuilder().Add("USA").Build();
                    using (ResponseMessage pKDeleteResponse = await containerInternal.DeleteAllItemsByPartitionKeyStreamAsync(partialPartitionKey))
                    {
                        Assert.AreEqual(pKDeleteResponse.StatusCode, HttpStatusCode.BadRequest);
                        Assert.AreEqual(pKDeleteResponse.CosmosException.SubStatusCode, (int)SubStatusCodes.PartitionKeyMismatch);
                        Assert.IsTrue(pKDeleteResponse.ErrorMessage.Contains("Partition key provided either doesn't correspond to definition in the collection or doesn't match partition key field values specified in the document."));
                    }
                }
                finally
                {
                    HttpConstants.Versions.CurrentVersion = currentVersion;
                    if (database != null) await database.DeleteAsync();
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, null);
            }
        }

        [TestMethod]
        public async Task ItemCustomSerializerTest()
        {
            DateTime createDateTime = DateTime.UtcNow;
            Dictionary<string, int> keyValuePairs = new Dictionary<string, int>()
            {
                {"test1", 42 },
                {"test42", 9001 }
            };

            dynamic testItem1 = new
            {
                id = "ItemCustomSerialzierTest1",
                cost = (double?)null,
                totalCost = 98.2789,
                pk = "MyCustomStatus",
                taskNum = 4909,
                createdDateTime = createDateTime,
                statusCode = HttpStatusCode.Accepted,
                itemIds = new int[] { 1, 5, 10 },
                dictionary = keyValuePairs
            };

            dynamic testItem2 = new
            {
                id = "ItemCustomSerialzierTest2",
                cost = (double?)null,
                totalCost = 98.2789,
                pk = "MyCustomStatus",
                taskNum = 4909,
                createdDateTime = createDateTime,
                statusCode = HttpStatusCode.Accepted,
                itemIds = new int[] { 1, 5, 10 },
                dictionary = keyValuePairs
            };

            JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings()
            {
                Converters = new List<JsonConverter>() { new CosmosSerializerHelper.FormatNumbersAsTextConverter() }
            };

            List<QueryDefinition> queryDefinitions = new List<QueryDefinition>()
            {
                new QueryDefinition("select * from t where t.pk = @pk" ).WithParameter("@pk", testItem1.pk),
                new QueryDefinition("select * from t where t.cost = @cost" ).WithParameter("@cost", testItem1.cost),
                new QueryDefinition("select * from t where t.taskNum = @taskNum" ).WithParameter("@taskNum", testItem1.taskNum),
                new QueryDefinition("select * from t where t.totalCost = @totalCost" ).WithParameter("@totalCost", testItem1.totalCost),
                new QueryDefinition("select * from t where t.createdDateTime = @createdDateTime" ).WithParameter("@createdDateTime", testItem1.createdDateTime),
                new QueryDefinition("select * from t where t.statusCode = @statusCode" ).WithParameter("@statusCode", testItem1.statusCode),
                new QueryDefinition("select * from t where t.itemIds = @itemIds" ).WithParameter("@itemIds", testItem1.itemIds),
                new QueryDefinition("select * from t where t.dictionary = @dictionary" ).WithParameter("@dictionary", testItem1.dictionary),
                new QueryDefinition("select * from t where t.pk = @pk and t.cost = @cost" )
                    .WithParameter("@pk", testItem1.pk)
                    .WithParameter("@cost", testItem1.cost),
            };

            int toStreamCount = 0;
            int fromStreamCount = 0;
            CosmosSerializerHelper cosmosSerializerHelper = new CosmosSerializerHelper(
                jsonSerializerSettings,
                toStreamCallBack: (itemValue) =>
                {
                    Type itemType = itemValue?.GetType();
                    if (itemValue == null
                        || itemType == typeof(int)
                        || itemType == typeof(double)
                        || itemType == typeof(string)
                        || itemType == typeof(DateTime)
                        || itemType == typeof(HttpStatusCode)
                        || itemType == typeof(int[])
                        || itemType == typeof(Dictionary<string, int>))
                    {
                        toStreamCount++;
                    }
                },
                fromStreamCallback: (item) => fromStreamCount++);

            CosmosClientOptions options = new CosmosClientOptions()
            {
                Serializer = cosmosSerializerHelper
            };

            CosmosClient clientSerializer = TestCommon.CreateCosmosClient(options);
            Container containerSerializer = clientSerializer.GetContainer(this.database.Id, this.Container.Id);

            try
            {
                await containerSerializer.CreateItemAsync<dynamic>(testItem1);
                await containerSerializer.CreateItemAsync<dynamic>(testItem2);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                // Ignore conflicts since the object already exists
            }

            foreach (QueryDefinition queryDefinition in queryDefinitions)
            {
                toStreamCount = 0;
                fromStreamCount = 0;

                List<dynamic> allItems = new List<dynamic>();
                int pageCount = 0;
                using (FeedIterator<dynamic> feedIterator = containerSerializer.GetItemQueryIterator<dynamic>(
                    queryDefinition: queryDefinition))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        // Only need once to verify correct serialization of the query definition
                        FeedResponse<dynamic> response = await feedIterator.ReadNextAsync(this.cancellationToken);
                        Assert.AreEqual(response.Count, response.Count());
                        allItems.AddRange(response);
                        pageCount++;
                    }
                }

                Assert.AreEqual(2, allItems.Count, $"missing query results. Only found: {allItems.Count} items for query:{queryDefinition.ToSqlQuerySpec().QueryText}");
                foreach (dynamic item in allItems)
                {
                    Assert.IsFalse(string.Equals(testItem1.id, item.id) || string.Equals(testItem2.id, item.id));
                    Assert.IsTrue(((JObject)item)["totalCost"].Type == JTokenType.String);
                    Assert.IsTrue(((JObject)item)["taskNum"].Type == JTokenType.String);
                }

                // Each parameter in query spec should be a call to the custom serializer
                int parameterCount = queryDefinition.ToSqlQuerySpec().Parameters.Count;
                Assert.AreEqual((parameterCount * pageCount) + parameterCount, toStreamCount, $"missing to stream call. Expected: {(parameterCount * pageCount) + parameterCount}, Actual: {toStreamCount} for query:{queryDefinition.ToSqlQuerySpec().QueryText}");
                Assert.AreEqual(pageCount, fromStreamCount);
            }
        }

        [TestMethod]
        public async Task QueryStreamValueTest()
        {
            DateTime createDateTime = DateTime.UtcNow;

            dynamic testItem1 = new
            {
                id = "testItem1",
                cost = (double?)null,
                totalCost = 98.2789,
                pk = "MyCustomStatus",
                taskNum = 4909,
                createdDateTime = createDateTime,
                statusCode = HttpStatusCode.Accepted,
                itemIds = new int[] { 1, 5, 10 },
                itemcode = new byte?[5] { 0x16, (byte)'\0', 0x3, null, (byte)'}' },
            };

            dynamic testItem2 = new
            {
                id = "testItem2",
                cost = (double?)null,
                totalCost = 98.2789,
                pk = "MyCustomStatus",
                taskNum = 4909,
                createdDateTime = createDateTime,
                statusCode = HttpStatusCode.Accepted,
                itemIds = new int[] { 1, 5, 10 },
                itemcode = new byte?[5] { 0x16, (byte)'\0', 0x3, null, (byte)'}' },
            };

            //with Custom Serializer.
            JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings()
            {
                Converters = new List<JsonConverter>() { new CosmosSerializerHelper.FormatNumbersAsTextConverter() }
            };

            int toStreamCount = 0;
            int fromStreamCount = 0;
            CosmosSerializerHelper cosmosSerializerHelper = new CosmosSerializerHelper(
                jsonSerializerSettings,
                toStreamCallBack: (itemValue) =>
                {
                    Type itemType = itemValue?.GetType();
                    if (itemValue == null
                        || itemType == typeof(int)
                        || itemType == typeof(double)
                        || itemType == typeof(string)
                        || itemType == typeof(DateTime)
                        || itemType == typeof(HttpStatusCode)
                        || itemType == typeof(int[])
                        || itemType == typeof(byte))
                    {
                        toStreamCount++;
                    }
                },
                fromStreamCallback: (item) => fromStreamCount++);

            CosmosClientOptions options = new CosmosClientOptions()
            {
                Serializer = cosmosSerializerHelper
            };

            CosmosClient clientSerializer = TestCommon.CreateCosmosClient(options);
            Container containerSerializer = clientSerializer.GetContainer(this.database.Id, this.Container.Id);

            List<QueryDefinition> queryDefinitions = new List<QueryDefinition>()
            {
                new QueryDefinition("select * from t where t.pk = @pk" )
                .WithParameterStream("@pk", cosmosSerializerHelper.ToStream<dynamic>(testItem1.pk)),
                new QueryDefinition("select * from t where t.cost = @cost" )
                .WithParameterStream("@cost", cosmosSerializerHelper.ToStream<dynamic>(testItem1.cost)),
                new QueryDefinition("select * from t where t.taskNum = @taskNum" )
                .WithParameterStream("@taskNum", cosmosSerializerHelper.ToStream<dynamic>(testItem1.taskNum)),
                new QueryDefinition("select * from t where t.totalCost = @totalCost" )
                .WithParameterStream("@totalCost", cosmosSerializerHelper.ToStream<dynamic>(testItem1.totalCost)),
                new QueryDefinition("select * from t where t.createdDateTime = @createdDateTime" )
                .WithParameterStream("@createdDateTime", cosmosSerializerHelper.ToStream<dynamic>(testItem1.createdDateTime)),
                new QueryDefinition("select * from t where t.statusCode = @statusCode" )
                .WithParameterStream("@statusCode", cosmosSerializerHelper.ToStream<dynamic>(testItem1.statusCode)),
                new QueryDefinition("select * from t where t.itemIds = @itemIds" )
                .WithParameterStream("@itemIds", cosmosSerializerHelper.ToStream<dynamic>(testItem1.itemIds)),
                new QueryDefinition("select * from t where t.itemcode = @itemcode" )
                .WithParameterStream("@itemcode", cosmosSerializerHelper.ToStream<dynamic>(testItem1.itemcode)),
                new QueryDefinition("select * from t where t.pk = @pk and t.cost = @cost" )
                    .WithParameterStream("@pk", cosmosSerializerHelper.ToStream<dynamic>(testItem1.pk))
                    .WithParameterStream("@cost", cosmosSerializerHelper.ToStream<dynamic>(testItem1.cost)),
            };

            try
            {
                await containerSerializer.CreateItemAsync<dynamic>(testItem1);
                await containerSerializer.CreateItemAsync<dynamic>(testItem2);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                // Ignore conflicts since the object already exists
            }

            foreach (QueryDefinition queryDefinition in queryDefinitions)
            {
                toStreamCount = 0;
                fromStreamCount = 0;

                List<dynamic> allItems = new List<dynamic>();
                int pageCount = 0;
                using (FeedIterator<dynamic> feedIterator = containerSerializer.GetItemQueryIterator<dynamic>(
                    queryDefinition: queryDefinition))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        // Only need once to verify correct serialization of the query definition
                        FeedResponse<dynamic> response = await feedIterator.ReadNextAsync(this.cancellationToken);
                        string diagnosticString = response.Diagnostics.ToString();
                        Assert.IsTrue(diagnosticString.Contains("Query Response Serialization"));
                        Assert.AreEqual(response.Count, response.Count());
                        allItems.AddRange(response);
                        pageCount++;
                    }
                }

                Assert.AreEqual(2, allItems.Count, $"missing query results. Only found: {allItems.Count} items for query:{queryDefinition.ToSqlQuerySpec().QueryText}");

                // There should be no call to custom serializer since the parameter values are already serialized.
                Assert.AreEqual(0, toStreamCount, $"missing to stream call. Expected: 0 , Actual: {toStreamCount} for query:{queryDefinition.ToSqlQuerySpec().QueryText}");
                Assert.AreEqual(pageCount, fromStreamCount);
            }

            // get result across pages,multiple requests by setting MaxItemCount to 1.
            foreach (QueryDefinition queryDefinition in queryDefinitions)
            {
                toStreamCount = 0;
                fromStreamCount = 0;

                List<dynamic> allItems = new List<dynamic>();
                int pageCount = 0;
                using (FeedIterator<dynamic> feedIterator = containerSerializer.GetItemQueryIterator<dynamic>(
                    queryDefinition: queryDefinition,
                    requestOptions: new QueryRequestOptions { MaxItemCount = 1 }))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        // Only need once to verify correct serialization of the query definition
                        FeedResponse<dynamic> response = await feedIterator.ReadNextAsync(this.cancellationToken);
                        Assert.AreEqual(response.Count, response.Count());
                        allItems.AddRange(response);
                        pageCount++;
                    }
                }

                Assert.AreEqual(2, allItems.Count, $"missing query results. Only found: {allItems.Count} items for query:{queryDefinition.ToSqlQuerySpec().QueryText}");

                // There should be no call to custom serializer since the parameter values are already serialized.
                Assert.AreEqual(0, toStreamCount, $"missing to stream call. Expected: 0 , Actual: {toStreamCount} for query:{queryDefinition.ToSqlQuerySpec().QueryText}");
                Assert.AreEqual(pageCount, fromStreamCount);
            }


            // Standard Cosmos Serializer Used

            CosmosClient clientStandardSerializer = TestCommon.CreateCosmosClient(useCustomSeralizer: false);
            Container containerStandardSerializer = clientStandardSerializer.GetContainer(this.database.Id, this.Container.Id);

            testItem1 = ToDoActivity.CreateRandomToDoActivity();
            testItem1.pk = "myPk";
            await containerStandardSerializer.CreateItemAsync(testItem1, new Cosmos.PartitionKey(testItem1.pk));

            testItem2 = ToDoActivity.CreateRandomToDoActivity();
            testItem2.pk = "myPk";
            await containerStandardSerializer.CreateItemAsync(testItem2, new Cosmos.PartitionKey(testItem2.pk));
            CosmosSerializer cosmosSerializer = containerStandardSerializer.Database.Client.ClientOptions.Serializer;

            queryDefinitions = new List<QueryDefinition>()
            {
                new QueryDefinition("select * from t where t.pk = @pk" )
                .WithParameterStream("@pk", cosmosSerializer.ToStream(testItem1.pk)),
                new QueryDefinition("select * from t where t.cost = @cost" )
                .WithParameterStream("@cost", cosmosSerializer.ToStream(testItem1.cost)),
                new QueryDefinition("select * from t where t.taskNum = @taskNum" )
                .WithParameterStream("@taskNum", cosmosSerializer.ToStream(testItem1.taskNum)),
                new QueryDefinition("select * from t where t.CamelCase = @CamelCase" )
                .WithParameterStream("@CamelCase", cosmosSerializer.ToStream(testItem1.CamelCase)),
                new QueryDefinition("select * from t where t.valid = @valid" )
                .WithParameterStream("@valid", cosmosSerializer.ToStream(testItem1.valid)),
                new QueryDefinition("select * from t where t.description = @description" )
                .WithParameterStream("@description", cosmosSerializer.ToStream(testItem1.description)),
                new QueryDefinition("select * from t where t.pk = @pk and t.cost = @cost" )
                    .WithParameterStream("@pk", cosmosSerializer.ToStream(testItem1.pk))
                    .WithParameterStream("@cost", cosmosSerializer.ToStream(testItem1.cost)),
            };

            foreach (QueryDefinition queryDefinition in queryDefinitions)
            {
                List<ToDoActivity> allItems = new List<ToDoActivity>();
                int pageCount = 0;
                using (FeedIterator<ToDoActivity> feedIterator = containerStandardSerializer.GetItemQueryIterator<ToDoActivity>(
                    queryDefinition: queryDefinition))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        // Only need once to verify correct serialization of the query definition
                        FeedResponse<ToDoActivity> response = await feedIterator.ReadNextAsync(this.cancellationToken);
                        Assert.AreEqual(response.Count, response.Count());
                        allItems.AddRange(response);
                        pageCount++;
                    }
                }

                Assert.AreEqual(2, allItems.Count, $"missing query results. Only found: {allItems.Count} items for query:{queryDefinition.ToSqlQuerySpec().QueryText}");
                if (queryDefinition.QueryText.Contains("pk"))
                {
                    Assert.AreEqual(1, pageCount);
                }
                else
                {
                    Assert.AreEqual(3, pageCount);
                }



                IReadOnlyList<(string Name, object Value)> parameters1 = queryDefinition.GetQueryParameters();
                IReadOnlyList<(string Name, object Value)> parameters2 = queryDefinition.GetQueryParameters();

                Assert.AreSame(parameters1, parameters2);
            }
        }

        [TestMethod]
        public async Task ItemIterator()
        {
            IList<ToDoActivity> deleteList = await ToDoActivity.CreateRandomItems(this.Container, 3, randomPartitionKey: true);
            HashSet<string> itemIds = deleteList.Select(x => x.id).ToHashSet<string>();
            FeedIterator<ToDoActivity> feedIterator =
                this.Container.GetItemQueryIterator<ToDoActivity>();
            while (feedIterator.HasMoreResults)
            {
                foreach (ToDoActivity toDoActivity in await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    if (itemIds.Contains(toDoActivity.id))
                    {
                        itemIds.Remove(toDoActivity.id);
                    }
                }
            }

            Assert.AreEqual(itemIds.Count, 0);
        }

        [TestMethod]
        public async Task PerfItemIterator()
        {
            IList<ToDoActivity> deleteList = await ToDoActivity.CreateRandomItems(this.Container, 2000, randomPartitionKey: true);
            HashSet<string> itemIds = deleteList.Select(x => x.id).ToHashSet<string>();

            FeedIterator<ToDoActivity> feedIterator =
                this.Container.GetItemQueryIterator<ToDoActivity>();
            while (feedIterator.HasMoreResults)
            {
                foreach (ToDoActivity toDoActivity in await feedIterator.ReadNextAsync(this.cancellationToken))
                {
                    if (itemIds.Contains(toDoActivity.id))
                    {
                        itemIds.Remove(toDoActivity.id);
                    }
                }
            }

            Assert.AreEqual(itemIds.Count, 0);
        }


        [DataRow(1, 1)]
        [DataRow(5, 5)]
        [DataRow(6, 2)]
        [DataTestMethod]
        public async Task QuerySinglePartitionItemStreamTest(int perPKItemCount, int maxItemCount)
        {
            IList<ToDoActivity> deleteList = deleteList = await ToDoActivity.CreateRandomItems(this.Container, pkCount: 3, perPKItemCount: perPKItemCount, randomPartitionKey: true);
            ToDoActivity find = deleteList.First();

            QueryDefinition sql = new QueryDefinition("select * from r where r.pk = @pk").WithParameter("@pk", find.pk);

            int iterationCount = 0;
            int totalReadItem = 0;
            int expectedIterationCount = perPKItemCount / maxItemCount;
            string lastContinuationToken = null;

            do
            {
                iterationCount++;
                FeedIterator feedIterator = this.Container.GetItemQueryStreamIterator(
                    sql,
                    continuationToken: lastContinuationToken,
                    requestOptions: new QueryRequestOptions()
                    {
                        MaxItemCount = maxItemCount,
                        MaxConcurrency = 1,
                        PartitionKey = new Cosmos.PartitionKey(find.pk),
                    });

                ResponseMessage response = await feedIterator.ReadNextAsync();
                lastContinuationToken = response.Headers.ContinuationToken;
                Assert.AreEqual(response.ContinuationToken, response.Headers.ContinuationToken);

                System.Diagnostics.Trace.TraceInformation($"ContinuationToken: {lastContinuationToken}");
                Newtonsoft.Json.JsonSerializer serializer = new Newtonsoft.Json.JsonSerializer();

                ServerSideCumulativeMetrics metrics = response.Diagnostics.GetQueryMetrics();
                Assert.IsTrue(metrics.PartitionedMetrics.Count > 0);
                Assert.IsTrue(metrics.PartitionedMetrics[0].RequestCharge > 0);
                Assert.IsTrue(metrics.CumulativeMetrics.TotalTime > TimeSpan.Zero);
                Assert.IsTrue(metrics.CumulativeMetrics.QueryPreparationTime > TimeSpan.Zero);
                Assert.IsTrue(metrics.TotalRequestCharge > 0);

                if (metrics.CumulativeMetrics.RetrievedDocumentCount >= 1)
                {
                    Assert.IsTrue(metrics.CumulativeMetrics.RetrievedDocumentSize > 0);
                    Assert.IsTrue(metrics.CumulativeMetrics.DocumentLoadTime > TimeSpan.Zero);
                    Assert.IsTrue(metrics.CumulativeMetrics.RuntimeExecutionTime > TimeSpan.Zero);
                }
                else
                {
                    Assert.AreEqual(0, metrics.CumulativeMetrics.RetrievedDocumentSize);
                }

                using (StreamReader sr = new StreamReader(response.Content))
                using (JsonTextReader jtr = new JsonTextReader(sr))
                {
                    ToDoActivity[] results = serializer.Deserialize<CosmosFeedResponseUtil<ToDoActivity>>(jtr).Data.ToArray();
                    ToDoActivity[] readTodoActivities = results.OrderBy(e => e.id)
                        .ToArray();

                    ToDoActivity[] expectedTodoActivities = deleteList
                            .Where(e => e.pk == find.pk)
                            .Where(e => readTodoActivities.Any(e1 => e1.id == e.id))
                            .OrderBy(e => e.id)
                            .ToArray();

                    totalReadItem += expectedTodoActivities.Length;
                    string expectedSerialized = JsonConvert.SerializeObject(expectedTodoActivities);
                    string readSerialized = JsonConvert.SerializeObject(readTodoActivities);
                    System.Diagnostics.Trace.TraceInformation($"Expected: {Environment.NewLine} {expectedSerialized}");
                    System.Diagnostics.Trace.TraceInformation($"Read: {Environment.NewLine} {readSerialized}");

                    int count = results.Length;
                    Assert.AreEqual(maxItemCount, count);

                    Assert.AreEqual(expectedSerialized, readSerialized);

                    Assert.AreEqual(maxItemCount, expectedTodoActivities.Length);
                }
            }
            while (lastContinuationToken != null);

            Assert.AreEqual(expectedIterationCount, iterationCount);
            Assert.AreEqual(perPKItemCount, totalReadItem);
        }

        /// <summary>
        /// Validate multiple partition query
        /// </summary>
        [TestMethod]
        public async Task ItemMultiplePartitionQuery()
        {
            IList<ToDoActivity> itemList = await ToDoActivity.CreateRandomItems(this.Container, 3, randomPartitionKey: true);

            ToDoActivity find = itemList.First();
            QueryDefinition sql = new QueryDefinition("select * from toDoActivity t where t.id = '" + find.id + "'");

            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                MaxItemCount = 1,
                MaxConcurrency = -1,
            };

            FeedIterator<ToDoActivity> feedIterator = this.Container.GetItemQueryIterator<ToDoActivity>(
                    sql,
                    requestOptions: requestOptions);

            bool found = false;
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> iter = await feedIterator.ReadNextAsync();
                Assert.IsTrue(iter.Count() <= 1);
                if (iter.Count() == 1)
                {
                    found = true;
                    ToDoActivity response = iter.First();
                    Assert.AreEqual(find.id, response.id);
                }

                ServerSideCumulativeMetrics metrics = iter.Diagnostics.GetQueryMetrics();

                if (metrics != null)
                {
                    // This assumes that we are using parallel prefetch to hit multiple partitions concurrently
                    Assert.IsTrue(metrics.PartitionedMetrics.Count == 3);
                    Assert.IsTrue(metrics.CumulativeMetrics.TotalTime > TimeSpan.Zero);
                    Assert.IsTrue(metrics.CumulativeMetrics.QueryPreparationTime > TimeSpan.Zero);
                    Assert.IsTrue(metrics.TotalRequestCharge > 0);

                    foreach (ServerSidePartitionedMetrics partitionedMetrics in metrics.PartitionedMetrics)
                    {
                        Assert.IsNotNull(partitionedMetrics);
                        Assert.IsNotNull(partitionedMetrics.FeedRange);
                        Assert.IsNotNull(partitionedMetrics.PartitionKeyRangeId);
                        Assert.IsTrue(partitionedMetrics.RequestCharge > 0);
                    }

                    if (metrics.CumulativeMetrics.RetrievedDocumentCount >= 1)
                    {
                        Assert.IsTrue(metrics.CumulativeMetrics.RetrievedDocumentSize > 0);
                        Assert.IsTrue(metrics.CumulativeMetrics.DocumentLoadTime > TimeSpan.Zero);
                        Assert.IsTrue(metrics.CumulativeMetrics.RuntimeExecutionTime > TimeSpan.Zero);
                    }
                    else
                    {
                        Assert.AreEqual(0, metrics.CumulativeMetrics.RetrievedDocumentSize);
                    }
                }
                else
                {
                    string diag = iter.Diagnostics.ToString();
                    Assert.IsNotNull(diag);
                }
            }

            Assert.IsTrue(found);
        }

        /// <summary>
        /// Validate single partition query using gateway mode.
        /// </summary>
        [TestMethod]
        public async Task ItemSinglePartitionQueryGateway()
        {
            ContainerResponse containerResponse = await this.database.CreateContainerAsync(
            new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: "/pk"));

            Container createdContainer = (ContainerInlineCore)containerResponse;
            CosmosClient client1 = TestCommon.CreateCosmosClient(useGateway: true);

            Container container = client1.GetContainer(this.database.Id, createdContainer.Id);

            string findId = "id2002";
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity("pk2002", findId);
            await container.CreateItemAsync<ToDoActivity>(item);

            QueryDefinition sql = new QueryDefinition("select * from toDoActivity t where t.id = '" + findId + "'");

            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                MaxBufferedItemCount = 10,
                ResponseContinuationTokenLimitInKb = 500,
                MaxItemCount = 1,
                MaxConcurrency = 1,
            };

            FeedIterator<ToDoActivity> feedIterator = container.GetItemQueryIterator<ToDoActivity>(
                    sql,
                    requestOptions: requestOptions);

            bool found = false;
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> iter = await feedIterator.ReadNextAsync();
                Assert.IsTrue(iter.Count() <= 1);
                if (iter.Count() == 1)
                {
                    found = true;
                    ToDoActivity response = iter.First();
                    Assert.AreEqual(findId, response.id);
                }

                ServerSideCumulativeMetrics metrics = iter.Diagnostics.GetQueryMetrics();

                if (metrics != null)
                {
                    Assert.IsTrue(metrics.PartitionedMetrics.Count == 1);
                    Assert.IsTrue(metrics.CumulativeMetrics.TotalTime > TimeSpan.Zero);
                    Assert.IsTrue(metrics.CumulativeMetrics.QueryPreparationTime > TimeSpan.Zero);
                    Assert.IsTrue(metrics.TotalRequestCharge > 0);

                    foreach (ServerSidePartitionedMetrics partitionedMetrics in metrics.PartitionedMetrics)
                    {
                        Assert.IsNotNull(partitionedMetrics);
                        Assert.IsNotNull(partitionedMetrics.FeedRange);
                        Assert.IsNull(partitionedMetrics.PartitionKeyRangeId);
                        Assert.IsTrue(partitionedMetrics.RequestCharge > 0);
                    }

                    if (metrics.CumulativeMetrics.RetrievedDocumentCount >= 1)
                    {
                        Assert.IsTrue(metrics.CumulativeMetrics.RetrievedDocumentSize > 0);
                        Assert.IsTrue(metrics.CumulativeMetrics.DocumentLoadTime > TimeSpan.Zero);
                        Assert.IsTrue(metrics.CumulativeMetrics.RuntimeExecutionTime > TimeSpan.Zero);
                    }
                    else
                    {
                        Assert.AreEqual(0, metrics.CumulativeMetrics.RetrievedDocumentSize);
                    }
                }
            }

            Assert.IsTrue(found);
        }

        /// <summary>
        /// Validate multiple partition query
        /// </summary>
        [TestMethod]
        public async Task ItemMultiplePartitionOrderByQueryStream()
        {
            CultureInfo defaultCultureInfo = System.Threading.Thread.CurrentThread.CurrentCulture;

            CultureInfo[] cultureInfoList = new CultureInfo[]
            {
                defaultCultureInfo,
                System.Globalization.CultureInfo.GetCultureInfo("fr-FR")
            };

            IList<ToDoActivity> deleteList = await ToDoActivity.CreateRandomItems(
                this.Container,
                300,
                randomPartitionKey: true,
                randomTaskNumber: true);

            try
            {
                foreach (CultureInfo cultureInfo in cultureInfoList)
                {
                    System.Threading.Thread.CurrentThread.CurrentCulture = cultureInfo;

                    QueryDefinition sql = new QueryDefinition("SELECT * FROM toDoActivity t ORDER BY t.taskNum ");

                    QueryRequestOptions requestOptions = new QueryRequestOptions()
                    {
                        MaxBufferedItemCount = 10,
                        ResponseContinuationTokenLimitInKb = 500,
                        MaxConcurrency = 5,
                        MaxItemCount = 1,
                    };

                    List<ToDoActivity> resultList = new List<ToDoActivity>();
                    double totalRequstCharge = 0;
                    FeedIterator feedIterator = this.Container.GetItemQueryStreamIterator(
                        sql,
                        requestOptions: requestOptions);

                    while (feedIterator.HasMoreResults)
                    {
                        ResponseMessage iter = await feedIterator.ReadNextAsync();
                        Assert.IsTrue(iter.IsSuccessStatusCode);
                        Assert.IsNull(iter.ErrorMessage);
                        totalRequstCharge += iter.Headers.RequestCharge;

                        ToDoActivity[] activities = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(iter.Content).Data.ToArray();
                        Assert.AreEqual(1, activities.Length);
                        ToDoActivity response = activities.First();
                        resultList.Add(response);
                    }

                    Assert.AreEqual(deleteList.Count, resultList.Count);
                    Assert.IsTrue(totalRequstCharge > 0);

                    List<ToDoActivity> verifiedOrderBy = deleteList.OrderBy(x => x.taskNum).ToList();
                    for (int i = 0; i < verifiedOrderBy.Count(); i++)
                    {
                        Assert.AreEqual(verifiedOrderBy[i].taskNum, resultList[i].taskNum);
                        Assert.AreEqual(verifiedOrderBy[i].id, resultList[i].id);
                    }
                }
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = defaultCultureInfo;
            }
        }

        /// <summary>
        /// Validate multiple partition query
        /// </summary>
        [TestMethod]
        public async Task ItemMultiplePartitionQueryStream()
        {
            IList<ToDoActivity> deleteList = await ToDoActivity.CreateRandomItems(this.Container, 101, randomPartitionKey: true);
            QueryDefinition sql = new QueryDefinition("SELECT * FROM toDoActivity t");

            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                MaxConcurrency = 5,
                MaxItemCount = 5,
            };

            List<ToDoActivity> resultList = new List<ToDoActivity>();
            double totalRequstCharge = 0;
            FeedIterator feedIterator = this.Container.GetItemQueryStreamIterator(sql, requestOptions: requestOptions);
            while (feedIterator.HasMoreResults)
            {
                ResponseMessage iter = await feedIterator.ReadNextAsync();
                Assert.IsTrue(iter.IsSuccessStatusCode);
                Assert.IsNull(iter.ErrorMessage);
                totalRequstCharge += iter.Headers.RequestCharge;
                ToDoActivity[] response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(iter.Content).Data.ToArray();
                Assert.IsTrue(response.Length <= 5);
                resultList.AddRange(response);
            }

            Assert.AreEqual(deleteList.Count, resultList.Count);
            Assert.IsTrue(totalRequstCharge > 0);

            List<ToDoActivity> verifiedOrderBy = deleteList.OrderBy(x => x.id).ToList();
            resultList = resultList.OrderBy(x => x.id).ToList();
            for (int i = 0; i < verifiedOrderBy.Count(); i++)
            {
                Assert.AreEqual(verifiedOrderBy[i].taskNum, resultList[i].taskNum);
                Assert.AreEqual(verifiedOrderBy[i].id, resultList[i].id);
            }
        }

        /// <summary>
        /// Validate multiple partition query
        /// </summary>
        [TestMethod]
        public async Task ItemSinglePartitionQueryStream()
        {
            //Create a 101 random items with random guid PK values
            IList<ToDoActivity> deleteList = await ToDoActivity.CreateRandomItems(this.Container, pkCount: 101, perPKItemCount: 1, randomPartitionKey: true);

            // Create 10 items with same pk value
            IList<ToDoActivity> findItems = await ToDoActivity.CreateRandomItems(this.Container, pkCount: 1, perPKItemCount: 10, randomPartitionKey: false);

            string findPkValue = findItems.First().pk;
            QueryDefinition sql = new QueryDefinition("SELECT * FROM toDoActivity t where t.pk = @pkValue").WithParameter("@pkValue", findPkValue);


            double totalRequstCharge = 0;
            FeedIterator setIterator = this.Container.GetItemQueryStreamIterator(
                sql,
                requestOptions: new QueryRequestOptions()
                {
                    MaxConcurrency = 1,
                    PartitionKey = new Cosmos.PartitionKey(findPkValue),
                });

            List<ToDoActivity> foundItems = new List<ToDoActivity>();
            while (setIterator.HasMoreResults)
            {
                ResponseMessage iter = await setIterator.ReadNextAsync();
                Assert.IsTrue(iter.IsSuccessStatusCode);
                Assert.IsNull(iter.ErrorMessage);
                totalRequstCharge += iter.Headers.RequestCharge;
                Collection<ToDoActivity> response = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(iter.Content).Data;
                foundItems.AddRange(response);
            }

            Assert.AreEqual(findItems.Count, foundItems.Count);
            Assert.IsFalse(foundItems.Any(x => !string.Equals(x.pk, findPkValue)), "All the found items should have the same PK value");
            Assert.IsTrue(totalRequstCharge > 0);
        }

        [TestMethod]
        public async Task EpkPointReadTest()
        {
            string pk = Guid.NewGuid().ToString();
            string epk = new PartitionKey(pk)
                            .InternalKey
                            .GetEffectivePartitionKeyString(this.containerSettings.PartitionKey);

            Dictionary<string, object> properties = new Dictionary<string, object>()
            {
                { WFConstants.BackendHeaders.EffectivePartitionKeyString, epk },
            };

            ItemRequestOptions itemRequestOptions = new ItemRequestOptions
            {
                IsEffectivePartitionKeyRouting = true,
                Properties = properties,
            };

            ResponseMessage response = await this.Container.ReadItemStreamAsync(
                Guid.NewGuid().ToString(),
                Cosmos.PartitionKey.Null,
                itemRequestOptions);

            // Ideally it should be NotFound
            // BadReqeust bcoz collection is regular and not binary 
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

            await this.Container.CreateItemAsync<dynamic>(new { id = Guid.NewGuid().ToString(), pk = "test" });
            epk = new PartitionKey("test")
                           .InternalKey
                           .GetEffectivePartitionKeyString(this.containerSettings.PartitionKey);
            properties = new Dictionary<string, object>()
            {
                { WFConstants.BackendHeaders.EffectivePartitionKeyString, epk },
            };

            QueryRequestOptions queryRequestOptions = new QueryRequestOptions
            {
                IsEffectivePartitionKeyRouting = true,
                Properties = properties,
            };

            using (FeedIterator<dynamic> resultSet = this.Container.GetItemQueryIterator<dynamic>(
                    queryText: "SELECT * FROM root",
                    requestOptions: queryRequestOptions))
            {
                FeedResponse<dynamic> feedresponse = await resultSet.ReadNextAsync();
                Assert.IsNotNull(feedresponse.Resource);
                Assert.AreEqual(1, feedresponse.Count());
            }

        }

        /// <summary>
        /// Validate that if the EPK is set in the options that only a single range is selected.
        /// </summary>
        [TestMethod]
        public async Task ItemEpkQuerySingleKeyRangeValidation()
        {
            ContainerInternal container = null;
            try
            {
                // Create a container large enough to have at least 2 partitions
                ContainerResponse containerResponse = await this.database.CreateContainerAsync(
                    id: Guid.NewGuid().ToString(),
                    partitionKeyPath: "/pk",
                    throughput: 15000);
                container = (ContainerInlineCore)containerResponse;

                // Get all the partition key ranges to verify there is more than one partition
                IRoutingMapProvider routingMapProvider = await this.GetClient().DocumentClient.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);
                IReadOnlyList<PartitionKeyRange> ranges = await routingMapProvider.TryGetOverlappingRangesAsync(
                    containerResponse.Resource.ResourceId,
                    new Documents.Routing.Range<string>("00", "FF", isMaxInclusive: true, isMinInclusive: true),
                    NoOpTrace.Singleton,
                    forceRefresh: false);

                // If this fails the RUs of the container needs to be increased to ensure at least 2 partitions.
                Assert.IsTrue(ranges.Count > 1, " RUs of the container needs to be increased to ensure at least 2 partitions.");


                ContainerQueryProperties containerQueryProperties = new ContainerQueryProperties(
                    containerResponse.Resource.ResourceId,
                    effectivePartitionKeyRanges: null,
                    //new List<Documents.Routing.Range<string>> { new Documents.Routing.Range<string>("AA", "AA", true, true) },
                    containerResponse.Resource.PartitionKey,
                    vectorEmbeddingPolicy: null,
                    containerResponse.Resource.GeospatialConfig.GeospatialType);

                // There should only be one range since the EPK option is set.
                List<PartitionKeyRange> partitionKeyRanges = await CosmosQueryExecutionContextFactory.GetTargetPartitionKeyRangesAsync(
                    queryClient: new CosmosQueryClientCore(container.ClientContext, container),
                    resourceLink: container.LinkUri,
                    partitionedQueryExecutionInfo: null,
                    containerQueryProperties: containerQueryProperties,
                    properties: new Dictionary<string, object>()
                    {
                        {"x-ms-effective-partition-key-string", "AA" }
                    },
                    feedRangeInternal: null,
                    trace: NoOpTrace.Singleton);

                Assert.IsTrue(partitionKeyRanges.Count == 1, "Only 1 partition key range should be selected since the EPK option is set.");

            }
            finally
            {
                if (container != null)
                {
                    await container.DeleteContainerStreamAsync();
                }
            }
        }

        /// <summary>
        /// Validate multiple partition query
        /// </summary>
        [TestMethod]
        public async Task ItemQueryStreamSerializationSetting()
        {
            IList<ToDoActivity> deleteList = await ToDoActivity.CreateRandomItems(
                container: this.Container,
                pkCount: 101,
                randomTaskNumber: true);

            QueryDefinition sql = new QueryDefinition("SELECT * FROM toDoActivity t ORDER BY t.taskNum");

            CosmosSerializationFormatOptions options = new CosmosSerializationFormatOptions(
                ContentSerializationFormat.CosmosBinary.ToString(),
                (content) => JsonNavigator.Create(content),
                () => JsonWriter.Create(JsonSerializationFormat.Binary));

            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                CosmosSerializationFormatOptions = options,
                MaxConcurrency = 5,
                MaxItemCount = 5,
            };

            List<ToDoActivity> resultList = new List<ToDoActivity>();
            double totalRequstCharge = 0;
            FeedIterator feedIterator = this.Container.GetItemQueryStreamIterator(
                sql,
                requestOptions: requestOptions);

            while (feedIterator.HasMoreResults)
            {
                ResponseMessage response = await feedIterator.ReadNextAsync();
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.IsNull(response.ErrorMessage);
                totalRequstCharge += response.Headers.RequestCharge;

                //Copy the stream and check that the first byte is the correct value
                MemoryStream memoryStream = new MemoryStream();
                response.Content.CopyTo(memoryStream);
                byte[] content = memoryStream.ToArray();
                response.Content.Position = 0;

                // Examine the first buffer byte to determine the serialization format
                byte firstByte = content[0];
                Assert.AreEqual(128, firstByte);
                Assert.AreEqual(JsonSerializationFormat.Binary, (JsonSerializationFormat)firstByte);

                IJsonReader reader = JsonReader.Create(content);
                IJsonWriter textWriter = JsonWriter.Create(JsonSerializationFormat.Text);
                reader.WriteAll(textWriter);
                string json = Encoding.UTF8.GetString(textWriter.GetResult().ToArray());
                Assert.IsNotNull(json);
                ToDoActivity[] responseActivities = JsonConvert.DeserializeObject<CosmosFeedResponseUtil<ToDoActivity>>(json).Data.ToArray();
                Assert.IsTrue(responseActivities.Length <= 5);
                resultList.AddRange(responseActivities);
            }

            Assert.AreEqual(deleteList.Count, resultList.Count);
            Assert.IsTrue(totalRequstCharge > 0);

            List<ToDoActivity> verifiedOrderBy = deleteList.OrderBy(x => x.taskNum).ToList();
            for (int i = 0; i < verifiedOrderBy.Count(); i++)
            {
                Assert.AreEqual(verifiedOrderBy[i].taskNum, resultList[i].taskNum);
                Assert.AreEqual(verifiedOrderBy[i].id, resultList[i].id);
            }
        }

        /// <summary>
        /// Validate that the max item count works correctly.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task ValidateMaxItemCountOnItemQuery()
        {
            IList<ToDoActivity> deleteList = await ToDoActivity.CreateRandomItems(container: this.Container, pkCount: 1, perPKItemCount: 6, randomPartitionKey: false);

            ToDoActivity toDoActivity = deleteList.First();
            QueryDefinition sql = new QueryDefinition(
                "select * from toDoActivity t where t.pk = @pk")
                .WithParameter("@pk", toDoActivity.pk);

            // Test max size at 1
            FeedIterator<ToDoActivity> feedIterator = this.Container.GetItemQueryIterator<ToDoActivity>(
                sql,
                requestOptions: new QueryRequestOptions()
                {
                    MaxItemCount = 1,
                    PartitionKey = new Cosmos.PartitionKey(toDoActivity.pk),
                });

            while (feedIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> iter = await feedIterator.ReadNextAsync();
                Assert.AreEqual(1, iter.Count());
            }

            // Test max size at 2
            FeedIterator<ToDoActivity> setIteratorMax2 = this.Container.GetItemQueryIterator<ToDoActivity>(
                sql,
                requestOptions: new QueryRequestOptions()
                {
                    MaxItemCount = 2,
                    PartitionKey = new Cosmos.PartitionKey(toDoActivity.pk),
                });

            while (setIteratorMax2.HasMoreResults)
            {
                FeedResponse<ToDoActivity> iter = await setIteratorMax2.ReadNextAsync();
                Assert.AreEqual(2, iter.Count());
            }
        }

        /// <summary>
        /// Validate that the max item count works correctly.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task NegativeQueryTest()
        {
            await ToDoActivity.CreateRandomItems(container: this.Container, pkCount: 10, perPKItemCount: 20, randomPartitionKey: true);

            try
            {
                using (FeedIterator<dynamic> resultSet = this.Container.GetItemQueryIterator<dynamic>(
                    queryText: "SELECT r.id FROM root r WHERE r._ts > 0",
                    requestOptions: new QueryRequestOptions()
                    {
                        ResponseContinuationTokenLimitInKb = 0,
                        MaxItemCount = 10,
                        MaxConcurrency = 1
                    }))
                {
                    await resultSet.ReadNextAsync();
                }
                Assert.Fail("Expected query to fail");
            }
            catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.BadRequest)
            {
                Assert.IsTrue(exception.Message.Contains("continuation token limit specified is not large enough"), exception.Message);
            }

            try
            {
                using (FeedIterator<dynamic> resultSet = this.Container.GetItemQueryIterator<dynamic>(
                    queryText: "SELECT r.id FROM root r WHERE r._ts >!= 0",
                    requestOptions: new QueryRequestOptions() { MaxConcurrency = 1 }))
                {
                    await resultSet.ReadNextAsync();
                }
                Assert.Fail("Expected query to fail");
            }
            catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.BadRequest)
            {
                Assert.IsTrue(exception.Message.Contains("Syntax error, incorrect syntax near"), exception.Message);
            }
        }

        [TestMethod]
        [DataRow(true, DisplayName = "Test scenario when binary encoding is enabled at client level.")]
        [DataRow(false, DisplayName = "Test scenario when binary encoding is disabled at client level.")]
        public async Task ItemRequestOptionAccessConditionTest(bool binaryEncodingEnabledInClient)
        {
            try
            {
                if (binaryEncodingEnabledInClient)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");
                }

                // Create an item
                ToDoActivity testItem = (await ToDoActivity.CreateRandomItems(this.Container, 1, randomPartitionKey: true)).First();

                ItemRequestOptions itemRequestOptions = new ItemRequestOptions()
                {
                    IfMatchEtag = Guid.NewGuid().ToString(),
                };

                using (ResponseMessage responseMessage = await this.Container.UpsertItemStreamAsync(
                        streamPayload: TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem),
                        partitionKey: new Cosmos.PartitionKey(testItem.pk),
                        requestOptions: itemRequestOptions))
                {
                    Assert.IsNotNull(responseMessage);
                    Assert.IsNull(responseMessage.Content);
                    Assert.AreEqual(HttpStatusCode.PreconditionFailed, responseMessage.StatusCode, responseMessage.ErrorMessage);
                    Assert.AreNotEqual(responseMessage.Headers.ActivityId, Guid.Empty);
                    Assert.IsTrue(responseMessage.Headers.RequestCharge > 0);
                    Assert.IsFalse(string.IsNullOrEmpty(responseMessage.ErrorMessage));
                    Assert.IsTrue(responseMessage.ErrorMessage.Contains("One of the specified pre-condition is not met"));
                }

                try
                {
                    ItemResponse<ToDoActivity> response = await this.Container.UpsertItemAsync<ToDoActivity>(
                        item: testItem,
                        requestOptions: itemRequestOptions);
                    Assert.Fail("Access condition should have failed");
                }
                catch (CosmosException e)
                {
                    Assert.IsNotNull(e);
                    Assert.AreEqual(HttpStatusCode.PreconditionFailed, e.StatusCode, e.Message);
                    Assert.AreNotEqual(e.ActivityId, Guid.Empty);
                    Assert.IsTrue(e.RequestCharge > 0);
                    string expectedResponseBody = $"{Environment.NewLine}Errors : [{Environment.NewLine}  \"One of the specified pre-condition is not met. Learn more: https://aka.ms/CosmosDB/sql/errors/precondition-failed\"{Environment.NewLine}]{Environment.NewLine}";
                    Assert.AreEqual(expectedResponseBody, e.ResponseBody);
                    string expectedMessage = $"Response status code does not indicate success: PreconditionFailed (412); Substatus: 0; ActivityId: {e.ActivityId}; Reason: ({expectedResponseBody});";
                    Assert.AreEqual(expectedMessage, e.Message);
                }
                finally
                {
                    ItemResponse<ToDoActivity> deleteResponse = await this.Container.DeleteItemAsync<ToDoActivity>(partitionKey: new Cosmos.PartitionKey(testItem.pk), id: testItem.id);
                    Assert.IsNotNull(deleteResponse);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, null);
            }
        }

        [TestMethod]
        [DataRow(true, DisplayName = "Test scenario when binary encoding is enabled at client level.")]
        [DataRow(false, DisplayName = "Test scenario when binary encoding is disabled at client level.")]
        public async Task ItemReplaceAsyncTest(bool binaryEncodingEnabledInClient)
        {
            try
            {
                if (binaryEncodingEnabledInClient)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");
                }

                // Create an item
                ToDoActivity testItem = (await ToDoActivity.CreateRandomItems(this.Container, 1, randomPartitionKey: true)).First();

                string originalId = testItem.id;
                testItem.id = Guid.NewGuid().ToString();

                ItemResponse<ToDoActivity> response = await this.Container.ReplaceItemAsync<ToDoActivity>(
                    id: originalId,
                    item: testItem);

                Assert.AreEqual(testItem.id, response.Resource.id);
                Assert.AreNotEqual(originalId, response.Resource.id);

                string originalStatus = testItem.pk;
                testItem.pk = Guid.NewGuid().ToString();

                try
                {
                    response = await this.Container.ReplaceItemAsync<ToDoActivity>(
                    id: testItem.id,
                    partitionKey: new Cosmos.PartitionKey(originalStatus),
                    item: testItem);
                    Assert.Fail("Replace changing partition key is not supported.");
                }
                catch (CosmosException ce)
                {
                    Assert.AreEqual((HttpStatusCode)400, ce.StatusCode);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, null);
            }
        }

        [TestMethod]
        public async Task ItemPatchFailureTest()
        {
            // Create an item
            ToDoActivity testItem = (await ToDoActivity.CreateRandomItems(this.Container, 1, randomPartitionKey: true)).First();
            ContainerInternal containerInternal = (ContainerInternal)this.Container;

            List<PatchOperation> patchOperations = new List<PatchOperation>
            {
                PatchOperation.Add("/nonExistentParent/Child", "bar"),
                PatchOperation.Remove("/cost")
            };

            // item does not exist - 404 Resource Not Found error
            try
            {
                await containerInternal.PatchItemAsync<ToDoActivity>(
                    id: Guid.NewGuid().ToString(),
                    partitionKey: new Cosmos.PartitionKey(testItem.pk),
                    patchOperations: patchOperations);

                Assert.Fail("Patch operation should fail if the item doesn't exist.");
            }
            catch (CosmosException ex)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
                Assert.IsTrue(ex.Message.Contains("Resource Not Found"));
                Assert.IsTrue(ex.Message.Contains("https://aka.ms/cosmosdb-tsg-not-found"));
                CosmosItemTests.ValidateCosmosException(ex);
            }

            // adding a child when parent / ancestor does not exist - 400 BadRequest response
            try
            {
                await containerInternal.PatchItemAsync<ToDoActivity>(
                    id: testItem.id,
                    partitionKey: new Cosmos.PartitionKey(testItem.pk),
                    patchOperations: patchOperations);

                Assert.Fail("Patch operation should fail for malformed PatchSpecification.");
            }
            catch (CosmosException ex)
            {
                Assert.AreEqual(HttpStatusCode.BadRequest, ex.StatusCode);
                Assert.IsTrue(ex.Message.Contains(@"For Operation(1): Add Operation can only create a child object of an existing node(array or object) and cannot create path recursively, no path found beyond: 'nonExistentParent'. Learn more: https://aka.ms/cosmosdbpatchdocs"), ex.Message);
                CosmosItemTests.ValidateCosmosException(ex);
            }

            // precondition failure - 412 response
            PatchItemRequestOptions requestOptions = new PatchItemRequestOptions()
            {
                IfMatchEtag = Guid.NewGuid().ToString()
            };

            try
            {
                await containerInternal.PatchItemAsync<ToDoActivity>(
                    id: testItem.id,
                    partitionKey: new Cosmos.PartitionKey(testItem.pk),
                    patchOperations: patchOperations,
                    requestOptions);

                Assert.Fail("Patch operation should fail in case of pre-condition failure.");
            }
            catch (CosmosException ex)
            {
                Assert.AreEqual(HttpStatusCode.PreconditionFailed, ex.StatusCode);
                Assert.IsTrue(ex.Message.Contains("One of the specified pre-condition is not met"));
                CosmosItemTests.ValidateCosmosException(ex);
            }
        }

        [TestMethod]
        public async Task ItemPatchSuccessTest()
        {
            // Create an item
            ToDoActivity testItem = (await ToDoActivity.CreateRandomItems(this.Container, 1, randomPartitionKey: true)).First();
            ContainerInternal containerInternal = (ContainerInternal)this.Container;

            int originalTaskNum = testItem.taskNum;
            int newTaskNum = originalTaskNum + 1;
            //Int16 one = 1;

            Assert.IsNull(testItem.children[1].pk);
            List<PatchOperation> patchOperations = new List<PatchOperation>()
            {
                PatchOperation.Set("/children/0/description", "testSet"),
                PatchOperation.Add("/children/1/pk", "patched"),
                PatchOperation.Remove("/description"),
                PatchOperation.Replace("/taskNum", newTaskNum),
                //PatchOperation.Increment("/taskNum", one)

                PatchOperation.Set<object>("/children/1/nullableInt",null)
            };

            // without content response
            PatchItemRequestOptions requestOptions = new PatchItemRequestOptions()
            {
                EnableContentResponseOnWrite = false
            };

            ItemResponse<ToDoActivity> response = await containerInternal.PatchItemAsync<ToDoActivity>(
                id: testItem.id,
                partitionKey: new Cosmos.PartitionKey(testItem.pk),
                patchOperations: patchOperations,
                requestOptions);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsNull(response.Resource);

            // read resource to validate the patch operation
            response = await containerInternal.ReadItemAsync<ToDoActivity>(
                testItem.id,
                partitionKey: new Cosmos.PartitionKey(testItem.pk));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsNotNull(response.Resource);
            Assert.AreEqual("testSet", response.Resource.children[0].description);
            Assert.AreEqual("patched", response.Resource.children[1].pk);
            Assert.IsNull(response.Resource.description);
            Assert.AreEqual(newTaskNum, response.Resource.taskNum);
            Assert.IsNull(response.Resource.children[1].nullableInt);

            patchOperations.Clear();
            patchOperations.Add(PatchOperation.Add("/children/0/cost", 1));
            // with content response
            response = await containerInternal.PatchItemAsync<ToDoActivity>(
                id: testItem.id,
                partitionKey: new Cosmos.PartitionKey(testItem.pk),
                patchOperations: patchOperations);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsNotNull(response.Resource);
            Assert.AreEqual(1, response.Resource.children[0].cost);

            patchOperations.Clear();
            patchOperations.Add(PatchOperation.Set<object>("/children/0/id", null));
            // with content response
            response = await containerInternal.PatchItemAsync<ToDoActivity>(
                id: testItem.id,
                partitionKey: new Cosmos.PartitionKey(testItem.pk),
                patchOperations: patchOperations);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsNotNull(response.Resource);
            Assert.AreEqual(null, response.Resource.children[0].id);

            patchOperations.Clear();
            patchOperations.Add(PatchOperation.Add("/children/1/description", "Child#1"));
            patchOperations.Add(PatchOperation.Move("/children/0/description", "/description"));
            patchOperations.Add(PatchOperation.Move("/children/1/description", "/children/0/description"));
            // with content response
            response = await containerInternal.PatchItemAsync<ToDoActivity>(
                id: testItem.id,
                partitionKey: new Cosmos.PartitionKey(testItem.pk),
                patchOperations: patchOperations);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsNotNull(response.Resource);
            Assert.AreEqual("testSet", response.Resource.description);
            Assert.AreEqual("Child#1", response.Resource.children[0].description);
            Assert.IsNull(response.Resource.children[1].description);
        }

        [TestMethod]
        public async Task PatchItemStreamTest()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ContainerInternal containerInternal = (ContainerInternal)this.Container;

            List<PatchOperation> patchOperations = new List<PatchOperation>()
            {
                PatchOperation.Add("/children/1/pk", "patched"),
                PatchOperation.Remove("/description"),
                PatchOperation.Replace("/taskNum", testItem.taskNum+1)
            };

            PatchItemRequestOptions requestOptions = new PatchItemRequestOptions()
            {
                FilterPredicate = "from root where root.x = 3"
            };

            // Patch a non-existing item. It should fail, and not throw an exception.
            using (ResponseMessage response = await containerInternal.PatchItemStreamAsync(
                partitionKey: new Cosmos.PartitionKey(testItem.pk),
                id: testItem.id,
                patchOperations: patchOperations,
                requestOptions: requestOptions))
            {
                Assert.IsFalse(response.IsSuccessStatusCode);
                Assert.IsNotNull(response);
                Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, response.ErrorMessage);
            }

            using (Stream stream = TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem))
            {
                // Create the item
                using (ResponseMessage response = await this.Container.CreateItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.pk), streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                }
            }

            // Patch
            using (ResponseMessage response = await containerInternal.PatchItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.pk), id: testItem.id, patchOperations: patchOperations))
            {
                Assert.IsNotNull(response);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }

            // Read and validate
            ItemResponse<ToDoActivity> itemResponse = await this.Container.ReadItemAsync<ToDoActivity>(testItem.id, partitionKey: new Cosmos.PartitionKey(testItem.pk));
            Assert.AreEqual(HttpStatusCode.OK, itemResponse.StatusCode);
            Assert.IsNotNull(itemResponse.Resource);
            Assert.AreEqual("patched", itemResponse.Resource.children[1].pk);
            Assert.IsNull(itemResponse.Resource.description);
            Assert.AreEqual(testItem.taskNum + 1, itemResponse.Resource.taskNum);

            // Delete
            using (ResponseMessage deleteResponse = await this.Container.DeleteItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.pk), id: testItem.id))
            {
                Assert.IsNotNull(deleteResponse);
                Assert.AreEqual(deleteResponse.StatusCode, HttpStatusCode.NoContent);
            }
        }

        [TestMethod]
        public async Task ContainerRecreateScenarioGatewayTest()
        {
            ContainerResponse response = await this.database.CreateContainerAsync(
                        new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: "/pk"));

            Container createdContainer = (ContainerInlineCore)response;

            CosmosClient client1 = TestCommon.CreateCosmosClient(useGateway: true);
            CosmosClient client2 = TestCommon.CreateCosmosClient(useGateway: true);

            Container container1 = client1.GetContainer(this.database.Id, createdContainer.Id);
            Container container2 = client2.GetContainer(this.database.Id, createdContainer.Id);
            Cosmos.Database database2 = client2.GetDatabase(this.database.Id);

            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity("pk2002", "id2002");
            await container1.CreateItemAsync<ToDoActivity>(item);

            await container2.DeleteContainerAsync();
            await database2.CreateContainerAsync(createdContainer.Id, "/pk");

            container2 = database2.GetContainer(this.Container.Id);
            await container2.CreateItemAsync<ToDoActivity>(item);

            // should not throw exception
            await this.Container.ReadItemAsync<ToDoActivity>("id2002", new Cosmos.PartitionKey("pk2002"));
        }

        [TestMethod]
        public async Task BatchPatchConditionTest()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ContainerInternal containerInternal = (ContainerInternal)this.Container;

            using (Stream stream = TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem))
            {
                // Create the item
                using (ResponseMessage response = await this.Container.CreateItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.pk), streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                }
            }

            List<PatchOperation> patchOperations = new List<PatchOperation>()
            {
                PatchOperation.Add("/children/1/pk", "patched"),
                PatchOperation.Remove("/description"),
                PatchOperation.Add("/taskNum", 8)
            };

            using (ResponseMessage response = await containerInternal.PatchItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.pk), id: testItem.id, patchOperations: patchOperations))
            {
                Assert.IsNotNull(response);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }

            List<PatchOperation> patchOperationsUpdateTaskNum12 = new List<PatchOperation>()
            {
                PatchOperation.Replace("/taskNum", 12)
            };

            TransactionalBatchPatchItemRequestOptions requestOptionsFalse = new TransactionalBatchPatchItemRequestOptions()
            {
                FilterPredicate = "from c where c.taskNum = 3"
            };

            TransactionalBatchInternal transactionalBatchInternalFalse = (TransactionalBatchInternal)containerInternal.CreateTransactionalBatch(new Cosmos.PartitionKey(testItem.pk));
            transactionalBatchInternalFalse.PatchItem(id: testItem.id, patchOperationsUpdateTaskNum12, requestOptionsFalse);
            using (TransactionalBatchResponse batchResponse = await transactionalBatchInternalFalse.ExecuteAsync())
            {
                Assert.IsNotNull(batchResponse);
                Assert.AreEqual(HttpStatusCode.PreconditionFailed, batchResponse.StatusCode);
            }

            {
                // Read and validate
                ItemResponse<ToDoActivity> itemResponsemid = await this.Container.ReadItemAsync<ToDoActivity>(testItem.id, partitionKey: new Cosmos.PartitionKey(testItem.pk));
                Assert.AreEqual(HttpStatusCode.OK, itemResponsemid.StatusCode);
                Assert.IsNotNull(itemResponsemid.Resource);
                Assert.AreEqual("patched", itemResponsemid.Resource.children[1].pk);
                Assert.IsNull(itemResponsemid.Resource.description);
                Assert.AreEqual(8, itemResponsemid.Resource.taskNum);
            }

            List<PatchOperation> patchOperationsUpdateTaskNum14 = new List<PatchOperation>()
            {
                PatchOperation.Increment("/taskNum", 6)
            };

            TransactionalBatchPatchItemRequestOptions requestOptionsTrue = new TransactionalBatchPatchItemRequestOptions()
            {
                FilterPredicate = "from root where root.taskNum = 8"
            };

            TransactionalBatchInternal transactionalBatchInternalTrue = (TransactionalBatchInternal)containerInternal.CreateTransactionalBatch(new Cosmos.PartitionKey(testItem.pk));
            transactionalBatchInternalTrue.PatchItem(id: testItem.id, patchOperationsUpdateTaskNum14, requestOptionsTrue);
            using (TransactionalBatchResponse batchResponse = await transactionalBatchInternalTrue.ExecuteAsync())
            {
                Assert.IsNotNull(batchResponse);
                Assert.AreEqual(HttpStatusCode.OK, batchResponse.StatusCode);
            }

            // Read and validate
            ItemResponse<ToDoActivity> itemResponse = await this.Container.ReadItemAsync<ToDoActivity>(testItem.id, partitionKey: new Cosmos.PartitionKey(testItem.pk));
            Assert.AreEqual(HttpStatusCode.OK, itemResponse.StatusCode);
            Assert.IsNotNull(itemResponse.Resource);
            Assert.AreEqual("patched", itemResponse.Resource.children[1].pk);
            Assert.IsNull(itemResponse.Resource.description);
            Assert.AreEqual(14, itemResponse.Resource.taskNum);

            // Delete
            using (ResponseMessage deleteResponse = await this.Container.DeleteItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.pk), id: testItem.id))
            {
                Assert.IsNotNull(deleteResponse);
                Assert.AreEqual(deleteResponse.StatusCode, HttpStatusCode.NoContent);
            }
        }

        [TestMethod]
        public async Task PatchConditionTest()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ContainerInternal containerInternal = (ContainerInternal)this.Container;

            using (Stream stream = TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem))
            {
                // Create the item
                using (ResponseMessage response = await this.Container.CreateItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.pk), streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                }
            }

            List<PatchOperation> patchOperations = new List<PatchOperation>()
            {
                PatchOperation.Add("/children/1/pk", "patched"),
                PatchOperation.Remove("/description"),
                PatchOperation.Add("/taskNum", 8)
            };

            // Patch
            using (ResponseMessage response = await containerInternal.PatchItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.pk), id: testItem.id, patchOperations: patchOperations))
            {
                Assert.IsNotNull(response);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }

            List<PatchOperation> patchOperationsUpdateTaskNum12 = new List<PatchOperation>()
            {
                PatchOperation.Replace("/taskNum", 12)
            };

            PatchItemRequestOptions requestOptionsFalse = new PatchItemRequestOptions()
            {
                FilterPredicate = "from c where c.taskNum = 3"
            };

            // Patch that fails due to condition not met.
            using (ResponseMessage response = await containerInternal.PatchItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.pk), id: testItem.id, patchOperations: patchOperationsUpdateTaskNum12, requestOptions: requestOptionsFalse))
            {
                Assert.IsNotNull(response);
                Assert.AreEqual(HttpStatusCode.PreconditionFailed, response.StatusCode);
            }

            {
                // Read and validate
                ItemResponse<ToDoActivity> itemResponsemid = await this.Container.ReadItemAsync<ToDoActivity>(testItem.id, partitionKey: new Cosmos.PartitionKey(testItem.pk));
                Assert.AreEqual(HttpStatusCode.OK, itemResponsemid.StatusCode);
                Assert.IsNotNull(itemResponsemid.Resource);
                Assert.AreEqual("patched", itemResponsemid.Resource.children[1].pk);
                Assert.IsNull(itemResponsemid.Resource.description);
                Assert.AreEqual(8, itemResponsemid.Resource.taskNum);
            }

            List<PatchOperation> patchOperationsUpdateTaskNum14 = new List<PatchOperation>()
            {
                PatchOperation.Increment("/taskNum", 6)
            };

            PatchItemRequestOptions requestOptionsTrue = new PatchItemRequestOptions()
            {
                FilterPredicate = "from root where root.taskNum = 8"
            };

            // Patch
            using (ResponseMessage response = await containerInternal.PatchItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.pk), id: testItem.id, patchOperations: patchOperationsUpdateTaskNum14, requestOptions: requestOptionsTrue))
            {
                Assert.IsNotNull(response);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            }

            // Read and validate
            ItemResponse<ToDoActivity> itemResponse = await this.Container.ReadItemAsync<ToDoActivity>(testItem.id, partitionKey: new Cosmos.PartitionKey(testItem.pk));
            Assert.AreEqual(HttpStatusCode.OK, itemResponse.StatusCode);
            Assert.IsNotNull(itemResponse.Resource);
            Assert.AreEqual("patched", itemResponse.Resource.children[1].pk);
            Assert.IsNull(itemResponse.Resource.description);
            Assert.AreEqual(14, itemResponse.Resource.taskNum);

            // Delete
            using (ResponseMessage deleteResponse = await this.Container.DeleteItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.pk), id: testItem.id))
            {
                Assert.IsNotNull(deleteResponse);
                Assert.AreEqual(deleteResponse.StatusCode, HttpStatusCode.NoContent);
            }
        }

        [TestMethod]
        public async Task ItemPatchViaGatewayTest()
        {
            CosmosClient gatewayClient = TestCommon.CreateCosmosClient(useGateway: true);
            Container gatewayContainer = gatewayClient.GetContainer(this.database.Id, this.Container.Id);
            ContainerInternal containerInternal = (ContainerInternal)gatewayContainer;

            // Create an item
            ToDoActivity testItem = (await ToDoActivity.CreateRandomItems(gatewayContainer, 1, randomPartitionKey: true)).First();

            int originalTaskNum = testItem.taskNum;
            int newTaskNum = originalTaskNum + 1;

            Assert.IsNull(testItem.children[1].pk);

            List<PatchOperation> patchOperations = new List<PatchOperation>()
            {
                PatchOperation.Add("/children/1/pk", "patched"),
                PatchOperation.Remove("/description"),
                PatchOperation.Replace("/taskNum", newTaskNum)
            };

            ItemResponse<ToDoActivity> response = await containerInternal.PatchItemAsync<ToDoActivity>(
                id: testItem.id,
                partitionKey: new Cosmos.PartitionKey(testItem.pk),
                patchOperations: patchOperations);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsNotNull(response.Resource);
            Assert.AreEqual("patched", response.Resource.children[1].pk);
            Assert.IsNull(response.Resource.description);
            Assert.AreEqual(newTaskNum, response.Resource.taskNum);
        }

        [TestMethod]
        public async Task ItemPatchCustomSerializerTest()
        {
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                Serializer = new CosmosJsonDotNetSerializer(
                    new JsonSerializerSettings()
                    {
                        DateFormatString = "dd / MM / yy hh:mm"
                    })
            };

            CosmosClient customSerializationClient = TestCommon.CreateCosmosClient(clientOptions);
            Container customSerializationContainer = customSerializationClient.GetContainer(this.database.Id, this.Container.Id);
            ContainerInternal containerInternal = (ContainerInternal)customSerializationContainer;

            ToDoActivity testItem = (await ToDoActivity.CreateRandomItems(customSerializationContainer, 1, randomPartitionKey: true)).First();

            PatchItemRequestOptions requestOptions = new PatchItemRequestOptions()
            {
                EnableContentResponseOnWrite = false
            };

            DateTime patchDate = new DateTime(2020, 07, 01, 01, 02, 03);
            Stream patchDateStreamInput = new CosmosJsonDotNetSerializer().ToStream(patchDate);
            string streamDateJson;
            using (Stream stream = new MemoryStream())
            {
                patchDateStreamInput.CopyTo(stream);
                stream.Position = 0;
                patchDateStreamInput.Position = 0;
                using (StreamReader streamReader = new StreamReader(stream))
                {
                    streamDateJson = streamReader.ReadToEnd();
                }
            }

            List<PatchOperation> patchOperations = new List<PatchOperation>()
            {
                PatchOperation.Add("/date", patchDate),
                PatchOperation.Add("/dateStream", patchDateStreamInput)
            };

            ItemResponse<dynamic> response = await containerInternal.PatchItemAsync<dynamic>(
                id: testItem.id,
                partitionKey: new Cosmos.PartitionKey(testItem.pk),
                patchOperations: patchOperations,
                requestOptions);

            JsonSerializerSettings jsonSettings = new JsonSerializerSettings();
            jsonSettings.DateFormatString = "dd / MM / yy hh:mm";
            string dateJson = JsonConvert.SerializeObject(patchDate, jsonSettings);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            // regular container
            response = await this.Container.ReadItemAsync<dynamic>(
                testItem.id,
                partitionKey: new Cosmos.PartitionKey(testItem.pk));

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsNotNull(response.Resource);
            Assert.IsTrue(dateJson.Contains(response.Resource["date"].ToString()));
            Assert.AreEqual(patchDate.ToString(), response.Resource["dateStream"].ToString());
            Assert.AreNotEqual(response.Resource["date"], response.Resource["dateStream"]);
        }

        [TestMethod]
        public async Task ItemPatchStreamInputTest()
        {
            dynamic testItem = new
            {
                id = "test",
                cost = (double?)null,
                totalCost = 98.2789,
                pk = "MyCustomStatus",
                taskNum = 4909,
                itemIds = new int[] { 1, 5, 10 },
                itemCode = new byte?[5] { 0x16, (byte)'\0', 0x3, null, (byte)'}' },
            };

            // Create item
            await this.Container.CreateItemAsync<dynamic>(item: testItem);
            ContainerInternal containerInternal = (ContainerInternal)this.Container;

            dynamic testItemUpdated = new
            {
                cost = 100,
                totalCost = 198.2789,
                taskNum = 4910,
                itemCode = new byte?[3] { 0x14, (byte)'\0', (byte)'{' }
            };

            CosmosJsonDotNetSerializer cosmosJsonDotNetSerializer = new CosmosJsonDotNetSerializer();

            List<PatchOperation> patchOperations = new List<PatchOperation>()
            {
                PatchOperation.Replace("/cost", cosmosJsonDotNetSerializer.ToStream(testItemUpdated.cost)),
                PatchOperation.Replace("/totalCost", cosmosJsonDotNetSerializer.ToStream(testItemUpdated.totalCost)),
                PatchOperation.Replace("/taskNum", cosmosJsonDotNetSerializer.ToStream(testItemUpdated.taskNum)),
                PatchOperation.Replace("/itemCode", cosmosJsonDotNetSerializer.ToStream(testItemUpdated.itemCode)),
            };

            ItemResponse<dynamic> response = await containerInternal.PatchItemAsync<dynamic>(
                id: testItem.id,
                partitionKey: new Cosmos.PartitionKey(testItem.pk),
                patchOperations: patchOperations);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsNotNull(response.Resource);

            Assert.AreEqual(testItemUpdated.cost.ToString(), response.Resource.cost.ToString());
            Assert.AreEqual(testItemUpdated.totalCost.ToString(), response.Resource.totalCost.ToString());
            Assert.AreEqual(testItemUpdated.taskNum.ToString(), response.Resource.taskNum.ToString());
            Assert.AreEqual(testItemUpdated.itemCode[0].ToString(), response.Resource.itemCode[0].ToString());
            Assert.AreEqual(testItemUpdated.itemCode[1].ToString(), response.Resource.itemCode[1].ToString());
            Assert.AreEqual(testItemUpdated.itemCode[2].ToString(), response.Resource.itemCode[2].ToString());
        }

        // Read write non partition Container item.
        [TestMethod]
        public async Task ReadNonPartitionItemAsync()
        {
            ContainerInternal fixedContainer = null;
            try
            {
                fixedContainer = await NonPartitionedContainerHelper.CreateNonPartitionedContainer(
                    this.database,
                    "ReadNonPartition" + Guid.NewGuid());

                await NonPartitionedContainerHelper.CreateItemInNonPartitionedContainer(fixedContainer, nonPartitionItemId);
                await NonPartitionedContainerHelper.CreateUndefinedPartitionItem((ContainerInlineCore)this.Container, undefinedPartitionItemId);

                ContainerResponse containerResponse = await fixedContainer.ReadContainerAsync();
                Assert.IsTrue(containerResponse.Resource.PartitionKey.Paths.Count > 0);
                Assert.AreEqual(PartitionKey.SystemKeyPath, containerResponse.Resource.PartitionKey.Paths[0]);

                //Reading item from fixed container with CosmosContainerSettings.NonePartitionKeyValue.
                ItemResponse<ToDoActivity> response = await fixedContainer.ReadItemAsync<ToDoActivity>(
                    partitionKey: Cosmos.PartitionKey.None,
                    id: nonPartitionItemId);

                Assert.IsNotNull(response.Resource);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual(nonPartitionItemId, response.Resource.id);

                //Adding item to fixed container with CosmosContainerSettings.NonePartitionKeyValue.
                ToDoActivity itemWithoutPK = ToDoActivity.CreateRandomToDoActivity();
                ItemResponse<ToDoActivity> createResponseWithoutPk = await fixedContainer.CreateItemAsync<ToDoActivity>(
                 item: itemWithoutPK,
                 partitionKey: Cosmos.PartitionKey.None);

                Assert.IsNotNull(createResponseWithoutPk.Resource);
                Assert.AreEqual(HttpStatusCode.Created, createResponseWithoutPk.StatusCode);
                Assert.AreEqual(itemWithoutPK.id, createResponseWithoutPk.Resource.id);

                //Updating item on fixed container with CosmosContainerSettings.NonePartitionKeyValue.
                itemWithoutPK.pk = "updatedStatus";
                ItemResponse<ToDoActivity> updateResponseWithoutPk = await fixedContainer.ReplaceItemAsync<ToDoActivity>(
                 id: itemWithoutPK.id,
                 item: itemWithoutPK,
                 partitionKey: Cosmos.PartitionKey.None);

                Assert.IsNotNull(updateResponseWithoutPk.Resource);
                Assert.AreEqual(HttpStatusCode.OK, updateResponseWithoutPk.StatusCode);
                Assert.AreEqual(itemWithoutPK.id, updateResponseWithoutPk.Resource.id);

                //Adding item to fixed container with non-none PK.
                ToDoActivityAfterMigration itemWithPK = this.CreateRandomToDoActivityAfterMigration("TestPk");
                ItemResponse<ToDoActivityAfterMigration> createResponseWithPk = await fixedContainer.CreateItemAsync<ToDoActivityAfterMigration>(
                 item: itemWithPK);

                Assert.IsNotNull(createResponseWithPk.Resource);
                Assert.AreEqual(HttpStatusCode.Created, createResponseWithPk.StatusCode);
                Assert.AreEqual(itemWithPK.id, createResponseWithPk.Resource.id);

                //Quering items on fixed container with cross partition enabled.
                QueryDefinition sql = new QueryDefinition("select * from r");
                using (FeedIterator<dynamic> feedIterator = fixedContainer.GetItemQueryIterator<dynamic>(
                    sql,
                    requestOptions: new QueryRequestOptions() { MaxConcurrency = 1, MaxItemCount = 10 }))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        FeedResponse<dynamic> queryResponse = await feedIterator.ReadNextAsync();
                        Assert.AreEqual(3, queryResponse.Count());
                    }
                }

                //Reading all items on fixed container.
                using (FeedIterator<dynamic> feedIterator = fixedContainer.GetItemQueryIterator<dynamic>(requestOptions: new QueryRequestOptions() { MaxItemCount = 10 }))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        FeedResponse<dynamic> queryResponse = await feedIterator.ReadNextAsync();
                        Assert.AreEqual(3, queryResponse.Count());
                    }
                }

                //Quering items on fixed container with CosmosContainerSettings.NonePartitionKeyValue.
                using (FeedIterator<dynamic> feedIterator = fixedContainer.GetItemQueryIterator<dynamic>(
                    new QueryDefinition("select * from r"),
                    requestOptions: new QueryRequestOptions() { MaxItemCount = 10, PartitionKey = Cosmos.PartitionKey.None, }))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        FeedResponse<dynamic> queryResponse = await feedIterator.ReadNextAsync();
                        Assert.AreEqual(2, queryResponse.Count());
                    }
                }

                //use ReadFeed on fixed container with CosmosContainerSettings.NonePartitionKeyValue.
                using (FeedIterator<dynamic> feedIterator = fixedContainer.GetItemQueryIterator<dynamic>(
                    queryText: null,
                    requestOptions: new QueryRequestOptions() { MaxItemCount = 10, PartitionKey = Cosmos.PartitionKey.None, }))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        FeedResponse<dynamic> readFeedResponse = await feedIterator.ReadNextAsync();
                        Assert.AreEqual(2, readFeedResponse.Count());
                    }
                }

                //Quering items on fixed container with non-none PK.
                using (FeedIterator<dynamic> feedIterator = fixedContainer.GetItemQueryIterator<dynamic>(
                    sql,
                    requestOptions: new QueryRequestOptions() { MaxItemCount = 10, PartitionKey = new Cosmos.PartitionKey(itemWithPK.partitionKey) }))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        FeedResponse<dynamic> queryResponse = await feedIterator.ReadNextAsync();
                        Assert.AreEqual(1, queryResponse.Count());
                    }
                }

                //ReadFeed on on fixed container with non-none PK.
                using (FeedIterator<dynamic> feedIterator = fixedContainer.GetItemQueryIterator<dynamic>(
                    queryText: null,
                    requestOptions: new QueryRequestOptions() { MaxItemCount = 10, PartitionKey = new Cosmos.PartitionKey(itemWithPK.partitionKey) }))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        FeedResponse<dynamic> readFeedResponse = await feedIterator.ReadNextAsync();
                        Assert.AreEqual(1, readFeedResponse.Count());
                    }
                }

                //Deleting item from fixed container with CosmosContainerSettings.NonePartitionKeyValue.
                ItemResponse<ToDoActivity> deleteResponseWithoutPk = await fixedContainer.DeleteItemAsync<ToDoActivity>(
                 partitionKey: Cosmos.PartitionKey.None,
                 id: itemWithoutPK.id);

                Assert.IsNull(deleteResponseWithoutPk.Resource);
                Assert.AreEqual(HttpStatusCode.NoContent, deleteResponseWithoutPk.StatusCode);

                //Deleting item from fixed container with non-none PK.
                ItemResponse<ToDoActivityAfterMigration> deleteResponseWithPk = await fixedContainer.DeleteItemAsync<ToDoActivityAfterMigration>(
                 partitionKey: new Cosmos.PartitionKey(itemWithPK.partitionKey),
                 id: itemWithPK.id);

                Assert.IsNull(deleteResponseWithPk.Resource);
                Assert.AreEqual(HttpStatusCode.NoContent, deleteResponseWithPk.StatusCode);

                //Reading item from partitioned container with CosmosContainerSettings.NonePartitionKeyValue.
                ItemResponse<ToDoActivity> undefinedItemResponse = await this.Container.ReadItemAsync<ToDoActivity>(
                    partitionKey: Cosmos.PartitionKey.None,
                    id: undefinedPartitionItemId);

                Assert.IsNotNull(undefinedItemResponse.Resource);
                Assert.AreEqual(HttpStatusCode.OK, undefinedItemResponse.StatusCode);
                Assert.AreEqual(undefinedPartitionItemId, undefinedItemResponse.Resource.id);
            }
            finally
            {
                if (fixedContainer != null)
                {
                    await fixedContainer.DeleteContainerStreamAsync();
                }
            }
        }

        // Move the data from None Partition to other logical partitions
        [TestMethod]
        public async Task MigrateDataInNonPartitionContainer()
        {
            ContainerInternal fixedContainer = null;
            try
            {
                fixedContainer = await NonPartitionedContainerHelper.CreateNonPartitionedContainer(
                    this.database,
                    "ItemTestMigrateData" + Guid.NewGuid().ToString());

                const int ItemsToCreate = 4;
                // Insert a few items with no Partition Key
                for (int i = 0; i < ItemsToCreate; i++)
                {
                    await NonPartitionedContainerHelper.CreateItemInNonPartitionedContainer(fixedContainer, Guid.NewGuid().ToString());
                }

                // Read the container metadata
                ContainerResponse containerResponse = await fixedContainer.ReadContainerAsync();

                // Query items on the container that have no partition key value
                int resultsFetched = 0;
                QueryDefinition sql = new QueryDefinition("select * from r ");
                FeedIterator<ToDoActivity> setIterator = fixedContainer.GetItemQueryIterator<ToDoActivity>(
                    sql,
                    requestOptions: new QueryRequestOptions() { MaxItemCount = 2, PartitionKey = Cosmos.PartitionKey.None, });

                while (setIterator.HasMoreResults)
                {
                    FeedResponse<ToDoActivity> queryResponse = await setIterator.ReadNextAsync();
                    resultsFetched += queryResponse.Count();

                    // For the items returned with NonePartitionKeyValue
                    IEnumerator<ToDoActivity> iter = queryResponse.GetEnumerator();
                    while (iter.MoveNext())
                    {
                        ToDoActivity activity = iter.Current;

                        // Re-Insert into container with a partition key
                        ToDoActivityAfterMigration itemWithPK = new ToDoActivityAfterMigration
                        { id = activity.id, cost = activity.cost, description = activity.description, partitionKey = "TestPK", taskNum = activity.taskNum };
                        ItemResponse<ToDoActivityAfterMigration> createResponseWithPk = await fixedContainer.CreateItemAsync<ToDoActivityAfterMigration>(
                         item: itemWithPK);
                        Assert.AreEqual(HttpStatusCode.Created, createResponseWithPk.StatusCode);

                        // Deleting item from fixed container with CosmosContainerSettings.NonePartitionKeyValue.
                        ItemResponse<ToDoActivity> deleteResponseWithoutPk = await fixedContainer.DeleteItemAsync<ToDoActivity>(
                         partitionKey: Cosmos.PartitionKey.None,
                         id: activity.id);
                        Assert.AreEqual(HttpStatusCode.NoContent, deleteResponseWithoutPk.StatusCode);
                    }
                }

                // Validate all items with no partition key value are returned
                Assert.AreEqual(ItemsToCreate, resultsFetched);

                // Re-Query the items on the container with NonePartitionKeyValue
                setIterator = fixedContainer.GetItemQueryIterator<ToDoActivity>(
                    sql,
                    requestOptions: new QueryRequestOptions() { MaxItemCount = ItemsToCreate, PartitionKey = Cosmos.PartitionKey.None, });

                Assert.IsTrue(setIterator.HasMoreResults);
                {
                    FeedResponse<ToDoActivity> queryResponse = await setIterator.ReadNextAsync();
                    Assert.AreEqual(0, queryResponse.Count());
                }

                // Query the items with newly inserted PartitionKey
                setIterator = fixedContainer.GetItemQueryIterator<ToDoActivity>(
                    sql,
                    requestOptions: new QueryRequestOptions() { MaxItemCount = ItemsToCreate + 1, PartitionKey = new Cosmos.PartitionKey("TestPK"), });

                Assert.IsTrue(setIterator.HasMoreResults);
                {
                    FeedResponse<ToDoActivity> queryResponse = await setIterator.ReadNextAsync();
                    Assert.AreEqual(ItemsToCreate, queryResponse.Count());
                }
            }
            finally
            {
                if (fixedContainer != null)
                {
                    await fixedContainer.DeleteContainerStreamAsync();
                }
            }
        }


        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        [TestCategory("Quarantine") /* Gated runs emulator as rate limiting disabled */]
        public async Task VerifyToManyRequestTest(bool isQuery)
        {
            using CosmosClient client = TestCommon.CreateCosmosClient();
            Cosmos.Database db = await client.CreateDatabaseIfNotExistsAsync("LoadTest");
            Container container = await db.CreateContainerIfNotExistsAsync("LoadContainer", "/pk");

            try
            {
                Task[] createItems = new Task[300];
                for (int i = 0; i < createItems.Length; i++)
                {
                    ToDoActivity temp = ToDoActivity.CreateRandomToDoActivity();
                    createItems[i] = container.CreateItemStreamAsync(
                        partitionKey: new Cosmos.PartitionKey(temp.pk),
                        streamPayload: TestCommon.SerializerCore.ToStream<ToDoActivity>(temp));
                }

                Task.WaitAll(createItems);

                List<Task> createQuery = new List<Task>(500);
                List<ResponseMessage> failedToManyRequests = new List<ResponseMessage>();
                for (int i = 0; i < 500 && failedToManyRequests.Count == 0; i++)
                {
                    createQuery.Add(VerifyQueryToManyExceptionAsync(
                        container,
                        isQuery,
                        failedToManyRequests));
                }

                Task[] tasks = createQuery.ToArray();
                Task.WaitAll(tasks);

                Assert.IsTrue(failedToManyRequests.Count > 0, "Rate limiting appears to be disabled");
                ResponseMessage failedResponseMessage = failedToManyRequests.First();
                Assert.AreEqual(failedResponseMessage.StatusCode, (HttpStatusCode)429);
                Assert.IsNotNull(failedResponseMessage.ErrorMessage);
                string diagnostics = failedResponseMessage.Diagnostics.ToString();
                Assert.IsNotNull(diagnostics);
            }
            finally
            {
                await db.DeleteStreamAsync();
            }
        }

        [TestMethod]
        public async Task VerifySessionTokenPassThrough()
        {
            ToDoActivity temp = ToDoActivity.CreateRandomToDoActivity("TBD");

            ItemResponse<ToDoActivity> responseAstype = await this.Container.CreateItemAsync<ToDoActivity>(partitionKey: new Cosmos.PartitionKey(temp.pk), item: temp);

            string sessionToken = responseAstype.Headers.Session;
            Assert.IsNotNull(sessionToken);

            ResponseMessage readResponse = await this.Container.ReadItemStreamAsync(temp.id, new Cosmos.PartitionKey(temp.pk), new ItemRequestOptions() { SessionToken = sessionToken });

            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
            Assert.IsNotNull(readResponse.Headers.Session);
            Assert.AreEqual(sessionToken, readResponse.Headers.Session);
        }

        [TestMethod]
        public async Task VerifySessionNotFoundStatistics()
        {
            using CosmosClient cosmosClient = TestCommon.CreateCosmosClient(new CosmosClientOptions() { ConsistencyLevel = Cosmos.ConsistencyLevel.Session });
            DatabaseResponse database = await cosmosClient.CreateDatabaseIfNotExistsAsync("NoSession");
            Container container = await database.Database.CreateContainerIfNotExistsAsync("NoSession", "/pk");

            try
            {
                ToDoActivity temp = ToDoActivity.CreateRandomToDoActivity("TBD");

                ItemResponse<ToDoActivity> responseAstype = await container.CreateItemAsync<ToDoActivity>(partitionKey: new Cosmos.PartitionKey(temp.pk), item: temp);

                string invalidSessionToken = this.GetDifferentLSNToken(responseAstype.Headers.Session, 2000);

                try
                {
                    ItemResponse<ToDoActivity> readResponse = await container.ReadItemAsync<ToDoActivity>(temp.id, new Cosmos.PartitionKey(temp.pk), new ItemRequestOptions() { SessionToken = invalidSessionToken });
                    Assert.Fail("Should had thrown ReadSessionNotAvailable");
                }
                catch (CosmosException cosmosException)
                {
                    Assert.IsTrue(cosmosException.Message.Contains("The read session is not available for the input session token."), cosmosException.Message);
                    string exception = cosmosException.ToString();
                    Assert.IsTrue(exception.Contains("Point Operation Statistics"), exception);
                }
            }
            finally
            {
                await database.Database.DeleteStreamAsync();
            }
        }

        private string GetDifferentLSNToken(string token, long lsnDifferent)
        {
            string[] tokenParts = token.Split(':');
            ISessionToken sessionToken = SessionTokenHelper.Parse(tokenParts[1]);
            ISessionToken differentSessionToken = TestCommon.CreateSessionToken(sessionToken, sessionToken.LSN + lsnDifferent);
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1}", tokenParts[0], differentSessionToken.ConvertToString());
        }

        /// <summary>
        /// Stateless container re-create test. 
        /// Create two client instances and do meta data operations through a single client
        /// but do all validation using both clients.
        /// </summary>
        [DataRow(true, true)]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(false, false)]
        [DataTestMethod]
        public async Task ContainterReCreateStatelessTest(bool operationBetweenRecreate, bool isQuery)
        {
            Func<Container, HttpStatusCode, Task> operation;
            if (isQuery)
            {
                operation = ExecuteQueryAsync;
            }
            else
            {
                operation = ExecuteReadFeedAsync;
            }

            using CosmosClient cc1 = TestCommon.CreateCosmosClient();
            using CosmosClient cc2 = TestCommon.CreateCosmosClient();
            Cosmos.Database db1 = null;
            try
            {
                string dbName = Guid.NewGuid().ToString();
                string containerName = Guid.NewGuid().ToString();

                db1 = await cc1.CreateDatabaseAsync(dbName);
                ContainerInternal container1 = (ContainerInlineCore)await db1.CreateContainerAsync(containerName, "/id");

                await operation(container1, HttpStatusCode.OK);

                // Read through client2 -> return 404
                Container container2 = cc2.GetDatabase(dbName).GetContainer(containerName);
                await operation(container2, HttpStatusCode.OK);

                // Delete container 
                await container1.DeleteContainerAsync();

                if (operationBetweenRecreate)
                {
                    // Read on deleted container through client1
                    await operation(container1, HttpStatusCode.NotFound);

                    // Read on deleted container through client2
                    await operation(container2, HttpStatusCode.NotFound);
                }

                // Re-create again 
                container1 = (ContainerInlineCore)await db1.CreateContainerAsync(containerName, "/id");

                // Read through client1
                await operation(container1, HttpStatusCode.OK);

                // Read through client2
                await operation(container2, HttpStatusCode.OK);
            }
            finally
            {
                await db1.DeleteStreamAsync();
                cc1.Dispose();
                cc2.Dispose();
            }
        }

        [TestMethod]
        public async Task NoAutoGenerateIdTest()
        {
            try
            {
                ToDoActivity t = new ToDoActivity
                {
                    pk = "AutoID"
                };
                ItemResponse<ToDoActivity> responseAstype = await this.Container.CreateItemAsync<ToDoActivity>(
                    partitionKey: new Cosmos.PartitionKey(t.pk), item: t);

                Assert.Fail("Unexpected ID auto-generation");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
            {
            }
        }

        [TestMethod]
        public async Task AutoGenerateIdPatternTest()
        {
            ToDoActivity itemWithoutId = new ToDoActivity
            {
                pk = "AutoID"
            };

            ToDoActivity createdItem = await this.AutoGenerateIdPatternTest<ToDoActivity>(
                new Cosmos.PartitionKey(itemWithoutId.pk), itemWithoutId);

            Assert.IsNotNull(createdItem.id);
            Assert.AreEqual(itemWithoutId.pk, createdItem.pk);
        }

        [TestMethod]
        [DataRow(false, DisplayName = "Test scenario when binary encoding is disabled at client level.")]
        [DataRow(true, DisplayName = "Test scenario when binary encoding is enabled at client level.")]
        public async Task CustomPropertiesItemRequestOptionsTest(bool binaryEncodingEnabledInClient)
        {
            try
            {
                if (binaryEncodingEnabledInClient)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");
                }

                string customHeaderName = "custom-header1";
                string customHeaderValue = "value1";

                CosmosClient clientWithIntercepter = TestCommon.CreateCosmosClient(
                   builder => builder.WithTransportClientHandlerFactory(transportClient => new TransportClientHelper.TransportClientWrapper(
                    transportClient,
                    (uri, resourceOperation, request) =>
                    {
                        if (resourceOperation.resourceType == ResourceType.Document &&
                         resourceOperation.operationType == OperationType.Create)
                        {
                            bool customHeaderExists = request.Properties.TryGetValue(customHeaderName, out object value);

                            Assert.IsTrue(customHeaderExists);
                            Assert.AreEqual(customHeaderValue, value);
                        }
                    })));

                Container container = clientWithIntercepter.GetContainer(this.database.Id, this.Container.Id);

                ToDoActivity temp = ToDoActivity.CreateRandomToDoActivity("TBD");

                Dictionary<string, object> properties = new Dictionary<string, object>()
            {
                { customHeaderName, customHeaderValue},
            };

                ItemRequestOptions ro = new ItemRequestOptions
                {
                    Properties = properties
                };

                ItemResponse<ToDoActivity> responseAstype = await container.CreateItemAsync<ToDoActivity>(
                    partitionKey: new Cosmos.PartitionKey(temp.pk),
                    item: temp,
                    requestOptions: ro);

                Assert.AreEqual(HttpStatusCode.Created, responseAstype.StatusCode);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, null);
            }
        }

        [TestMethod]
        [DataRow(false, DisplayName = "Test scenario when binary encoding is disabled at client level.")]
        [DataRow(true, DisplayName = "Test scenario when binary encoding is enabled at client level.")]
        public async Task RegionsContactedTest(bool binaryEncodingEnabledInClient)
        {
            try
            {
                if (binaryEncodingEnabledInClient)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");
                }

                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                ItemResponse<ToDoActivity> response = await this.Container.CreateItemAsync<ToDoActivity>(item, new Cosmos.PartitionKey(item.pk));
                Assert.IsNotNull(response.Diagnostics);
                IReadOnlyList<(string region, Uri uri)> regionsContacted = response.Diagnostics.GetContactedRegions();
                Assert.AreEqual(regionsContacted.Count, 1);
                Assert.AreEqual(regionsContacted[0].region, Regions.SouthCentralUS);
                Assert.IsNotNull(regionsContacted[0].uri);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, null);
            }
        }

        [TestMethod]
        public async Task HaLayerDoesNotThrowNullOnGoneExceptionTest()
        {
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                TransportClientHandlerFactory = (x) =>
                    new TransportClientWrapper(client: x, interceptor: (uri, resource, dsr) =>
                    {
                        dsr.RequestContext.ClientRequestStatistics.GetType().GetField("systemUsageHistory", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(
                            dsr.RequestContext.ClientRequestStatistics,
                            new Documents.Rntbd.SystemUsageHistory(new List<Documents.Rntbd.SystemUsageLoad>()
                            {
                                new Documents.Rntbd.SystemUsageLoad(
                                    DateTime.UtcNow,
                                    Documents.Rntbd.ThreadInformation.Get(),
                                    80,
                                    9000),
                                new Documents.Rntbd.SystemUsageLoad(
                                    DateTime.UtcNow - TimeSpan.FromSeconds(10),
                                    Documents.Rntbd.ThreadInformation.Get(),
                                    95,
                                    9000)
                            }.AsReadOnly(),
                            TimeSpan.FromMinutes(1)));
                        if (resource.operationType.IsReadOperation())
                        {
                            throw Documents.Rntbd.TransportExceptions.GetGoneException(
                                uri,
                                Guid.NewGuid());
                        }
                    })
            };

            CosmosClient cosmosClient = TestCommon.CreateCosmosClient(clientOptions);
            Container container = cosmosClient.GetContainer(this.database.Id, this.Container.Id);
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            await container.CreateItemAsync<ToDoActivity>(testItem);

            try
            {
                await container.ReadItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.pk));
            }
            catch (CosmosException ex)
            {
                Assert.AreEqual(ex.StatusCode, HttpStatusCode.ServiceUnavailable);
                CosmosTraceDiagnostics diagnostics = (CosmosTraceDiagnostics)ex.Diagnostics;
                Assert.IsTrue(diagnostics.IsGoneExceptionHit());
                string diagnosticString = diagnostics.ToString();
                Assert.IsFalse(string.IsNullOrEmpty(diagnosticString));
                Assert.IsTrue(diagnosticString.Contains("ForceAddressRefresh"));
                Assert.IsTrue(diagnosticString.Contains("No change to cache"));
                Assert.AreNotEqual(0, diagnostics.GetFailedRequestCount());
            }
        }

        /// <summary>
        /// <see href="https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4115"/>
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [Owner("philipthomas")]
        [Description("ReadItemStreamAsync is yielding a Newtonsoft.Json.JsonSerializationException whenever " +
            "the item is not found and the MissingMemberHandling is set to MissingMemberHandling.Error. " +
            "ReadItemStreamAsync should yield a CosmosException with a NotFound StatusCode.")]
        public async Task GivenReadItemStreamAsyncWhenMissingMemberHandlingIsErrorThenExpectsCosmosExceptionTestAsync()
        {
            await CosmosItemTests.GivenItemStreamAsyncWhenMissingMemberHandlingIsErrorThenExpectsCosmosExceptionTestAsync(
                itemStreamAsync: async (container, itemIdThatWillNotExist, partitionKeyValue, cancellationToken) => await container.ReadItemStreamAsync(
                        id: itemIdThatWillNotExist,
                        partitionKey: new Cosmos.PartitionKey(partitionKeyValue),
                        cancellationToken: cancellationToken));
        }

        /// <summary>
        /// <see href="https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4115"/>
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [Owner("philipthomas")]
        [Description("ReadItemAsync is yielding a Newtonsoft.Json.JsonSerializationException whenever " +
            "the item is not found and MissingMemberHandling is set to MissingMemberHandling.Error. " +
            "ReadItemAsync should yield a CosmosException with a NotFound StatusCode.")]
        public async Task GivenReadItemAsyncWhenMissingMemberHandlingIsErrorThenExpectsCosmosExceptionTestAsync()
        {
            await CosmosItemTests.GivenItemAsyncWhenMissingMemberHandlingIsErrorThenExpectsCosmosExceptionTestAsync(
                itemAsync: async (container, itemIdThatWillNotExist, partitionKeyValue, toDoActivity, cancellationToken) => await container.ReadItemAsync<ToDoActivity>(
                    id: itemIdThatWillNotExist,
                    partitionKey: new Cosmos.PartitionKey(partitionKeyValue),
                    cancellationToken: cancellationToken));
        }

        /// <summary>
        /// <see href="https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4115"/>
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [Owner("philipthomas")]
        [Description("DeleteItemStreamAsync is yielding a Newtonsoft.Json.JsonSerializationException whenever " +
            "the item is not found and the MissingMemberHandling is set to MissingMemberHandling.Error. " +
            "DeleteItemStreamAsync should yield a CosmosException with a NotFound StatusCode.")]
        public async Task GivenDeleteItemStreamAsyncWhenMissingMemberHandlingIsErrorThenExpectsCosmosExceptionTestAsync()
        {
            await CosmosItemTests.GivenItemStreamAsyncWhenMissingMemberHandlingIsErrorThenExpectsCosmosExceptionTestAsync(
                itemStreamAsync: async (container, itemIdThatWillNotExist, partitionKeyValue, cancellationToken) => await container.DeleteItemStreamAsync(
                    id: itemIdThatWillNotExist,
                    partitionKey: new Cosmos.PartitionKey(partitionKeyValue),
                    cancellationToken: cancellationToken));
        }

        /// <summary>
        /// <see href="https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4115"/>
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [Owner("philipthomas")]
        [Description("DeleteItemAsync is yielding a Newtonsoft.Json.JsonSerializationException whenever " +
            "the item is not found and MissingMemberHandling is set to MissingMemberHandling.Error. " +
            "DeleteItemAsync should yield a CosmosException with a NotFound StatusCode.")]
        public async Task GivenDeleteItemAsyncWhenMissingMemberHandlingIsErrorThenExpectsCosmosExceptionTestAsync()
        {
            await CosmosItemTests.GivenItemAsyncWhenMissingMemberHandlingIsErrorThenExpectsCosmosExceptionTestAsync(
                itemAsync: async (container, itemIdThatWillNotExist, partitionKeyValue, toDoActivity, cancellationToken) => await container.DeleteItemAsync<ToDoActivity>(
                    id: itemIdThatWillNotExist,
                    partitionKey: new Cosmos.PartitionKey(partitionKeyValue),
                    cancellationToken: cancellationToken));
        }

        /// <summary>
        /// <see href="https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4115"/>
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [Owner("philipthomas")]
        [Description("DeleteItemStreamAsync is yielding a Newtonsoft.Json.JsonSerializationException whenever " +
            "the item is not found and the MissingMemberHandling is set to MissingMemberHandling.Error. " +
            "DeleteItemStreamAsync should yield a CosmosException with a NotFound StatusCode.")]
        public async Task GivenReplaceItemStreamAsyncWhenMissingMemberHandlingIsErrorThenExpectsCosmosExceptionTestAsync()
        {
            await CosmosItemTests.GivenItemStreamAsyncWhenMissingMemberHandlingIsErrorThenExpectsCosmosExceptionTestAsync(
                itemStreamAsync: async (container, itemIdThatWillNotExist, partitionKeyValue, cancellationToken) => await container.ReplaceItemStreamAsync(
                    streamPayload: new MemoryStream(),
                    id: itemIdThatWillNotExist,
                    partitionKey: new Cosmos.PartitionKey(partitionKeyValue),
                    cancellationToken: cancellationToken));
        }

        /// <summary>
        /// <see href="https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4115"/>
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [Owner("philipthomas")]
        [Description("ReplaceItemAsync is yielding a Newtonsoft.Json.JsonSerializationException whenever " +
            "the item is not found and MissingMemberHandling is set to MissingMemberHandling.Error. " +
            "ReplaceItemAsync should yield a CosmosException with a NotFound StatusCode.")]
        public async Task GivenReplaceItemAsyncWhenMissingMemberHandlingIsErrorThenExpectsCosmosExceptionTestAsync()
        {
            await CosmosItemTests.GivenItemAsyncWhenMissingMemberHandlingIsErrorThenExpectsCosmosExceptionTestAsync(
                itemAsync: async (container, itemIdThatWillNotExist, partitionKeyValue, toDoActivity, cancellationToken) => await container.ReplaceItemAsync(
                    item: toDoActivity,
                    id: itemIdThatWillNotExist,
                    partitionKey: new Cosmos.PartitionKey(partitionKeyValue),
                    cancellationToken: cancellationToken));
        }

        /// <summary>
        /// <see href="https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4115"/>
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [Owner("philipthomas")]
        [Description("PatchItemStreamAsync is yielding a Newtonsoft.Json.JsonSerializationException whenever " +
            "the item is not found and the MissingMemberHandling is set to MissingMemberHandling.Error. " +
            "PatchItemStreamAsync should yield a CosmosException with a NotFound StatusCode.")]
        public async Task GivenPatchItemStreamAsyncWhenMissingMemberHandlingIsErrorThenExpectsCosmosExceptionTestAsync()
        {
            List<PatchOperation> patchOperations = new()
            {
                PatchOperation.Add("/children/1/pk", "patched"),
                PatchOperation.Remove("/description"),
                PatchOperation.Replace("/taskNum", 1)
            };

            await CosmosItemTests.GivenItemStreamAsyncWhenMissingMemberHandlingIsErrorThenExpectsCosmosExceptionTestAsync(
                itemStreamAsync: async (container, itemIdThatWillNotExist, partitionKeyValue, cancellationToken) => await container.PatchItemStreamAsync(
                    patchOperations: patchOperations,
                    id: itemIdThatWillNotExist,
                    partitionKey: new Cosmos.PartitionKey(partitionKeyValue),
                    cancellationToken: cancellationToken));
        }

        /// <summary>
        /// <see href="https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4115"/>
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [Owner("philipthomas")]
        [Description("PatchItemAsync is yielding a Newtonsoft.Json.JsonSerializationException whenever " +
            "the item is not found and MissingMemberHandling is set to MissingMemberHandling.Error. " +
            "PatchItemAsync should yield a CosmosException with a NotFound StatusCode.")]
        public async Task GivenPatchItemAsyncWhenMissingMemberHandlingIsErrorThenExpectsCosmosExceptionTestAsync()
        {
            List<PatchOperation> patchOperations = new()
            {
                PatchOperation.Add("/children/1/pk", "patched"),
                PatchOperation.Remove("/description"),
                PatchOperation.Replace("/taskNum", 1)
            };

            await CosmosItemTests.GivenItemAsyncWhenMissingMemberHandlingIsErrorThenExpectsCosmosExceptionTestAsync(
                itemAsync: async (container, itemIdThatWillNotExist, partitionKeyValue, toDoActivity, cancellationToken) => await container.PatchItemAsync<ToDoActivity>(
                    patchOperations: patchOperations,
                    id: itemIdThatWillNotExist,
                    partitionKey: new Cosmos.PartitionKey(partitionKeyValue),
                    cancellationToken: cancellationToken));
        }

        [TestMethod]
        public async Task MalformedChangeFeedContinuationTokenSubStatusCodeTest()
        {
            FeedIterator badIterator = this.Container.GetChangeFeedStreamIterator(
                    ChangeFeedStartFrom.ContinuationToken("AMalformedContinuationToken"),
                    ChangeFeedMode.Incremental,
                    new ChangeFeedRequestOptions()
                    {
                        PageSizeHint = 100
                    });

            ResponseMessage response = await badIterator.ReadNextAsync();

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual(SubStatusCodes.MalformedContinuationToken, response.Headers.SubStatusCode);
        }

        private static async Task GivenItemStreamAsyncWhenMissingMemberHandlingIsErrorThenExpectsCosmosExceptionTestAsync(
            Func<Container, string, string, CancellationToken, Task<ResponseMessage>> itemStreamAsync)
        {
            // AAA
            //     Arrange
            CancellationTokenSource cancellationTokenSource = new();
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            // Food for thought, actionable items.
            //
            // 1. Is there anything else that we should be concerned with that would give us the same behavior?
            // 2. Are there other operations other than those that can yield an NotFound exception that we should be
            //    concerned with?
            // 3. Can we also reset the DefaultSettings before we make the call, and reset it back once it is done?

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Error
            };

            CosmosClient cosmosClient = TestCommon.CreateCosmosClient();

            string databaseId = Guid.NewGuid().ToString();
            Cosmos.Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync(
                id: databaseId,
                cancellationToken: cancellationToken);

            try
            {
                string containerId = Guid.NewGuid().ToString();
                Container container = await database.CreateContainerIfNotExistsAsync(
                    containerProperties: new ContainerProperties
                    {
                        Id = containerId,
                        PartitionKeyPath = "/pk",
                    },
                    cancellationToken: cancellationToken);


                //     Act
                string itemIdThatWillNotExist = Guid.NewGuid().ToString();
                string partitionKeyValue = Guid.NewGuid().ToString();

                ResponseMessage response = await itemStreamAsync(container, itemIdThatWillNotExist, partitionKeyValue, cancellationToken);

                //     Assert
                Debug.Assert(
                    condition: response != null,
                    message: $"{response}");

                Assert.AreEqual(
                    expected: HttpStatusCode.NotFound,
                    actual: response.StatusCode);

                string content = JsonConvert.SerializeObject(response.Content);

                Assert.AreEqual(
                    expected: "null",
                    actual: content);

                string errorMessage = JsonConvert.SerializeObject(response.ErrorMessage);

                Assert.IsNotNull(value: errorMessage);

                Debug.Assert(
                    condition: response.CosmosException != null,
                    message: $"{response.CosmosException}");

                Assert.AreEqual(
                    expected: HttpStatusCode.NotFound,
                    actual: response.StatusCode);

                Debug.WriteLine(message: $"{nameof(response.CosmosException)}: {response.CosmosException}");

                Assert.AreEqual(
                    actual: response.CosmosException.StatusCode,
                    expected: HttpStatusCode.NotFound);
            }
            finally
            {
                if (database != null)
                {
                    // Remove the test database. Cleanup.
                    _ = await database.DeleteAsync(cancellationToken: cancellationToken);

                    Debug.WriteLine($"The {nameof(database)} with id '{databaseId}' was removed.");
                }

                // Setting this back because it blows up other serialization tests.

                JsonConvert.DefaultSettings = () => default;
            }
        }

        private static async Task GivenItemAsyncWhenMissingMemberHandlingIsErrorThenExpectsCosmosExceptionTestAsync(
            Func<Container, string, string, ToDoActivity, CancellationToken, Task<ItemResponse<ToDoActivity>>> itemAsync)
        {
            // AAA
            //     Arrange
            CancellationTokenSource cancellationTokenSource = new();
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            // Food for thought, actionable items.
            //
            // 1. Is there anything else that we should be concerned with that would give us the same behavior?
            // 2. Are there other operations other than those that can yield an NotFound exception that we should be
            //    concerned with?
            // 3. Can we also reset the DefaultSettings before we make the call, and reset it back once it is done?

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Error
            };

            CosmosClient cosmosClient = TestCommon.CreateCosmosClient();

            string databaseId = Guid.NewGuid().ToString();
            Cosmos.Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync(
                id: databaseId,
                cancellationToken: cancellationToken);

            try
            {
                string containerId = Guid.NewGuid().ToString();
                Container container = await database.CreateContainerIfNotExistsAsync(
                    containerProperties: new ContainerProperties
                    {
                        Id = containerId,
                        PartitionKeyPath = "/pk",
                    },
                    cancellationToken: cancellationToken);


                //     Act
                // If any thing other than a CosmosException is thrown, the call to ReadItemAsync below will fail.
                string itemIdThatWillNotExist = Guid.NewGuid().ToString();
                string partitionKeyValue = Guid.NewGuid().ToString();

                CosmosException cosmosException = await Assert.ThrowsExceptionAsync<CosmosException>(action:
                    async () => await itemAsync(container, itemIdThatWillNotExist, partitionKeyValue, new ToDoActivity { id = Guid.NewGuid().ToString(), pk = "Georgia" }, cancellationToken)) ;

                //     Assert
                Debug.Assert(
                    condition: cosmosException != null,
                    message: $"{cosmosException}");

                Debug.WriteLine(message: $"{nameof(cosmosException)}: {cosmosException}");

                Assert.AreEqual(
                    actual: cosmosException.StatusCode,
                    expected: HttpStatusCode.NotFound);
            }
            finally
            {
                if (database != null)
                {
                    // Remove the test database. Cleanup.
                    _ = await database.DeleteAsync(cancellationToken: cancellationToken);

                    Debug.WriteLine($"The {nameof(database)} with id '{databaseId}' was removed.");
                }

                // Setting this back because it blows up other serialization tests.

                JsonConvert.DefaultSettings = () => default;
            }
        }

        private async Task<T> AutoGenerateIdPatternTest<T>(Cosmos.PartitionKey pk, T itemWithoutId)
        {
            string autoId = Guid.NewGuid().ToString();

            JObject tmpJObject = JObject.FromObject(itemWithoutId);
            tmpJObject["id"] = autoId;

            ItemResponse<JObject> response = await this.Container.CreateItemAsync<JObject>(
                partitionKey: pk, item: tmpJObject);

            return response.Resource.ToObject<T>();
        }

        private static async Task VerifyQueryToManyExceptionAsync(
            Container container,
            bool isQuery,
            List<ResponseMessage> failedToManyMessages)
        {
            string queryText = null;
            if (isQuery)
            {
                queryText = "select * from r";
            }

            FeedIterator iterator = container.GetItemQueryStreamIterator(queryText);
            while (iterator.HasMoreResults && failedToManyMessages.Count == 0)
            {
                ResponseMessage response = await iterator.ReadNextAsync();
                if (response.StatusCode == (HttpStatusCode)429)
                {
                    failedToManyMessages.Add(response);
                    return;
                }
            }
        }

        private static void ValidateCosmosException(CosmosException exception)
        {
            if (exception.StatusCode == HttpStatusCode.RequestTimeout ||
                exception.StatusCode == HttpStatusCode.InternalServerError ||
                exception.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                Assert.IsTrue(exception.Message.Contains("Diagnostics"));
            }
            else
            {
                Assert.IsFalse(exception.Message.Contains("Diagnostics"));
            }

            string toString = exception.ToString();
            Assert.AreEqual(1, Regex.Matches(toString, "Client Configuration").Count, $"The Cosmos Diagnostics does not exists or multiple instance are in the ToString(). {toString}");
        }

        private static async Task ExecuteQueryAsync(Container container, HttpStatusCode expected)
        {
            FeedIterator iterator = container.GetItemQueryStreamIterator("select * from r");
            while (iterator.HasMoreResults)
            {
                ResponseMessage response = await iterator.ReadNextAsync();
                Assert.AreEqual(expected, response.StatusCode, $"ExecuteQueryAsync substatuscode: {response.Headers.SubStatusCode} ");
            }
        }

        private static async Task ExecuteReadFeedAsync(Container container, HttpStatusCode expected)
        {
            FeedIterator iterator = container.GetItemQueryStreamIterator();
            while (iterator.HasMoreResults)
            {
                ResponseMessage response = await iterator.ReadNextAsync();
                Assert.AreEqual(expected, response.StatusCode, $"ExecuteReadFeedAsync substatuscode: {response.Headers.SubStatusCode} ");
            }
        }

        public class ToDoActivityAfterMigration
        {
            public string id { get; set; }
            public int taskNum { get; set; }
            public double cost { get; set; }
            public string description { get; set; }
            [JsonProperty(PropertyName = "_partitionKey")]
            public string partitionKey { get; set; }
        }

        private ToDoActivityAfterMigration CreateRandomToDoActivityAfterMigration(string pk = null, string id = null)
        {
            if (string.IsNullOrEmpty(pk))
            {
                pk = "TBD" + Guid.NewGuid().ToString();
            }
            if (id == null)
            {
                id = Guid.NewGuid().ToString();
            }
            return new ToDoActivityAfterMigration()
            {
                id = id,
                description = "CreateRandomToDoActivity",
                partitionKey = pk,
                taskNum = 42,
                cost = double.MaxValue
            };
        }

        private static async Task TestNonePKForNonExistingContainer(Container container)
        {
            // Stream implementation should not throw
            ResponseMessage response = await container.ReadItemStreamAsync("id1", Cosmos.PartitionKey.None);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
            Assert.IsNotNull(response.Headers.ActivityId);
            Assert.IsNotNull(response.ErrorMessage);

            // For typed, it will throw 
            try
            {
                ItemResponse<string> typedResponse = await container.ReadItemAsync<string>("id1", Cosmos.PartitionKey.None);
                Assert.Fail("Should throw exception.");
            }
            catch (CosmosException ex)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
            }
        }

        private static void AssertOnResponseSerializationBinaryType(
            Stream inputStream)
        {
            if (inputStream != null)
            {
                MemoryStream binaryStream = new();
                inputStream.CopyTo(binaryStream);
                byte[] content = binaryStream.ToArray();
                inputStream.Position = 0;

                Assert.IsTrue(content.Length > 0);
                Assert.IsTrue(CosmosItemTests.IsBinaryFormat(content[0], JsonSerializationFormat.Binary));
            }
        }

        private static void AssertOnResponseSerializationTextType(
            Stream inputStream)
        {
            if (inputStream != null)
            {
                MemoryStream binaryStream = new();
                inputStream.CopyTo(binaryStream);
                byte[] content = binaryStream.ToArray();
                inputStream.Position = 0;

                Assert.IsTrue(content.Length > 0);
                Assert.IsTrue(CosmosItemTests.IsTextFormat(content[0], JsonSerializationFormat.Text));
            }
        }

        private static bool IsBinaryFormat(
            int firstByte,
            JsonSerializationFormat desiredFormat)
        {
            return desiredFormat == JsonSerializationFormat.Binary && firstByte == (int)JsonSerializationFormat.Binary;
        }

        private static bool IsTextFormat(
            int firstByte,
            JsonSerializationFormat desiredFormat)
        {
            return desiredFormat == JsonSerializationFormat.Text && firstByte < (int)JsonSerializationFormat.Binary;
        }
    }
}
