//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class CosmosBasicQueryTests
    {
        private static readonly QueryRequestOptions RequestOptions = new QueryRequestOptions() { MaxItemCount = 1 };
        private static readonly CosmosSerializer CosmosSerializer = new CosmosJsonSerializerCore();
        protected static CosmosClient CosmosClient;
        protected static Database Database;
        protected static Container Container;

        [ClassInitialize]
        public static async Task TestInit(TestContext textContext)
        {
            CosmosBasicQueryTests.CosmosClient = TestCommon.CreateCosmosClient();
            CosmosBasicQueryTests.Database = await CosmosClient.CreateDatabaseIfNotExistsAsync("CosmosBasicQueryTests");
            CosmosBasicQueryTests.Container = await Database.CreateContainerIfNotExistsAsync("ContainerBaseicQueryTests", "/pk");
        }

        [ClassCleanup]
        public static void TestCleanup()
        {
            if (CosmosBasicQueryTests.CosmosClient == null)
            {
                return;
            }

            if (CosmosBasicQueryTests.Database != null)
            {
                CosmosBasicQueryTests.Database.DeleteAsync();
            }

            CosmosBasicQueryTests.CosmosClient.Dispose();
        }

        [TestMethod]
        public async Task DatabaseTest()
        {
            List<Database> deleteList = new List<Database>();
            HashSet<string> createdIds = new HashSet<string>();

            try
            {
                DatabaseResponse createResponse = await CosmosClient.CreateDatabaseIfNotExistsAsync(id: "BasicQuery1");
                deleteList.Add(createResponse.Database);
                createdIds.Add(createResponse.Database.Id);

                createResponse = await CosmosClient.CreateDatabaseIfNotExistsAsync(id: "BasicQuery2");
                deleteList.Add(createResponse.Database);
                createdIds.Add(createResponse.Database.Id);

                createResponse = await CosmosClient.CreateDatabaseIfNotExistsAsync(id: "BasicQuery3");
                deleteList.Add(createResponse.Database);
                createdIds.Add(createResponse.Database.Id);

                //Read All
                List<DatabaseProperties> results = await this.ToListAsync(
                    CosmosClient.GetDatabaseQueryStreamIterator,
                    CosmosClient.GetDatabaseQueryIterator<DatabaseProperties>,
                    null);

                HashSet<string> resultIds = new HashSet<string>(results.Select(x => x.Id));
                Assert.IsTrue(createdIds.All(x => resultIds.Contains(x)));

                //Basic query
                List<DatabaseProperties> queryResults = await this.ToListAsync(
                    CosmosClient.GetDatabaseQueryStreamIterator,
                    CosmosClient.GetDatabaseQueryIterator<DatabaseProperties>,
                    "select * from T where STARTSWITH(T.id, \"BasicQuery\"");
                resultIds = new HashSet<string>(queryResults.Select(x => x.Id));
                Assert.IsTrue(createdIds.All(x => resultIds.Contains(x)));
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
        public async Task ContainerTest()
        {
            Database database = CosmosBasicQueryTests.Database;
            HashSet<string> createdIds = new HashSet<string>();

            try
            {
                ContainerResponse createResponse = await database.CreateContainerIfNotExistsAsync(id: "BasicQuery1", partitionKeyPath: "/pk");
                createdIds.Add(createResponse.Container.Id);

                createResponse = await database.CreateContainerIfNotExistsAsync(id: "BasicQuery2", partitionKeyPath: "/pk2");
                createdIds.Add(createResponse.Container.Id);

                createResponse = await database.CreateContainerIfNotExistsAsync(id: "BasicQuery3", partitionKeyPath: "/pk3");
                createdIds.Add(createResponse.Container.Id);

                //Read All
                List<ContainerProperties> results = await this.ToListAsync(
                    database.GetContainerQueryStreamIterator,
                    database.GetContainerQueryIterator<ContainerProperties>,
                    null);

                HashSet<string> resultIds = new HashSet<string>(results.Select(x => x.Id));
                Assert.IsTrue(createdIds.All(x => resultIds.Contains(x)));

                //Basic query
                List<ContainerProperties> queryResults = await this.ToListAsync(
                    database.GetContainerQueryStreamIterator,
                    database.GetContainerQueryIterator<ContainerProperties>,
                    "select * from T where STARTSWITH(T.id, \"BasicQuery\"");

                resultIds = new HashSet<string>(queryResults.Select(x => x.Id));
                Assert.IsTrue(createdIds.All(x => resultIds.Contains(x)));
            }
            finally
            {
                foreach (var id in createdIds)
                {
                    //Don't wait for the container cleanup
                    Task ignore = database.GetContainer(id).DeleteContainerAsync();
                }
            }
        }

        [TestMethod]
        public async Task ItemTest()
        {
            Container container = Container;
            HashSet<string> createdIds = new HashSet<string>();

            List<dynamic> items = new List<dynamic>();
            items.Add(new
            {
                id = "BasicQuery",
                pk = "Test",
            });

            items.Add(new
            {
                id = "BasicQuery2",
                pk = "Test2",
            });

            items.Add(new
            {
                id = "BasicQuery3",
                pk = "Test3",
            });

            foreach (var item in items)
            {
                ItemResponse<dynamic> createResponse = await container.CreateItemAsync<dynamic>(item: item);
                createdIds.Add((string)createResponse.Resource.id);
            }

            //Read All
            List<dynamic> results = await this.ToListAsync(
                container.GetItemQueryStreamIterator,
                container.GetItemQueryIterator<dynamic>,
                null);

            IEnumerable<string> ids = results.Select(x => (string)x.id);
            HashSet<string> resultIds = new HashSet<string>(ids);
            Assert.IsTrue(createdIds.All(x => resultIds.Contains(x)));

            //Basic query
            List<dynamic> queryResults = await this.ToListAsync(
                 container.GetItemQueryStreamIterator,
                container.GetItemQueryIterator<dynamic>,
                "select * from T where STARTSWITH(T.id, \"BasicQuery\"");

            ids = queryResults.Select(x => (string)x.id);
            resultIds = new HashSet<string>(ids);
            Assert.IsTrue(createdIds.All(x => resultIds.Contains(x)));
        }

        private delegate FeedIterator<T> Query<T>(string querytext, string continuationToken, QueryRequestOptions options);
        private delegate FeedIterator QueryStream(string querytext, string continuationToken, QueryRequestOptions options);

        private async Task<List<T>> ToListAsync<T>(QueryStream createStreamQuery, Query<T> createQuery, string queryText)
        {
            FeedIterator feedStreamIterator = createStreamQuery(null, null, RequestOptions);
            List<T> streamResults = new List<T>();
            while (feedStreamIterator.HasMoreResults)
            {
                ResponseMessage response = await feedStreamIterator.ReadNextAsync();
                response.EnsureSuccessStatusCode();

                ICollection<T> responseResults = CosmosSerializer.FromStream<CosmosFeedResponseUtil<T>>(response.Content).Data;
                Assert.AreEqual(1, responseResults.Count);

                streamResults.AddRange(responseResults);
            }

            string continuationToken = null;
            List<T> pagedStreamResults = new List<T>();
            do
            {
                FeedIterator pagedFeedIterator = createStreamQuery(null, continuationToken, RequestOptions);
                ResponseMessage response = await pagedFeedIterator.ReadNextAsync();
                response.EnsureSuccessStatusCode();

                ICollection<T> responseResults = CosmosSerializer.FromStream<CosmosFeedResponseUtil<T>>(response.Content).Data;
                Assert.AreEqual(1, responseResults.Count);

                pagedStreamResults.AddRange(responseResults);
                continuationToken = response.Headers.Continuation;
            } while (continuationToken != null);

            Assert.AreEqual(pagedStreamResults.Count, streamResults.Count);

            // Both lists should be the same
            string streamResultString = JsonConvert.SerializeObject(streamResults);
            string streamPagedResultString = JsonConvert.SerializeObject(pagedStreamResults);
            Assert.AreEqual(streamPagedResultString, streamResultString);

            FeedIterator<T> feedIterator = createQuery(null, null, RequestOptions);
            List<T> results = new List<T>();
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<T> iterator = await feedIterator.ReadNextAsync();
                Assert.AreEqual(1, iterator.Resource.Count());
                Assert.AreEqual(1, iterator.Count);

                results.AddRange(iterator);
            }

            continuationToken = null;
            List<T> pagedResults = new List<T>();
            do
            {
                FeedIterator<T> pagedFeedIterator = createQuery(null, continuationToken, RequestOptions);
                FeedResponse<T> iterator = await pagedFeedIterator.ReadNextAsync();
                Assert.AreEqual(1, iterator.Resource.Count());
                Assert.AreEqual(1, iterator.Count);
                pagedResults.AddRange(iterator);
                continuationToken = iterator.Continuation;
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
