//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Scripts;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosBasicQueryTests
    {
        private static readonly QueryRequestOptions RequestOptions = new QueryRequestOptions() { MaxItemCount = 1 };
        private static CosmosClient DirectCosmosClient;
        private static CosmosClient GatewayCosmosClient;
        private const string DatabaseId = "CosmosBasicQueryTests";
        private const string ContainerId = "ContainerBasicQueryTests";

        [ClassInitialize]
        public static async Task TestInit(TestContext textContext)
        {
            CosmosBasicQueryTests.DirectCosmosClient = TestCommon.CreateCosmosClient();
            CosmosBasicQueryTests.GatewayCosmosClient = TestCommon.CreateCosmosClient((builder) => builder.WithConnectionModeGateway());
            CosmosDatabase database = await DirectCosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseId);
            await database.CreateContainerIfNotExistsAsync(ContainerId, "/pk");
        }

        [ClassCleanup]
        public static async Task TestCleanup()
        {
            if (CosmosBasicQueryTests.DirectCosmosClient == null)
            {
                return;
            }

            CosmosDatabase database = DirectCosmosClient.GetDatabase(DatabaseId);
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
            List<CosmosDatabase> deleteList = new List<CosmosDatabase>();
            List<string> createdIds = new List<string>();

            try
            {
                CosmosDatabaseResponse createResponse = await client.CreateDatabaseIfNotExistsAsync(id: "BasicQueryDb1");
                deleteList.Add(createResponse.Database);
                createdIds.Add(createResponse.Database.Id);

                createResponse = await client.CreateDatabaseIfNotExistsAsync(id: "BasicQueryDb2");
                deleteList.Add(createResponse.Database);
                createdIds.Add(createResponse.Database.Id);

                createResponse = await client.CreateDatabaseIfNotExistsAsync(id: "BasicQueryDb3");
                deleteList.Add(createResponse.Database);
                createdIds.Add(createResponse.Database.Id);

                //Read All
                List<CosmosDatabaseProperties> results = await this.ToListAsync(
                    client.GetDatabaseQueryStreamResultsAsync,
                    client.GetDatabaseQueryResultsAsync<CosmosDatabaseProperties>,
                    null,
                    CosmosBasicQueryTests.RequestOptions);

                CollectionAssert.IsSubsetOf(createdIds, results.Select(x => x.Id).ToList());

                //Basic query
                List<CosmosDatabaseProperties> queryResults = await this.ToListAsync(
                    client.GetDatabaseQueryStreamResultsAsync,
                    client.GetDatabaseQueryResultsAsync<CosmosDatabaseProperties>,
                    "select * from T where STARTSWITH(T.id, \"BasicQueryDb\")",
                    CosmosBasicQueryTests.RequestOptions);

                CollectionAssert.AreEquivalent(createdIds, queryResults.Select(x => x.Id).ToList());
            }
            finally
            {
                foreach (Cosmos.CosmosDatabase database in deleteList)
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
            CosmosDatabase database = client.GetDatabase(DatabaseId);
            List<string> createdIds = new List<string>();

            try
            {
                CosmosContainerResponse createResponse = await database.CreateContainerIfNotExistsAsync(id: "BasicQueryContainer1", partitionKeyPath: "/pk");
                createdIds.Add(createResponse.Container.Id);

                createResponse = await database.CreateContainerIfNotExistsAsync(id: "BasicQueryContainer2", partitionKeyPath: "/pk2");
                createdIds.Add(createResponse.Container.Id);

                createResponse = await database.CreateContainerIfNotExistsAsync(id: "BasicQueryContainer3", partitionKeyPath: "/pk3");
                createdIds.Add(createResponse.Container.Id);

                //Read All
                List<CosmosContainerProperties> results = await this.ToListAsync(
                    database.GetContainerQueryStreamResultsAsync,
                    database.GetContainerQueryResultsAsync<CosmosContainerProperties>,
                    null,
                    CosmosBasicQueryTests.RequestOptions);

                CollectionAssert.IsSubsetOf(createdIds, results.Select(x => x.Id).ToList());

                //Basic query
                List<CosmosContainerProperties> queryResults = await this.ToListAsync(
                    database.GetContainerQueryStreamResultsAsync,
                    database.GetContainerQueryResultsAsync<CosmosContainerProperties>,
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
        public async Task ItemTest(bool directMode)
        {
            CosmosClient client = directMode ? DirectCosmosClient : GatewayCosmosClient;
            CosmosContainer container = client.GetContainer(DatabaseId, ContainerId);
            List<string> createdIds = new List<string>()
            {
                "BasicQueryItem",
                "BasicQueryItem2",
                "BasicQueryItem3"
            };

            List<dynamic> queryResults = await this.ToListAsync(
                  container.GetItemQueryStreamResultsAsync,
                 container.GetItemQueryResultsAsync<dynamic>,
                 "select * from T where STARTSWITH(T.id, \"BasicQueryItem\")",
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
                  container.GetItemQueryStreamResultsAsync,
                 container.GetItemQueryResultsAsync<dynamic>,
                 "select * from T where STARTSWITH(T.id, \"BasicQueryItem\")",
                 CosmosBasicQueryTests.RequestOptions);
            }

            List<string> ids = queryResults.Select(x => ((JsonElement)x).GetProperty("id").GetString()).ToList();
            CollectionAssert.AreEquivalent(createdIds, ids);

            //Read All
            List<dynamic> results = await this.ToListAsync(
                container.GetItemQueryStreamResultsAsync,
                container.GetItemQueryResultsAsync<dynamic>,
                null,
                CosmosBasicQueryTests.RequestOptions);


            ids = results.Select(x => ((JsonElement)x).GetProperty("id").GetString()).ToList();
            CollectionAssert.IsSubsetOf(createdIds, ids);

            //Read All with partition key
            results = await this.ToListAsync(
               container.GetItemQueryStreamResultsAsync,
               container.GetItemQueryResultsAsync<dynamic>,
               null,
               new QueryRequestOptions()
               {
                   MaxItemCount = 1,
                   PartitionKey = new PartitionKey("BasicQueryItem")
               });

            Assert.AreEqual(1, results.Count);

            //Read All with partition key
            //results = container.GetItemLinqQueryable<dynamic>(
            //    allowSynchronousQueryExecution: true,
            //    requestOptions: new QueryRequestOptions()
            //    {
            //        MaxItemCount = 1,
            //        PartitionKey = new PartitionKey("BasicQueryItem")
            //    }).ToList();

            //Assert.AreEqual(1, results.Count);

            //// LINQ to feed iterator Read All with partition key
            //FeedIterator<dynamic> iterator = container.GetItemLinqQueryable<dynamic>(
            //    allowSynchronousQueryExecution: true,
            //    requestOptions: new QueryRequestOptions()
            //    {
            //        MaxItemCount = 1,
            //        PartitionKey = new PartitionKey("BasicQueryItem")
            //    }).ToFeedIterator();

            //List<dynamic> linqResults = new List<dynamic>();
            //while (iterator.HasMoreResults)
            //{
            //    linqResults.AddRange(await iterator.ReadNextAsync());
            //}

            //Assert.AreEqual(1, linqResults.Count);
            //Assert.AreEqual("BasicQueryItem", linqResults.First().pk.ToString());
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ScriptsStoredProcedureTest(bool directMode)
        {
            CosmosClient client = directMode ? DirectCosmosClient : GatewayCosmosClient;
            CosmosScripts scripts = client.GetContainer(DatabaseId, ContainerId).Scripts;

            List<string> createdIds = new List<string>()
            {
                "BasicQuerySp1",
                "BasicQuerySp2",
                "BasicQuerySp3"
            };

            //Basic query
            List<StoredProcedureProperties> queryResults = await this.ToListAsync(
                scripts.GetStoredProcedureQueryStreamResultsAsync,
                scripts.GetStoredProcedureQueryResultsAsync<StoredProcedureProperties>,
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
                    scripts.GetStoredProcedureQueryStreamResultsAsync,
                    scripts.GetStoredProcedureQueryResultsAsync<StoredProcedureProperties>,
                    "select * from T where STARTSWITH(T.id, \"BasicQuerySp\")",
                    CosmosBasicQueryTests.RequestOptions);
            }

            CollectionAssert.AreEquivalent(createdIds, queryResults.Select(x => x.Id).ToList());

            //Read All
            List<StoredProcedureProperties> results = await this.ToListAsync(
                scripts.GetStoredProcedureQueryStreamResultsAsync,
                scripts.GetStoredProcedureQueryResultsAsync<StoredProcedureProperties>,
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
            CosmosScripts scripts = client.GetContainer(DatabaseId, ContainerId).Scripts;

            List<string> createdIds = new List<string>()
            {
                "BasicQueryUdf1",
                "BasicQueryUdf2",
                "BasicQueryUdf3"
            };

            //Basic query
            List<UserDefinedFunctionProperties> queryResults = await this.ToListAsync(
                scripts.GetUserDefinedFunctionQueryStreamResultsAsync,
                scripts.GetUserDefinedFunctionQueryResultsAsync<UserDefinedFunctionProperties>,
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
                    scripts.GetUserDefinedFunctionQueryStreamResultsAsync,
                    scripts.GetUserDefinedFunctionQueryResultsAsync<UserDefinedFunctionProperties>,
                    "select * from T where STARTSWITH(T.id, \"BasicQueryUdf\")",
                    CosmosBasicQueryTests.RequestOptions);
            }

            CollectionAssert.AreEquivalent(createdIds, queryResults.Select(x => x.Id).ToList());

            //Read All
            List<UserDefinedFunctionProperties> results = await this.ToListAsync(
                scripts.GetUserDefinedFunctionQueryStreamResultsAsync,
                scripts.GetUserDefinedFunctionQueryResultsAsync<UserDefinedFunctionProperties>,
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
            CosmosScripts scripts = client.GetContainer(DatabaseId, ContainerId).Scripts;

            List<string> createdIds = new List<string>()
            {
                "BasicQueryTrigger1",
                "BasicQueryTrigger2",
                "BasicQueryTrigger3"
            };

            //Basic query
            List<TriggerProperties> queryResults = await this.ToListAsync(
                scripts.GetTriggerQueryStreamResultsAsync,
                scripts.GetTriggerQueryResultsAsync<TriggerProperties>,
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
                    scripts.GetTriggerQueryStreamResultsAsync,
                    scripts.GetTriggerQueryResultsAsync<TriggerProperties>,
                    "select * from T where STARTSWITH(T.id, \"BasicQueryTrigger\")",
                    CosmosBasicQueryTests.RequestOptions);
            }

            CollectionAssert.AreEquivalent(createdIds, queryResults.Select(x => x.Id).ToList());

            //Read All
            List<TriggerProperties> results = await this.ToListAsync(
                scripts.GetTriggerQueryStreamResultsAsync,
                scripts.GetTriggerQueryResultsAsync<TriggerProperties>,
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
            DatabaseCore database = (DatabaseCore)client.GetDatabase(DatabaseId);
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
                    database.GetUserQueryStreamResultsAsync,
                    database.GetUserQueryResultsAsync<UserProperties>,
                    null,
                    CosmosBasicQueryTests.RequestOptions
                );

                CollectionAssert.IsSubsetOf(createdIds, results.Select(x => x.Id).ToList());

                //Basic query
                List<UserProperties> queryResults = await this.ToListAsync(
                    database.GetUserQueryStreamResultsAsync,
                    database.GetUserQueryResultsAsync<UserProperties>,
                    "select * from T where STARTSWITH(T.id, \"BasicQueryUser\")",
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
            CosmosDatabase database = client.GetDatabase(DatabaseId);
            List<string> createdPermissionIds = new List<string>();
            List<string> createdContainerIds = new List<string>();
            string userId = Guid.NewGuid().ToString();
            UserCore user = null;

            try
            {
                UserResponse createUserResponse = await database.CreateUserAsync(userId);
                Assert.AreEqual((int)HttpStatusCode.Created, createUserResponse.GetRawResponse().Status);
                user = (UserCore)createUserResponse.User;

                CosmosContainerResponse createContainerResponse = await database.CreateContainerIfNotExistsAsync(Guid.NewGuid().ToString(), partitionKeyPath: "/pk");
                CosmosContainer container = createContainerResponse.Container;
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
                    user.GetPermissionQueryStreamResultsAsync,
                    user.GetPermissionQueryResultsAsync<PermissionProperties>,
                    null,
                    CosmosBasicQueryTests.RequestOptions
                );

                CollectionAssert.IsSubsetOf(createdPermissionIds, results.Select(x => x.Id).ToList());

                //Basic query
                List<PermissionProperties> queryResults = await this.ToListAsync(
                    user.GetPermissionQueryStreamResultsAsync,
                    user.GetPermissionQueryResultsAsync<PermissionProperties>,
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

        private delegate AsyncPageable<T> Query<T>(string querytext, string continuationToken, QueryRequestOptions options, CancellationToken cancellationToken = default(CancellationToken));
        private delegate IAsyncEnumerable<Response> QueryStream(string querytext, string continuationToken, QueryRequestOptions options, CancellationToken cancellationToken = default(CancellationToken));

        private async Task<List<T>> ToListAsync<T>(
            QueryStream createStreamQuery,
            Query<T> createQuery,
            string queryText,
            QueryRequestOptions requestOptions)
        {
            HttpStatusCode expectedStatus = HttpStatusCode.OK;
            IAsyncEnumerable<Response> feedStreamIterator = createStreamQuery(queryText, null, requestOptions);
            List<T> streamResults = new List<T>();
            await foreach(Response response in feedStreamIterator)
            {
                response.EnsureSuccessStatusCode();
                Assert.AreEqual((int)expectedStatus, response.Status);

                StreamReader sr = new StreamReader(response.ContentStream);
                string result = await sr.ReadToEndAsync();
                ICollection<T> responseResults = JsonSerializer.Deserialize<CosmosFeedResponseUtil<T>>(result, this.jsonSerializerOptions.Value).Data;
                Assert.IsTrue(responseResults.Count <= 1);

                streamResults.AddRange(responseResults);
            }

            string continuationToken = null;
            List<T> pagedStreamResults = new List<T>();
            do
            {
                IAsyncEnumerable<Response> pagedFeedIterator = createStreamQuery(queryText, continuationToken, requestOptions);
                Response response = null;
                await foreach(Response response1 in pagedFeedIterator)
                {
                    response = response1;
                    break;
                }
                
                response.EnsureSuccessStatusCode();
                Assert.AreEqual((int)expectedStatus, response.Status);

                ICollection<T> responseResults = TestCommon.Serializer.Value.FromStream<CosmosFeedResponseUtil<T>>(response.ContentStream).Data;
                Assert.IsTrue(responseResults.Count <= 1);

                pagedStreamResults.AddRange(responseResults);
                continuationToken = response.Headers.GetContinuationToken();
            } while (continuationToken != null);

            Assert.AreEqual(pagedStreamResults.Count, streamResults.Count);

            // Both lists should be the same if not PermssionsProperties. PermissionProperties will have a different ResouceToken in the payload when read.
            string streamResultString = JsonSerializer.Serialize(streamResults, this.jsonSerializerOptions.Value);
            string streamPagedResultString = JsonSerializer.Serialize(pagedStreamResults, this.jsonSerializerOptions.Value);

            if (typeof(T) != typeof(PermissionProperties))
            {
                Assert.AreEqual(streamPagedResultString, streamResultString);
            }

            AsyncPageable<T> feedIterator = createQuery(queryText, null, requestOptions);
            List<T> results = new List<T>();
            await foreach(Page<T> response in feedIterator.AsPages())
            {
                Assert.AreEqual((int)expectedStatus, response.GetRawResponse().Status);
                Assert.IsTrue(response.Values.Count <= 1);

                results.AddRange(response.Values);
            }

            continuationToken = null;
            List<T> pagedResults = new List<T>();
            do
            {
                AsyncPageable<T> pagedFeedIterator = createQuery(queryText, continuationToken, requestOptions);
                await foreach(Page<T> response in pagedFeedIterator.AsPages())
                {
                    Assert.AreEqual((int)expectedStatus, response.GetRawResponse().Status);
                    Assert.IsTrue(response.Values.Count <= 1);
                    pagedResults.AddRange(response.Values);
                    continuationToken = response.ContinuationToken;
                    break;
                }
            } while (continuationToken != null);

            Assert.AreEqual(pagedResults.Count, results.Count);

            // Both lists should be the same
            string resultString = JsonSerializer.Serialize(results, this.jsonSerializerOptions.Value);
            string pagedResultString = JsonSerializer.Serialize(pagedResults, this.jsonSerializerOptions.Value);

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

        private Lazy<JsonSerializerOptions> jsonSerializerOptions = new Lazy<JsonSerializerOptions>(() =>
        {
            JsonSerializerOptions options = new JsonSerializerOptions();
            CosmosTextJsonSerializer.InitializeRESTConverters(options);
            return options;
        });

        //[TestMethod]
        //public async Task InvalidRangesOnQuery()
        //{
        //    CosmosClient cosmosClient = DirectCosmosClient;

        //    DatabaseResponse databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseName, Throughput);
        //    Database database = databaseResponse.Database;

        //    Container container = await database.DefineContainer(TestCollection, $"/{DefaultKey}")
        //        .WithUniqueKey().Path($"/{DefaultKey}").Attach().CreateIfNotExistsAsync();

        //    List<string> queryKeys = new List<string>();

        //    List<TestCollectionObject> testCollectionObjects = JsonConvert.DeserializeObject<List<TestCollectionObject>>(
        //        "[{\"id\":\"70627503-7cb2-4a79-bcec-5e55765aa080\",\"objectKey\":\"message~phone~u058da564bfaa66cb031606db664dbfda~phone~ud75ce020af5f8bfb75a9097a66d452f2~Chat~20190927000042Z\",\"text\":null,\"text2\":null},{\"id\":\"507079b7-a5be-4da4-9158-16fc961cd474\",\"objectKey\":\"message~phone~u058da564bfaa66cb031606db664dbfda~phone~ud75ce020af5f8bfb75a9097a66d452f2~Chat~20190927125742Z\",\"text\":null,\"text2\":null}]");
        //    foreach (TestCollectionObject testCollectionObject in testCollectionObjects)
        //    {
        //        await WriteDocument(container, testCollectionObject);
        //        queryKeys.Add(testCollectionObject.ObjectKey);
        //    }

        //    List<TestCollectionObject> results = container
        //        .GetItemLinqQueryable<TestCollectionObject>(true, requestOptions: RunInParallelOptions())
        //        .Where(r => queryKeys.Contains(r.ObjectKey))
        //        .ToList(); // ERROR OCCURS WHEN QUERY IS EXECUTED

        //    Console.WriteLine($"[\"{string.Join("\", \n\"", results.Select(r => r.ObjectKey))}\"]");
        //}

        private static async Task WriteDocument(CosmosContainer container, TestCollectionObject testData)
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
        [JsonPropertyName("id")]
        public Guid Id { get; set; }
        [JsonPropertyName(CosmosBasicQueryTests.DefaultKey)]
        public string ObjectKey { get; set; }
        [JsonPropertyName("text")]
        public string Text { get; set; }
        [JsonPropertyName("text2")]
        public string Text2 { get; set; }
    }
}
