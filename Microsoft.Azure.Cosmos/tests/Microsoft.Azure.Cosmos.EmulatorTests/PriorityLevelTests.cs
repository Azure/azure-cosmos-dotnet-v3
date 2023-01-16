namespace Microsoft.Azure.Cosmos.EmulatorTests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos;
    using IndexingPolicy = IndexingPolicy;
    using IndexingMode = IndexingMode;
    using PriorityLevel = PriorityLevel;

    [TestClass]
    public class PriorityLevelTests
    {
        // The Azure Cosmos DB endpoint for running this sample.
        private static readonly string EndpointUri = "https://testdocumentservice-northcentralus.documents-test.windows-int.net:443/";
        // The primary key for the Azure Cosmos account.
        private static readonly string PrimaryKey = "";

        private Cosmos.Database database;

        private Container container;

        private CosmosClient cosmosClient;

        private readonly string databaseId = "db_priorityMixed";
        private readonly string containerId = "col";

        private class Document
        {
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }

            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "address")]
            public string Address { get; set; }

            [JsonProperty(PropertyName = "city")]
            public string City { get; set; }

            [JsonProperty(PropertyName = "pkey")]
            public string Pkey { get; set; }

        }

        public async Task InitializeTest()
        {
            // Direct Mode
            CosmosClientBuilder builder = null;
            try
            {
                builder = new CosmosClientBuilder(EndpointUri, PrimaryKey);
            }
            catch (CosmosException)
            {
                Console.WriteLine("CosmosClientBuilder error");
            }

            try
            {
                builder.WithConnectionModeDirect()
                    .WithThrottlingRetryOptions(TimeSpan.FromSeconds(1), 0);
            }
            catch (CosmosException)
            {
                Console.WriteLine("WithConnectionModeDirect error");
            }
            this.cosmosClient = builder.Build();

            await this.CreateDatabaseAsync();
            await this.CreateContainerAsync();
        }
        private async Task CreateDatabaseAsync()
        {
            // Create a new database
            this.database = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(this.databaseId);
            Console.WriteLine("Created Database: {0}\n", this.database.Id);
        }

        private async Task CreateContainerAsync()
        {
            // Create a new container
            IndexingPolicy policy = new()
            {
                IndexingMode = IndexingMode.None,
                Automatic = false
            };
            ContainerProperties options = new(this.containerId, "/pkey")
            {
                IndexingPolicy = policy
            };
            ContainerResponse containerResponse = await this.database.CreateContainerIfNotExistsAsync(options, throughput: 1600);
            this.container = containerResponse.Container;
            Console.WriteLine("Created Container: {0}\n", this.container.Id);
        }

        [TestMethod]
        public async Task PriorityLevelHighDirectTestAsync()
        {
            await this.InitializeTest();

            Document doc = new Document()
            {
                Id = "id",
                Address = "address1",
                Pkey = "pkey1"
            };
            try
            {
                for (int i = 0; i < 10; i++)
                {
                    doc.Id = "id" + i;
                    ItemResponse<Document> response = await this.container.CreateItemAsync<Document>(doc, new Cosmos.PartitionKey("pkey1"), requestOptions: new ItemRequestOptions { PriorityLevel = Cosmos.PriorityLevel.High });
                }
            }
            catch (CosmosException)
            {
                Assert.Fail("Document insert failed with Priority Level Headers");
            }
        }

        [TestMethod]
        public async Task PriorityLevelLowDirectTestAsync()
        {
            await this.InitializeTest();

            Document doc = new Document()
            {
                Id = "id",
                Address = "address1",
                Pkey = "pkey1"
            };
            try
            {
                for (int i = 10; i < 20; i++)
                {
                    doc.Id = "id" + i;
                    ItemResponse<Document> response = await this.container.CreateItemAsync<Document>(doc, new Cosmos.PartitionKey("pkey1"), requestOptions: new ItemRequestOptions { PriorityLevel = Cosmos.PriorityLevel.Low });
                }
            }
            catch (CosmosException)
            {
                Assert.Fail("Document insert failed with Priority Level Headers");
            }
        }

        [TestMethod]
        public async Task DirectTestAsync()
        {
            await this.InitializeTest();

            Document doc = new Document()
            {
                Id = "id",
                Address = "address1",
                Pkey = "pkey1"
            };
            Microsoft.Azure.Cosmos.ItemRequestOptions itemRequestOptions = new ItemRequestOptions();
            try
            {
                doc.Id = "id";
                ItemResponse<Document> response = await this.container.CreateItemAsync<Document>(doc, new Cosmos.PartitionKey("pkey1"), itemRequestOptions);
            }
            catch (CosmosException)
            {
                Assert.Fail("Document insert failed with Priority Level Headers");
            }
        }

        [TestCleanup]
        public async Task TestCleanupAsync()
        {
            if (this.container != null)
            {
                await this.container.DeleteContainerAsync();
            }

            if (this.database != null)
            {
                await this.database.DeleteAsync();
            }

            this.cosmosClient?.Dispose();
        }
    }
}