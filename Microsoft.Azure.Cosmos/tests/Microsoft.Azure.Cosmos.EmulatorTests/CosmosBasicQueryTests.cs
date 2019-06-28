//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Cosmos.Scripts;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class CosmosBasicQueryTests
    {
        private static readonly QueryRequestOptions RequestOptions = new QueryRequestOptions() { MaxItemCount = 1 };
        private static readonly CosmosSerializer CosmosSerializer = new CosmosJsonSerializerCore();
        private static CosmosClient DirectCosmosClient;
        private static CosmosClient GatewayCosmosClient;
        private const string DatabaseId = "CosmosBasicQueryTests";
        private const string ContainerId = "ContainerBasicQueryTests";

        [ClassInitialize]
        public static async Task TestInit(TestContext textContext)
        {
            CosmosBasicQueryTests.DirectCosmosClient = TestCommon.CreateCosmosClient();
            CosmosBasicQueryTests.GatewayCosmosClient = TestCommon.CreateCosmosClient((builder) => builder.WithConnectionModeGateway());
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
            await database.DeleteAsync();

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
                    null);

                CollectionAssert.IsSubsetOf(createdIds, results.Select(x => x.Id).ToList());

                //Basic query
                List<DatabaseProperties> queryResults = await this.ToListAsync(
                    client.GetDatabaseQueryStreamIterator,
                    client.GetDatabaseQueryIterator<DatabaseProperties>,
                    "select * from T where STARTSWITH(T.id, \"BasicQueryDb\")");

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
                    null);

                CollectionAssert.IsSubsetOf(createdIds, results.Select(x => x.Id).ToList());

                //Basic query
                List<ContainerProperties> queryResults = await this.ToListAsync(
                    database.GetContainerQueryStreamIterator,
                    database.GetContainerQueryIterator<ContainerProperties>,
                    "select * from T where STARTSWITH(T.id, \"BasicQueryContainer\")");

                CollectionAssert.AreEquivalent(createdIds, queryResults.Select(x => x.Id).ToList());
            }
            finally
            {
                foreach (var id in createdIds)
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
            Container container = client.GetContainer(DatabaseId, ContainerId);
            List<string> createdIds = new List<string>()
            {
                "BasicQueryItem",
                "BasicQueryItem2",
                "BasicQueryItem3"
            };

            List<dynamic> queryResults = await this.ToListAsync(
                  container.GetItemQueryStreamIterator,
                 container.GetItemQueryIterator<dynamic>,
                 "select * from T where STARTSWITH(T.id, \"BasicQueryItem\")");

            if (queryResults.Count < 3)
            {
                foreach(string id in createdIds)
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
                 "select * from T where STARTSWITH(T.id, \"BasicQueryItem\")");
            }

            List<string> ids = queryResults.Select(x => (string)x.id).ToList();
            CollectionAssert.AreEquivalent(createdIds, ids);

            //Read All
            List<dynamic> results = await this.ToListAsync(
                container.GetItemQueryStreamIterator,
                container.GetItemQueryIterator<dynamic>,
                null);

            ids = results.Select(x => (string)x.id).ToList();
            CollectionAssert.IsSubsetOf(createdIds, ids);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ScriptsStoredProcedureTest(bool directMode)
        {
            CosmosClient client = directMode ? DirectCosmosClient : GatewayCosmosClient;
            Scripts scripts = client.GetContainer(DatabaseId, ContainerId).Scripts;

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
                "select * from T where STARTSWITH(T.id, \"BasicQuerySp\")");

            if(queryResults.Count < 3)
            {
                foreach(string id in createdIds)
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
                    "select * from T where STARTSWITH(T.id, \"BasicQuerySp\")");
            }

            CollectionAssert.AreEquivalent(createdIds, queryResults.Select(x => x.Id).ToList());

            //Read All
            List<StoredProcedureProperties> results = await this.ToListAsync(
                scripts.GetStoredProcedureQueryStreamIterator,
                scripts.GetStoredProcedureQueryIterator<StoredProcedureProperties>,
                null);

            CollectionAssert.IsSubsetOf(createdIds, results.Select(x => x.Id).ToList());
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ScriptsUserDefinedFunctionTest(bool directMode)
        {
            CosmosClient client = directMode ? DirectCosmosClient : GatewayCosmosClient;
            Scripts scripts = client.GetContainer(DatabaseId, ContainerId).Scripts;

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
                "select * from T where STARTSWITH(T.id, \"BasicQueryUdf\")");

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
                    "select * from T where STARTSWITH(T.id, \"BasicQueryUdf\")");
            }

            CollectionAssert.AreEquivalent(createdIds, queryResults.Select(x => x.Id).ToList());

            //Read All
            List<UserDefinedFunctionProperties> results = await this.ToListAsync(
                scripts.GetUserDefinedFunctionQueryStreamIterator,
                scripts.GetUserDefinedFunctionQueryIterator<UserDefinedFunctionProperties>,
                null);

            CollectionAssert.IsSubsetOf(createdIds, results.Select(x => x.Id).ToList());
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ScriptsTriggerTest(bool directMode)
        {
            CosmosClient client = directMode ? DirectCosmosClient : GatewayCosmosClient;
            Scripts scripts = client.GetContainer(DatabaseId, ContainerId).Scripts;

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
                "select * from T where STARTSWITH(T.id, \"BasicQueryTrigger\")");

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
                    "select * from T where STARTSWITH(T.id, \"BasicQueryTrigger\")");
            }

            CollectionAssert.AreEquivalent(createdIds, queryResults.Select(x => x.Id).ToList());

            //Read All
            List<TriggerProperties> results = await this.ToListAsync(
                scripts.GetTriggerQueryStreamIterator,
                scripts.GetTriggerQueryIterator<TriggerProperties>,
                null);

            CollectionAssert.IsSubsetOf(createdIds, results.Select(x => x.Id).ToList());
        }

        private delegate FeedIterator<T> Query<T>(string querytext, string continuationToken, QueryRequestOptions options);
        private delegate FeedIterator QueryStream(string querytext, string continuationToken, QueryRequestOptions options);

        private async Task<List<T>> ToListAsync<T>(QueryStream createStreamQuery, Query<T> createQuery, string queryText)
        {
            FeedIterator feedStreamIterator = createStreamQuery(queryText, null, RequestOptions);
            List<T> streamResults = new List<T>();
            while (feedStreamIterator.HasMoreResults)
            {
                ResponseMessage response = await feedStreamIterator.ReadNextAsync();
                response.EnsureSuccessStatusCode();

                StreamReader sr = new StreamReader(response.Content);
                string result = await sr.ReadToEndAsync();
                ICollection<T> responseResults = JsonConvert.DeserializeObject<CosmosFeedResponseUtil<T>>(result).Data;
                Assert.IsTrue(responseResults.Count <= 1);

                streamResults.AddRange(responseResults);
            }

            string continuationToken = null;
            List<T> pagedStreamResults = new List<T>();
            do
            {
                FeedIterator pagedFeedIterator = createStreamQuery(queryText, continuationToken, RequestOptions);
                ResponseMessage response = await pagedFeedIterator.ReadNextAsync();
                response.EnsureSuccessStatusCode();

                ICollection<T> responseResults = CosmosSerializer.FromStream<CosmosFeedResponseUtil<T>>(response.Content).Data;
                Assert.IsTrue(responseResults.Count <= 1);

                pagedStreamResults.AddRange(responseResults);
                continuationToken = response.Headers.ContinuationToken;
            } while (continuationToken != null);

            Assert.AreEqual(pagedStreamResults.Count, streamResults.Count);

            // Both lists should be the same
            string streamResultString = JsonConvert.SerializeObject(streamResults);
            string streamPagedResultString = JsonConvert.SerializeObject(pagedStreamResults);
            Assert.AreEqual(streamPagedResultString, streamResultString);

            FeedIterator<T> feedIterator = createQuery(queryText, null, RequestOptions);
            List<T> results = new List<T>();
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<T> iterator = await feedIterator.ReadNextAsync();
                Assert.IsTrue(iterator.Count <= 1);
                Assert.IsTrue(iterator.Resource.Count() <= 1);

                results.AddRange(iterator);
            }

            continuationToken = null;
            List<T> pagedResults = new List<T>();
            do
            {
                FeedIterator<T> pagedFeedIterator = createQuery(queryText, continuationToken, RequestOptions);
                FeedResponse<T> iterator = await pagedFeedIterator.ReadNextAsync();
                Assert.IsTrue(iterator.Count <= 1);
                Assert.IsTrue(iterator.Resource.Count() <= 1);
                pagedResults.AddRange(iterator);
                continuationToken = iterator.ContinuationToken;
            } while (continuationToken != null);

            Assert.AreEqual(pagedResults.Count, results.Count);

            // Both lists should be the same
            string resultString = JsonConvert.SerializeObject(results);
            string pagedResultString = JsonConvert.SerializeObject(pagedResults);
            Assert.AreEqual(pagedResultString, resultString);

            Assert.AreEqual(streamPagedResultString, resultString);
            return results;
        }
    }
}
