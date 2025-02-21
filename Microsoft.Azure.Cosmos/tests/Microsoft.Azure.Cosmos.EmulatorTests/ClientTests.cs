//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Security;
    using System.Reflection;
    using System.Runtime.Serialization.Json;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using Microsoft.Azure.Cosmos.FaultInjection;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Services.Management.Tests.LinqProviderTests;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class ClientTests
    {
        [TestMethod]
        public async Task ValidateExceptionOnInitTask()
        {
            int httpCallCount = 0;
            HttpClientHandlerHelper httpClientHandlerHelper = new HttpClientHandlerHelper()
            {
                RequestCallBack = (request, cancellToken) =>
                {
                    Interlocked.Increment(ref httpCallCount);
                    return null;
                }
            };

            using CosmosClient cosmosClient = new CosmosClient(
                accountEndpoint: "https://localhost:8081",
                authKeyOrResourceToken: Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())),
                clientOptions: new CosmosClientOptions()
                {
                    HttpClientFactory = () => new HttpClient(httpClientHandlerHelper),
                });

            CosmosException cosmosException1 = null;
            try
            {
                await cosmosClient.GetContainer("db", "c").ReadItemAsync<JObject>("Random", new Cosmos.PartitionKey("DoesNotExist"));
            }
            catch (CosmosException ex)
            {
                cosmosException1 = ex;
                Assert.IsTrue(httpCallCount > 0);
            }

            httpCallCount = 0;
            try
            {
                await cosmosClient.GetContainer("db", "c").ReadItemAsync<JObject>("Random2", new Cosmos.PartitionKey("DoesNotExist2"));
            }
            catch (CosmosException ex)
            {
                Assert.IsFalse(object.ReferenceEquals(ex, cosmosException1));
                Assert.IsTrue(httpCallCount > 0);
            }
        }

        [TestMethod]
        public async Task InitTaskThreadSafe()
        {
            int httpCallCount = 0;
            int metadataCallCount = 0;
            bool delayCallBack = true;

            var isInitializedField = typeof(VmMetadataApiHandler).GetField("isInitialized",
               BindingFlags.Static |
               BindingFlags.NonPublic);
            isInitializedField.SetValue(null, false);

            var azMetadataField = typeof(VmMetadataApiHandler).GetField("azMetadata",
               BindingFlags.Static |
               BindingFlags.NonPublic);
            azMetadataField.SetValue(null, null);

            HttpClientHandlerHelper httpClientHandlerHelper = new HttpClientHandlerHelper()
            {
                RequestCallBack = async (request, cancellToken) =>
                {
                    if(request.RequestUri.AbsoluteUri ==  VmMetadataApiHandler.vmMetadataEndpointUrl.AbsoluteUri)
                    {
                        Interlocked.Increment(ref metadataCallCount);
                    } 
                    else
                    {
                        Interlocked.Increment(ref httpCallCount);
                    }
                    
                    while (delayCallBack)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(100));
                    }

                    return null;
                }
            };

            using CosmosClient cosmosClient = new CosmosClient(
                accountEndpoint: "https://localhost:8081",
                authKeyOrResourceToken: Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())),
                clientOptions: new CosmosClientOptions()
                {
                    HttpClientFactory = () => new HttpClient(httpClientHandlerHelper),
                });

            List<Task> tasks = new List<Task>();

            Container container = cosmosClient.GetContainer("db", "c");

            for (int loop = 0; loop < 3; loop++)
            {
                for (int i = 0; i < 10; i++)
                {
                    tasks.Add(this.ReadNotFound(container));
                }

                ValueStopwatch sw = ValueStopwatch.StartNew();
                while(this.TaskStartedCount < 10 && sw.Elapsed.TotalSeconds < 2)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(50));
                }

                Assert.AreEqual(10, this.TaskStartedCount, "Tasks did not start");
                delayCallBack = false;

                await Task.WhenAll(tasks);

                Assert.AreEqual(1, metadataCallCount, "Only one call for VM Metadata call with be made");
                Assert.AreEqual(1, httpCallCount, "Only the first task should do the http call. All other should wait on the first task");

                // Reset counters and retry the client to verify a new http call is done for new requests
                tasks.Clear();
                delayCallBack = true;
                this.TaskStartedCount = 0;
                httpCallCount = 0;
            }
        }


        [TestMethod]
        public async Task ValidateAzureKeyCredentialDirectModeUpdateAsync()
        {
            string authKey = ConfigurationManager.AppSettings["MasterKey"];
            string endpoint = ConfigurationManager.AppSettings["GatewayEndpoint"];

            AzureKeyCredential masterKeyCredential = new AzureKeyCredential(authKey);
            using (CosmosClient client = new CosmosClient(
                    endpoint,
                    masterKeyCredential))
            {
                string databaseName = Guid.NewGuid().ToString();

                try
                {
                    Cosmos.Database database = client.GetDatabase(databaseName);
                    ResponseMessage responseMessage = await database.ReadStreamAsync();
                    Assert.AreEqual(HttpStatusCode.NotFound, responseMessage.StatusCode);

                    {
                        // Random key: Next set of actions are expected to fail => 401 (UnAuthorized)
                        masterKeyCredential.Update(Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())));

                        responseMessage = await database.ReadStreamAsync();
                        Assert.AreEqual(HttpStatusCode.Unauthorized, responseMessage.StatusCode);

                        string diagnostics = responseMessage.Diagnostics.ToString();
                        Assert.IsTrue(diagnostics.Contains("AuthProvider LifeSpan InSec"), diagnostics.ToString());
                    }

                    {
                        // Resetting back to master key => 404 (NotFound)
                        masterKeyCredential.Update(authKey);
                        responseMessage = await database.ReadStreamAsync();
                        Assert.AreEqual(HttpStatusCode.NotFound, responseMessage.StatusCode);
                    }


                    // Test with resource token interchageability 
                    masterKeyCredential.Update(authKey);
                    database = await client.CreateDatabaseAsync(databaseName);

                    string containerId = Guid.NewGuid().ToString();
                    ContainerResponse containerResponse = await database.CreateContainerAsync(containerId, "/id");
                    Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);


                    {
                        // Resource token with ALL permissoin's
                        string userId = Guid.NewGuid().ToString();
                        UserResponse userResponse = await database.CreateUserAsync(userId);
                        Cosmos.User user = userResponse.User;
                        Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode);
                        Assert.AreEqual(userId, user.Id);

                        string permissionId = Guid.NewGuid().ToString();
                        PermissionProperties permissionProperties = new PermissionProperties(permissionId, Cosmos.PermissionMode.All, client.GetContainer(databaseName, containerId));
                        PermissionResponse permissionResponse = await database.GetUser(userId).CreatePermissionAsync(permissionProperties);
                        Assert.AreEqual(HttpStatusCode.Created, permissionResponse.StatusCode);
                        Assert.AreEqual(permissionId, permissionResponse.Resource.Id);
                        Assert.AreEqual(Cosmos.PermissionMode.All, permissionResponse.Resource.PermissionMode);
                        Assert.IsNotNull(permissionResponse.Resource.Token);
                        SelflinkValidator.ValidatePermissionSelfLink(permissionResponse.Resource.SelfLink);

                        // Valdiate ALL on contianer
                        masterKeyCredential.Update(permissionResponse.Resource.Token);
                        ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();

                        Cosmos.Container container = client.GetContainer(databaseName, containerId);

                        responseMessage = await container.ReadContainerStreamAsync();
                        Assert.AreEqual(HttpStatusCode.OK, responseMessage.StatusCode);

                        responseMessage = await container.CreateItemStreamAsync(TestCommon.SerializerCore.ToStream(item), new Cosmos.PartitionKey(item.id));
                        Assert.AreEqual(HttpStatusCode.Created, responseMessage.StatusCode); // Read Only resorce token
                    }

                    // Reset to master key for new permission creation
                    masterKeyCredential.Update(authKey);

                    {
                        // Resource token with Read-ONLY permissoin's
                        string userId = Guid.NewGuid().ToString();
                        UserResponse userResponse = await database.CreateUserAsync(userId);
                        Cosmos.User user = userResponse.User;
                        Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode);
                        Assert.AreEqual(userId, user.Id);

                        string permissionId = Guid.NewGuid().ToString();
                        PermissionProperties permissionProperties = new PermissionProperties(permissionId, Cosmos.PermissionMode.Read, client.GetContainer(databaseName, containerId));
                        PermissionResponse permissionResponse = await database.GetUser(userId).CreatePermissionAsync(permissionProperties);
                        //Backend returns Created instead of OK
                        Assert.AreEqual(HttpStatusCode.Created, permissionResponse.StatusCode);
                        Assert.AreEqual(permissionId, permissionResponse.Resource.Id);
                        Assert.AreEqual(Cosmos.PermissionMode.Read, permissionResponse.Resource.PermissionMode);

                        // Valdiate read on contianer
                        masterKeyCredential.Update(permissionResponse.Resource.Token);
                        ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();

                        Cosmos.Container container = client.GetContainer(databaseName, containerId);

                        responseMessage = await container.ReadContainerStreamAsync();
                        Assert.AreEqual(HttpStatusCode.OK, responseMessage.StatusCode);

                        responseMessage = await container.CreateItemStreamAsync(TestCommon.SerializerCore.ToStream(item), new Cosmos.PartitionKey(item.id));
                        Assert.AreEqual(HttpStatusCode.Forbidden, responseMessage.StatusCode); // Read Only resorce token
                    }

                    {

                        // Reset to master key for new permission creation
                        masterKeyCredential.Update(Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())));

                        ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                        Cosmos.Container container = client.GetContainer(databaseName, containerId);

                        responseMessage = await container.CreateItemStreamAsync(TestCommon.SerializerCore.ToStream(item), new Cosmos.PartitionKey(item.id));
                        Assert.AreEqual(HttpStatusCode.Unauthorized, responseMessage.StatusCode); // Read Only resorce token

                        string diagnostics = responseMessage.Diagnostics.ToString();
                        Assert.IsTrue(diagnostics.Contains("AuthProvider LifeSpan InSec"), diagnostics.ToString());
                    }
                }
                finally
                {
                    // Reset to master key for clean-up
                    masterKeyCredential.Update(authKey);
                    await TestCommon.DeleteDatabaseAsync(client, client.GetDatabase(databaseName));
                }
            }
        }

        [TestMethod]
        public async Task ValidateTryGetAccountProperties()
        {
            using CosmosClient cosmosClient = new CosmosClient(
                ConfigurationManager.AppSettings["GatewayEndpoint"],
                ConfigurationManager.AppSettings["MasterKey"]
            );

            Assert.IsFalse(cosmosClient.DocumentClient.TryGetCachedAccountProperties(out AccountProperties propertiesFromMethod));

            AccountProperties accountProperties = await cosmosClient.ReadAccountAsync();

            Assert.IsTrue(cosmosClient.DocumentClient.TryGetCachedAccountProperties(out propertiesFromMethod));

            Assert.AreEqual(accountProperties.Consistency.DefaultConsistencyLevel, propertiesFromMethod.Consistency.DefaultConsistencyLevel);
            Assert.AreEqual(accountProperties.Id, propertiesFromMethod.Id);
        }

        private int TaskStartedCount = 0;

        private async Task<Exception> ReadNotFound(Container container)
        {
            try
            {
                Interlocked.Increment(ref this.TaskStartedCount);
                await container.ReadItemAsync<JObject>("Random", new Cosmos.PartitionKey("DoesNotExist"));
                throw new Exception("Should throw a CosmosException 403");
            }
            catch (CosmosException ex)
            {
                return ex;
            }
        }

        public async Task ResourceResponseStreamingTest()
        {
            using (DocumentClient client = TestCommon.CreateClient(true))
            {

                Database db = (await client.CreateDatabaseAsync(new Database() { Id = Guid.NewGuid().ToString() })).Resource;
                DocumentCollection coll = await TestCommon.CreateCollectionAsync(client, db, new DocumentCollection()
                {
                    Id = Guid.NewGuid().ToString(),
                    PartitionKey = new PartitionKeyDefinition()
                    {
                        Paths = new System.Collections.ObjectModel.Collection<string>() { "/id" }
                    }
                });
                ResourceResponse<Document> doc = await client.CreateDocumentAsync(coll.SelfLink, new Document() { Id = Guid.NewGuid().ToString() });

                Assert.AreEqual(doc.ResponseStream.Position, 0);

                StreamReader streamReader = new StreamReader(doc.ResponseStream);
                string text = streamReader.ReadToEnd();

                Assert.AreEqual(doc.ResponseStream.Position, doc.ResponseStream.Length);

                try
                {
                    doc.Resource.ToString();
                    Assert.Fail("Deserializing Resource here should throw exception since the stream was already read");
                }
                catch (JsonReaderException ex)
                {
                    Console.WriteLine("Expected exception while deserializing Resource: " + ex.Message);
                }
            }
        }

        [TestMethod]
        public async Task TestHeadersPassedinByClient()
        {
            int httpCallCount = 0;
            IEnumerable<string> sdkSupportedCapabilities = null;
            HttpClientHandlerHelper httpClientHandlerHelper = new HttpClientHandlerHelper()
            {
                RequestCallBack = (request, cancellToken) =>
                {
                    Interlocked.Increment(ref httpCallCount);
                    request.Headers.TryGetValues(HttpConstants.HttpHeaders.SDKSupportedCapabilities, out sdkSupportedCapabilities);
                    return null;
                }
            };

            using CosmosClient cosmosClient = new CosmosClient(
                accountEndpoint: "https://localhost:8081",
                authKeyOrResourceToken: Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())),
                clientOptions: new CosmosClientOptions()
                {
                    HttpClientFactory = () => new HttpClient(httpClientHandlerHelper),
                });

            CosmosException cosmosException1 = null;
            try
            {
                await cosmosClient.GetContainer("db", "c").ReadItemAsync<JObject>("Random", new Cosmos.PartitionKey("DoesNotExist"));
            }
            catch (CosmosException ex)
            {
                cosmosException1 = ex;
                Assert.IsTrue(httpCallCount > 0);
            }

            Assert.IsNotNull(sdkSupportedCapabilities);
            Assert.AreEqual(1, sdkSupportedCapabilities.Count());

            string sdkSupportedCapability = sdkSupportedCapabilities.Single();
            ulong capability = ulong.Parse(sdkSupportedCapability);

            Assert.AreEqual((ulong)SDKSupportedCapabilities.PartitionMerge, capability & (ulong)SDKSupportedCapabilities.PartitionMerge,$" received header value as {sdkSupportedCapability}");
        }

        [TestMethod]
        public async Task TestEtagOnUpsertOperationForGatewayClient()
        {
            await this.TestEtagOnUpsertOperation(true);
        }

        [TestMethod]
        public async Task TestEtagOnUpsertOperationForDirectTCPClient()
        {
            await this.TestEtagOnUpsertOperation(false, Protocol.Tcp);
        }

        internal async Task TestEtagOnUpsertOperation(bool useGateway, Protocol protocol = Protocol.Tcp)
        {
            using (DocumentClient client = TestCommon.CreateClient(false, Protocol.Tcp))
            {
                Database db = (await client.CreateDatabaseAsync(new Database() { Id = Guid.NewGuid().ToString() })).Resource;
                try
                {
                    DocumentCollection coll = await TestCommon.CreateCollectionAsync(client, db, new DocumentCollection()
                    {
                        Id = Guid.NewGuid().ToString(),
                        PartitionKey = new PartitionKeyDefinition()
                        {
                            Paths = new System.Collections.ObjectModel.Collection<string>() { "/id" }
                        }
                    });

                    LinqGeneralBaselineTests.Book myBook = new LinqGeneralBaselineTests.Book();
                    myBook.Id = Guid.NewGuid().ToString();
                    myBook.Title = "Azure DocumentDB 101";

                    Document doc = (await client.CreateDocumentAsync(coll.SelfLink, myBook)).Resource;

                    myBook.Title = "Azure DocumentDB 201";
                    await client.ReplaceDocumentAsync(doc.SelfLink, myBook);

                    AccessCondition condition = new AccessCondition();
                    condition.Type = AccessConditionType.IfMatch;
                    condition.Condition = doc.ETag;

                    RequestOptions requestOptions = new RequestOptions();
                    requestOptions.AccessCondition = condition;

                    myBook.Title = "Azure DocumentDB 301";

                    try
                    {
                        await client.UpsertDocumentAsync(coll.SelfLink, myBook, requestOptions);
                        Assert.Fail("Upsert Document should fail since the Etag is not matching.");
                    }
                    catch (Exception ex)
                    {
                        DocumentClientException innerException = ex as DocumentClientException;
                        Assert.AreEqual(HttpStatusCode.PreconditionFailed, innerException.StatusCode, "Invalid status code");
                    }
                }
                finally
                {
                    await client.DeleteDatabaseAsync(db);
                }
            }
        }
        
        [TestMethod]
        public async Task Verify_CertificateCallBackGetsCalled_ForTCP_HTTP()
        {
            string authKey = ConfigurationManager.AppSettings["MasterKey"];
            string endpoint = ConfigurationManager.AppSettings["GatewayEndpoint"];
            int counter = 0;
            AzureKeyCredential masterKeyCredential = new AzureKeyCredential(authKey);
            using CosmosClient cosmosClient = new CosmosClient(
                    endpoint,
                    masterKeyCredential,
                    new CosmosClientOptions()
                    {
                        ConnectionMode = ConnectionMode.Direct,
                        ConnectionProtocol = Protocol.Tcp,
                        ServerCertificateCustomValidationCallback = (X509Certificate2 cerf, X509Chain chain, SslPolicyErrors error) => { counter ++; return true; }
                    });

            Cosmos.Database database = null;
            try
            {
                string databaseName = Guid.NewGuid().ToString();
                string databaseId = Guid.NewGuid().ToString();
                
                //HTTP callback
                database = await cosmosClient.CreateDatabaseAsync(databaseId);

                Cosmos.Container container = await database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id");

                //TCP callback
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                ResponseMessage responseMessage = await container.CreateItemStreamAsync(TestCommon.SerializerCore.ToStream(item), new Cosmos.PartitionKey(item.id));

                Assert.IsTrue(counter >= 2);
            }
            finally
            {
                await database?.DeleteStreamAsync();
            }
        }

        [TestMethod]
        public async Task Verify_DisableCertificateValidationCallBackGetsCalled_ForTCP_HTTP()
        {
            int counter = 0;
            CosmosClientOptions options = new CosmosClientOptions()
            {
                DisableServerCertificateValidationInvocationCallback = () => counter++,
            };

            string authKey = ConfigurationManager.AppSettings["MasterKey"];
            string endpoint = ConfigurationManager.AppSettings["GatewayEndpoint"];
            string connectionStringWithSslDisable = $"AccountEndpoint={endpoint};AccountKey={authKey};DisableServerCertificateValidation=true";

            using CosmosClient cosmosClient = new CosmosClient(connectionStringWithSslDisable, options);

            string databaseName = Guid.NewGuid().ToString();
            string databaseId = Guid.NewGuid().ToString();
            Cosmos.Database database = null;

            try
            {
                //HTTP callback
                Trace.TraceInformation("Creating test database and container");
                database = await cosmosClient.CreateDatabaseAsync(databaseId);
                Cosmos.Container container = await database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id");

                // TCP callback
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                ResponseMessage responseMessage = await container.CreateItemStreamAsync(TestCommon.SerializerCore.ToStream(item), new Cosmos.PartitionKey(item.id));
            }
            finally
            {
                await database?.DeleteStreamAsync();
            }

            Assert.IsTrue(counter >= 2);
        }

        [TestMethod]
        public void SqlQuerySpecSerializationTest()
        {
            Action<string, SqlQuerySpec> verifyJsonSerialization = (expectedText, query) =>
            {
                string actualText = SerializeQuerySpecToJson(query);
                SqlQuerySpec querySpec = DeserializeJsonToQuerySpec(actualText);
                string otherText = SerializeQuerySpecToJson(querySpec);

                Assert.AreEqual(expectedText, actualText);
                Assert.AreEqual(expectedText, otherText);
            };

            Action<string> verifyJsonSerializationText = (text) =>
            {

                SqlQuerySpec querySpec = DeserializeJsonToQuerySpec(text);
                string otherText = SerializeQuerySpecToJson(querySpec);

                Assert.AreEqual(text, otherText);
            };

            // Verify serialization
            verifyJsonSerialization("{\"parameters\":[],\"query\":null}", new SqlQuerySpec());
            verifyJsonSerialization("{\"parameters\":[],\"query\":\"SELECT 1\"}", new SqlQuerySpec("SELECT 1"));
            verifyJsonSerialization("{\"parameters\":[{\"name\":null,\"value\":null}],\"query\":\"SELECT 1\"}",
                new SqlQuerySpec()
                {
                    QueryText = "SELECT 1",
                    Parameters = new SqlParameterCollection() { new SqlParameter() }
                });

            verifyJsonSerialization("{\"parameters\":[" +
                    "{\"name\":\"@p1\",\"value\":5}" +
                "],\"query\":\"SELECT 1\"}",
                new SqlQuerySpec()
                {
                    QueryText = "SELECT 1",
                    Parameters = new SqlParameterCollection() { new SqlParameter("@p1", 5) }
                });
            verifyJsonSerialization("{\"parameters\":[" +
                    "{\"name\":\"@p1\",\"value\":5}," +
                    "{\"name\":\"@p1\",\"value\":true}" +
                "],\"query\":\"SELECT 1\"}",
                new SqlQuerySpec()
                {
                    QueryText = "SELECT 1",
                    Parameters = new SqlParameterCollection() { new SqlParameter("@p1", 5), new SqlParameter("@p1", true) }
                });
            verifyJsonSerialization("{\"parameters\":[" +
                    "{\"name\":\"@p1\",\"value\":\"abc\"}" +
                "],\"query\":\"SELECT 1\"}",
                new SqlQuerySpec()
                {
                    QueryText = "SELECT 1",
                    Parameters = new SqlParameterCollection() { new SqlParameter("@p1", "abc") }
                });
            verifyJsonSerialization("{\"parameters\":[" +
                    "{\"name\":\"@p1\",\"value\":[1,2,3]}" +
                "],\"query\":\"SELECT 1\"}",
                new SqlQuerySpec()
                {
                    QueryText = "SELECT 1",
                    Parameters = new SqlParameterCollection() { new SqlParameter("@p1", new int[] { 1, 2, 3 }) }
                });

            // Verify roundtrips
            verifyJsonSerializationText("{\"parameters\":[],\"query\":null}");
            verifyJsonSerializationText("{\"parameters\":[],\"query\":\"SELECT 1\"}");
            verifyJsonSerializationText(
                "{" +
                    "\"parameters\":[" +
                        "{\"name\":null,\"value\":null}" +
                    "]," + "\"query\":\"SELECT 1\"" +
                "}");
            verifyJsonSerializationText(
                "{" +
                    "\"parameters\":[" +
                        "{\"name\":\"@p1\",\"value\":null}" +
                    "]," + "\"query\":\"SELECT 1\"" +
                "}");
            verifyJsonSerializationText(
                "{" +
                    "\"parameters\":[" +
                        "{\"name\":\"@p1\",\"value\":true}" +
                    "]," + "\"query\":\"SELECT 1\"" +
                "}");
            verifyJsonSerializationText(
                "{" +
                    "\"parameters\":[" +
                        "{\"name\":\"@p1\",\"value\":false}" +
                    "]," + "\"query\":\"SELECT 1\"" +
                "}");
            verifyJsonSerializationText(
                "{" +
                    "\"parameters\":[" +
                        "{\"name\":\"@p1\",\"value\":123}" +
                    "]," + "\"query\":\"SELECT 1\"" +
                "}");
            verifyJsonSerializationText(
                "{" +
                    "\"parameters\":[" +
                        "{\"name\":\"@p1\",\"value\":\"abc\"}" +
                    "]," + "\"query\":\"SELECT 1\"" +
                "}");
        }

        [TestMethod]
        public void QueryDefinitionSerializationTest()
        {
            Action<string, SqlQuerySpec> verifyJsonSerialization = (expectedText, query) =>
            {
                QueryDefinition queryDefinition = QueryDefinition.CreateFromQuerySpec(query);
                string actualText = JsonConvert.SerializeObject(queryDefinition);

                Assert.AreEqual(expectedText, actualText);

                QueryDefinition otherQuery = JsonConvert.DeserializeObject<QueryDefinition>(actualText);
                string otherText = JsonConvert.SerializeObject(otherQuery);
                Assert.AreEqual(expectedText, otherText);
            };

            Action<string> verifyJsonSerializationText = (text) =>
            {
                QueryDefinition query = JsonConvert.DeserializeObject<QueryDefinition>(text);
                string otherText = JsonConvert.SerializeObject(query);

                Assert.AreEqual(text, otherText);
            };

            // Verify serialization
            verifyJsonSerialization("{\"query\":\"SELECT 1\"}", new SqlQuerySpec("SELECT 1"));

            verifyJsonSerialization("{\"query\":\"SELECT 1\",\"parameters\":[" +
                                    "{\"name\":\"@p1\",\"value\":5}" +
                                    "]}",
                new SqlQuerySpec()
                {
                    QueryText = "SELECT 1",
                    Parameters = new SqlParameterCollection() { new SqlParameter("@p1", 5) }
                });
            verifyJsonSerialization("{\"query\":\"SELECT 1\",\"parameters\":[" +
                    "{\"name\":\"@p1\",\"value\":true}" +
                "]}",
                new SqlQuerySpec()
                {
                    QueryText = "SELECT 1",
                    Parameters = new SqlParameterCollection() { new SqlParameter("@p1", 5), new SqlParameter("@p1", true) }
                });
            verifyJsonSerialization("{\"query\":\"SELECT 1\",\"parameters\":[" +
                    "{\"name\":\"@p1\",\"value\":\"abc\"}" +
                "]}",
                new SqlQuerySpec()
                {
                    QueryText = "SELECT 1",
                    Parameters = new SqlParameterCollection() { new SqlParameter("@p1", "abc") }
                });
            verifyJsonSerialization("{\"query\":\"SELECT 1\",\"parameters\":[" +
                    "{\"name\":\"@p1\",\"value\":[1,2,3]}" +
                "]}",
                new SqlQuerySpec()
                {
                    QueryText = "SELECT 1",
                    Parameters = new SqlParameterCollection() { new SqlParameter("@p1", new int[] { 1, 2, 3 }) }
                });
            verifyJsonSerialization("{\"query\":\"SELECT 1\",\"parameters\":[" +
                    "{\"name\":\"@p1\",\"value\":{\"a\":[1,2,3]}}" +
                "]}",
                new SqlQuerySpec()
                {
                    QueryText = "SELECT 1",
                    Parameters = new SqlParameterCollection() { new SqlParameter("@p1", JObject.Parse("{\"a\":[1,2,3]}")) }
                });
            verifyJsonSerialization("{\"query\":\"SELECT 1\",\"parameters\":[" +
                    "{\"name\":\"@p1\",\"value\":{\"a\":[1,2,3]}}" +
                "]}",
                new SqlQuerySpec()
                {
                    QueryText = "SELECT 1",
                    Parameters = new SqlParameterCollection() { new SqlParameter("@p1", new JRaw("{\"a\":[1,2,3]}")) }
                });

            // Verify roundtrips
            verifyJsonSerializationText("{\"query\":\"SELECT 1\"}");
            verifyJsonSerializationText(
                "{" +
                    "\"query\":\"SELECT 1\"," +
                    "\"parameters\":[" +
                        "{\"name\":\"@p1\",\"value\":null}" +
                    "]" +
                "}");
            verifyJsonSerializationText(
                "{" +
                    "\"query\":\"SELECT 1\"," +
                    "\"parameters\":[" +
                        "{\"name\":\"@p1\",\"value\":true}" +
                    "]" +
                "}");
            verifyJsonSerializationText(
                "{" +
                    "\"query\":\"SELECT 1\"," +
                    "\"parameters\":[" +
                        "{\"name\":\"@p1\",\"value\":false}" +
                    "]" +
                "}");
            verifyJsonSerializationText(
                "{" +
                    "\"query\":\"SELECT 1\"," +
                    "\"parameters\":[" +
                        "{\"name\":\"@p1\",\"value\":123}" +
                    "]" +
                "}");
            verifyJsonSerializationText(
                "{" +
                    "\"query\":\"SELECT 1\"," +
                    "\"parameters\":[" +
                        "{\"name\":\"@p1\",\"value\":\"abc\"}" +
                    "]" +
                "}");
            verifyJsonSerializationText(
                "{" +
                    "\"query\":\"SELECT 1\"," +
                    "\"parameters\":[" +
                        "{\"name\":\"@p1\",\"value\":{\"a\":[1,2,\"abc\"]}}" +
                    "]" +
                "}");
        }


        [TestMethod]
        public async Task VerifyNegativeWebProxySettings()
        {
            //proxy will be bypassed if the endpoint is localhost or 127.0.0.1
            string endpoint = $"https://{Environment.MachineName}";

            IWebProxy proxy = new WebProxy
            {
                Address = new Uri("http://www.cosmostestproxyshouldfail.com"),
                BypassProxyOnLocal = false,
                BypassList = new string[] { },
            };

            CosmosClient cosmosClient = new CosmosClient(
                endpoint,
                ConfigurationManager.AppSettings["MasterKey"],
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    ConnectionProtocol = Protocol.Https,
                    WebProxy = proxy
                }
            );

            await Assert.ThrowsExceptionAsync<HttpRequestException>(async () =>
            {
                DatabaseResponse databaseResponse = await cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString());
            });

            proxy = new TestWebProxy { Credentials = new NetworkCredential("test", "test") };

            cosmosClient.Dispose();
            cosmosClient = new CosmosClient(
                endpoint,
                ConfigurationManager.AppSettings["MasterKey"],
                new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    ConnectionProtocol = Protocol.Https,
                    WebProxy = proxy
                }
            );

            await Assert.ThrowsExceptionAsync<HttpRequestException>(async () =>
            {
                DatabaseResponse databaseResponse = await cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString());
            });
            cosmosClient.Dispose();
        }

        [TestMethod]
        public async Task HttpClientFactorySmokeTest()
        {
            HttpClient client = new HttpClient();
            Mock<Func<HttpClient>> factory = new Mock<Func<HttpClient>>();
            factory.Setup(f => f()).Returns(client);
            using CosmosClient cosmosClient = new CosmosClient(
                ConfigurationManager.AppSettings["GatewayEndpoint"],
                ConfigurationManager.AppSettings["MasterKey"],
                new CosmosClientOptions
                {
                    ApplicationName = "test",
                    ConnectionMode = ConnectionMode.Gateway,
                    ConnectionProtocol = Protocol.Https,
                    HttpClientFactory = factory.Object
                }
            );

            string someId = Guid.NewGuid().ToString();
            Cosmos.Database database = null;
            try
            {
                database = await cosmosClient.CreateDatabaseAsync(someId);
                Cosmos.Container container = await database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id");
                await container.CreateItemAsync<dynamic>(new { id = someId });
                await container.ReadItemAsync<dynamic>(someId, new Cosmos.PartitionKey(someId));
                await container.DeleteItemAsync<dynamic>(someId, new Cosmos.PartitionKey(someId));
                await container.DeleteContainerAsync();
                Mock.Get(factory.Object).Verify(f => f(), Times.Once);
            }
            finally
            {
                if (database != null)
                {
                    await database.DeleteAsync();
                }
            }
        }

        [TestMethod]
        public async Task HttpClientConnectionLimitTest()
        {
            int gatewayConnectionLimit = 1;

            IReadOnlyList<string> excludeConnections = GetActiveConnections();
            using (CosmosClient cosmosClient = new CosmosClient(
                ConfigurationManager.AppSettings["GatewayEndpoint"],
                ConfigurationManager.AppSettings["MasterKey"],
                new CosmosClientOptions
                {
                    ApplicationName = "test",
                    GatewayModeMaxConnectionLimit = gatewayConnectionLimit,
                    ConnectionMode = ConnectionMode.Gateway,
                    ConnectionProtocol = Protocol.Https
                }
            ))
            {
                CosmosHttpClient cosmosHttpClient = cosmosClient.DocumentClient.httpClient;
                SocketsHttpHandler httpClientHandler = (SocketsHttpHandler)cosmosHttpClient.HttpMessageHandler;
                Assert.AreEqual(gatewayConnectionLimit, httpClientHandler.MaxConnectionsPerServer);

                Cosmos.Database database = await cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString());
                Container container = await database.CreateContainerAsync(
                    "TestConnections",
                    "/pk",
                    throughput: 20000);

                List<Task> creates = new List<Task>();
                for (int i = 0; i < 100; i++)
                {
                    creates.Add(container.CreateItemAsync<dynamic>(new { id = Guid.NewGuid().ToString(), pk = Guid.NewGuid().ToString() }));
                }

                await Task.WhenAll(creates);

                // Clean up the database
                await database.DeleteAsync();
            }


            IReadOnlyList<string> afterConnections = GetActiveConnections();

            int connectionDiff = afterConnections.Count - excludeConnections.Count;
            Assert.IsTrue(connectionDiff <= gatewayConnectionLimit, $"Connection before : {excludeConnections.Count}, after {afterConnections.Count};" +
                $"Before connections: {JsonConvert.SerializeObject(excludeConnections)}; After connections: {JsonConvert.SerializeObject(afterConnections)}");
        }

        [TestMethod]
        public void PooledConnectionLifetimeTest()
        {
            //Create Cosmos Client
            using CosmosClient cosmosClient = new CosmosClient(
                accountEndpoint: "https://localhost:8081",
                authKeyOrResourceToken: Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())));

            //Assert type of message handler
            Type socketHandlerType = Type.GetType("System.Net.Http.SocketsHttpHandler, System.Net.Http");
            Type clientMessageHandlerType = cosmosClient.ClientContext.DocumentClient.httpClient.HttpMessageHandler.GetType();
            Assert.AreEqual(socketHandlerType, clientMessageHandlerType);
        }

        [TestMethod]
        [TestCategory("MultiRegion")]
        public async Task MultiRegionAccountTest()
        {
            string connectionString = TestCommon.GetMultiRegionConnectionString();
            Assert.IsFalse(string.IsNullOrEmpty(connectionString), "Connection String Not Set");
            using CosmosClient cosmosClient = new CosmosClient(connectionString);
            Assert.IsNotNull(cosmosClient);
            AccountProperties properties = await cosmosClient.ReadAccountAsync();
            Assert.IsNotNull(properties);
        }

        [TestMethod]
        [Owner("amudumba")]
        public async Task CreateItemDuringTimeoutTest()
        {
            //Prepare
            //Enabling aggressive timeout detection that empowers connnection health checker whih marks a channel/connection as "unhealthy" if there are a set of consecutive timeouts.
            Environment.SetEnvironmentVariable("AZURE_COSMOS_AGGRESSIVE_TIMEOUT_DETECTION_ENABLED", "True");
            Environment.SetEnvironmentVariable("AZURE_COSMOS_TIMEOUT_DETECTION_TIME_LIMIT_IN_SECONDS", "1");

            // Enabling fault injection rule to simulate a timeout scenario.
            string timeoutRuleId = "timeoutRule-" + Guid.NewGuid().ToString();
            FaultInjectionRule timeoutRule = new FaultInjectionRuleBuilder(
                id: timeoutRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.CreateItem)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.SendDelay)
                    .WithDelay(TimeSpan.FromSeconds(100))
                        .Build())
                .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule> { timeoutRule };
            FaultInjector faultInjector = new FaultInjector(rules);


            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConsistencyLevel = Cosmos.ConsistencyLevel.Session,
                FaultInjector = faultInjector,
                RequestTimeout = TimeSpan.FromSeconds(2)

            };

            Cosmos.Database db = null;
            try
            {
                CosmosClient cosmosClient = TestCommon.CreateCosmosClient(clientOptions: cosmosClientOptions);

                db = await cosmosClient.CreateDatabaseIfNotExistsAsync("TimeoutFaultTest");
                Container container = await db.CreateContainerIfNotExistsAsync("TimeoutFaultContainer", "/pk");

                // Act.
                // Simulate a aggressive timeout scenario by performing 3 writes which will all timeout due to fault injection rule.
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
                        await container.CreateItemAsync<ToDoActivity>(testItem);
                    }
                    catch (CosmosException exx)
                    {
                        Assert.AreEqual(HttpStatusCode.RequestTimeout, exx.StatusCode);
                    }
                }

                //Assert that the old channel that is now made unhealthy by the timeouts and a new healthy channel is available for next requests.


                // Get all the channels that are under TransportClient -> ChannelDictionary -> Channels.
                IStoreClientFactory factory = (IStoreClientFactory)cosmosClient.DocumentClient.GetType()
                    .GetField("storeClientFactory", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(cosmosClient.DocumentClient);
                StoreClientFactory storeClientFactory = (StoreClientFactory)factory;

                TransportClient client = (TransportClient)storeClientFactory.GetType()
                                .GetField("transportClient", BindingFlags.NonPublic | BindingFlags.Instance)
                                .GetValue(storeClientFactory);
                Documents.Rntbd.TransportClient transportClient = (Documents.Rntbd.TransportClient)client;

                Documents.Rntbd.ChannelDictionary channelDict = (Documents.Rntbd.ChannelDictionary)transportClient.GetType()
                                .GetField("channelDictionary", BindingFlags.NonPublic | BindingFlags.Instance)
                                .GetValue(transportClient);
                ConcurrentDictionary<Documents.Rntbd.ServerKey, Documents.Rntbd.IChannel> allChannels = (ConcurrentDictionary<Documents.Rntbd.ServerKey, Documents.Rntbd.IChannel>)channelDict.GetType()
                    .GetField("channels", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(channelDict);

                //Assert that the old channel that is now made unhealthy by the timeouts.
                //Get the channel by channelDict -> LoadBalancingChannel -> LoadBalancingPartition -> LbChannelState -> IChannel.
                Documents.Rntbd.LoadBalancingChannel loadBalancingUnhealthyChannel = (Documents.Rntbd.LoadBalancingChannel)allChannels[allChannels.Keys.ElementAt(1)];
                Documents.Rntbd.LoadBalancingPartition loadBalancingPartitionUnHealthy = (Documents.Rntbd.LoadBalancingPartition)loadBalancingUnhealthyChannel.GetType()
                                        .GetField("singlePartition", BindingFlags.NonPublic | BindingFlags.Instance)
                                        .GetValue(loadBalancingUnhealthyChannel);

                Assert.IsNotNull(loadBalancingPartitionUnHealthy);

                List<Documents.Rntbd.LbChannelState> openChannelsUnhealthy = (List<Documents.Rntbd.LbChannelState>)loadBalancingPartitionUnHealthy.GetType()
                                        .GetField("openChannels", BindingFlags.NonPublic | BindingFlags.Instance)
                                        .GetValue(loadBalancingPartitionUnHealthy);
                Assert.AreEqual(1, openChannelsUnhealthy.Count);

                foreach (Documents.Rntbd.LbChannelState channelState in openChannelsUnhealthy)
                {
                    Documents.Rntbd.IChannel channel = (Documents.Rntbd.IChannel)openChannelsUnhealthy[0].GetType()
                                        .GetField("channel", BindingFlags.NonPublic | BindingFlags.Instance)
                                        .GetValue(channelState);
                    Assert.IsFalse(channel.Healthy);
                }

                //Assert that the new channel which is healthy. Picking the first channel from the allChannels dictionary as the new channel.
                Documents.Rntbd.LoadBalancingChannel loadBalancingChannel = (Documents.Rntbd.LoadBalancingChannel)allChannels[allChannels.Keys.First()];
                Documents.Rntbd.LoadBalancingPartition loadBalancingPartition = (Documents.Rntbd.LoadBalancingPartition)loadBalancingChannel.GetType()
                                        .GetField("singlePartition", BindingFlags.NonPublic | BindingFlags.Instance)
                                        .GetValue(loadBalancingChannel);

                Assert.IsNotNull(loadBalancingPartition);

                List<Documents.Rntbd.LbChannelState> openChannels = (List<Documents.Rntbd.LbChannelState>)loadBalancingPartition.GetType()
                                        .GetField("openChannels", BindingFlags.NonPublic | BindingFlags.Instance)
                                        .GetValue(loadBalancingPartition);
                Assert.AreEqual(1, openChannels.Count);

                foreach (Documents.Rntbd.LbChannelState channelState in openChannels)
                {
                    Documents.Rntbd.IChannel channel = (Documents.Rntbd.IChannel)openChannels[0].GetType()
                                        .GetField("channel", BindingFlags.NonPublic | BindingFlags.Instance)
                                        .GetValue(channelState);
                    Assert.IsTrue(channel.Healthy);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("AZURE_COSMOS_AGGRESSIVE_TIMEOUT_DETECTION_ENABLED", null);
                Environment.SetEnvironmentVariable("AZURE_COSMOS_TIMEOUT_DETECTION_TIME_LIMIT_IN_SECONDS", null);
                if (db != null) await db.DeleteAsync();
            }
        }
        public static IReadOnlyList<string> GetActiveConnections()
        {
            string testPid = Process.GetCurrentProcess().Id.ToString();
            using (Process p = new Process())
            {
                ProcessStartInfo ps = new ProcessStartInfo
                {
                    Arguments = "-a -n -o",
                    FileName = "netstat.exe",
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                p.StartInfo = ps;
                p.Start();

                StreamReader stdOutput = p.StandardOutput;
                StreamReader stdError = p.StandardError;

                string content = stdOutput.ReadToEnd() + stdError.ReadToEnd();
                string exitStatus = p.ExitCode.ToString();

                if (exitStatus != "0")
                {
                    // Command Errored. Handle Here If Need Be
                }

                List<string> connections = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => x.EndsWith(testPid)).ToList();

                Assert.IsTrue(connections.Count > 0);

                return connections;
            }
        }

        private static string SerializeQuerySpecToJson(SqlQuerySpec querySpec)
        {
            string queryText;
            using (MemoryStream stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(SqlQuerySpec), new[] { typeof(object[]), typeof(int[]), typeof(SqlParameterCollection) }).WriteObject(stream, querySpec);
                queryText = Encoding.UTF8.GetString(stream.ToArray());
            }

            return queryText;
        }

        private static SqlQuerySpec DeserializeJsonToQuerySpec(string queryText)
        {
            SqlQuerySpec querySpec;
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(queryText)))
            {
                querySpec = (SqlQuerySpec)new DataContractJsonSerializer(typeof(SqlQuerySpec), new[] { typeof(object[]), typeof(int[]), typeof(SqlParameterCollection) }).ReadObject(stream);
            }

            return querySpec;
        }
    }

    internal static class StringHelper
    {
        internal static string EscapeForSQL(this string input)
        {
            return input.Replace("'", "\\'").Replace("\"", "\\\"");
        }
    }

    internal class TestWebProxy : IWebProxy
    {
        public ICredentials Credentials { get; set; }

        public Uri GetProxy(Uri destination)
        {
            return new Uri("http://www.cosmostestproxyshouldfail.com");
        }

        public bool IsBypassed(Uri host)
        {
            return false;
        }
    }
}
