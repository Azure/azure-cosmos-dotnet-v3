//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Cosmos.Scripts;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    [TestClass]
    public class PartitionKeyDeleteQueryHeaderTests
    {
        private static readonly QueryRequestOptions RequestOptions = new QueryRequestOptions() { MaxItemCount = 1 };
        private static CosmosClient DirectCosmosClient;
        private static CosmosClient GatewayCosmosClient;
        private const string DatabaseId = "PartitionKeyDeleteQueryHeaderTests";
        private static readonly string ContainerId = "PartitionKeyDeleteQueryHeaderTests" + Guid.NewGuid();

        [ClassInitialize]
        public static async Task TestInit(TestContext textContext)
        {
            PartitionKeyDeleteQueryHeaderTests.DirectCosmosClient = TestCommon.CreateCosmosClient();
            PartitionKeyDeleteQueryHeaderTests.GatewayCosmosClient = TestCommon.CreateCosmosClient((builder) => builder.WithConnectionModeGateway());
            Database database = await DirectCosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseId);
            await database.CreateContainerIfNotExistsAsync(ContainerId, "/pk");
        }

        [ClassCleanup]
        public static async Task TestCleanup()
        {
            if (PartitionKeyDeleteQueryHeaderTests.DirectCosmosClient == null)
            {
                return;
            }

            Database database = DirectCosmosClient.GetDatabase(DatabaseId);
            await database.DeleteStreamAsync();

            PartitionKeyDeleteQueryHeaderTests.DirectCosmosClient.Dispose();
            PartitionKeyDeleteQueryHeaderTests.GatewayCosmosClient.Dispose();
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task PendingPKDeleteHeaderTest(bool directMode)
        {
            CosmosClient client = directMode ? DirectCosmosClient : GatewayCosmosClient;
            Database database = client.GetDatabase(DatabaseId);
            Container container = await database.CreateContainerAsync(Guid.NewGuid().ToString(), "/pk");

            string pKString = "PK1";

            ContainerInternal containerInternal = (ContainerInternal)container;

            for (int i = 0; i < 1000; i++)
            {
                dynamic item = new
                {
                    id = "Item" + i,
                    pk = pKString,
                };

                ItemResponse<dynamic> createResponse = await container.CreateItemAsync<dynamic>(item: item);
            }

            string queryText = "select * from t where t.pk = '" + pKString + "'";

            //Read All
            List<dynamic> results = await this.ToListAsync(
                container.GetItemQueryStreamIterator,
                container.GetItemQueryIterator<dynamic>,
                queryText,
                PartitionKeyDeleteQueryHeaderTests.RequestOptions);

            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey(pKString);
            await containerInternal.DeleteAllItemsByPartitionKeyStreamAsync(partitionKey);

            //Read All
            results = await this.ToListAsync(
                container.GetItemQueryStreamIterator,
                container.GetItemQueryIterator<dynamic>,
                queryText,
                PartitionKeyDeleteQueryHeaderTests.RequestOptions,
                true);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task PendingPKDeleteHeaderMuliplePartitionTest(bool directMode)
        {
            CosmosClient client = directMode ? DirectCosmosClient : GatewayCosmosClient;
            Database database = client.GetDatabase(DatabaseId);
            Container container = await database.CreateContainerAsync(Guid.NewGuid().ToString(), "/pk", 12000);

            string pKString = "PK";
            //string idString = "Item";

            ContainerInternal containerInternal = (ContainerInternal)container;

            for (int i = 0; i < 1000; i++)
            {
                dynamic item = new
                {
                    id = "Item" + i,
                    pk = pKString + "1" ,
                };

                ItemResponse<dynamic> createResponse = await container.CreateItemAsync<dynamic>(item: item);
            }

            for (int i = 0; i < 1000; i++)
            {
                dynamic item = new
                {
                    id = "Item" + i,
                    pk = pKString + "2",
                };

                ItemResponse<dynamic> createResponse = await container.CreateItemAsync<dynamic>(item: item);
            }

            string queryText = "select * from t where t.pk = '" + pKString + "1" + "'";

            //Read All
            List<dynamic> results = await this.ToListAsync(
                container.GetItemQueryStreamIterator,
                container.GetItemQueryIterator<dynamic>,
                queryText,
                PartitionKeyDeleteQueryHeaderTests.RequestOptions);

            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey(pKString + "1");
            await containerInternal.DeleteAllItemsByPartitionKeyStreamAsync(partitionKey);

            //Read All
            results = await this.ToListAsync(
                container.GetItemQueryStreamIterator,
                container.GetItemQueryIterator<dynamic>,
                queryText,
                PartitionKeyDeleteQueryHeaderTests.RequestOptions,
                true);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task PendingPKDeleteHeaderFalseTest(bool directMode)
        {
            CosmosClient client = directMode ? DirectCosmosClient : GatewayCosmosClient;
            Database database = client.GetDatabase(DatabaseId);
            Container container = await database.CreateContainerAsync(Guid.NewGuid().ToString(), "/pk");

            string pKString = "PK1";

            ContainerInternal containerInternal = (ContainerInternal)container;

            for (int i = 0; i < 100; i++)
            {
                dynamic item = new
                {
                    id = "Item" + i,
                    pk = pKString,
                };

                ItemResponse<dynamic> createResponse = await container.CreateItemAsync<dynamic>(item: item);
            }

            string queryText = "select * from t where t.pk = '" + pKString + "'";

            //Read All
            List<dynamic> results = await this.ToListAsync(
                container.GetItemQueryStreamIterator,
                container.GetItemQueryIterator<dynamic>,
                queryText,
                PartitionKeyDeleteQueryHeaderTests.RequestOptions);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task PKDeleteConfirmCompletedTest(bool directMode)
        {
            CosmosClient client = directMode ? DirectCosmosClient : GatewayCosmosClient;
            Database database = client.GetDatabase(DatabaseId);
            Container container = await database.CreateContainerAsync(Guid.NewGuid().ToString(), "/pk");

            string pKString = "PK1";

            ContainerInternal containerInternal = (ContainerInternal)container;

            for (int i = 0; i < 100; i++)
            {
                dynamic item = new
                {
                    id = "Item" + i,
                    pk = pKString,
                };

                ItemResponse<dynamic> createResponse = await container.CreateItemAsync<dynamic>(item: item);
            }

            string queryText = "select * from t where t.pk = '" + pKString + "'";

            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey(pKString);
            await containerInternal.DeleteAllItemsByPartitionKeyStreamAsync(partitionKey);

            //Read All
            List<dynamic> results = await this.ToListAsync(
                container.GetItemQueryStreamIterator,
                container.GetItemQueryIterator<dynamic>,
                queryText,
                PartitionKeyDeleteQueryHeaderTests.RequestOptions,
                true);

            // Check progress
            ContainerRequestOptions requestOptions = new ContainerRequestOptions();
            requestOptions.PopulateQuotaInfo = true;

            await Task.Delay(TimeSpan.FromMilliseconds(1000));
            ContainerResponse readResponse = await container.ReadContainerAsync(requestOptions);
            string currentresourcequotausage = readResponse.Headers["x-ms-resource-usage"];
            Assert.IsTrue(currentresourcequotausage.Contains("documentsCount=0"));
        }

        private delegate FeedIterator<T> Query<T>(string querytext, string continuationToken, QueryRequestOptions options);
        private delegate FeedIterator QueryStream(string querytext, string continuationToken, QueryRequestOptions options);

        private async Task<List<T>> ToListAsync<T>(
            QueryStream createStreamQuery,
            Query<T> createQuery,
            string queryText,
            QueryRequestOptions requestOptions,
            bool expectedPKDelete = false)
        {
            HttpStatusCode expectedStatus = HttpStatusCode.OK;
            FeedIterator feedStreamIterator = createStreamQuery(queryText, null, requestOptions);
            List<T> streamResults = new List<T>();
            while (feedStreamIterator.HasMoreResults)
            {
                ResponseMessage response = await feedStreamIterator.ReadNextAsync();
                response.EnsureSuccessStatusCode();
                Assert.AreEqual(expectedStatus, response.StatusCode);
                Assert.AreEqual(expectedPKDelete.ToString(), response.Headers.PendingPartitionKeyDelete);

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
}
