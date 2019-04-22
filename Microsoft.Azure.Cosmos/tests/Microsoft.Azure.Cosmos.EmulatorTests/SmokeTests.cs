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
    using System.Threading.Tasks;
    using Linq;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    [TestCategory("Emulator")]
    public class SmokeTests
    {
        private static readonly string Host;
        private static readonly string MasterKey;

        private const string DatabaseName = "netcore_test_db";
        private const string CollectionName = "netcore_test_coll";
        private const string PartitionedCollectionName = "netcore_test_pcoll";

        private const string VSTSContainerHostEnvironmentName = "COSMOSDBEMULATOR_ENDPOINT";

        private DocumentClient client;

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
            Assert.IsTrue(ServiceInteropWrapper.AssembliesExist.Value);
        }

        /// <summary>
        /// Test if 64-bit and native assembly dependencies exist
        /// </summary>
        [Ignore]
        [TestMethod]
        public void ByPassQueryParsing()
        {            
            if(IntPtr.Size == 8)
            {
                Assert.IsFalse(CustomTypeExtensions.ByPassQueryParsing());
            }
            else
            {
                Assert.IsTrue(CustomTypeExtensions.ByPassQueryParsing());
            }
        }

        [TestMethod]
        public async Task DocumentInsertsTest_GatewayHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Gateway, Protocol.Https);
            await this.DocumentInsertsTest();
        }

        [TestMethod]
        public async Task DocumentInsertsTest_DirectHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Protocol.Https);
            await this.DocumentInsertsTest();
        }

        [TestMethod]
        public async Task DocumentInsertsTest_DirectTcp()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Protocol.Tcp);
            await this.DocumentInsertsTest();
        }

        private async Task DocumentInsertsTest()
        {   
            await this.client.CreateDatabaseIfNotExistsAsync(new Database() { Id = DatabaseName });
            this.CreatePartitionedCollectionIfNotExists(DatabaseName, PartitionedCollectionName);

            Uri documentCollectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, PartitionedCollectionName);

            for (int i = 0; i < 2; i++)
            {
                string id = i.ToString();
                await this.client.CreateDocumentAsync(documentCollectionUri, new Person() { Id = id, FirstName = "James", LastName = "Smith" });
            }

            var query =
                this.client.CreateDocumentQuery<Document>(documentCollectionUri, "SELECT * FROM coll",
                    new FeedOptions() { EnableCrossPartitionQuery = true })
                    .AsEnumerable();

            Assert.AreEqual(query.ToList().Count, 2);

            await this.CleanupDocumentCollection(documentCollectionUri);
        }

        [TestMethod]
        public async Task QueryWithPaginationTest_GatewayHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Gateway, Protocol.Https);
            await this.QueryWithPagination();
        }

        [TestMethod]
        public async Task QueryWithPaginationTest_DirectHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Protocol.Https);
            await this.QueryWithPagination();
        }

        [TestMethod]
        public async Task QueryWithPaginationTest_DirectTcp()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Protocol.Tcp);
            await this.QueryWithPagination();
        }

        private async Task QueryWithPagination()
        {
            await this.client.CreateDatabaseIfNotExistsAsync(new Database() { Id = DatabaseName });
            this.CreatePartitionedCollectionIfNotExists(DatabaseName, PartitionedCollectionName);

            Uri documentCollectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, PartitionedCollectionName);

            await this.client.UpsertDocumentAsync(documentCollectionUri, new Person() { Id = "1", FirstName = "David", LastName = "Smith"});
            await this.client.UpsertDocumentAsync(documentCollectionUri, new Person() { Id = "2", FirstName = "Robert", LastName = "Johnson" });
            await this.client.UpsertDocumentAsync(documentCollectionUri, new Person() { Id = "3", FirstName = "William", LastName = "Smith" });
            
            FeedOptions options = new FeedOptions { MaxItemCount = 1, EnableCrossPartitionQuery = true };
            
            var smithFamily =
                this.client.CreateDocumentQuery<Person>(documentCollectionUri, options)
                    .Where(d => d.LastName == "Smith")
                    .ToList();

            Assert.AreEqual(2, smithFamily.Count);

            var persons =
                this.client.CreateDocumentQuery<Person>(documentCollectionUri, options)
                    .ToList();

            Assert.AreEqual(3, persons.Count);

            var query = this.client.CreateDocumentQuery<Person>(documentCollectionUri, options);
            var documentQuery = query.AsDocumentQuery();

            List<Person> personsList = new List<Person>();

            while (documentQuery.HasMoreResults)
            {
                var feedResponse = await documentQuery.ExecuteNextAsync<Person>();
                int maxItemCount = options.MaxItemCount ?? default(int);
                Assert.IsTrue(feedResponse.Count >= 0 && feedResponse.Count <= maxItemCount);

                personsList.AddRange(feedResponse);
            }

            Assert.AreEqual(3, personsList.Count);

            await this.CleanupDocumentCollection(documentCollectionUri);
        }

        [TestMethod]
        public async Task CrossPartitionQueries_GatewayHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Gateway, Protocol.Https);
            await this.CrossPartitionQueries();
        }

        [TestMethod]
        public async Task CrossPartitionQueries_DirectHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Protocol.Https);
            await this.CrossPartitionQueries();
        }

        [TestMethod]
        public async Task CrossPartitionQueries_DirectTcp()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Protocol.Tcp);
            await this.CrossPartitionQueries();
        }

        private async Task CrossPartitionQueries()
        {
            await this.client.CreateDatabaseIfNotExistsAsync(new Database() { Id = DatabaseName });
            this.CreatePartitionedCollectionIfNotExists(DatabaseName, PartitionedCollectionName);

            Uri documentCollectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, PartitionedCollectionName);

            for (int i = 0; i < 2; i++)
            {
                string id = i.ToString();
                await this.client.CreateDocumentAsync(documentCollectionUri, new Person() { Id = id + Guid.NewGuid().ToString(), FirstName = "James", LastName = "Smith" });
            }

            var query =
                this.client.CreateDocumentQuery<Document>(documentCollectionUri, "SELECT TOP 10 * FROM coll",
                    new FeedOptions() { EnableCrossPartitionQuery = true })
                    .AsEnumerable();

            List<Document> list = query.ToList();

            await this.CleanupDocumentCollection(documentCollectionUri);
        }

        [TestMethod]
        public void CreateDatabaseIfNotExists_GatewayHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Gateway, Protocol.Https);
            this.CreateDatabaseIfNotExists();
        }

        [TestMethod]
        public void CreateDatabaseIfNotExists_DirectHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Protocol.Https);
            this.CreateDatabaseIfNotExists();
        }

        [TestMethod]
        public void CreateDatabaseIfNotExists_DirectTcp()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Protocol.Tcp);
            this.CreateDatabaseIfNotExists();
        }

        private void CreateDatabaseIfNotExists()
        {
            string databaseId = Guid.NewGuid().ToString();
            Database db = new Database { Id = databaseId };

            // Create the database with this unique id
            Database createdDatabase = this.client.CreateDatabaseIfNotExistsAsync(db).Result;

            // CreateDatabaseIfNotExistsAsync should create the new database
            Assert.AreEqual(databaseId, createdDatabase.Id);

            string databaseId2 = Guid.NewGuid().ToString();
            db = new Database { Id = databaseId2 };

            // Pre-create the database with this unique id
            createdDatabase = this.client.CreateDatabaseAsync(db).Result;

            Database readDatabase = this.client.CreateDatabaseIfNotExistsAsync(db).Result;

            // CreateDatabaseIfNotExistsAsync should return the same database
            Assert.AreEqual(createdDatabase.SelfLink, readDatabase.SelfLink);

            // cleanup created databases
            this.client.DeleteDatabaseAsync(UriFactory.CreateDatabaseUri(databaseId)).Wait();
            this.client.DeleteDatabaseAsync(UriFactory.CreateDatabaseUri(databaseId2)).Wait();
        }

        [TestMethod]
        public void CreateDocumentCollectionIfNotExists_GatewayHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Gateway, Protocol.Https);
            this.CreateDocumentCollectionIfNotExists();
        }

        [TestMethod]
        public void CreateDocumentCollectionIfNotExists_DirectHttps()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Protocol.Https);
            this.CreateDocumentCollectionIfNotExists();
        }

        [TestMethod]
        public void CreateDocumentCollectionIfNotExists_DirectTcp()
        {
            this.client = this.GetDocumentClient(ConnectionMode.Direct, Protocol.Tcp);
            this.CreateDocumentCollectionIfNotExists();
        }
            
        private void CreateDocumentCollectionIfNotExists()
        {
            string databaseId = Guid.NewGuid().ToString();
            Database db = new Database { Id = databaseId };

            // Create the database with this unique id
            Database createdDatabase = this.client.CreateDatabaseIfNotExistsAsync(db).Result;

            string collectionId = Guid.NewGuid().ToString();
            DocumentCollection collection = new DocumentCollection
                {
                    Id = collectionId,
                    PartitionKey = new PartitionKeyDefinition()
                    {
                        Paths = new Collection<string>() { "/id" }
                    },
                };

            DocumentCollection createdCollection = this.client.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri(createdDatabase.Id), collection).Result;

            // CreateDocumentCollectionIfNotExistsAsync should create the new collection
            Assert.AreEqual(collectionId, createdCollection.Id);

            string collectionId2 = Guid.NewGuid().ToString();
            collection = new DocumentCollection
            {
                Id = collectionId2,
                PartitionKey = new PartitionKeyDefinition()
                {
                    Paths = new Collection<string>() { "/id" }
                },
            };

            // Pre-create the collection with this unique id
            createdCollection = this.client.CreateDocumentCollectionIfNotExistsAsync(createdDatabase.SelfLink, collection).Result;

            DocumentCollection readCollection = this.client.CreateDocumentCollectionIfNotExistsAsync(createdDatabase.SelfLink, collection).Result;

            // CreateDocumentCollectionIfNotExistsAsync should return the same collection
            Assert.AreEqual(createdCollection.SelfLink, readCollection.SelfLink);

            // cleanup created database
            this.client.DeleteDatabaseAsync(UriFactory.CreateDatabaseUri(databaseId)).Wait();
        }

        private DocumentClient GetDocumentClient(ConnectionMode connectionMode, Protocol protocol)
        {
            ConnectionPolicy connectionPolicy = new ConnectionPolicy() { ConnectionMode = connectionMode, ConnectionProtocol = protocol };

            return new DocumentClient(new Uri(Host), MasterKey, (HttpMessageHandler)null, connectionPolicy);
        }

        private void CreatePartitionedCollectionIfNotExists(string databaseName, string collectionName)
        {
            DocumentCollection coll = new DocumentCollection { Id = collectionName };
            coll.PartitionKey.Paths.Add("/id");
            this.client.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri(databaseName), coll, new RequestOptions() { OfferThroughput = 10200 }).Wait();
        }

        private async Task CleanupDocumentCollection(Uri documentCollectionUri)
        {
            var query =
                this.client.CreateDocumentQuery(documentCollectionUri,
                    new FeedOptions {EnableCrossPartitionQuery = true})
                    .AsEnumerable();

            foreach (Document doc in query)
            {
                await this.client.DeleteDocumentAsync(doc.SelfLink, new RequestOptions {PartitionKey = new PartitionKey(doc.Id)});
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
