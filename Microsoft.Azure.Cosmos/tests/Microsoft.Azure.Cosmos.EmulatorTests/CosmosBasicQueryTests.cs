//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Scripts;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    [TestClass]
    public class CosmosBasicQueryTests
    {
        private static readonly QueryRequestOptions RequestOptions = new QueryRequestOptions() { MaxItemCount = 1 };
        private static CosmosClient DirectCosmosClient;
        private static CosmosClient GatewayCosmosClient;
        private const string DatabaseId = "CosmosBasicQueryTests";
        private static readonly string ContainerId = "ContainerBasicQueryTests" + Guid.NewGuid();

        [ClassInitialize]
        public static void TestInit(TestContext _)
        {
            CosmosBasicQueryTests.DirectCosmosClient = TestCommon.CreateCosmosClient();
            CosmosBasicQueryTests.GatewayCosmosClient = TestCommon.CreateCosmosClient((builder) => builder.WithConnectionModeGateway());
        }

        [TestInitialize]
        public async Task TestInitialize()
        {
            await Util.DeleteAllDatabasesAsync(CosmosBasicQueryTests.DirectCosmosClient);

            Database database = await DirectCosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseId);
            await database.CreateContainerIfNotExistsAsync(ContainerId, "/pk");
        }

        [ClassCleanup]
        public static async Task TestCleanup()
        {
            if (CosmosBasicQueryTests.DirectCosmosClient == null)
            {
                return;
            }

            Database database = DirectCosmosClient.GetDatabase(DatabaseId);
            await database.DeleteStreamAsync();

            CosmosBasicQueryTests.DirectCosmosClient.Dispose();
            CosmosBasicQueryTests.GatewayCosmosClient.Dispose();
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task DatabaseTest(bool directMode)
        {
            CosmosClient client = directMode ? DirectCosmosClient : GatewayCosmosClient;
            List<Database> deleteList = new List<Database>();
            List<string> createdIds = new List<string>();

            try
            {
                DatabaseResponse createResponse = await client.CreateDatabaseIfNotExistsAsync(id: "BasicQueryDb1");
                deleteList.Add(createResponse.Database);
                createdIds.Add(createResponse.Database.Id);

                createResponse = await client.CreateDatabaseIfNotExistsAsync(id: "BasicQueryDb2");
                deleteList.Add(createResponse.Database);
                createdIds.Add(createResponse.Database.Id);

                createResponse = await client.CreateDatabaseIfNotExistsAsync(id: "BasicQueryDb3");
                deleteList.Add(createResponse.Database);
                createdIds.Add(createResponse.Database.Id);

                //Read All
                List<DatabaseProperties> results = await this.ToListAsync(
                    client.GetDatabaseQueryStreamIterator,
                    client.GetDatabaseQueryIterator<DatabaseProperties>,
                    null,
                    CosmosBasicQueryTests.RequestOptions);

                CollectionAssert.IsSubsetOf(createdIds, results.Select(x => x.Id).ToList());

                //Basic query
                List<DatabaseProperties> queryResults = await this.ToListAsync(
                    client.GetDatabaseQueryStreamIterator,
                    client.GetDatabaseQueryIterator<DatabaseProperties>,
                    "select * from T where STARTSWITH(T.id, \"BasicQueryDb\")",
                    CosmosBasicQueryTests.RequestOptions);

                CollectionAssert.AreEquivalent(createdIds, queryResults.Select(x => x.Id).ToList());
            }
            finally
            {
                foreach (Cosmos.Database database in deleteList)
                {
                    await database.DeleteAsync();
                }
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ContainerTest(bool directMode)
        {
            CosmosClient client = directMode ? DirectCosmosClient : GatewayCosmosClient;
            Database database = client.GetDatabase(DatabaseId);
            List<string> createdIds = new List<string>();

            try
            {
                ContainerResponse createResponse = await database.CreateContainerIfNotExistsAsync(id: "BasicQueryContainer1", partitionKeyPath: "/pk");
                createdIds.Add(createResponse.Container.Id);

                createResponse = await database.CreateContainerIfNotExistsAsync(id: "BasicQueryContainer2", partitionKeyPath: "/pk2");
                createdIds.Add(createResponse.Container.Id);

                createResponse = await database.CreateContainerIfNotExistsAsync(id: "BasicQueryContainer3", partitionKeyPath: "/pk3");
                createdIds.Add(createResponse.Container.Id);

                //Read All
                List<ContainerProperties> results = await this.ToListAsync(
                    database.GetContainerQueryStreamIterator,
                    database.GetContainerQueryIterator<ContainerProperties>,
                    null,
                    CosmosBasicQueryTests.RequestOptions);

                CollectionAssert.IsSubsetOf(createdIds, results.Select(x => x.Id).ToList());

                //Basic query
                List<ContainerProperties> queryResults = await this.ToListAsync(
                    database.GetContainerQueryStreamIterator,
                    database.GetContainerQueryIterator<ContainerProperties>,
                    "select * from T where STARTSWITH(T.id, \"BasicQueryContainer\")",
                    CosmosBasicQueryTests.RequestOptions);

                CollectionAssert.AreEquivalent(createdIds, queryResults.Select(x => x.Id).ToList());
            }
            finally
            {
                foreach (string id in createdIds)
                {
                    //Don't wait for the container cleanup
                    await database.GetContainer(id).DeleteContainerAsync();
                }
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task QueryRequestRateTest(bool directMode)
        {
            string firstItemIdAndPk = "BasicQueryItem" + Guid.NewGuid();

            // Prevent the test from changing the static client
            {
                CosmosClient client = directMode ? DirectCosmosClient : GatewayCosmosClient;
                Container container = client.GetContainer(DatabaseId, ContainerId);

                List<string> createdIds = new List<string>()
                {
                    firstItemIdAndPk,
                    "BasicQueryItem2"+ Guid.NewGuid(),
                    "BasicQueryItem3"+ Guid.NewGuid()
                };

                foreach (string id in createdIds)
                {
                    dynamic item = new
                    {
                        id = id,
                        pk = id,
                    };

                    await container.CreateItemAsync<dynamic>(item: item);
                }
            }

            CosmosClient clientWithThrottle;
            if (directMode)
            {
                clientWithThrottle = TestCommon.CreateCosmosClient();
            }
            else
            {
                clientWithThrottle = TestCommon.CreateCosmosClient((builder) => builder.WithConnectionModeGateway());
            }

            Container containerWithThrottle = clientWithThrottle.GetContainer(DatabaseId, ContainerId);

            // Do a read to warm up all the caches to prevent them from getting the throttle errors
            using (await containerWithThrottle.ReadItemStreamAsync(firstItemIdAndPk, new PartitionKey(firstItemIdAndPk))) { }

            Documents.IStoreModel storeModel = clientWithThrottle.ClientContext.DocumentClient.StoreModel;
            Mock<Documents.IStoreModelExtension> mockStore = new Mock<Documents.IStoreModelExtension>();
            clientWithThrottle.ClientContext.DocumentClient.StoreModel = mockStore.Object;

            // Cause 429 after the first call
            int callCount = 0;
            string activityId = null;
            string errorMessage = "QueryRequestRateTest Resource Not Found";
            mockStore.Setup(x => x.ProcessMessageAsync(It.IsAny<Documents.DocumentServiceRequest>(), It.IsAny<CancellationToken>()))
                .Returns<Documents.DocumentServiceRequest, CancellationToken>((dsr, token) =>
                {
                    callCount++;

                    if (callCount > 1)
                    {
                        INameValueCollection headers = new Documents.Collections.StoreResponseNameValueCollection
                        {
                            { Documents.HttpConstants.HttpHeaders.RetryAfterInMilliseconds, "42" }
                        };
                        activityId = Guid.NewGuid().ToString();
                        headers.Add(Documents.HttpConstants.HttpHeaders.ActivityId, activityId);
                        Documents.DocumentServiceResponse response = new Documents.DocumentServiceResponse(
                            body: TestCommon.GenerateStreamFromString(@"{""Errors"":[""" + errorMessage + @"""]}"),
                            headers: headers,
                            statusCode: (HttpStatusCode)429,
                            clientSideRequestStatistics: dsr.RequestContext.ClientRequestStatistics);

                        return Task.FromResult(response);
                    }

                    return storeModel.ProcessMessageAsync(dsr, token);
                });

            List<dynamic> results = new List<dynamic>();
            try
            {
                using (FeedIterator<dynamic> feedIterator = containerWithThrottle.GetItemQueryIterator<dynamic>(
                    "select * from T where STARTSWITH(T.id, \"BasicQueryItem\")",
                    requestOptions: new QueryRequestOptions()
                    {
                        MaxItemCount = 1,
                        MaxConcurrency = 1,
                        EnableOptimisticDirectExecution = false,
                    }))
                {
                    while (feedIterator.HasMoreResults)
                    {
                        FeedResponse<dynamic> response = await feedIterator.ReadNextAsync();
                        Assert.IsTrue(response.Count <= 1);
                        Assert.IsTrue(response.Resource.Count() <= 1);

                        results.AddRange(response);
                    }
                }
                Assert.Fail("Should throw 429 exception after the first page.");
            }
            catch (CosmosException ce)
            {
                Assert.IsTrue(ce.RetryAfter.HasValue);
                Assert.AreEqual(42, ce.RetryAfter.Value.TotalMilliseconds);
                Assert.AreEqual(activityId, ce.ActivityId);
                Assert.IsTrue(ce.Message.Contains(errorMessage));
            }

            callCount = 0;
            FeedIterator streamIterator = containerWithThrottle.GetItemQueryStreamIterator(
                "select * from T where STARTSWITH(T.id, \"BasicQueryItem\")",
                requestOptions: new QueryRequestOptions()
                {
                    MaxItemCount = 1,
                    MaxConcurrency = 1,
                    EnableOptimisticDirectExecution = false,
                });

            // First request should be a success
            using (ResponseMessage response = await streamIterator.ReadNextAsync())
            {
                response.EnsureSuccessStatusCode();
                Assert.IsNotNull(response.Content);
            }

            // Second page should be a failure
            using (ResponseMessage response = await streamIterator.ReadNextAsync())
            {
                Assert.AreEqual(429, (int)response.StatusCode);
                Assert.AreEqual("42", response.Headers.RetryAfterLiteral);
                Assert.AreEqual(activityId, response.Headers.ActivityId);
                Assert.IsNotNull(response.Trace);
                Assert.IsTrue(response.ErrorMessage.Contains(errorMessage));
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ItemTest(bool directMode)
        {
            CosmosClient client = directMode ? DirectCosmosClient : GatewayCosmosClient;
            Database database = client.GetDatabase(DatabaseId);
            Container container = await database.CreateContainerAsync(Guid.NewGuid().ToString(), "/pk");

            List<string> createdIds = new List<string>()
            {
                "BasicQueryItem",
                "BasicQueryItem2",
                "BasicQueryItem3"
            };

            List<dynamic> queryResults = await this.ToListAsync(
                  container.GetItemQueryStreamIterator,
                 container.GetItemQueryIterator<dynamic>,
                 "select * from T where STARTSWITH(T.id, \"basicQueryItem\", true)",
                 CosmosBasicQueryTests.RequestOptions);

            if (queryResults.Count < 3)
            {
                foreach (string id in createdIds)
                {
                    dynamic item = new
                    {
                        id = id,
                        pk = id,
                    };

                    ItemResponse<dynamic> createResponse = await container.CreateItemAsync<dynamic>(item: item);
                }

                queryResults = await this.ToListAsync(
                  container.GetItemQueryStreamIterator,
                 container.GetItemQueryIterator<dynamic>,
                 "select * from T where Contains(T.id, \"basicqueryitem\", true)",
                 CosmosBasicQueryTests.RequestOptions);
            }

            List<string> ids = queryResults.Select(x => (string)x.id).ToList();
            CollectionAssert.AreEquivalent(createdIds, ids);

            //Read All
            List<dynamic> results = await this.ToListAsync(
                container.GetItemQueryStreamIterator,
                container.GetItemQueryIterator<dynamic>,
                null,
                CosmosBasicQueryTests.RequestOptions);


            ids = results.Select(x => (string)x.id).ToList();
            CollectionAssert.IsSubsetOf(createdIds, ids);

            //Read All with partition key
            results = await this.ToListAsync(
               container.GetItemQueryStreamIterator,
               container.GetItemQueryIterator<dynamic>,
               null,
               new QueryRequestOptions()
               {
                   MaxItemCount = 1,
                   PartitionKey = new PartitionKey("BasicQueryItem")
               });

            Assert.AreEqual(1, results.Count);

            //Read All with partition key
            results = container.GetItemLinqQueryable<dynamic>(
                allowSynchronousQueryExecution: true,
                requestOptions: new QueryRequestOptions()
                {
                    MaxItemCount = 1,
                    PartitionKey = new PartitionKey("BasicQueryItem")
                }).ToList();

            Assert.AreEqual(1, results.Count);

            // LINQ to feed iterator Read All with partition key
            using (FeedIterator<dynamic> iterator = container.GetItemLinqQueryable<dynamic>(
                allowSynchronousQueryExecution: true,
                requestOptions: new QueryRequestOptions()
                {
                    MaxItemCount = 1,
                    PartitionKey = new PartitionKey("BasicQueryItem")
                }).ToFeedIterator())
            {
                List<dynamic> linqResults = new List<dynamic>();
                while (iterator.HasMoreResults)
                {
                    linqResults.AddRange(await iterator.ReadNextAsync());
                }

                Assert.AreEqual(1, linqResults.Count);
                Assert.AreEqual("BasicQueryItem", linqResults.First().pk.ToString());
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ScriptsStoredProcedureTest(bool directMode)
        {
            CosmosClient client = directMode ? DirectCosmosClient : GatewayCosmosClient;
            Database database = client.GetDatabase(DatabaseId);
            Container container = await database.CreateContainerAsync(Guid.NewGuid().ToString(), "/pk");
            Scripts scripts = container.Scripts;

            List<string> createdIds = new List<string>()
            {
                "BasicQuerySp1",
                "BasicQuerySp2",
                "BasicQuerySp3"
            };

            //Basic query
            List<StoredProcedureProperties> queryResults = await this.ToListAsync(
                scripts.GetStoredProcedureQueryStreamIterator,
                scripts.GetStoredProcedureQueryIterator<StoredProcedureProperties>,
                "select * from T where STARTSWITH(T.id, \"BasicQuerySp\")",
                CosmosBasicQueryTests.RequestOptions);

            if (queryResults.Count < 3)
            {
                foreach (string id in createdIds)
                {
                    StoredProcedureProperties properties = await scripts.CreateStoredProcedureAsync(new StoredProcedureProperties()
                    {
                        Id = id,
                        Body = "function() {var x = 10;}"
                    });
                }

                queryResults = await this.ToListAsync(
                    scripts.GetStoredProcedureQueryStreamIterator,
                    scripts.GetStoredProcedureQueryIterator<StoredProcedureProperties>,
                    "select * from T where STARTSWITH(T.id, \"BasicQuerySp\")",
                    CosmosBasicQueryTests.RequestOptions);
            }

            CollectionAssert.AreEquivalent(createdIds, queryResults.Select(x => x.Id).ToList());

            //Read All
            List<StoredProcedureProperties> results = await this.ToListAsync(
                scripts.GetStoredProcedureQueryStreamIterator,
                scripts.GetStoredProcedureQueryIterator<StoredProcedureProperties>,
                null,
                CosmosBasicQueryTests.RequestOptions);

            CollectionAssert.IsSubsetOf(createdIds, results.Select(x => x.Id).ToList());
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ScriptsUserDefinedFunctionTest(bool directMode)
        {
            CosmosClient client = directMode ? DirectCosmosClient : GatewayCosmosClient;
            Database database = client.GetDatabase(DatabaseId);
            Container container = await database.CreateContainerAsync(Guid.NewGuid().ToString(), "/pk");
            Scripts scripts = container.Scripts;

            List<string> createdIds = new List<string>()
            {
                "BasicQueryUdf1",
                "BasicQueryUdf2",
                "BasicQueryUdf3"
            };

            //Basic query
            List<UserDefinedFunctionProperties> queryResults = await this.ToListAsync(
                scripts.GetUserDefinedFunctionQueryStreamIterator,
                scripts.GetUserDefinedFunctionQueryIterator<UserDefinedFunctionProperties>,
                "select * from T where STARTSWITH(T.id, \"BasicQueryUdf\")",
                CosmosBasicQueryTests.RequestOptions);

            if (queryResults.Count < 3)
            {
                foreach (string id in createdIds)
                {
                    UserDefinedFunctionProperties properties = await scripts.CreateUserDefinedFunctionAsync(new UserDefinedFunctionProperties()
                    {
                        Id = id,
                        Body = "function() {var x = 10;}"
                    });
                }

                queryResults = await this.ToListAsync(
                    scripts.GetUserDefinedFunctionQueryStreamIterator,
                    scripts.GetUserDefinedFunctionQueryIterator<UserDefinedFunctionProperties>,
                    "select * from T where STARTSWITH(T.id, \"BasicQueryUdf\")",
                    CosmosBasicQueryTests.RequestOptions);
            }

            CollectionAssert.AreEquivalent(createdIds, queryResults.Select(x => x.Id).ToList());

            //Read All
            List<UserDefinedFunctionProperties> results = await this.ToListAsync(
                scripts.GetUserDefinedFunctionQueryStreamIterator,
                scripts.GetUserDefinedFunctionQueryIterator<UserDefinedFunctionProperties>,
                null,
                CosmosBasicQueryTests.RequestOptions);

            CollectionAssert.IsSubsetOf(createdIds, results.Select(x => x.Id).ToList());
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ScriptsTriggerTest(bool directMode)
        {
            CosmosClient client = directMode ? DirectCosmosClient : GatewayCosmosClient;
            Database database = client.GetDatabase(DatabaseId);
            Container container = await database.CreateContainerAsync(Guid.NewGuid().ToString(), "/pk");
            Scripts scripts = container.Scripts;

            List<string> createdIds = new List<string>()
            {
                "BasicQueryTrigger1",
                "BasicQueryTrigger2",
                "BasicQueryTrigger3"
            };

            //Basic query
            List<TriggerProperties> queryResults = await this.ToListAsync(
                scripts.GetTriggerQueryStreamIterator,
                scripts.GetTriggerQueryIterator<TriggerProperties>,
                "select * from T where STARTSWITH(T.id, \"BasicQueryTrigger\")",
                CosmosBasicQueryTests.RequestOptions);

            if (queryResults.Count < 3)
            {
                foreach (string id in createdIds)
                {
                    TriggerProperties properties = await scripts.CreateTriggerAsync(new TriggerProperties()
                    {
                        Id = id,
                        Body = "function() {var x = 10;}"
                    });
                }

                queryResults = await this.ToListAsync(
                    scripts.GetTriggerQueryStreamIterator,
                    scripts.GetTriggerQueryIterator<TriggerProperties>,
                    "select * from T where STARTSWITH(T.id, \"BasicQueryTrigger\")",
                    CosmosBasicQueryTests.RequestOptions);
            }

            CollectionAssert.AreEquivalent(createdIds, queryResults.Select(x => x.Id).ToList());

            //Read All
            List<TriggerProperties> results = await this.ToListAsync(
                scripts.GetTriggerQueryStreamIterator,
                scripts.GetTriggerQueryIterator<TriggerProperties>,
                null,
                CosmosBasicQueryTests.RequestOptions);

            CollectionAssert.IsSubsetOf(createdIds, results.Select(x => x.Id).ToList());
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task UserTests(bool directMode)
        {
            CosmosClient client = directMode ? DirectCosmosClient : GatewayCosmosClient;
            DatabaseInternal database = (DatabaseInlineCore)client.GetDatabase(DatabaseId);
            List<string> createdIds = new List<string>();

            try
            {
                UserResponse userResponse = await database.CreateUserAsync("BasicQueryUser1");
                createdIds.Add(userResponse.User.Id);

                userResponse = await database.CreateUserAsync("BasicQueryUser2");
                createdIds.Add(userResponse.User.Id);

                userResponse = await database.CreateUserAsync("BasicQueryUser3");
                createdIds.Add(userResponse.User.Id);

                //Read All
                List<UserProperties> results = await this.ToListAsync(
                    database.GetUserQueryStreamIterator,
                    database.GetUserQueryIterator<UserProperties>,
                    null,
                    CosmosBasicQueryTests.RequestOptions
                );

                CollectionAssert.IsSubsetOf(createdIds, results.Select(x => x.Id).ToList());

                //Basic query
                List<UserProperties> queryResults = await this.ToListAsync(
                    database.GetUserQueryStreamIterator,
                    database.GetUserQueryIterator<UserProperties>,
                    "SELECT * FROM T where STARTSWITH(T.id, \"BasicQueryUser\")",
                    CosmosBasicQueryTests.RequestOptions
                );

                CollectionAssert.AreEquivalent(createdIds, queryResults.Select(x => x.Id).ToList());
            }
            finally
            {
                foreach (string id in createdIds)
                {
                    await database.GetUser(id).DeleteAsync();
                }
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task PermissionTests(bool directMode)
        {
            CosmosClient client = directMode ? DirectCosmosClient : GatewayCosmosClient;
            Database database = client.GetDatabase(DatabaseId);
            List<string> createdPermissionIds = new List<string>();
            List<string> createdContainerIds = new List<string>();
            string userId = Guid.NewGuid().ToString();
            UserCore user = null;

            try
            {
                UserResponse createUserResponse = await database.CreateUserAsync(userId);
                Assert.AreEqual(HttpStatusCode.Created, createUserResponse.StatusCode);
                user = (UserInlineCore)createUserResponse.User;

                ContainerResponse createContainerResponse = await database.CreateContainerIfNotExistsAsync(Guid.NewGuid().ToString(), partitionKeyPath: "/pk");
                Container container = createContainerResponse.Container;
                PermissionResponse permissionResponse = await user.CreatePermissionAsync(new PermissionProperties("BasicQueryPermission1", PermissionMode.All, container));
                createdContainerIds.Add(createContainerResponse.Container.Id);
                createdPermissionIds.Add(permissionResponse.Permission.Id);


                createContainerResponse = await database.CreateContainerIfNotExistsAsync(Guid.NewGuid().ToString(), partitionKeyPath: "/pk");
                container = createContainerResponse.Container;
                permissionResponse = await user.CreatePermissionAsync(new PermissionProperties("BasicQueryPermission2", PermissionMode.All, container));
                createdContainerIds.Add(createContainerResponse.Container.Id);
                createdPermissionIds.Add(permissionResponse.Permission.Id);

                createContainerResponse = await database.CreateContainerIfNotExistsAsync(Guid.NewGuid().ToString(), partitionKeyPath: "/pk");
                container = createContainerResponse.Container;
                permissionResponse = await user.CreatePermissionAsync(new PermissionProperties("BasicQueryPermission3", PermissionMode.All, container));
                createdContainerIds.Add(createContainerResponse.Container.Id);
                createdPermissionIds.Add(permissionResponse.Permission.Id);

                //Read All
                List<PermissionProperties> results = await this.ToListAsync(
                    user.GetPermissionQueryStreamIterator,
                    user.GetPermissionQueryIterator<PermissionProperties>,
                    null,
                    CosmosBasicQueryTests.RequestOptions
                );

                CollectionAssert.IsSubsetOf(createdPermissionIds, results.Select(x => x.Id).ToList());

                //Basic query
                List<PermissionProperties> queryResults = await this.ToListAsync(
                    user.GetPermissionQueryStreamIterator,
                    user.GetPermissionQueryIterator<PermissionProperties>,
                    "select * from T where STARTSWITH(T.id, \"BasicQueryPermission\")",
                    CosmosBasicQueryTests.RequestOptions
                );

                CollectionAssert.AreEquivalent(createdPermissionIds, queryResults.Select(x => x.Id).ToList());
            }
            finally
            {
                foreach (string id in createdPermissionIds)
                {
                    await user.GetPermission(id).DeleteAsync();
                }
                foreach (string id in createdContainerIds)
                {
                    await database.GetContainer(id).DeleteContainerAsync();
                }
                await user?.DeleteAsync();
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ConclictsTests(bool directMode)
        {
            CosmosClient client = directMode ? DirectCosmosClient : GatewayCosmosClient;
            Database database = client.GetDatabase(DatabaseId);
            Container container = await database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id");
            //Read All
            List<ConflictProperties> results = await this.ToListAsync(
                container.Conflicts.GetConflictQueryStreamIterator,
                container.Conflicts.GetConflictQueryIterator<ConflictProperties>,
                null,
                CosmosBasicQueryTests.RequestOptions
            );

            // There is no way to simulate MM conflicts on the emulator but the list operations should work
        }

        [TestMethod]
        public async Task QueryActivityIdTests()
        {
            RequestHandler[] requestHandlers = new RequestHandler[1];
            requestHandlers[0] = new CustomHandler();

            CosmosClientBuilder builder = TestCommon.GetDefaultConfiguration();
            builder.AddCustomHandlers(requestHandlers);

            CosmosClient cosmosClient = builder.Build();
            Database database = await cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString());
            Container container = await database.CreateContainerAsync(Guid.NewGuid().ToString(),
                                                                      "/pk",
                                                                      throughput: 12000);

            // Create items
            for (int i = 0; i < 500; i++)
            {
                await container.CreateItemAsync<ToDoActivity>(ToDoActivity.CreateRandomToDoActivity());
            }

            QueryRequestOptions queryRequestOptions = new QueryRequestOptions
            {
                MaxItemCount = 50
            };

            FeedIterator<ToDoActivity> feedIterator = container.GetItemQueryIterator<ToDoActivity>(
                "select * from c",
                null,
                queryRequestOptions);

            while (feedIterator.HasMoreResults)
            {
                await feedIterator.ReadNextAsync();
            }

            await database.DeleteAsync();
            cosmosClient.Dispose();
        }

        [TestMethod]
        public async Task QueryActivityIdWithContinuationTokenAndTraceTest()
        {
            using (ITrace rootTrace = Trace.GetRootTrace("Root Trace"))
            {
                CosmosClient client = DirectCosmosClient;
                Container container = client.GetContainer(DatabaseId, ContainerId);
                // Create items
                for (int i = 0; i < 500; i++)
                {
                    await container.CreateItemAsync<ToDoActivity>(ToDoActivity.CreateRandomToDoActivity());
                }

                QueryRequestOptions queryRequestOptions = new QueryRequestOptions
                {
                    MaxItemCount = 50
                };

                FeedIteratorInternal feedIterator = 
                    (FeedIteratorInternal)container.GetItemQueryStreamIterator(
                    "select * from c",
                    null,
                    queryRequestOptions);

                string continuationToken = (await feedIterator.ReadNextAsync(rootTrace, CancellationToken.None)).ContinuationToken;
                rootTrace.Data.TryGetValue("Query Correlated ActivityId",
                                            out object firstCorrelatedActivityId);

                // use Continuation Token to create new iterator and use same trace
                FeedIteratorInternal feedIteratorNew =
                    (FeedIteratorInternal)container.GetItemQueryStreamIterator(
                    "select * from c",
                    continuationToken,
                    queryRequestOptions);

                while (feedIteratorNew.HasMoreResults)
                {
                    await feedIteratorNew.ReadNextAsync(rootTrace, CancellationToken.None);
                }

                // Test trace has 2 correlated ActivityIds
                rootTrace.Data.TryGetValue("Query Correlated ActivityId",
                                            out object correlatedActivityIds);
                List<string> correlatedIdList = correlatedActivityIds.ToString().Split(',').ToList();
                Assert.AreEqual(correlatedIdList.Count, 2);
                Assert.AreEqual(correlatedIdList[0], firstCorrelatedActivityId.ToString());
            }

        }

        //TODO: Remove Ignore flag once emulator is updated to 0415
        [Ignore]
        [TestMethod]
        public async Task TesOdeTokenCompatibilityWithNonOdePipeline()
        {
            string query = "select top 200 * from c";
            CosmosClient client = DirectCosmosClient;
            Container container = client.GetContainer(DatabaseId, ContainerId);
                
            // Create items
            for (int i = 0; i < 500; i++)
            {
                await container.CreateItemAsync<ToDoActivity>(ToDoActivity.CreateRandomToDoActivity());
            }

            QueryRequestOptions queryRequestOptions = new QueryRequestOptions
            {
                MaxItemCount = 50,
            };

            FeedIteratorInternal feedIterator =
                (FeedIteratorInternal)container.GetItemQueryStreamIterator(
                query,
                null,
                queryRequestOptions);

            ResponseMessage responseMessage = await feedIterator.ReadNextAsync(CancellationToken.None);
            string continuationToken = responseMessage.ContinuationToken;

            QueryRequestOptions newQueryRequestOptions = new QueryRequestOptions
            {
                MaxItemCount = 50,
                EnableOptimisticDirectExecution = false
            };

            // use Continuation Token to create new iterator and use same trace
            FeedIterator feedIteratorNew =
                container.GetItemQueryStreamIterator(
                query,
                continuationToken,
                newQueryRequestOptions);

            while (feedIteratorNew.HasMoreResults)
            {
                responseMessage = await feedIteratorNew.ReadNextAsync(CancellationToken.None);
            }

            string expectedErrorMessage = "Execution of this query using the supplied continuation token requires EnableOptimisticDirectExecution to be set in QueryRequestOptions. ";
            Assert.IsTrue(responseMessage.CosmosException.ToString().Contains(expectedErrorMessage));
        }

        private class CustomHandler : RequestHandler
        {
            string correlatedActivityId;

            public CustomHandler()
            {
                this.correlatedActivityId = null;
            }

            public override async Task<ResponseMessage> SendAsync(RequestMessage requestMessage,
                                                                CancellationToken cancellationToken)
            {
                if (requestMessage.OperationType == Documents.OperationType.Query)
                {
                    bool headerPresent = requestMessage.Headers.CosmosMessageHeaders.TryGetValue(Microsoft.Azure.Documents.WFConstants.BackendHeaders.CorrelatedActivityId, out string requestActivityId);
                    if (!headerPresent)
                    {
                        Assert.Fail("Correlated ActivityId header not present in request");
                    }

                    if (this.correlatedActivityId == null)
                    {
                        if (requestActivityId == Guid.Empty.ToString())
                        {
                            Assert.Fail("Request has empty guid as correlated activity id");
                        }

                        this.correlatedActivityId = requestActivityId;
                    }

                    if (this.correlatedActivityId != requestActivityId)
                    {
                        Assert.Fail("Correlated ActivityId is different between query requests");
                    }
                }

                return await base.SendAsync(requestMessage, cancellationToken);
            }
        }

        private delegate FeedIterator<T> Query<T>(string querytext, string continuationToken, QueryRequestOptions options);
        private delegate FeedIterator QueryStream(string querytext, string continuationToken, QueryRequestOptions options);

        private async Task<List<T>> ToListAsync<T>(
            QueryStream createStreamQuery,
            Query<T> createQuery,
            string queryText,
            QueryRequestOptions requestOptions)
        {
            HttpStatusCode expectedStatus = HttpStatusCode.OK;
            FeedIterator feedStreamIterator = createStreamQuery(queryText, null, requestOptions);
            List<T> streamResults = new List<T>();
            while (feedStreamIterator.HasMoreResults)
            {
                ResponseMessage response = await feedStreamIterator.ReadNextAsync();
                response.EnsureSuccessStatusCode();
                Assert.AreEqual(expectedStatus, response.StatusCode);

                StreamReader sr = new StreamReader(response.Content);
                string result = await sr.ReadToEndAsync();
                ICollection<T> responseResults;
                responseResults = JsonConvert.DeserializeObject<CosmosFeedResponseUtil<T>>(result).Data;

                Assert.IsTrue(responseResults.Count <= 1);

                streamResults.AddRange(responseResults);
            }

            string continuationToken = null;
            List<T> pagedStreamResults = new List<T>();
            do
            {
                FeedIterator pagedFeedIterator = createStreamQuery(queryText, continuationToken, requestOptions);
                ResponseMessage response = await pagedFeedIterator.ReadNextAsync();
                response.EnsureSuccessStatusCode();
                Assert.AreEqual(expectedStatus, response.StatusCode);

                IEnumerable<T> responseResults = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<T>>(response.Content).Data;
                Assert.IsTrue(responseResults.Count() <= 1);

                pagedStreamResults.AddRange(responseResults);
                continuationToken = response.Headers.ContinuationToken;
                Assert.AreEqual(response.ContinuationToken, response.Headers.ContinuationToken);
            } while (continuationToken != null);

            Assert.AreEqual(pagedStreamResults.Count, streamResults.Count);

            // Both lists should be the same if not PermssionsProperties. PermissionProperties will have a different ResouceToken in the payload when read.
            string streamResultString = JsonConvert.SerializeObject(streamResults);
            string streamPagedResultString = JsonConvert.SerializeObject(pagedStreamResults);

            if (typeof(T) != typeof(PermissionProperties))
            {
                Assert.AreEqual(streamPagedResultString, streamResultString);
            }

            FeedIterator<T> feedIterator = createQuery(queryText, null, requestOptions);
            List<T> results = new List<T>();
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<T> response = await feedIterator.ReadNextAsync();
                Assert.AreEqual(expectedStatus, response.StatusCode);
                Assert.IsTrue(response.Count <= 1);
                Assert.IsTrue(response.Resource.Count() <= 1);

                results.AddRange(response);
            }

            continuationToken = null;
            List<T> pagedResults = new List<T>();
            do
            {
                FeedIterator<T> pagedFeedIterator = createQuery(queryText, continuationToken, requestOptions);
                FeedResponse<T> response = await pagedFeedIterator.ReadNextAsync();
                Assert.AreEqual(expectedStatus, response.StatusCode);
                Assert.IsTrue(response.Count <= 1);
                Assert.IsTrue(response.Resource.Count() <= 1);
                pagedResults.AddRange(response);
                continuationToken = response.ContinuationToken;
            } while (continuationToken != null);

            Assert.AreEqual(pagedResults.Count, results.Count);

            // Both lists should be the same
            string resultString = JsonConvert.SerializeObject(results);
            string pagedResultString = JsonConvert.SerializeObject(pagedResults);

            if (typeof(T) != typeof(PermissionProperties))
            {
                Assert.AreEqual(pagedResultString, resultString);
                Assert.AreEqual(streamPagedResultString, resultString);
            }

            return results;
        }

        public const string DatabaseName = "testcosmosclient";
        public const int Throughput = 1200;
        public const string DefaultKey = "objectKey";
        public const string TestCollection = "testcollection";
        private static readonly Random Random = new Random();

        [TestMethod]
        public async Task InvalidRangesOnQuery()
        {
            CosmosClient cosmosClient = DirectCosmosClient;

            DatabaseResponse databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseName, Throughput);
            Database database = databaseResponse.Database;

            try
            {
                Container container = await database.DefineContainer(TestCollection, $"/{DefaultKey}")
                    .WithUniqueKey().Path($"/{DefaultKey}").Attach().CreateIfNotExistsAsync();

                List<string> queryKeys = new List<string>();

                List<TestCollectionObject> testCollectionObjects = JsonConvert.DeserializeObject<List<TestCollectionObject>>(
                    "[{\"id\":\"70627503-7cb2-4a79-bcec-5e55765aa080\",\"objectKey\":\"message~phone~u058da564bfaa66cb031606db664dbfda~phone~ud75ce020af5f8bfb75a9097a66d452f2~Chat~20190927000042Z\",\"text\":null,\"text2\":null},{\"id\":\"507079b7-a5be-4da4-9158-16fc961cd474\",\"objectKey\":\"message~phone~u058da564bfaa66cb031606db664dbfda~phone~ud75ce020af5f8bfb75a9097a66d452f2~Chat~20190927125742Z\",\"text\":null,\"text2\":null}]");
                foreach (TestCollectionObject testCollectionObject in testCollectionObjects)
                {
                    await WriteDocument(container, testCollectionObject);
                    queryKeys.Add(testCollectionObject.ObjectKey);
                }

                List<TestCollectionObject> results = container
                    .GetItemLinqQueryable<TestCollectionObject>(true, requestOptions: RunInParallelOptions())
                    .Where(r => queryKeys.Contains(r.ObjectKey))
                    .ToList(); // ERROR OCCURS WHEN QUERY IS EXECUTED

                Console.WriteLine($"[\"{string.Join("\", \n\"", results.Select(r => r.ObjectKey))}\"]");
            }
            finally
            {
                await database.DeleteAsync();
            }
        }

        private static async Task WriteDocument(Container container, TestCollectionObject testData)
        {
            try
            {
                await container.CreateItemAsync(testData, requestOptions: null);
            }
            catch (CosmosException e)
            {
                if (e.StatusCode != HttpStatusCode.Conflict)
                {
                    throw;
                }
            }
        }

        private static QueryRequestOptions RunInParallelOptions()
        {
            return new QueryRequestOptions
            {
                MaxItemCount = -1,
                MaxBufferedItemCount = -1,
                MaxConcurrency = -1
            };
        }
    }

    public class TestCollectionObject
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }
        [JsonProperty(CosmosBasicQueryTests.DefaultKey)]
        public string ObjectKey { get; set; }
        [JsonProperty("text")]
        public string Text { get; set; }
        [JsonProperty("text2")]
        public string Text2 { get; set; }
    }
}
