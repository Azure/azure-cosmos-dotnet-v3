//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Core;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Microsoft.Azure.Cosmos.SDK.EmulatorTests.MultiRegionSetupHelpers;
    using TestObject = MultiRegionSetupHelpers.CosmosIntegrationTestObject;

    [TestClass]
    public class CosmosItemThinClientTests
    {
        private const string CentralUs = "Central US";
        private const string EastUs2 = "East US 2";
        private static readonly IReadOnlyList<string> PreferredRegions = new List<string> { CentralUs, EastUs2 };
        private static readonly IReadOnlyList<string> ExcludeRegions = new List<string> { CentralUs };

        private string connectionString;
        private CosmosClient client;
        private Database database;
        private Container container;
        private MultiRegionSetupHelpers.CosmosSystemTextJsonSerializer cosmosSystemTextJsonSerializer;
        private const int ItemCount = 100;
        private const int ThinClientProxyPort = 10250;

        [TestInitialize]
        public async Task TestInitAsync()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager.BypassQueryParsing, Boolean.TrueString);
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "True");
            this.connectionString = Environment.GetEnvironmentVariable("COSMOSDB_THINCLIENT");
            if (string.IsNullOrEmpty(this.connectionString))
            {
                Assert.Fail("Set environment variable COSMOSDB_THINCLIENT to run the tests");
            }

            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            this.cosmosSystemTextJsonSerializer = new MultiRegionSetupHelpers.CosmosSystemTextJsonSerializer(jsonSerializerOptions);

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Gateway,
                ApplicationPreferredRegions = PreferredRegions,
                Serializer = this.cosmosSystemTextJsonSerializer,
            };
            clientOptions.CustomHandlers.Add(new ExcludeRegionsInjectingHandler(ExcludeRegions));

            this.client = new CosmosClient(this.connectionString, clientOptions);

            string uniqueDbName = "TestDb_" + Guid.NewGuid().ToString();
            this.database = await this.client.CreateDatabaseIfNotExistsAsync(uniqueDbName);
            string uniqueContainerName = "TestContainer_" + Guid.NewGuid().ToString();
            this.container = await this.database.CreateContainerIfNotExistsAsync(uniqueContainerName, "/pk");
        }

        [TestCleanup]
        public async Task TestCleanupAsync()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "False");
            Environment.SetEnvironmentVariable(ConfigurationManager.BypassQueryParsing, null);
            if (this.database != null)
            {
                await this.database.DeleteAsync();
            }

            if (this.client != null)
            {
                this.client.Dispose();
            }
        }

        private IEnumerable<TestObject> GenerateItems(string partitionKey)
        {
            List<TestObject> items = new List<TestObject>();
            for (int i = 0; i < ItemCount; i++)
            {
                items.Add(new TestObject
                {
                    Id = Guid.NewGuid().ToString(),
                    Pk = partitionKey,
                    Other = "Test Item " + i
                });
            }
            return items;
        }

        private async Task<List<TestObject>> CreateItemsSafeAsync(IEnumerable<TestObject> items)
        {
            List<TestObject> itemsCreated = new List<TestObject>();
            await Task.Delay(TimeSpan.FromSeconds(30));
            foreach (TestObject item in items)
            {
                try
                {
                    ItemResponse<TestObject> response = await this.container.CreateItemAsync(item, new PartitionKey(item.Pk));
                    if (response.StatusCode == HttpStatusCode.Created)
                    {
                        itemsCreated.Add(item);
                    }
                }
                catch (CosmosException)
                {
                }
            }
            return itemsCreated;
        }

        /// <summary>
        /// Issues a warm-up write on a freshly-created thin-client client so lazy initialization runs and the
        /// connectivity probe (fire-and-forget) caches the regional endpoints as healthy before the asserted
        /// operation. The freshly-created container (TestInitAsync creates a new database/container per test)
        /// also needs time to propagate on the live multi-region account, so the warm-up tolerates the transient
        /// "Collection is not yet available for read" (404/1013) by retrying, then waits for the probe to finish.
        /// </summary>
        private static async Task WarmUpThinClientProbeAsync(Container container, TestObject warmUpItem)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    await container.CreateItemAsync(warmUpItem, new PartitionKey(warmUpItem.Pk));
                    break;
                }
                catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.NotFound && attempt < 11)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(30));
        }

        private static void AssertExcludedRegionsNotInDiagnostics(string diagnostics)
        {
            foreach (string excludedRegion in ExcludeRegions)
            {
                string excludedHost = excludedRegion.Replace(" ", string.Empty).ToLowerInvariant() + ".documents.azure.com:10250";
                Assert.IsFalse(
                    diagnostics.Contains(excludedHost),
                    $"Operation with ExcludeRegions=[{string.Join(",", ExcludeRegions)}] must not route to '{excludedHost}'. Diagnostics: {diagnostics}");
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task RegionalDatabaseAccountNameIsEmptyInPayload()
        {
            byte[] capturedPayload = null;
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "True");

            // Initialize the serializer locally
            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            CosmosSystemTextJsonSerializer serializer = new CosmosSystemTextJsonSerializer(jsonSerializerOptions);

            CosmosClientOptions options = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Gateway,
                Serializer = serializer,
                SendingRequestEventArgs = async (sender, e) =>
                {
                    if (e.HttpRequest.Version == new Version(2, 0))
                    {
                        if (e.HttpRequest.Content != null)
                        {
                            capturedPayload = await e.HttpRequest.Content.ReadAsByteArrayAsync();
                        }
                    }
                },
            };

            using CosmosClient client = new CosmosClient(this.connectionString, options);
            string uniqueDbName = "TestRegional_" + Guid.NewGuid().ToString();
            Database database = await client.CreateDatabaseIfNotExistsAsync(uniqueDbName);
            string uniqueContainerName = "TestRegionalContainer_" + Guid.NewGuid().ToString();
            Container container = await database.CreateContainerIfNotExistsAsync(uniqueContainerName, "/pk");

            string pk = "pk_regional";
            TestObject testItem = this.GenerateItems(pk).First();

            // Act
            ItemResponse<TestObject> response = await container.CreateItemAsync(testItem, new PartitionKey(testItem.Pk));
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

            // Assert
            Assert.IsNotNull(capturedPayload, "The request payload was not captured.");


            // The RNTBD protocol serializes an empty string as a token with a length of 0.
            // For `regionalDatabaseAccountName`, which is a SmallString (type 0x02), this is
            // serialized as two bytes: 0x02 (type) and 0x00 (length).
            // This byte pair represents an empty string value in RNTBD’s small-string encoding.
            byte[] emptyStringToken = { 0x02, 0x00 };

            bool foundEmptyStringToken = false;
            for (int i = 0; i <= capturedPayload.Length - emptyStringToken.Length; i++)
            {
                if (capturedPayload[i] == emptyStringToken[0] && capturedPayload[i + 1] == emptyStringToken[1])
                {
                    foundEmptyStringToken = true;
                    break;
                }
            }

            Assert.IsTrue(foundEmptyStringToken, "The RNTBD payload should contain a token representing an empty string for the regional account name.");

            // Cleanup
            await database.DeleteAsync();
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task ResourceTokenAuth_ThinClient_CreateAndReadItemAsync()
        {
            // Ensure thin-client mode is on for this test (TestInit already sets it, but be explicit).
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "True");

            // 1) Using the key-based client from TestInit, create a User and a Permission
            //    scoped to the test container with PermissionMode.All so we can write + read.
            string userId = "thinclient-user-" + Guid.NewGuid();
            UserResponse userResponse = await this.database.CreateUserAsync(userId);
            Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode);
            User user = userResponse.User;

            string permissionId = "thinclient-perm-" + Guid.NewGuid();
            PermissionProperties permissionProperties = new PermissionProperties(
                permissionId,
                PermissionMode.All,
                this.container);

            PermissionResponse permissionResponse = await user.CreatePermissionAsync(permissionProperties);
            Assert.AreEqual(HttpStatusCode.Created, permissionResponse.StatusCode);
            string resourceToken = permissionResponse.Resource.Token;
            Assert.IsFalse(string.IsNullOrEmpty(resourceToken), "Resource token should be issued by the service.");

            // 2) Parse the AccountEndpoint out of the connection string so we can build a
            //    resource-token-based client (the token overload does not accept a full
            //    connection string).
            string accountEndpoint = this.connectionString
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .First(part => part.StartsWith("AccountEndpoint=", StringComparison.OrdinalIgnoreCase))
                ["AccountEndpoint=".Length..];

            CosmosClientOptions tokenClientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Gateway,
                ApplicationPreferredRegions = PreferredRegions,
                Serializer = this.cosmosSystemTextJsonSerializer,
            };
            tokenClientOptions.CustomHandlers.Add(new ExcludeRegionsInjectingHandler(ExcludeRegions));

            // 3) Build a second CosmosClient authenticated only via the resource token,
            //    with thin-client mode enabled, and exercise item CRUD through it.
            using (CosmosClient tokenClient = new CosmosClient(accountEndpoint, resourceToken, tokenClientOptions))
            {
                Container tokenContainer = tokenClient.GetContainer(this.database.Id, this.container.Id);

                string pk = "rt-pk-" + Guid.NewGuid();
                TestObject warmUpItem = new TestObject
                {
                    Id = Guid.NewGuid().ToString(),
                    Pk = pk,
                    Other = "ThinClient resource-token warm-up"
                };

                // Warm up the thin-client connectivity probe on the token-auth client.
                await WarmUpThinClientProbeAsync(tokenContainer, warmUpItem);

                // Create via resource-token + thin client.
                TestObject item = new TestObject
                {
                    Id = Guid.NewGuid().ToString(),
                    Pk = pk,
                    Other = "ThinClient resource-token item"
                };

                ItemResponse<TestObject> createResponse = await tokenContainer.CreateItemAsync(
                    item,
                    new PartitionKey(item.Pk));
                Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
                AssertExcludedRegionsNotInDiagnostics(createResponse.Diagnostics.ToString());

                // Read it back via resource-token + thin client.
                ItemResponse<TestObject> readResponse = await tokenContainer.ReadItemAsync<TestObject>(
                    item.Id,
                    new PartitionKey(item.Pk));
                Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
                Assert.AreEqual(item.Id, readResponse.Resource.Id);
                Assert.AreEqual(item.Pk, readResponse.Resource.Pk);
                AssertExcludedRegionsNotInDiagnostics(readResponse.Diagnostics.ToString());
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task TestThinClientWithExecuteStoredProcedureAsync()
        {
            CosmosClient localClient = null;
            Database localDatabase = null;

            try
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "true");

                JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null,
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };
                CosmosSystemTextJsonSerializer localSerializer = new MultiRegionSetupHelpers.CosmosSystemTextJsonSerializer(jsonSerializerOptions);

                localClient = new CosmosClient(
                        this.connectionString,
                        new CosmosClientOptions()
                        {
                            ConnectionMode = ConnectionMode.Gateway,
                            Serializer = localSerializer,
                        });

                string uniqueDbName = "TestDbStoreProc_" + Guid.NewGuid().ToString();
                localDatabase = await localClient.CreateDatabaseIfNotExistsAsync(uniqueDbName);
                string uniqueContainerName = "TestDbStoreProcContainer_" + Guid.NewGuid().ToString();
                Container localContainer = await localDatabase.CreateContainerIfNotExistsAsync(uniqueContainerName, "/pk");


                string sprocId = "testSproc_" + Guid.NewGuid().ToString();
                string sprocBody = @"function(itemToCreate) {
                    var context = getContext();
                    var collection = context.getCollection();
                    var response = context.getResponse();
        
                    if (!itemToCreate) throw new Error('Item is undefined or null.');
        
                    // Create a document
                    var accepted = collection.createDocument(
                        collection.getSelfLink(),
                        itemToCreate,
                        function(err, newItem) {
                            if (err) throw err;
                
                            // Query the created document
                            var query = 'SELECT * FROM c WHERE c.id = ""' + newItem.id + '""';
                            var isAccepted = collection.queryDocuments(
                                collection.getSelfLink(),
                                query,
                                function(queryErr, documents) {
                                    if (queryErr) throw queryErr;
                                    response.setBody({
                                        created: newItem,
                                        queried: documents[0]
                                    });
                                }
                            );
                            if (!isAccepted) throw 'Query not accepted';
                        });
        
                    if (!accepted) throw new Error('Create was not accepted.');
                }";

                // Create stored procedure
                Scripts.StoredProcedureResponse createResponse = await localContainer.Scripts.CreateStoredProcedureAsync(
                    new Scripts.StoredProcedureProperties(sprocId, sprocBody));
                Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

                // Execute stored procedure
                string testPartitionId = Guid.NewGuid().ToString();
                TestObject testItem = new TestObject
                {
                    Id = Guid.NewGuid().ToString(),
                    Pk = testPartitionId,
                    Other = "Created by Stored Procedure"
                };

                Scripts.StoredProcedureExecuteResponse<dynamic> executeResponse =
                    await localContainer.Scripts.ExecuteStoredProcedureAsync<dynamic>(
                        sprocId,
                        new PartitionKey(testPartitionId),
                        new dynamic[] { testItem });

                Assert.AreEqual(HttpStatusCode.OK, executeResponse.StatusCode);
                Assert.IsNotNull(executeResponse.Resource);
                string diagnostics = executeResponse.Diagnostics.ToString();
                Assert.IsTrue(diagnostics.Contains("|F4"), "Diagnostics User Agent should contain '|F4' for ThinClient");

                // Delete stored procedure
                await localContainer.Scripts.DeleteStoredProcedureAsync(sprocId);
            }
            finally
            {
                if (localDatabase != null)
                {
                    try
                    {
                        await localDatabase.DeleteAsync();
                    }
                    catch { }
                }

                if (localClient != null)
                {
                    localClient.Dispose();
                }
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task TestThinClientWithExecuteStoredProcedureStreamAsync()
        {
            CosmosClient localClient = null;
            Database localDatabase = null;

            try
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "true");

                JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null,
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };
                CosmosSystemTextJsonSerializer localSerializer = new MultiRegionSetupHelpers.CosmosSystemTextJsonSerializer(jsonSerializerOptions);

                localClient = new CosmosClient(
                        this.connectionString,
                        new CosmosClientOptions()
                        {
                            ConnectionMode = ConnectionMode.Gateway,
                            Serializer = localSerializer,
                        });

                string uniqueDbName = "TestDbStoreProc_" + Guid.NewGuid().ToString();
                localDatabase = await localClient.CreateDatabaseIfNotExistsAsync(uniqueDbName);
                string uniqueContainerName = "TestDbStoreProcContainer_" + Guid.NewGuid().ToString();
                Container localContainer = await localDatabase.CreateContainerIfNotExistsAsync(uniqueContainerName, "/pk");


                string sprocId = "testSproc_" + Guid.NewGuid().ToString();
                string sprocBody = @"function(itemToCreate) {
                    var context = getContext();
                    var collection = context.getCollection();
                    var response = context.getResponse();
        
                    if (!itemToCreate) throw new Error('Item is undefined or null.');
        
                    // Create a document
                    var accepted = collection.createDocument(
                        collection.getSelfLink(),
                        itemToCreate,
                        function(err, newItem) {
                            if (err) throw err;
                
                            // Query the created document
                            var query = 'SELECT * FROM c WHERE c.id = ""' + newItem.id + '""';
                            var isAccepted = collection.queryDocuments(
                                collection.getSelfLink(),
                                query,
                                function(queryErr, documents) {
                                    if (queryErr) throw queryErr;
                                    response.setBody({
                                        created: newItem,
                                        queried: documents[0]
                                    });
                                }
                            );
                            if (!isAccepted) throw 'Query not accepted';
                        });
        
                    if (!accepted) throw new Error('Create was not accepted.');
                }";

                // Create stored procedure
                Scripts.StoredProcedureResponse createResponse = await localContainer.Scripts.CreateStoredProcedureAsync(
                    new Scripts.StoredProcedureProperties(sprocId, sprocBody));
                Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

                // Execute stored procedure
                string testPartitionId = Guid.NewGuid().ToString();
                TestObject testItem = new TestObject
                {
                    Id = Guid.NewGuid().ToString(),
                    Pk = testPartitionId,
                    Other = "Created by Stored Procedure"
                };

                using (ResponseMessage executeResponse =
                    await localContainer.Scripts.ExecuteStoredProcedureStreamAsync(
                        sprocId,
                        new PartitionKey(testPartitionId),
                        new dynamic[] { testItem }))
                {
                    Assert.AreEqual(HttpStatusCode.OK, executeResponse.StatusCode);
                    Assert.IsNotNull(executeResponse.Content);
                    string diagnostics = executeResponse.Diagnostics.ToString();
                    Assert.IsTrue(diagnostics.Contains("|F4"), "Diagnostics User Agent should contain '|F4' for ThinClient");
                }

                // Delete stored procedure
                await localContainer.Scripts.DeleteStoredProcedureAsync(sprocId);
            }
            finally
            {
                if (localDatabase != null)
                {
                    try
                    {
                        await localDatabase.DeleteAsync();
                    }
                    catch { }
                }

                if (localClient != null)
                {
                    localClient.Dispose();
                }
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task HttpRequestVersionIsTwoPointZeroWhenUsingThinClientMode()
        {
            Version expectedGatewayVersion = new(1, 1);
            Version expectedThinClientVersion = new(2, 0);

            List<Version> postRequestVersions = new();

            CosmosClientOptions options = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Gateway,
                SendingRequestEventArgs = (sender, e) =>
                {
                    if (e.HttpRequest.Method == HttpMethod.Post
                        && !string.Equals(e.HttpRequest.RequestUri?.AbsolutePath, "/connectivity-probe", StringComparison.OrdinalIgnoreCase))
                    {
                        // Ignore the thin-client connectivity-probe POSTs (POST /connectivity-probe over HTTP/2),
                        // which the SDK fires after topology refresh when HTTP/2 is enabled; this test asserts the
                        // request versions of the DB / Container / Item control- and data-plane POSTs only.
                        postRequestVersions.Add(e.HttpRequest.Version);
                    }
                },
            };

            using CosmosClient client = new CosmosClient(this.connectionString, options);

            string dbId = "HttpVersionTestDb_" + Guid.NewGuid();
            Cosmos.Database database = await client.CreateDatabaseIfNotExistsAsync(dbId);
            Container container = await database.CreateContainerIfNotExistsAsync("HttpVersionTestContainer", "/pk");

            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();

            ItemResponse<ToDoActivity> response = await container.CreateItemAsync(testItem, new Cosmos.PartitionKey(testItem.pk));
            Assert.IsNotNull(response);

            Assert.AreEqual(3, postRequestVersions.Count, "Expected exactly 3 POST requests (DB, Container, Item).");

            Assert.AreEqual(expectedGatewayVersion, postRequestVersions[0], "Expected HTTP/1.1 for CreateDatabaseAsync.");
            Assert.AreEqual(expectedGatewayVersion, postRequestVersions[1], "Expected HTTP/1.1 for CreateContainerAsync.");
            Assert.AreEqual(expectedThinClientVersion, postRequestVersions[2], "Expected HTTP/2.0 for CreateItemAsync.");

            await database.DeleteAsync();
        }

        /// <summary>
        /// End-to-end happy path against an account that advertises thin-client (Gateway V2) proxy
        /// regional endpoints and whose connectivity probe (<c>POST /connectivity-probe</c> over HTTP/2)
        /// succeeds for every endpoint. With HTTP/2 opted in, the SDK must route data-plane requests
        /// through the proxy. Routing is gated by <c>ThinClientStoreModel.IsThinClientRoutable</c> (the live
        /// topology signal — the account returned proxy endpoints) AND the per-region connectivity-probe gate
        /// (<c>GlobalEndpointManager.IsProxyEndpointHealthy</c> against the request's resolved regional
        /// endpoint), so the presence of the <c>|F4</c> ThinClient
        /// user-agent flag on the diagnostics is proof that BOTH the proxy endpoints were returned and the
        /// probe came back healthy (HTTP 200). The HTTP/2.0 request version on the data-plane POST confirms
        /// the client and Gateway V2 successfully negotiated HTTP/2.
        /// </summary>
        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task EndToEnd_ProbeEnabledAccountWithHttp2_RoutesDataPlaneThroughThinClientProxy()
        {
            List<Version> dataPlanePostVersions = new();

            CosmosClient localClient = null;
            Database localDatabase = null;
            try
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "True");

                localClient = new CosmosClient(
                    this.connectionString,
                    new CosmosClientOptions()
                    {
                        ConnectionMode = ConnectionMode.Gateway,
                        ApplicationPreferredRegions = PreferredRegions,
                        Serializer = this.cosmosSystemTextJsonSerializer,
                        SendingRequestEventArgs = (sender, e) =>
                        {
                            if (e.HttpRequest.Method == HttpMethod.Post
                                && !string.Equals(e.HttpRequest.RequestUri?.AbsolutePath, "/connectivity-probe", StringComparison.OrdinalIgnoreCase))
                            {
                                // Exclude the connectivity-probe POSTs so dataPlanePostVersions reflects only real
                                // data-plane traffic, proving the item request itself negotiated HTTP/2 to the proxy.
                                dataPlanePostVersions.Add(e.HttpRequest.Version);
                            }
                        },
                    });

                string dbName = "TestDbProbeE2E_" + Guid.NewGuid().ToString();
                localDatabase = await localClient.CreateDatabaseIfNotExistsAsync(dbName);
                string containerName = "TestContainerProbeE2E_" + Guid.NewGuid().ToString();
                Container localContainer = await localDatabase.CreateContainerIfNotExistsAsync(containerName, "/pk");

                // A freshly created container is not immediately available for data-plane reads on the gateway
                // (404/1013 "Collection is not yet available for read"). Mirror the other thin-client tests in
                // this class and let the routing/partition metadata propagate before issuing item operations.
                await Task.Delay(TimeSpan.FromSeconds(30));

                TestObject item = new TestObject
                {
                    Id = Guid.NewGuid().ToString(),
                    Pk = "pk_probe_e2e_" + Guid.NewGuid().ToString(),
                    Other = "probe end-to-end"
                };

                // Create routes through the proxy: |F4 proves proxy endpoints were returned AND the probe was green.
                ItemResponse<TestObject> createResponse = await localContainer.CreateItemAsync(item, new PartitionKey(item.Pk));
                Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
                string createDiagnostics = createResponse.Diagnostics.ToString();
                Assert.IsTrue(
                    createDiagnostics.Contains("|F4"),
                    $"Create must route via the ThinClient proxy when the account returns proxy endpoints and the probe is healthy. Diagnostics:\n{createDiagnostics}");

                // Read must also route through the proxy.
                ItemResponse<TestObject> readResponse = await localContainer.ReadItemAsync<TestObject>(item.Id, new PartitionKey(item.Pk));
                Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
                Assert.AreEqual(item.Id, readResponse.Resource.Id);
                string readDiagnostics = readResponse.Diagnostics.ToString();
                Assert.IsTrue(
                    readDiagnostics.Contains("|F4"),
                    $"Read must route via the ThinClient proxy. Diagnostics:\n{readDiagnostics}");

                // At least one data-plane POST must have been sent over HTTP/2.0 to the proxy, confirming the
                // client <-> Gateway V2 HTTP/2 negotiation succeeded (the same prerequisite the probe enforces).
                Assert.IsTrue(
                    dataPlanePostVersions.Any(v => v == new Version(2, 0)),
                    "A data-plane POST must be sent over HTTP/2.0 when routed through the ThinClient proxy.");
            }
            finally
            {
                if (localDatabase != null)
                {
                    try
                    {
                        await localDatabase.DeleteAsync();
                    }
                    catch
                    {
                    }
                }

                localClient?.Dispose();
            }
        }

        /// <summary>
        /// A region advertises a Gateway V2 (proxy) endpoint, but its connectivity probe is forced to fail
        /// (simulating a region that does not yet support <c>POST /connectivity-probe</c>). The other region
        /// probes green. This proves the per-region probe gate: requests resolved to the healthy region route
        /// through the proxy (port 10250), while requests resolved to the un-probeable region fall back to
        /// Gateway V1 (port 443) and NEVER target that region's proxy port.
        /// </summary>
        [TestMethod]
        [TestCategory("ThinClient")]
        [Owner("aavasthy")]
        public async Task EndToEnd_RegionWithoutConnectivityProbe_FallsBackToGatewayForThatRegion()
        {
            string failedRegionFragment = RegionHostFragment(EastUs2);
            string healthyRegionFragment = RegionHostFragment(CentralUs);

            ConnectivityProbeFailingHandler probeHandler = new ConnectivityProbeFailingHandler(failedRegionFragment);

            CosmosClient localClient = null;
            Database localDatabase = null;
            try
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "True");

                localClient = new CosmosClient(
                    this.connectionString,
                    new CosmosClientOptions
                    {
                        ConnectionMode = ConnectionMode.Gateway,
                        ApplicationPreferredRegions = PreferredRegions,
                        Serializer = this.cosmosSystemTextJsonSerializer,
                        HttpClientFactory = () => new HttpClient(probeHandler),
                    });

                string dbName = "TestDbProbeRed_" + Guid.NewGuid().ToString();
                localDatabase = await localClient.CreateDatabaseIfNotExistsAsync(dbName);
                string containerName = "TestContainerProbeRed_" + Guid.NewGuid().ToString();
                Container localContainer = await localDatabase.CreateContainerIfNotExistsAsync(containerName, "/pk");

                TestObject item = new TestObject
                {
                    Id = Guid.NewGuid().ToString(),
                    Pk = "pk_probe_red_" + Guid.NewGuid().ToString(),
                    Other = "probe red region"
                };

                // Seed the item and let the (fire-and-forget) probe cycle run against both regional endpoints.
                await WarmUpThinClientProbeAsync(localContainer, item);

                // Read steered to the healthy region (Central US) by excluding the un-probeable region.
                await ReadItemWithRegionRetryAsync(localContainer, item, new List<string> { EastUs2 });

                // Read steered to the un-probeable region (East US 2) by excluding the healthy region.
                ItemResponse<TestObject> redRegionRead = await ReadItemWithRegionRetryAsync(localContainer, item, new List<string> { CentralUs });
                Assert.AreEqual(
                    HttpStatusCode.OK,
                    redRegionRead.StatusCode,
                    "A read steered to the un-probeable region must still succeed via the Gateway V1 fallback.");

                bool healthyRegionUsedProxy = ProxyWasUsedForRegion(probeHandler, healthyRegionFragment);
                bool failedRegionUsedProxy = ProxyWasUsedForRegion(probeHandler, failedRegionFragment);

                if (!healthyRegionUsedProxy)
                {
                    Assert.Inconclusive(
                        "The thin-client proxy was never used for the healthy region (Central US). This happens when the " +
                        "client and Gateway V2 could not negotiate HTTP/2 in this environment, so the per-region probe gate " +
                        "cannot be observed.");
                }

                Assert.IsFalse(
                    failedRegionUsedProxy,
                    $"A region whose connectivity probe fails ('{EastUs2}') must NEVER receive data-plane traffic on the proxy " +
                    $"port {ThinClientProxyPort}; it must fall back to Gateway V1. Proxy hosts observed: " +
                    string.Join(", ", ProxyRequestHosts(probeHandler)));
            }
            finally
            {
                if (localDatabase != null)
                {
                    try
                    {
                        await localDatabase.DeleteAsync();
                    }
                    catch
                    {
                    }
                }

                localClient?.Dispose();
            }
        }

        /// <summary>
        /// One region's connectivity probe is forced to fail (a region without probe support); the other probes
        /// green. While requests target the un-probeable region they fall back to Gateway V1 (port 443). Once that
        /// region is removed from consideration (so requests resolve to the healthy region), routing returns to the
        /// Gateway V2 proxy (port 10250).
        /// </summary>
        [TestMethod]
        [TestCategory("ThinClient")]
        [Owner("aavasthy")]
        public async Task EndToEnd_RemovingRegionWithoutConnectivityProbe_RestoresThinClientRouting()
        {
            string failedRegionFragment = RegionHostFragment(EastUs2);
            string healthyRegionFragment = RegionHostFragment(CentralUs);

            ConnectivityProbeFailingHandler probeHandler = new ConnectivityProbeFailingHandler(failedRegionFragment);

            CosmosClient localClient = null;
            Database localDatabase = null;
            try
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "True");

                localClient = new CosmosClient(
                    this.connectionString,
                    new CosmosClientOptions
                    {
                        ConnectionMode = ConnectionMode.Gateway,
                        ApplicationPreferredRegions = PreferredRegions,
                        Serializer = this.cosmosSystemTextJsonSerializer,
                        HttpClientFactory = () => new HttpClient(probeHandler),
                    });

                string dbName = "TestDbProbeRemove_" + Guid.NewGuid().ToString();
                localDatabase = await localClient.CreateDatabaseIfNotExistsAsync(dbName);
                string containerName = "TestContainerProbeRemove_" + Guid.NewGuid().ToString();
                Container localContainer = await localDatabase.CreateContainerIfNotExistsAsync(containerName, "/pk");

                TestObject item = new TestObject
                {
                    Id = Guid.NewGuid().ToString(),
                    Pk = "pk_probe_remove_" + Guid.NewGuid().ToString(),
                    Other = "probe remove region"
                };

                await WarmUpThinClientProbeAsync(localContainer, item);

                // Phase 1: target the un-probeable region (exclude the healthy region) -> must fall back to Gateway V1.
                ItemResponse<TestObject> phase1Read = await ReadItemWithRegionRetryAsync(localContainer, item, new List<string> { CentralUs });
                Assert.AreEqual(
                    HttpStatusCode.OK,
                    phase1Read.StatusCode,
                    "Phase 1: a read steered to the un-probeable region must succeed via the Gateway V1 fallback.");
                Assert.IsFalse(
                    ProxyWasUsedForRegion(probeHandler, failedRegionFragment),
                    $"Phase 1: the un-probeable region '{EastUs2}' must not receive data-plane traffic on the proxy port {ThinClientProxyPort}.");

                // Phase 2: remove the un-probeable region from consideration (exclude it) -> resolves to the healthy region.
                await ReadItemWithRegionRetryAsync(localContainer, item, new List<string> { EastUs2 });

                bool healthyRegionUsedProxy = ProxyWasUsedForRegion(probeHandler, healthyRegionFragment);
                if (!healthyRegionUsedProxy)
                {
                    Assert.Inconclusive(
                        "After removing the un-probeable region, the healthy region (Central US) did not route via the proxy. " +
                        "This happens when the client and Gateway V2 could not negotiate HTTP/2 in this environment.");
                }

                Assert.IsTrue(
                    healthyRegionUsedProxy,
                    $"After removing the un-probeable region, reads should route back to the Gateway V2 proxy (port {ThinClientProxyPort}) for the healthy region.");
                Assert.IsFalse(
                    ProxyWasUsedForRegion(probeHandler, failedRegionFragment),
                    $"The un-probeable region '{EastUs2}' must never receive proxy traffic at any point.");
            }
            finally
            {
                if (localDatabase != null)
                {
                    try
                    {
                        await localDatabase.DeleteAsync();
                    }
                    catch
                    {
                    }
                }

                localClient?.Dispose();
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task CreateItemsTest()
        {
            string pk = "pk_create";
            IEnumerable<TestObject> items = this.GenerateItems(pk);
            await Task.Delay(TimeSpan.FromSeconds(30));
            foreach (TestObject item in items)
            {
                ItemResponse<TestObject> response = await this.container.CreateItemAsync(item, new PartitionKey(item.Pk));
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                string diagnostics = response.Diagnostics.ToString();
                Assert.IsTrue(diagnostics.Contains("|F4"), "Diagnostics User Agent should contain '|F4' for ThinClient");
                AssertExcludedRegionsNotInDiagnostics(diagnostics);
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task CreateItemsTestWithThinClientFlagEnabledAndAccountDisabled()
        {
            CosmosClient localClient = null;
            Database localDatabase = null;
            Container localContainer = null;
            ConcurrentBag<int> requestPorts = new ConcurrentBag<int>();

            try
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "True");
                string authKey = Utils.ConfigurationManager.AppSettings["MasterKey"];
                string endpoint = Utils.ConfigurationManager.AppSettings["GatewayEndpoint"];
                AzureKeyCredential masterKeyCredential = new AzureKeyCredential(authKey);

                JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null,
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };
                CosmosSystemTextJsonSerializer localSerializer = new MultiRegionSetupHelpers.CosmosSystemTextJsonSerializer(jsonSerializerOptions);

                localClient = new CosmosClient(
                      endpoint,
                      masterKeyCredential,
                      new CosmosClientOptions()
                      {
                          ConnectionMode = ConnectionMode.Gateway,
                          Serializer = localSerializer,
                          SendingRequestEventArgs = (sender, e) =>
                          {
                              if (e.HttpRequest.RequestUri != null)
                              {
                                  requestPorts.Add(e.HttpRequest.RequestUri.Port);
                              }
                          },
                      });

                string uniqueDbName = "TestDb2_" + Guid.NewGuid().ToString();
                localDatabase = await localClient.CreateDatabaseIfNotExistsAsync(uniqueDbName);
                string uniqueContainerName = "TestContainer2_" + Guid.NewGuid().ToString();
                localContainer = await localDatabase.CreateContainerIfNotExistsAsync(uniqueContainerName, "/pk");

                string pk = "pk_create";
                IEnumerable<TestObject> items = this.GenerateItems(pk);

                foreach (TestObject item in items)
                {
                    ItemResponse<TestObject> response = await localContainer.CreateItemAsync(item, new PartitionKey(item.Pk));
                    Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                    Assert.IsFalse(
                        response.Diagnostics.ToString().Contains("|F4"),
                        "User agent must not advertise the ThinClient capability (|F4) when the account does not advertise thin-client endpoints.");
                }

                // The account does not advertise thin-client (proxy) endpoints, so no request - data-plane or
                // connectivity-probe - must ever target the proxy port (10250). All traffic stays on Gateway V1.
                Assert.IsFalse(
                    requestPorts.Contains(ThinClientProxyPort),
                    $"No request should target the ThinClient proxy port {ThinClientProxyPort} when the account does not advertise thin-client endpoints. Observed ports: {string.Join(", ", requestPorts.Distinct())}");
            }
            finally
            {
                if (localDatabase != null)
                {
                    try
                    {
                        await localDatabase.DeleteAsync();
                    }
                    catch { }
                }

                if (localClient != null)
                {
                    localClient.Dispose();
                }
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task CreateItemsTestWithDirectMode_ThinClientFlagEnabledAndAccountEnabled()
        {
            CosmosClient localClient = null;
            Database localDatabase = null;
            Container localContainer = null;

            try
            {
                JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null,
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };
                CosmosSystemTextJsonSerializer localSerializer = new MultiRegionSetupHelpers.CosmosSystemTextJsonSerializer(jsonSerializerOptions);

                localClient = new CosmosClient(
                      this.connectionString,
                      new CosmosClientOptions()
                      {
                          ConnectionMode = ConnectionMode.Direct,
                          Serializer = localSerializer,
                      });

                string uniqueDbName = "TestDb2_" + Guid.NewGuid().ToString();
                localDatabase = await localClient.CreateDatabaseIfNotExistsAsync(uniqueDbName);
                string uniqueContainerName = "TestContainer2_" + Guid.NewGuid().ToString();
                localContainer = await localDatabase.CreateContainerIfNotExistsAsync(uniqueContainerName, "/pk");

                string pk = "pk_create";
                IEnumerable<TestObject> items = this.GenerateItems(pk);

                foreach (TestObject item in items)
                {
                    ItemResponse<TestObject> response = await localContainer.CreateItemAsync(item, new PartitionKey(item.Pk));
                    Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                    JsonDocument doc = JsonDocument.Parse(response.Diagnostics.ToString());
                    string connectionMode = doc.RootElement
                        .GetProperty("data")
                        .GetProperty("Client Configuration")
                        .GetProperty("ConnectionMode")
                        .GetString();

                    Assert.AreEqual("Direct", connectionMode, "Diagnostics should have ConnectionMode set to 'Direct'");
                }
            }
            finally
            {
                if (localDatabase != null)
                {
                    try
                    {
                        await localDatabase.DeleteAsync();
                    }
                    catch { }
                }

                if (localClient != null)
                {
                    localClient.Dispose();
                }
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task CreateItemsTestWithThinClientFlagDisabledAccountEnabled()
        {
            CosmosClient localClient = null;
            Database localDatabase = null;
            Container localContainer = null;

            try
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "False");

                JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null,
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };
                CosmosSystemTextJsonSerializer localSerializer = new MultiRegionSetupHelpers.CosmosSystemTextJsonSerializer(jsonSerializerOptions);

                localClient = new CosmosClient(
                      this.connectionString,
                      new CosmosClientOptions()
                      {
                          ConnectionMode = ConnectionMode.Gateway,
                          Serializer = localSerializer,
                      });

                string uniqueDbName = "TestDbTCDisabled_" + Guid.NewGuid().ToString();
                localDatabase = await localClient.CreateDatabaseIfNotExistsAsync(uniqueDbName);
                string uniqueContainerName = "TestContainerTCDisabled_" + Guid.NewGuid().ToString();
                localContainer = await localDatabase.CreateContainerIfNotExistsAsync(uniqueContainerName, "/pk");

                string pk = "pk_create";
                IEnumerable<TestObject> items = this.GenerateItems(pk);

                foreach (TestObject item in items)
                {
                    ItemResponse<TestObject> response = await localContainer.CreateItemAsync(item, new PartitionKey(item.Pk));
                    Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                    string diagnostics = response.Diagnostics.ToString();
                    Assert.IsFalse(diagnostics.Contains("|F4"), "Diagnostics User Agent should NOT contain '|F4' for Gateway");
                }
            }
            finally
            {
                if (localDatabase != null)
                {
                    try
                    {
                        await localDatabase.DeleteAsync();
                    }
                    catch { }
                }

                if (localClient != null)
                {
                    localClient.Dispose();
                }
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task ReadItemsTest()
        {
            string pk = "pk_read";
            List<TestObject> items = this.GenerateItems(pk).ToList();

            List<TestObject> createdItems = await this.CreateItemsSafeAsync(items);

            foreach (TestObject item in createdItems)
            {
                ItemResponse<TestObject> response = await this.container.ReadItemAsync<TestObject>(item.Id, new PartitionKey(item.Pk));
                string diagnostics = response.Diagnostics.ToString();
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual(item.Id, response.Resource.Id);
                Assert.IsTrue(diagnostics.Contains("|F4"), "Diagnostics User Agent should contain '|F4' for ThinClient");
                AssertExcludedRegionsNotInDiagnostics(diagnostics);
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task ReadItem_WithHedgingAndExcludeRegions_OnThinClient_Succeeds()
        {
            CosmosClientOptions hedgingClientOptions = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                ApplicationPreferredRegions = PreferredRegions,
                Serializer = this.cosmosSystemTextJsonSerializer,
                AvailabilityStrategy = AvailabilityStrategy.CrossRegionHedgingStrategy(
                    threshold: TimeSpan.FromMilliseconds(100),
                    thresholdStep: TimeSpan.FromMilliseconds(50)),
            };

            using CosmosClient hedgingClient = new CosmosClient(this.connectionString, hedgingClientOptions);
            Container hedgingContainer = hedgingClient.GetContainer(this.database.Id, this.container.Id);
            TestObject seed = new TestObject
            {
                Id = Guid.NewGuid().ToString(),
                Pk = "pk_hedging",
                Other = "hedging composition fixture",
            };

            // The first operation on this freshly-created client triggers lazy initialization, which wires and
            // fires the connectivity probe (fire-and-forget). Thin-client routing only engages once the probe has
            // cached the regional endpoints as healthy, so use this create as a warm-up (resilient to the
            // freshly-created container's propagation delay) and then wait for the probe before the asserted read.
            await WarmUpThinClientProbeAsync(hedgingContainer, seed);

            ItemResponse<TestObject> readResponse = await hedgingContainer.ReadItemAsync<TestObject>(
                seed.Id,
                new PartitionKey(seed.Pk),
                new ItemRequestOptions
                {
                    ExcludeRegions = new List<string>(ExcludeRegions),
                });

            string diagnostics = readResponse.Diagnostics.ToString();
            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
            Assert.AreEqual(seed.Id, readResponse.Resource.Id);
            Assert.IsTrue(
                diagnostics.Contains("|F4"),
                "Read should route through the thin client pipeline (|F4 user agent token).");
            Assert.IsTrue(
                diagnostics.Contains($"\"Hedge Context\":[\"{EastUs2}\"]"),
                $"Diagnostics should contain Hedge Context with only the non-excluded preferred region ('{EastUs2}'). Diagnostics: {diagnostics}");
            AssertExcludedRegionsNotInDiagnostics(diagnostics);
        }

        /// <summary>
        /// When every preferred region is excluded,
        /// <see cref="LocationCache.ResolveThinClientEndpoint"/> falls back to the primary thin
        /// client write endpoint instead of failing the request. The operation must succeed and
        /// route through the thin client pipeline.
        /// </summary>
        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task CreateItem_WithAllPreferredRegionsExcluded_OnThinClient_FallsBackToPrimaryWriteRegion()
        {
            List<string> allPreferredRegionsExcluded = new List<string>(PreferredRegions);

            CosmosClientOptions fallbackClientOptions = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                ApplicationPreferredRegions = PreferredRegions,
                Serializer = this.cosmosSystemTextJsonSerializer,
            };

            using CosmosClient fallbackClient = new CosmosClient(this.connectionString, fallbackClientOptions);
            Container fallbackContainer = fallbackClient.GetContainer(this.database.Id, this.container.Id);

            // The first operation on this freshly-created client triggers lazy initialization, which wires and
            // fires the connectivity probe (fire-and-forget). Thin-client routing only engages once the probe has
            // cached the regional endpoint as healthy, so issue a warm-up write (resilient to the freshly-created
            // container's propagation delay) and then wait for the probe before the asserted all-excluded create.
            TestObject warmup = new TestObject
            {
                Id = Guid.NewGuid().ToString(),
                Pk = "pk_fallback_warmup",
                Other = "fallback warm-up fixture",
            };
            await WarmUpThinClientProbeAsync(fallbackContainer, warmup);

            TestObject seed = new TestObject
            {
                Id = Guid.NewGuid().ToString(),
                Pk = "pk_fallback",
                Other = "all-preferred-excluded fallback fixture",
            };

            ItemResponse<TestObject> createResponse = await fallbackContainer.CreateItemAsync(
                seed,
                new PartitionKey(seed.Pk),
                new ItemRequestOptions
                {
                    ExcludeRegions = allPreferredRegionsExcluded,
                });

            string diagnostics = createResponse.Diagnostics.ToString();
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.AreEqual(seed.Id, createResponse.Resource.Id);
            Assert.IsTrue(
                diagnostics.Contains("|F4"),
                "Create should route through the thin client pipeline (|F4 user agent token).");
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task ReplaceItemsTest()
        {
            string pk = "pk_replace";
            List<TestObject> items = this.GenerateItems(pk).ToList();

            List<TestObject> createdItems = await this.CreateItemsSafeAsync(items);

            foreach (TestObject item in createdItems)
            {
                TestObject updatedItem = new TestObject
                {
                    Id = item.Id,
                    Pk = item.Pk,
                    Other = "Updated " + item.Other
                };

                ItemResponse<TestObject> response = await this.container.ReplaceItemAsync(updatedItem, updatedItem.Id, new PartitionKey(updatedItem.Pk));
                string diagnostics = response.Diagnostics.ToString();
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual("Updated " + item.Other, response.Resource.Other);
                Assert.IsTrue(diagnostics.Contains("|F4"), "Diagnostics User Agent should contain '|F4' for ThinClient");
                AssertExcludedRegionsNotInDiagnostics(diagnostics);
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task UpsertItemsTest()
        {
            string pk = "pk_upsert";
            IEnumerable<TestObject> items = this.GenerateItems(pk);

            await Task.Delay(TimeSpan.FromSeconds(30));
            foreach (TestObject item in items)
            {
                ItemResponse<TestObject> response = await this.container.UpsertItemAsync(item, new PartitionKey(item.Pk));
                string diagnostics = response.Diagnostics.ToString();
                Assert.IsTrue(response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK);
                Assert.IsTrue(diagnostics.Contains("|F4"), "Diagnostics User Agent should contain '|F4' for ThinClient");
                AssertExcludedRegionsNotInDiagnostics(diagnostics);
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task DeleteItemsTest()
        {
            string pk = "pk_delete";
            List<TestObject> items = this.GenerateItems(pk).ToList();

            List<TestObject> createdItems = await this.CreateItemsSafeAsync(items);

            foreach (TestObject item in createdItems)
            {
                ItemResponse<TestObject> response = await this.container.DeleteItemAsync<TestObject>(item.Id, new PartitionKey(item.Pk));
                string diagnostics = response.Diagnostics.ToString();
                Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
                Assert.IsTrue(diagnostics.Contains("|F4"), "Diagnostics User Agent should contain '|F4' for ThinClient");
                AssertExcludedRegionsNotInDiagnostics(diagnostics);
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task CreateItemStreamTest()
        {
            string pk = "pk_create_stream";
            IEnumerable<TestObject> items = this.GenerateItems(pk);
            await Task.Delay(TimeSpan.FromSeconds(30));
            foreach (TestObject item in items)
            {
                using (Stream stream = this.cosmosSystemTextJsonSerializer.ToStream(item))
                {
                    using (ResponseMessage response = await this.container.CreateItemStreamAsync(stream, new PartitionKey(item.Pk)))
                    {
                        string diagnostics = response.Diagnostics.ToString();
                        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                        Assert.IsTrue(diagnostics.Contains("|F4"), "Diagnostics User Agent should contain '|F4' for ThinClient");
                        AssertExcludedRegionsNotInDiagnostics(diagnostics);
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task ReadItemStreamTest()
        {
            string pk = "pk_read_stream";
            List<TestObject> items = this.GenerateItems(pk).ToList();

            List<TestObject> createdItems = await this.CreateItemsSafeAsync(items);

            foreach (TestObject item in createdItems)
            {
                using (ResponseMessage response = await this.container.ReadItemStreamAsync(item.Id, new PartitionKey(item.Pk)))
                {
                    string diagnostics = response.Diagnostics.ToString();
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                    Assert.IsTrue(diagnostics.Contains("|F4"), "Diagnostics User Agent should contain '|F4' for ThinClient");
                    AssertExcludedRegionsNotInDiagnostics(diagnostics);
                }
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task ReplaceItemStreamTest()
        {
            string pk = "pk_replace_stream";
            List<TestObject> items = this.GenerateItems(pk).ToList();

            List<TestObject> createdItems = await this.CreateItemsSafeAsync(items);

            foreach (TestObject item in createdItems)
            {
                TestObject updatedItem = new TestObject
                {
                    Id = item.Id,
                    Pk = item.Pk,
                    Other = "Updated " + item.Other
                };

                using (Stream stream = this.cosmosSystemTextJsonSerializer.ToStream(updatedItem))
                {
                    using (ResponseMessage response = await this.container.ReplaceItemStreamAsync(stream, updatedItem.Id, new PartitionKey(updatedItem.Pk)))
                    {
                        string diagnostics = response.Diagnostics.ToString();
                        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                        Assert.IsTrue(diagnostics.Contains("|F4"), "Diagnostics User Agent should contain '|F4' for ThinClient");
                        AssertExcludedRegionsNotInDiagnostics(diagnostics);
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task UpsertItemStreamTest()
        {
            string pk = "pk_upsert_stream";
            IEnumerable<TestObject> items = this.GenerateItems(pk);
            await Task.Delay(TimeSpan.FromSeconds(30));
            foreach (TestObject item in items)
            {
                using (Stream stream = this.cosmosSystemTextJsonSerializer.ToStream(item))
                {
                    using (ResponseMessage response = await this.container.UpsertItemStreamAsync(stream, new PartitionKey(item.Pk)))
                    {
                        string diagnostics = response.Diagnostics.ToString();
                        Assert.IsTrue(response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK);
                        Assert.IsTrue(diagnostics.Contains("|F4"), "Diagnostics User Agent should contain '|F4' for ThinClient");
                        AssertExcludedRegionsNotInDiagnostics(diagnostics);
                    }
                }
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task DeleteItemStreamTest()
        {
            string pk = "pk_delete_stream";
            List<TestObject> items = this.GenerateItems(pk).ToList();

            List<TestObject> createdItems = await this.CreateItemsSafeAsync(items);

            foreach (TestObject item in createdItems)
            {
                using (ResponseMessage response = await this.container.DeleteItemStreamAsync(item.Id, new PartitionKey(item.Pk)))
                {
                    string diagnostics = response.Diagnostics.ToString();
                    Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
                    Assert.IsTrue(diagnostics.Contains("|F4"), "Diagnostics User Agent should contain '|F4' for ThinClient");
                    AssertExcludedRegionsNotInDiagnostics(diagnostics);
                }
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task QueryItemsTest()
        {
            string pk = "pk_query";
            List<TestObject> items = this.GenerateItems(pk).ToList();

            List<TestObject> createdItems = await this.CreateItemsSafeAsync(items);

            string query = $"SELECT * FROM c WHERE c.pk = '{pk}'";
            FeedIterator<TestObject> iterator = this.container.GetItemQueryIterator<TestObject>(query);

            int count = 0;
            while (iterator.HasMoreResults)
            {
                FeedResponse<TestObject> response = await iterator.ReadNextAsync();
                count += response.Count;
                AssertExcludedRegionsNotInDiagnostics(response.Diagnostics.ToString());
            }

            Assert.AreEqual(createdItems.Count, count);
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task QueryItemsTestWithStrongConsistency()
        {
            CosmosClient localClient = null;
            Database localDatabase = null;

            try
            {
                string connectionString = ConfigurationManager.GetEnvironmentVariable<string>("COSMOSDB_THINCLIENTSTRONG", string.Empty);
                if (string.IsNullOrEmpty(connectionString))
                {
                    Assert.Fail("Set environment variable COSMOSDB_THINCLIENTSTRONG to run the tests");
                }

                localClient = new CosmosClient(
                     connectionString,
                     new CosmosClientOptions()
                     {
                         ConnectionMode = ConnectionMode.Gateway,
                         RequestTimeout = TimeSpan.FromSeconds(60),
                         ConsistencyLevel = Microsoft.Azure.Cosmos.ConsistencyLevel.Strong,
                     });

                string uniqueDbName = "TestDbTC_" + Guid.NewGuid().ToString();
                localDatabase = await localClient.CreateDatabaseIfNotExistsAsync(uniqueDbName);
                string uniqueContainerName = "TestContainerTC_" + Guid.NewGuid().ToString();
                Container localContainer = await localDatabase.CreateContainerIfNotExistsAsync(uniqueContainerName, "/pk");

                string pk = "pk_query";
                List<TestObject> items = this.GenerateItems(pk).ToList();

                List<TestObject> itemsCreated = new List<TestObject>();
                foreach (TestObject item in items)
                {
                    try
                    {
                        ItemResponse<TestObject> response = await localContainer.CreateItemAsync(item, new PartitionKey(item.Pk));
                        if (response.StatusCode == HttpStatusCode.Created)
                        {
                            itemsCreated.Add(item);
                        }
                    }
                    catch (CosmosException)
                    {
                    }
                }

                string query = $"SELECT * FROM c WHERE c.pk = '{pk}'";
                FeedIterator<TestObject> iterator = localContainer.GetItemQueryIterator<TestObject>(query);

                int count = 0;
                while (iterator.HasMoreResults)
                {
                    FeedResponse<TestObject> response = await iterator.ReadNextAsync();
                    count += response.Count;
                }

                Assert.AreEqual(itemsCreated.Count, count);
            }
            finally
            {
                if (localDatabase != null)
                {
                    try
                    {
                        await localDatabase.DeleteAsync();
                    }
                    catch { }
                }

                if (localClient != null)
                {
                    localClient.Dispose();
                }
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task QueryItemsTestWithSessionConsistency()
        {
            CosmosClient localClient = null;
            Database localDatabase = null;

            try
            {
                localClient = new CosmosClient(
                     this.connectionString,
                     new CosmosClientOptions()
                     {
                         ConnectionMode = ConnectionMode.Gateway,
                         RequestTimeout = TimeSpan.FromSeconds(60),
                         ConsistencyLevel = Microsoft.Azure.Cosmos.ConsistencyLevel.Session,
                     });

                string uniqueDbName = "TestDbTC_" + Guid.NewGuid().ToString();
                localDatabase = await localClient.CreateDatabaseIfNotExistsAsync(uniqueDbName);
                string uniqueContainerName = "TestContainerTC_" + Guid.NewGuid().ToString();
                Container localContainer = await localDatabase.CreateContainerIfNotExistsAsync(uniqueContainerName, "/pk");

                string pk = "pk_query";
                List<TestObject> items = this.GenerateItems(pk).ToList();

                List<TestObject> itemsCreated = new List<TestObject>();
                foreach (TestObject item in items)
                {
                    try
                    {
                        ItemResponse<TestObject> response = await localContainer.CreateItemAsync(item, new PartitionKey(item.Pk));
                        if (response.StatusCode == HttpStatusCode.Created)
                        {
                            itemsCreated.Add(item);
                        }
                    }
                    catch (CosmosException)
                    {
                    }
                }

                string query = $"SELECT * FROM c WHERE c.pk = '{pk}'";
                FeedIterator<TestObject> iterator = localContainer.GetItemQueryIterator<TestObject>(query);

                int count = 0;
                while (iterator.HasMoreResults)
                {
                    FeedResponse<TestObject> response = await iterator.ReadNextAsync();
                    count += response.Count;
                }

                Assert.AreEqual(itemsCreated.Count, count);
            }
            finally
            {
                if (localDatabase != null)
                {
                    try
                    {
                        await localDatabase.DeleteAsync();
                    }
                    catch { }
                }

                if (localClient != null)
                {
                    localClient.Dispose();
                }
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task QueryItemsStreamTest()
        {
            string pk = "pk_query_stream";
            List<TestObject> items = this.GenerateItems(pk).ToList();

            List<TestObject> createdItems = await this.CreateItemsSafeAsync(items);

            QueryDefinition query = new QueryDefinition("SELECT * FROM c WHERE c.pk = @pk").WithParameter("@pk", pk);
            FeedIterator iterator = this.container.GetItemQueryStreamIterator(query);

            int count = 0;
            while (iterator.HasMoreResults)
            {
                using (ResponseMessage response = await iterator.ReadNextAsync())
                {
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                    AssertExcludedRegionsNotInDiagnostics(response.Diagnostics.ToString());

                    using (StreamReader reader = new StreamReader(response.Content))
                    {
                        string json = await reader.ReadToEndAsync();
                        using (JsonDocument doc = JsonDocument.Parse(json))
                        {
                            count += doc.RootElement.GetProperty("Documents").GetArrayLength();
                        }
                    }
                }
            }

            Assert.AreEqual(createdItems.Count, count);
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task BulkCreateItemsTest()
        {
            CosmosClientOptions bulkOptions = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                ApplicationPreferredRegions = PreferredRegions,
                Serializer = this.cosmosSystemTextJsonSerializer,
                AllowBulkExecution = true,
            };
            bulkOptions.CustomHandlers.Add(new ExcludeRegionsInjectingHandler(ExcludeRegions));

            CosmosClient bulkClient = new CosmosClient(this.connectionString, bulkOptions);

            string pk = "pk_bulk";
            List<TestObject> items = this.GenerateItems(pk).ToList();
            List<Task<ItemResponse<TestObject>>> tasks = new List<Task<ItemResponse<TestObject>>>();

            Container bulkContainer = bulkClient.GetContainer(this.database.Id, this.container.Id);
            await Task.Delay(TimeSpan.FromSeconds(30));
            foreach (TestObject item in items)
            {
                tasks.Add(bulkContainer.CreateItemAsync(item, new PartitionKey(item.Pk)));
            }

            await Task.WhenAll(tasks);

            foreach (Task<ItemResponse<TestObject>> task in tasks)
            {
                Assert.AreEqual(HttpStatusCode.Created, task.Result.StatusCode);
                AssertExcludedRegionsNotInDiagnostics(task.Result.Diagnostics.ToString());
            }

            bulkClient.Dispose();
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task TransactionalBatchCreateItemsTest()
        {
            string pk = "pk_batch";
            List<TestObject> items = this.GenerateItems(pk).Take(100).ToList();
            await Task.Delay(TimeSpan.FromSeconds(30));
            TransactionalBatch batch = this.container.CreateTransactionalBatch(new PartitionKey(pk));

            foreach (TestObject item in items)
            {
                batch.CreateItem(item);
            }

            TransactionalBatchResponse batchResponse = await batch.ExecuteAsync();
            Assert.AreEqual(HttpStatusCode.OK, batchResponse.StatusCode);
            AssertExcludedRegionsNotInDiagnostics(batchResponse.Diagnostics.ToString());

            for (int i = 0; i < items.Count; i++)
            {
                Assert.AreEqual(HttpStatusCode.Created, batchResponse[i].StatusCode);
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task TestQueryPlanWithOrderBy_GatewayMode()
        {
            // Removing thinclient support for queryplan
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "False");
            List<TestObject> items = new List<TestObject>();
            string commonPk = "pk_orderby_test_" + Guid.NewGuid().ToString();

            try
            {
                // Create a fresh client that honors the disabled ThinClient flag
                using CosmosClient queryPlanClient = new CosmosClient(
                    this.connectionString,
                    new CosmosClientOptions()
                    {
                        ConnectionMode = ConnectionMode.Gateway,
                        ApplicationPreferredRegions = PreferredRegions,
                        Serializer = this.cosmosSystemTextJsonSerializer,
                    });

                Container queryPlanContainer = queryPlanClient.GetContainer(this.database.Id, this.container.Id);

                for (int i = 0; i < 5; i++)
                {
                    items.Add(new TestObject
                    {
                        Id = Guid.NewGuid().ToString(),
                        Pk = commonPk,
                        Other = $"Item_{i:D3}",
                    });
                }

                List<TestObject> createdItems = await this.CreateItemsSafeAsync(items);
                Assert.AreEqual(5, createdItems.Count, "All items should be created");

                // Execute ORDER BY query - this requires QueryPlan and EPK range conversion
                string query = "SELECT * FROM c WHERE c.pk = @pk ORDER BY c.other DESC";
                QueryDefinition queryDef = new QueryDefinition(query).WithParameter("@pk", commonPk);

                FeedIterator<TestObject> iterator = queryPlanContainer.GetItemQueryIterator<TestObject>(queryDef);

                List<TestObject> results = new List<TestObject>();
                int pageCount = 0;

                while (iterator.HasMoreResults)
                {
                    FeedResponse<TestObject> response = await iterator.ReadNextAsync();
                    results.AddRange(response);
                    pageCount++;

                    string diagnostics = response.Diagnostics.ToString();
                    Assert.IsFalse(diagnostics.Contains("ThinClientStoreModel"), $"Page {pageCount}: Should NOT use ThinClient");
                }

                Assert.AreEqual(5, results.Count, "Should return all 5 items");

            }
            finally
            {

                foreach (TestObject item in items)
                {
                    try
                    {
                        await this.container.DeleteItemAsync<TestObject>(item.Id, new PartitionKey(item.Pk));
                    }
                    catch { }
                }
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task TestQueryPlanCrossPartitionWithFilter_GatewayMode()
        {
            // Removing thinclient support for queryplan
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "False");
            List<TestObject> items = new List<TestObject>();
            string baseGuid = Guid.NewGuid().ToString();

            try
            {
                // Create a fresh client that honors the disabled ThinClient flag
                using CosmosClient queryPlanClient = new CosmosClient(
                    this.connectionString,
                    new CosmosClientOptions()
                    {
                        ConnectionMode = ConnectionMode.Gateway,
                        ApplicationPreferredRegions = PreferredRegions,
                        Serializer = this.cosmosSystemTextJsonSerializer,
                    });

                Container queryPlanContainer = queryPlanClient.GetContainer(this.database.Id, this.container.Id);

                string[] partitionKeys = {
                    $"pk_filter_1_{baseGuid}",
                    $"pk_filter_2_{baseGuid}",
                    $"pk_filter_3_{baseGuid}"
                };

                for (int pkIndex = 0; pkIndex < partitionKeys.Length; pkIndex++)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        items.Add(new TestObject
                        {
                            Id = Guid.NewGuid().ToString(),
                            Pk = partitionKeys[pkIndex],
                            Other = $"Value_{i}",
                        });
                    }
                }

                List<TestObject> createdItems = await this.CreateItemsSafeAsync(items);
                Assert.AreEqual(9, createdItems.Count, "All 9 items should be created");

                string query = "SELECT * FROM c ORDER BY c._ts";

                FeedIterator<TestObject> iterator = queryPlanContainer.GetItemQueryIterator<TestObject>(query);

                List<TestObject> results = new List<TestObject>();
                int pageCount = 0;

                while (iterator.HasMoreResults)
                {
                    FeedResponse<TestObject> response = await iterator.ReadNextAsync();
                    results.AddRange(response);
                    pageCount++;

                    string diagnostics = response.Diagnostics.ToString();
                    Assert.IsFalse(diagnostics.Contains("ThinClientStoreModel"), $"Page {pageCount}: Should NOT use ThinClient");
                }

                Assert.IsTrue(results.Count >= 9,
                    $"Should return at least 9 items, got {results.Count}");

                int foundCount = 0;
                foreach (TestObject item in createdItems)
                {
                    if (results.Any(r => r.Id == item.Id))
                    {
                        foundCount++;
                    }
                }

                Assert.IsTrue(foundCount >= 9,
                    $"Should find all 9 test items in results, found {foundCount}");

            }
            finally
            {

                foreach (TestObject item in items)
                {
                    try
                    {
                        await this.container.DeleteItemAsync<TestObject>(item.Id, new PartitionKey(item.Pk));
                    }
                    catch { }
                }
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task TestQueryPlanMultiPartitionFanout_GatewayMode()
        {
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "False");
            List<TestObject> items = new List<TestObject>();
            string baseGuid = Guid.NewGuid().ToString();

            try
            {
                // Create a fresh client that honors the disabled ThinClient flag
                using CosmosClient queryPlanClient = new CosmosClient(
                    this.connectionString,
                    new CosmosClientOptions()
                    {
                        ConnectionMode = ConnectionMode.Gateway,
                        ApplicationPreferredRegions = PreferredRegions,
                        Serializer = this.cosmosSystemTextJsonSerializer,
                    });

                Container queryPlanContainer = queryPlanClient.GetContainer(this.database.Id, this.container.Id);

                // Create items across many distinct partition keys to ensure multi-partition fanout
                int partitionCount = 10;
                int itemsPerPartition = 3;

                for (int pkIndex = 0; pkIndex < partitionCount; pkIndex++)
                {
                    string pk = $"pk_fanout_{pkIndex}_{baseGuid}";
                    for (int i = 0; i < itemsPerPartition; i++)
                    {
                        items.Add(new TestObject
                        {
                            Id = Guid.NewGuid().ToString(),
                            Pk = pk,
                            Other = $"Partition_{pkIndex}_Item_{i}",
                        });
                    }
                }

                int totalExpected = partitionCount * itemsPerPartition;
                List<TestObject> createdItems = await this.CreateItemsSafeAsync(items);
                Assert.AreEqual(totalExpected, createdItems.Count, $"All {totalExpected} items should be created");

                // Execute a cross-partition ORDER BY query (requires QueryPlan + fanout)
                string query = "SELECT * FROM c WHERE STARTSWITH(c.other, 'Partition_') ORDER BY c.other ASC";

                // Run query via non-ThinClient mode
                FeedIterator<TestObject> queryPlanIterator = queryPlanContainer.GetItemQueryIterator<TestObject>(query);

                List<TestObject> queryPlanResults = new List<TestObject>();
                while (queryPlanIterator.HasMoreResults)
                {
                    FeedResponse<TestObject> response = await queryPlanIterator.ReadNextAsync();
                    queryPlanResults.AddRange(response);

                    string diagnostics = response.Diagnostics.ToString();
                    Assert.IsFalse(diagnostics.Contains("ThinClientStoreModel"), "Should NOT use ThinClient mode");
                }

                // Verify all items are returned
                int foundCount = createdItems.Count(created =>
                    queryPlanResults.Any(r => r.Id == created.Id));
                Assert.AreEqual(totalExpected, foundCount,
                    $"Should find all {totalExpected} test items in fanout results, found {foundCount}");

                // Compare with Gateway mode results to verify correctness
                using CosmosClient gatewayClient = new CosmosClient(
                    this.connectionString,
                    new CosmosClientOptions()
                    {
                        ConnectionMode = ConnectionMode.Gateway,
                        Serializer = this.cosmosSystemTextJsonSerializer,
                    });

                Container gatewayContainer = gatewayClient.GetContainer(this.database.Id, this.container.Id);
                FeedIterator<TestObject> gatewayIterator = gatewayContainer.GetItemQueryIterator<TestObject>(query);

                List<TestObject> gatewayResults = new List<TestObject>();
                while (gatewayIterator.HasMoreResults)
                {
                    FeedResponse<TestObject> response = await gatewayIterator.ReadNextAsync();
                    gatewayResults.AddRange(response);
                }

                // QueryPlan client and Gateway should return the same item count
                Assert.AreEqual(gatewayResults.Count, queryPlanResults.Count,
                    $"QueryPlan client ({queryPlanResults.Count}) and Gateway ({gatewayResults.Count}) should return the same number of items.");

                // Verify both results contain the same item IDs
                HashSet<string> queryPlanIds = new HashSet<string>(queryPlanResults.Select(r => r.Id));
                HashSet<string> gatewayIds = new HashSet<string>(gatewayResults.Select(r => r.Id));
                Assert.IsTrue(queryPlanIds.SetEquals(gatewayIds),
                    "QueryPlan client and Gateway should return the same set of items.");
            }
            finally
            {

                foreach (TestObject item in items)
                {
                    try
                    {
                        await this.container.DeleteItemAsync<TestObject>(item.Id, new PartitionKey(item.Pk));
                    }
                    catch { }
                }
            }
        }

        [TestMethod]
        [TestCategory("ThinClient")]
        public async Task TestThinClientChangeFeedLatestVersionAsync()
        {
            // Arrange:
            string pk = "pk_changefeed";
            List<TestObject> items = this.GenerateItems(pk).Take(10).ToList();
            List<TestObject> createdItems = await this.CreateItemsSafeAsync(items);

            Assert.IsTrue(createdItems.Count > 0, "At least one item must be created for the change feed test.");

            // Act: Read change feed using LatestVersion mode
            List<TestObject> changeFeedResults = new List<TestObject>();
            FeedIterator<TestObject> changeFeedIterator = this.container.GetChangeFeedIterator<TestObject>(
                ChangeFeedStartFrom.Beginning(),
                ChangeFeedMode.LatestVersion,
                new ChangeFeedRequestOptions()
                {
                    PageSizeHint = 10
                });

            while (changeFeedIterator.HasMoreResults)
            {
                FeedResponse<TestObject> response = await changeFeedIterator.ReadNextAsync();

                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    break;
                }

                string diagnostics = response.Diagnostics.ToString();
                Assert.IsTrue(diagnostics.Contains("|F4"), "Diagnostics User Agent should contain '|F4' for ThinClient change feed");
                AssertExcludedRegionsNotInDiagnostics(diagnostics);

                changeFeedResults.AddRange(response);
            }

            // Assert: Verify all created items appear in the change feed
            Assert.IsTrue(changeFeedResults.Count >= createdItems.Count,
                $"Change feed should return at least {createdItems.Count} items but got {changeFeedResults.Count}.");

            HashSet<string> createdIds = new HashSet<string>(createdItems.Select(i => i.Id));
            HashSet<string> changeFeedIds = new HashSet<string>(changeFeedResults.Select(i => i.Id));
            Assert.IsTrue(createdIds.IsSubsetOf(changeFeedIds),
                "All created items should appear in the change feed results.");
        }

        /// <summary>
        /// End-to-end test against the live thin-client endpoint: verifies that when a
        /// <see cref="Microsoft.Azure.Cosmos.ReadConsistencyStrategy"/> is supplied on a point read,
        /// the SDK actually carries the strategy on the wire — for the ThinClient path that means
        /// the corresponding RNTBD token is encoded into the body of the HTTP request sent to the
        /// thin-client proxy. (The SDK clears HTTP headers post-serialization so the strategy does
        /// not appear as an <c>x-ms-*</c> HTTP header.)
        ///
        /// For strategies that are compatible with the test account's default consistency level
        /// (Eventual / Session / LatestCommitted on a Session-consistency account) the read must
        /// also succeed and return the item. For <c>GlobalStrong</c> against a Session-consistency
        /// account the server is expected to reject the request with <c>400 BadRequest</c>; that
        /// rejection is itself the proof that the SDK delivered the strategy to the service, and
        /// the wire-level token assertion confirms the SDK encoded it correctly before sending.
        ///
        /// <see cref="ReadConsistencyStrategy.LastCommittedSingleWriteRegion"/> is intentionally
        /// not covered here because it is rewritten by the SDK into LatestCommitted + a hub-region
        /// flag for single-master accounts and the test account is not guaranteed to be single-master.
        /// </summary>
        [TestMethod]
        [TestCategory("ThinClient")]
        [DataRow("Eventual", (byte)1, true, DisplayName = "ThinClient point-read with Eventual (compatible with Session account)")]
        [DataRow("Session", (byte)2, true, DisplayName = "ThinClient point-read with Session (compatible with Session account)")]
        [DataRow("LatestCommitted", (byte)3, true, DisplayName = "ThinClient point-read with LatestCommitted (compatible with Session account)")]
        [DataRow("GlobalStrong", (byte)4, false, DisplayName = "ThinClient point-read with GlobalStrong rejected by server when account consistency is Session")]
        public async Task ReadItemWithReadConsistencyStrategyOnThinClientAsync(
            string strategyName,
            byte expectedRntbdStrategyValue,
            bool expectReadToSucceed)
        {
            Microsoft.Azure.Cosmos.ReadConsistencyStrategy readConsistencyStrategy =
                (Microsoft.Azure.Cosmos.ReadConsistencyStrategy)Enum.Parse(
                    typeof(Microsoft.Azure.Cosmos.ReadConsistencyStrategy), strategyName);

            byte[] expectedStrategyTokenBytes = { 0xFE, 0x00, 0x00, expectedRntbdStrategyValue };

            BodyCapturingHandler bodyCapturingHandler = new BodyCapturingHandler();

            CosmosClient capturingClient = null;
            Database capturingDatabase = null;
            try
            {
                capturingClient = new CosmosClient(
                    this.connectionString,
                    new CosmosClientOptions
                    {
                        ConnectionMode = ConnectionMode.Gateway,
                        Serializer = this.cosmosSystemTextJsonSerializer,
                        HttpClientFactory = () => new HttpClient(bodyCapturingHandler),
                    });

                string dbName = "TestDbRcs_" + Guid.NewGuid().ToString();
                capturingDatabase = await capturingClient.CreateDatabaseIfNotExistsAsync(dbName);
                string containerName = "TestContainerRcs_" + Guid.NewGuid().ToString();
                Container capturingContainer = await capturingDatabase.CreateContainerIfNotExistsAsync(containerName, "/pk");

                TestObject testItem = new TestObject
                {
                    Id = Guid.NewGuid().ToString(),
                    Pk = "pk_rcs_" + Guid.NewGuid().ToString(),
                    Other = "ReadConsistencyStrategy " + strategyName
                };

                ItemResponse<TestObject> createResponse = await capturingContainer.CreateItemAsync(
                    testItem,
                    new PartitionKey(testItem.Pk));
                Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

                string readDiagnostics;
                try
                {
                    ItemResponse<TestObject> readResponse = await capturingContainer.ReadItemAsync<TestObject>(
                        testItem.Id,
                        new PartitionKey(testItem.Pk),
                        new ItemRequestOptions { ReadConsistencyStrategy = readConsistencyStrategy });

                    Assert.IsTrue(
                        expectReadToSucceed,
                        $"Strategy '{strategyName}' was expected to be rejected by the server (e.g. due to account consistency mismatch) but the read returned {readResponse.StatusCode}.");

                    Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode, "Point read via thin-client proxy must succeed.");
                    Assert.AreEqual(testItem.Id, readResponse.Resource.Id, "Point read must return the previously-created item.");
                    Assert.AreEqual(testItem.Pk, readResponse.Resource.Pk);
                    readDiagnostics = readResponse.Diagnostics.ToString();
                }
                catch (CosmosException ex) when (!expectReadToSucceed && ex.StatusCode == HttpStatusCode.BadRequest)
                {
                    // Expected for strategies that the test account's default consistency level does
                    // not support (e.g. GlobalStrong against a Session-consistency account). The fact
                    // that the SERVER returned this specific error proves the SDK transmitted the
                    // strategy on the wire; the byte-level assertion below confirms it was encoded
                    // into the RNTBD body of the proxy request.
                    readDiagnostics = ex.Diagnostics.ToString();
                }

                Assert.IsTrue(
                    readDiagnostics.Contains("|F4"),
                    $"Diagnostics user-agent should contain '|F4' indicating the read with strategy '{strategyName}' was routed via the ThinClient proxy. Diagnostics:\n{readDiagnostics}");

                // With the HTTP/2 connectivity-probe gate, the SDK only routes the data plane to the proxy
                // when the probe is green. If the target account does not (yet) enable the
                // '/connectivity-probe' endpoint - or client/proxy cannot negotiate HTTP/2 - the first probe
                // cycle is red and the SDK correctly falls back to Gateway V1. In that case the read travels
                // the gateway REST path (a GET with the strategy carried as a header) and the RNTBD body token
                // simply cannot exist on the wire. Detect real proxy routing by a captured request that targets
                // the proxy host (different from the account gateway host) on a path other than the probe path;
                // only then is the byte-level token assertion meaningful.
                string gatewayHost = capturingClient.Endpoint.Host;

                bool readRoutedThroughProxy = bodyCapturingHandler.CapturedRequests.Any(r =>
                    r.Uri != null
                    && !string.Equals(r.Uri.Host, gatewayHost, StringComparison.OrdinalIgnoreCase)
                    && r.Uri.AbsolutePath.IndexOf("connectivity-probe", StringComparison.OrdinalIgnoreCase) < 0);

                if (!readRoutedThroughProxy)
                {
                    Assert.Inconclusive(
                        $"ThinClient routing did not occur for strategy '{strategyName}': the SDK served the request via " +
                        $"Gateway V1, which is the expected behavior when the HTTP/2 connectivity probe is not green on the " +
                        $"target account (e.g. the proxy does not enable '/connectivity-probe', or HTTP/2 could not be " +
                        $"negotiated). The on-the-wire RNTBD strategy token can only be verified when the request is routed " +
                        $"to the thin-client proxy.");
                }

                bool strategyTokenFoundOnWire = bodyCapturingHandler
                    .CapturedBodies
                    .Any(body => ContainsByteSequence(body, expectedStrategyTokenBytes));

                Assert.IsTrue(
                    strategyTokenFoundOnWire,
                    $"The outbound thin-client request body must encode the ReadConsistencyStrategy '{strategyName}' as RNTBD token [FE 00 00 {expectedRntbdStrategyValue:X2}], proving the SDK forwarded the strategy to the proxy regardless of whether the server accepted it.");
            }
            finally
            {
                if (capturingDatabase != null)
                {
                    try
                    {
                        await capturingDatabase.DeleteAsync();
                    }
                    catch
                    {
                    }
                }

                capturingClient?.Dispose();
            }
        }

        private static string RegionHostFragment(string region)
        {
            return region.Replace(" ", string.Empty).ToLowerInvariant();
        }

        private static bool ProxyWasUsedForRegion(ConnectivityProbeFailingHandler handler, string regionHostFragment)
        {
            return handler.DataPlaneRequestUris.Any(uri =>
                uri != null
                && uri.Port == ThinClientProxyPort
                && uri.Host.IndexOf(regionHostFragment, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static IEnumerable<string> ProxyRequestHosts(ConnectivityProbeFailingHandler handler)
        {
            return handler.DataPlaneRequestUris
                .Where(uri => uri != null && uri.Port == ThinClientProxyPort)
                .Select(uri => uri.Host)
                .Distinct();
        }

        private static async Task<ItemResponse<TestObject>> ReadItemWithRegionRetryAsync(
            Container container,
            TestObject item,
            IReadOnlyList<string> excludeRegions)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    return await container.ReadItemAsync<TestObject>(
                        item.Id,
                        new PartitionKey(item.Pk),
                        new ItemRequestOptions { ExcludeRegions = new List<string>(excludeRegions) });
                }
                catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.NotFound && attempt < 11)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }
        }

        private static bool ContainsByteSequence(byte[] haystack, byte[] needle)
        {
            if (haystack == null || needle == null || needle.Length == 0 || haystack.Length < needle.Length)
            {
                return false;
            }

            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// DelegatingHandler that buffers every outbound HTTP request body so the test can inspect
        /// the wire-level bytes that were actually sent. This is required for ThinClient because
        /// the SDK clears HTTP headers after RNTBD serialization (<c>ThinClientStoreClient.cs</c>),
        /// so the consistency-strategy value only appears in the request body, not in any
        /// <c>x-ms-*</c> header.
        /// </summary>
        private sealed class BodyCapturingHandler : DelegatingHandler
        {
            public ConcurrentQueue<byte[]> CapturedBodies { get; } = new ConcurrentQueue<byte[]>();

            // Captures the target URI of every outbound request (alongside its body) so a test can tell
            // whether a request was sent to the thin-client proxy host (different from the account gateway
            // host) or fell back to Gateway V1.
            public ConcurrentQueue<(Uri Uri, byte[] Body)> CapturedRequests { get; } = new ConcurrentQueue<(Uri, byte[])>();

            public BodyCapturingHandler()
                : base(new HttpClientHandler())
            {
            }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                byte[] body = null;
                if (request.Content != null)
                {
                    await request.Content.LoadIntoBufferAsync();
                    body = await request.Content.ReadAsByteArrayAsync();
                    this.CapturedBodies.Enqueue(body);
                }

                this.CapturedRequests.Enqueue((request.RequestUri, body));

                return await base.SendAsync(request, cancellationToken);
            }
        }

        /// <summary>
        /// DelegatingHandler that intercepts HTTP requests and can inject faults
        /// </summary>
        private class FaultInjectionDelegatingHandler : DelegatingHandler
        {
            private readonly Action<HttpRequestMessage> requestCallback;

            public FaultInjectionDelegatingHandler(Action<HttpRequestMessage> requestCallback)
                : base(new HttpClientHandler())
            {
                this.requestCallback = requestCallback;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                // Invoke callback which can inspect request or throw exceptions
                this.requestCallback?.Invoke(request);

                // If no exception was thrown, proceed with the actual request
                return base.SendAsync(request, cancellationToken);
            }
        }


        /// <summary>
        /// DelegatingHandler that simulates a region whose Gateway V2 endpoint does not support the connectivity
        /// probe. Any <c>POST /connectivity-probe</c> whose host matches <c>regionHostFragmentToFail</c> is answered
        /// with a synthetic non-200 (without contacting the service), so the SDK marks that region red and keeps it
        /// on Gateway V1. Probes for other regions and all data-plane traffic pass through unchanged; the target URI
        /// of every non-probe request is recorded so a test can tell which port (proxy 10250 vs gateway 443) and
        /// region each request used.
        /// </summary>
        private sealed class ConnectivityProbeFailingHandler : DelegatingHandler
        {
            private const string ConnectivityProbePath = "/connectivity-probe";

            private readonly string regionHostFragmentToFail;

            public ConnectivityProbeFailingHandler(string regionHostFragmentToFail)
                : base(new HttpClientHandler())
            {
                this.regionHostFragmentToFail = regionHostFragmentToFail;
            }

            public ConcurrentQueue<Uri> DataPlaneRequestUris { get; } = new ConcurrentQueue<Uri>();

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                bool isConnectivityProbe = request.Method == HttpMethod.Post
                    && string.Equals(request.RequestUri?.AbsolutePath, ConnectivityProbePath, StringComparison.OrdinalIgnoreCase);

                if (isConnectivityProbe)
                {
                    if (request.RequestUri.Host.IndexOf(this.regionHostFragmentToFail, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                        {
                            RequestMessage = request
                        });
                    }
                }
                else
                {
                    this.DataPlaneRequestUris.Enqueue(request.RequestUri);
                }

                return base.SendAsync(request, cancellationToken);
            }
        }

        private sealed class ExcludeRegionsInjectingHandler : RequestHandler
        {
            private readonly IReadOnlyList<string> excludeRegions;

            public ExcludeRegionsInjectingHandler(IReadOnlyList<string> excludeRegions)
            {
                this.excludeRegions = excludeRegions;
            }

            public override Task<ResponseMessage> SendAsync(RequestMessage request, CancellationToken cancellationToken)
            {
                if (request.RequestOptions == null)
                {
                    request.RequestOptions = new RequestOptions();
                }

                if (request.RequestOptions.ExcludeRegions == null || request.RequestOptions.ExcludeRegions.Count == 0)
                {
                    request.RequestOptions.ExcludeRegions = new List<string>(this.excludeRegions);
                }

                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}
