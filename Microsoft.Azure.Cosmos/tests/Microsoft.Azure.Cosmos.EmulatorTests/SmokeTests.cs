//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Linq;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    [TestCategory("Emulator")]
    public class SmokeTests
    {
        private const string DatabaseName = "netcore_test_db";
        private const string CollectionName = "netcore_test_coll";
        private const string PartitionedCollectionName = "netcore_test_pcoll";

        private const string VSTSContainerHostEnvironmentName = "COSMOSDBEMULATOR_ENDPOINT";

        private CosmosClient client;

        static SmokeTests()
        {
        }

        /// <summary>
        /// Test for the existence of native assembly dependencies
        /// </summary>
        [Ignore]
        [TestMethod]
        public void AssembliesExist()
        {
            Assert.IsTrue(Documents.ServiceInteropWrapper.AssembliesExist.Value);
        }

        /// <summary>
        /// Test if 64-bit and native assembly dependencies exist
        /// </summary>
        [Ignore]
        [TestMethod]
        public void ByPassQueryParsing()
        {
            if (IntPtr.Size == 8)
            {
                Assert.IsFalse(Documents.CustomTypeExtensions.ByPassQueryParsing());
            }
            else
            {
                Assert.IsTrue(Documents.CustomTypeExtensions.ByPassQueryParsing());
            }
        }

        [TestMethod]
        public async Task DocumentInsertsTest_GatewayHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Gateway, Documents.Client.Protocol.Https);
            await this.DocumentInsertsTest();
        }

        [TestMethod]
        public async Task DocumentInsertsTest_DirectHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Documents.Client.Protocol.Https);
            await this.DocumentInsertsTest();
        }

        [TestMethod]
        public async Task DocumentInsertsTest_DirectTcp()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Documents.Client.Protocol.Tcp);
            await this.DocumentInsertsTest();
        }

        private async Task DocumentInsertsTest()
        {
            Database database = await this.client.CreateDatabaseIfNotExistsAsync(DatabaseName);
            Container container = await this.CreatePartitionedCollectionIfNotExists(database, PartitionedCollectionName);

            for (int i = 0; i < 2; i++)
            {
                string id = i.ToString();
                await container.CreateItemAsync(new Person() { Id = id, FirstName = "James", LastName = "Smith" });
            }

            IOrderedQueryable<dynamic> query =
                container.GetItemLinqQueryable<dynamic>(allowSynchronousQueryExecution: true);

            Assert.AreEqual(query.ToList().Count, 2);

            await this.CleanupDocumentCollection(container);
        }

        [TestMethod]
        public async Task QueryWithPaginationTest_GatewayHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Gateway, Documents.Client.Protocol.Https);
            await this.QueryWithPagination();
        }

        [TestMethod]
        public async Task QueryWithPaginationTest_DirectHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Documents.Client.Protocol.Https);
            await this.QueryWithPagination();
        }

        [TestMethod]
        public async Task QueryWithPaginationTest_DirectTcp()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Documents.Client.Protocol.Tcp);
            await this.QueryWithPagination();
        }

        private async Task QueryWithPagination()
        {
            Database database = await this.client.CreateDatabaseIfNotExistsAsync(DatabaseName);
            Container container = await this.CreatePartitionedCollectionIfNotExists(database, PartitionedCollectionName);

            Uri documentCollectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, PartitionedCollectionName);

            await container.UpsertItemAsync<Person>(new Person() { Id = "1", FirstName = "David", LastName = "Smith" });
            await container.UpsertItemAsync<Person>(new Person() { Id = "2", FirstName = "Robert", LastName = "Johnson" });
            await container.UpsertItemAsync<Person>(new Person() { Id = "3", FirstName = "William", LastName = "Smith" });

            QueryRequestOptions options = new QueryRequestOptions { MaxItemCount = 1 };

            List<Person> smithFamily =
                container.GetItemLinqQueryable<Person>(true, null, options)
                    .Where(d => d.LastName == "Smith")
                    .ToList();

            Assert.AreEqual(2, smithFamily.Count);

            List<Person> persons =
                container.GetItemLinqQueryable<Person>(true, null, options)
                    .ToList();

            Assert.AreEqual(3, persons.Count);

            IOrderedQueryable<Person> query = container.GetItemLinqQueryable<Person>(true, null, options);
            FeedIterator<Person> documentQuery = query.ToFeedIterator<Person>();

            List<Person> personsList = new List<Person>();

            while (documentQuery.HasMoreResults)
            {
                FeedResponse<Person> feedResponse = await documentQuery.ReadNextAsync();
                int maxItemCount = options.MaxItemCount ?? default(int);
                Assert.IsTrue(feedResponse.Count >= 0 && feedResponse.Count <= maxItemCount);

                personsList.AddRange(feedResponse);
            }

            Assert.AreEqual(3, personsList.Count);

            await this.CleanupDocumentCollection(container);
        }

        [TestMethod]
        public async Task CrossPartitionQueries_GatewayHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Gateway, Documents.Client.Protocol.Https);
            await this.CrossPartitionQueries();
        }

        [TestMethod]
        public async Task CrossPartitionQueries_DirectHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Documents.Client.Protocol.Https);
            await this.CrossPartitionQueries();
        }

        [TestMethod]
        public async Task CrossPartitionQueries_DirectTcp()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Documents.Client.Protocol.Tcp);
            await this.CrossPartitionQueries();
        }

        private async Task CrossPartitionQueries()
        {
            Database db = await this.client.CreateDatabaseIfNotExistsAsync(DatabaseName);
            Container container = await this.CreatePartitionedCollectionIfNotExists(db, PartitionedCollectionName);

            Uri documentCollectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, PartitionedCollectionName);

            for (int i = 0; i < 2; i++)
            {
                string id = i.ToString();
                await container.CreateItemAsync<Person>(new Person() { Id = id + Guid.NewGuid().ToString(), FirstName = "James", LastName = "Smith" });
            }

            List<dynamic> list = new List<dynamic>();
            using (FeedIterator<dynamic> query =
                container.GetItemQueryIterator<dynamic>("SELECT TOP 10 * FROM coll"))
            {
                while (query.HasMoreResults)
                {
                    list.AddRange(await query.ReadNextAsync());
                }
            }

            await this.CleanupDocumentCollection(container);
        }

        [TestMethod]
        public async Task CreateDatabaseIfNotExists_GatewayHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Gateway, Documents.Client.Protocol.Https);
            await this.CreateDatabaseIfNotExists(this.client);
        }

        [TestMethod]
        public async Task CreateDatabaseIfNotExists_DirectHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Documents.Client.Protocol.Https);
            await this.CreateDatabaseIfNotExists(this.client);
        }

        [TestMethod]
        public async Task CreateDatabaseIfNotExists_DirectTcp()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Documents.Client.Protocol.Tcp);
            await this.CreateDatabaseIfNotExists(this.client);
        }

        private async Task CreateDatabaseIfNotExists(CosmosClient client)
        {
            string databaseId = Guid.NewGuid().ToString();

            // Create the database with this unique id
            Database createdDatabase = await client.CreateDatabaseIfNotExistsAsync(databaseId);

            // CreateDatabaseIfNotExistsAsync should create the new database
            Assert.AreEqual(databaseId, createdDatabase.Id);

            string databaseId2 = Guid.NewGuid().ToString();

            // Pre-create the database with this unique id
            Database createdDatabase2 = await client.CreateDatabaseAsync(databaseId2);

            Database readDatabase = await client.CreateDatabaseIfNotExistsAsync(databaseId2);

            // CreateDatabaseIfNotExistsAsync should return the same database
            Assert.AreEqual(createdDatabase2.Id, readDatabase.Id);

            // cleanup created databases
            await createdDatabase.DeleteAsync();
            await readDatabase.DeleteAsync();
        }

        [TestMethod]
        public async Task CreateDocumentCollectionIfNotExists_GatewayHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Gateway, Documents.Client.Protocol.Https);
            await this.CreateDocumentCollectionIfNotExists();
        }

        [TestMethod]
        public async Task CreateDocumentCollectionIfNotExists_DirectHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Documents.Client.Protocol.Https);
            await this.CreateDocumentCollectionIfNotExists();
        }

        [TestMethod]
        public async Task CreateDocumentCollectionIfNotExists_DirectTcp()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Documents.Client.Protocol.Tcp);
            await this.CreateDocumentCollectionIfNotExists();
        }

        private async Task CreateDocumentCollectionIfNotExists()
        {
            string databaseId = Guid.NewGuid().ToString();

            // Create the database with this unique id
            Database createdDatabase = await this.client.CreateDatabaseIfNotExistsAsync(databaseId);

            string collectionId = Guid.NewGuid().ToString();
            ContainerProperties collection = new ContainerProperties(collectionId, "/id");

            ContainerProperties createdCollection = await createdDatabase.CreateContainerIfNotExistsAsync(collection);

            // CreateDocumentCollectionIfNotExistsAsync should create the new collection
            Assert.AreEqual(collectionId, createdCollection.Id);

            string collectionId2 = Guid.NewGuid().ToString();
            collection = new ContainerProperties(collectionId2, "/id");

            // Pre-create the collection with this unique id
            createdCollection = await createdDatabase.CreateContainerIfNotExistsAsync(collection);

            ContainerProperties readCollection = await createdDatabase.CreateContainerIfNotExistsAsync(collection);

            // CreateDocumentCollectionIfNotExistsAsync should return the same collection
            Assert.AreEqual(createdCollection.Id, readCollection.Id);

            // cleanup created database
            await createdDatabase.DeleteAsync();
        }

        private CosmosClient GetDocumentClient(ConnectionMode connectionMode, Documents.Client.Protocol protocol)
        {
            CosmosClientOptions connectionPolicy = new CosmosClientOptions()
            {
                ConnectionMode = connectionMode,
                ConnectionProtocol = protocol,
                ConsistencyLevel = ConsistencyLevel.Session,
            };

            return TestCommon.CreateCosmosClient(connectionPolicy);
        }

        private async Task<Container> CreatePartitionedCollectionIfNotExists(Database database, string collectionName)
        {
            return await database.CreateContainerIfNotExistsAsync(collectionName, partitionKeyPath: "/id", throughput: 10200);
        }

        private async Task CleanupDocumentCollection(Container container)
        {
            FeedIterator<JObject> query = container.GetItemQueryIterator<JObject>();

            while (query.HasMoreResults)
            {
                FeedResponse<JObject> items = await query.ReadNextAsync();
                foreach (JObject doc in items)
                {
                    string id = doc["id"].ToString();
                    await container.DeleteItemAsync<JObject>(id, new PartitionKey(id));
                }
            }
        }
    }

    internal sealed class Person
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }
    }
}
