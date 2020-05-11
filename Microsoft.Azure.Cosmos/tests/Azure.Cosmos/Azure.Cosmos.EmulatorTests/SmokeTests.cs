//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Emulator")]
    public class SmokeTests
    {
        private static readonly string Host;
        private static readonly string MasterKey;

        private const string DatabaseName = "netcore_test_db";
        private const string PartitionedCollectionName = "netcore_test_pcoll";

        private CosmosClient client;

        static SmokeTests()
        {
            SmokeTests.MasterKey = ConfigurationManager.AppSettings["MasterKey"];
            SmokeTests.Host = ConfigurationManager.AppSettings["GatewayEndpoint"];
        }

        /// <summary>
        /// Test for the existence of native assembly dependencies
        /// </summary>
        [Ignore]
        [TestMethod]
        public void AssembliesExist()
        {
            Assert.IsTrue(Microsoft.Azure.Documents.ServiceInteropWrapper.AssembliesExist.Value);
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
                Assert.IsFalse(Microsoft.Azure.Documents.CustomTypeExtensions.ByPassQueryParsing());
            }
            else
            {
                Assert.IsTrue(Microsoft.Azure.Documents.CustomTypeExtensions.ByPassQueryParsing());
            }
        }

        [TestMethod]
        public async Task DocumentInsertsTest_GatewayHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Gateway, Microsoft.Azure.Documents.Client.Protocol.Https);
            await this.DocumentInsertsTest();
        }

        [TestMethod]
        public async Task DocumentInsertsTest_DirectHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Microsoft.Azure.Documents.Client.Protocol.Https);
            await this.DocumentInsertsTest();
        }

        [TestMethod]
        public async Task DocumentInsertsTest_DirectTcp()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Microsoft.Azure.Documents.Client.Protocol.Tcp);
            await this.DocumentInsertsTest();
        }

        private async Task DocumentInsertsTest()
        {
            CosmosDatabase database = await this.client.CreateDatabaseIfNotExistsAsync(DatabaseName);
            CosmosContainer container = await this.CreatePartitionedCollectionIfNotExists(database, PartitionedCollectionName);

            for (int i = 0; i < 2; i++)
            {
                string id = i.ToString();
                await container.CreateItemAsync(new Person() { Id = id, FirstName = "James", LastName = "Smith" });
            }

            int count = 0;
            await foreach(dynamic item in container.GetItemQueryResultsAsync<dynamic>())
            {
                count++;
            }

            Assert.AreEqual(2, count);

            await this.CleanupDocumentCollection(container);
        }

        [TestMethod]
        public async Task QueryWithPaginationTest_GatewayHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Gateway, Microsoft.Azure.Documents.Client.Protocol.Https);
            await this.QueryWithPagination();
        }

        [TestMethod]
        public async Task QueryWithPaginationTest_DirectHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Microsoft.Azure.Documents.Client.Protocol.Https);
            await this.QueryWithPagination();
        }

        [TestMethod]
        public async Task QueryWithPaginationTest_DirectTcp()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Microsoft.Azure.Documents.Client.Protocol.Tcp);
            await this.QueryWithPagination();
        }

        private async Task QueryWithPagination()
        {
            CosmosDatabase database = await this.client.CreateDatabaseIfNotExistsAsync(DatabaseName);
            CosmosContainer container = await this.CreatePartitionedCollectionIfNotExists(database, PartitionedCollectionName);

            await container.UpsertItemAsync<Person>(new Person() { Id = "1", FirstName = "David", LastName = "Smith" });
            await container.UpsertItemAsync<Person>(new Person() { Id = "2", FirstName = "Robert", LastName = "Johnson" });
            await container.UpsertItemAsync<Person>(new Person() { Id = "3", FirstName = "William", LastName = "Smith" });

            QueryRequestOptions options = new QueryRequestOptions { MaxItemCount = 1 };

            int smithFamilyCount = 0;
            await foreach (Person person in container.GetItemQueryResultsAsync<Person>("SELECT * FROM d WHERE d.LastName = 'Smith'", requestOptions: options))
            {
                smithFamilyCount++;
            }

            Assert.AreEqual(2, smithFamilyCount);

            List<Person> personsList = new List<Person>();
            await foreach (Page<Person> page in container.GetItemQueryResultsAsync<Person>(requestOptions: options).AsPages())
            {
                int maxItemCount = options.MaxItemCount ?? default(int);
                Assert.IsTrue(page.Values.Count >= 0 && page.Values.Count <= maxItemCount);
                personsList.AddRange(page.Values);
            }

            Assert.AreEqual(3, personsList.Count);

            await this.CleanupDocumentCollection(container);
        }

        [TestMethod]
        public async Task CrossPartitionQueries_GatewayHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Gateway, Microsoft.Azure.Documents.Client.Protocol.Https);
            await this.CrossPartitionQueries();
        }

        [TestMethod]
        public async Task CrossPartitionQueries_DirectHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Microsoft.Azure.Documents.Client.Protocol.Https);
            await this.CrossPartitionQueries();
        }

        [TestMethod]
        public async Task CrossPartitionQueries_DirectTcp()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Microsoft.Azure.Documents.Client.Protocol.Tcp);
            await this.CrossPartitionQueries();
        }

        private async Task CrossPartitionQueries()
        {
            CosmosDatabase db = await this.client.CreateDatabaseIfNotExistsAsync(DatabaseName);
            CosmosContainer container = await this.CreatePartitionedCollectionIfNotExists(db, PartitionedCollectionName);

            for (int i = 0; i < 2; i++)
            {
                string id = i.ToString();
                await container.CreateItemAsync<Person>(new Person() { Id = id + Guid.NewGuid().ToString(), FirstName = "James", LastName = "Smith" });
            }

            List<dynamic> list = new List<dynamic>();
            await foreach(Page<dynamic> page in container.GetItemQueryResultsAsync<dynamic>("SELECT TOP 10 * FROM coll").AsPages())
            {
                list.AddRange(page.Values);
            }
            
            await this.CleanupDocumentCollection(container);
        }

        [TestMethod]
        public async Task CreateDatabaseIfNotExists_GatewayHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Gateway, Microsoft.Azure.Documents.Client.Protocol.Https);
            await this.CreateDatabaseIfNotExists(this.client);
        }

        [TestMethod]
        public async Task CreateDatabaseIfNotExists_DirectHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Microsoft.Azure.Documents.Client.Protocol.Https);
            await this.CreateDatabaseIfNotExists(this.client);
        }

        [TestMethod]
        public async Task CreateDatabaseIfNotExists_DirectTcp()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Microsoft.Azure.Documents.Client.Protocol.Tcp);
            await this.CreateDatabaseIfNotExists(this.client);
        }

        private async Task CreateDatabaseIfNotExists(CosmosClient client)
        {
            string databaseId = Guid.NewGuid().ToString();

            // Create the database with this unique id
            CosmosDatabase createdDatabase = await client.CreateDatabaseIfNotExistsAsync(databaseId);

            // CreateDatabaseIfNotExistsAsync should create the new database
            Assert.AreEqual(databaseId, createdDatabase.Id);

            string databaseId2 = Guid.NewGuid().ToString();

            // Pre-create the database with this unique id
            CosmosDatabase createdDatabase2 = await client.CreateDatabaseAsync(databaseId2);

            CosmosDatabase readDatabase = await client.CreateDatabaseIfNotExistsAsync(databaseId2);

            // CreateDatabaseIfNotExistsAsync should return the same database
            Assert.AreEqual(createdDatabase2.Id, readDatabase.Id);

            // cleanup created databases
            await createdDatabase.DeleteAsync();
            await readDatabase.DeleteAsync();
        }

        [TestMethod]
        public async Task CreateDocumentCollectionIfNotExists_GatewayHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Gateway, Microsoft.Azure.Documents.Client.Protocol.Https);
            await this.CreateDocumentCollectionIfNotExists();
        }

        [TestMethod]
        public async Task CreateDocumentCollectionIfNotExists_DirectHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Microsoft.Azure.Documents.Client.Protocol.Https);
            await this.CreateDocumentCollectionIfNotExists();
        }

        [TestMethod]
        public async Task CreateDocumentCollectionIfNotExists_DirectTcp()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Microsoft.Azure.Documents.Client.Protocol.Tcp);
            await this.CreateDocumentCollectionIfNotExists();
        }

        private async Task CreateDocumentCollectionIfNotExists()
        {
            string databaseId = Guid.NewGuid().ToString();

            // Create the database with this unique id
            CosmosDatabase createdDatabase = await this.client.CreateDatabaseIfNotExistsAsync(databaseId);

            string collectionId = Guid.NewGuid().ToString();
            CosmosContainerProperties collection = new CosmosContainerProperties(collectionId, "/id");

            CosmosContainerProperties createdCollection = await createdDatabase.CreateContainerIfNotExistsAsync(collection);

            // CreateDocumentCollectionIfNotExistsAsync should create the new collection
            Assert.AreEqual(collectionId, createdCollection.Id);

            string collectionId2 = Guid.NewGuid().ToString();
            collection = new CosmosContainerProperties(collectionId2, "/id");

            // Pre-create the collection with this unique id
            createdCollection = await createdDatabase.CreateContainerIfNotExistsAsync(collection);

            CosmosContainerProperties readCollection = await createdDatabase.CreateContainerIfNotExistsAsync(collection);

            // CreateDocumentCollectionIfNotExistsAsync should return the same collection
            Assert.AreEqual(createdCollection.Id, readCollection.Id);

            // cleanup created database
            await createdDatabase.DeleteAsync();
        }

        private CosmosClient GetDocumentClient(ConnectionMode connectionMode, Microsoft.Azure.Documents.Client.Protocol protocol)
        {
            CosmosClientOptions connectionPolicy = new CosmosClientOptions() { ConnectionMode = connectionMode, ConnectionProtocol = protocol };

            return new CosmosClient(Host, MasterKey, connectionPolicy);
        }

        private async Task<CosmosContainer> CreatePartitionedCollectionIfNotExists(CosmosDatabase database, string collectionName)
        {
            return await database.CreateContainerIfNotExistsAsync(collectionName, partitionKeyPath: "/id", throughput: 10200);
        }

        private async Task CleanupDocumentCollection(CosmosContainer container)
        {
            await foreach(JsonElement doc in container.GetItemQueryResultsAsync<JsonElement>())
            {
                string id = doc.GetProperty("id").GetString();
                await container.DeleteItemAsync<JsonElement>(id, new PartitionKey(id));
            }
        }
    }

    internal sealed class Person
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }
    }
}
