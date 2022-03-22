namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Testing Prefix and Full Partition for <see cref="ChangeFeed"/>, <see cref="Query"/> against a <see cref="Container"/> with Hierarchical Partition Keys.
    /// <see href="https://github.com/AzureCosmosDB/HierarchicalPartitionKeysFeedbackGroup"/>
    /// </summary>
    [TestClass]
    public class FeedRangeCreateFromPartitionKeyAsyncEmulatorTests
    {
        private CosmosClient cosmosClient = null;
        private Database cosmosDatabase = null;

        [TestInitialize]
        public async Task TestInit()
        {
            this.cosmosClient = TestCommon.CreateCosmosClient();

            string databaseName = Guid.NewGuid().ToString();
            DatabaseResponse cosmosDatabaseResponse = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName);
            this.cosmosDatabase = cosmosDatabaseResponse;
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            if (this.cosmosClient == null)
            {
                return;
            }

            if (this.cosmosDatabase != null)
            {
                await this.cosmosDatabase.DeleteStreamAsync();
            }
            this.cosmosClient.Dispose();
        }

        /// <summary>
        /// Using <see cref="Database.CreateContainerIfNotExistsAsync(ContainerProperties, ThroughputProperties, RequestOptions, System.Threading.CancellationToken)"/> to create a new <see cref="Container"/> with Hierarchical Partition Keys.
        /// Using <see cref="Container.GetChangeFeedIterator{T}(ChangeFeedStartFrom, ChangeFeedMode, ChangeFeedRequestOptions)"/> with a Prefix partition on a MultiHash V2 <see cref="Container"/>.
        /// </summary>
        /// <see href="https://github.com/AzureCosmosDB/HierarchicalPartitionKeysFeedbackGroup#net-v3-sdk-1"/>
        /// <returns></returns>
        [TestMethod]
        public async Task GetChangeFeedIteratorWithPrefixPartitionKeyReturnsFeedIterator()
        {
            Container container = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(new(id: @"TestMultiHashedContainer", partitionKeyPaths: new List<string>() { "/city", "/state", "/zipCode" }));
            ContainerProperties containerProperties = await container.ReadContainerAsync();

            Assert.AreEqual(expected: PartitionKeyDefinitionVersion.V2, actual: containerProperties.PartitionKeyDefinitionVersion);
            Assert.AreEqual(expected: 3, actual: containerProperties.PartitionKey.Paths.Count);
            Assert.IsTrue(containerProperties.PartitionKey.Paths.Contains("/city"));
            Assert.IsTrue(containerProperties.PartitionKey.Paths.Contains("/state"));
            Assert.IsTrue(containerProperties.PartitionKey.Paths.Contains("/zipCode"));
            Assert.AreEqual(expected: Documents.PartitionKind.MultiHash, actual: containerProperties.PartitionKey.Kind);

            try
            {
                dynamic item = new { id = Guid.NewGuid().ToString(), city = "Redmond", state = "WA", zipCode = "98502" };
                PartitionKey partitionKey = new PartitionKeyBuilder()
                    .Add(item.city)
                    .Add(item.state)
                    .Add(item.zipCode)
                    .Build();

                _ = await container.CreateItemAsync<dynamic>(item: item, partitionKey: partitionKey);

                partitionKey = new PartitionKeyBuilder()
                    .Add(item.city)
                    .Add(item.state)
                    .Build();

                FeedRange feedRange = await FeedRange.CreateFromPartitionKeyAsync(container: container, partitionKey: partitionKey);
                FeedIterator<dynamic> iterator = container.GetChangeFeedIterator<dynamic>(ChangeFeedStartFrom.Beginning(feedRange), ChangeFeedMode.Incremental);
                FeedResponse<dynamic> response = await iterator.ReadNextAsync();

                string json = JsonConvert.SerializeObject(response.First());
                JObject @object = JObject.Parse(json);

                Assert.AreEqual(expected: item.id, actual: @object["id"]);
                Assert.AreEqual(expected: item.city, actual: @object["city"]);
                Assert.AreEqual(expected: item.state, actual: @object["state"]);
                Assert.AreEqual(expected: item.zipCode, actual: @object["zipCode"]);
            }
            finally
            {
                _ = await container.DeleteContainerAsync();
            }
        }

        /// <summary>
        /// Using <see cref="Database.CreateContainerIfNotExistsAsync(ContainerProperties, ThroughputProperties, RequestOptions, System.Threading.CancellationToken)"/> to create a new <see cref="Container"/> with hierarchical partition keys.
        /// Using <see cref="Container.GetChangeFeedStreamIterator(ChangeFeedStartFrom, ChangeFeedMode, ChangeFeedRequestOptions)"/> with a Prefix Partition on a MultiHash <see cref="Container"/>.
        /// </summary>
        /// <see href="https://github.com/AzureCosmosDB/HierarchicalPartitionKeysFeedbackGroup#net-v3-sdk-1"/>
        /// <returns></returns>
        [TestMethod]
        public async Task GetChangeFeedStreamIteratorWithPrefixPartitionKeyReturnsFeedIterator()
        {
            Container container = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(new(id: @"TestMultiHashedContainer", partitionKeyPaths: new List<string>() { "/city", "/state", "/zipCode" }));
            ContainerProperties containerProperties = await container.ReadContainerAsync();

            Assert.AreEqual(expected: PartitionKeyDefinitionVersion.V2, actual: containerProperties.PartitionKeyDefinitionVersion);
            Assert.AreEqual(expected: 3, actual: containerProperties.PartitionKey.Paths.Count);
            Assert.IsTrue(containerProperties.PartitionKey.Paths.Contains("/city"));
            Assert.IsTrue(containerProperties.PartitionKey.Paths.Contains("/state"));
            Assert.IsTrue(containerProperties.PartitionKey.Paths.Contains("/zipCode"));
            Assert.AreEqual(expected: Documents.PartitionKind.MultiHash, actual: containerProperties.PartitionKey.Kind);

            try
            {
                dynamic item = new { id = Guid.NewGuid().ToString(), city = "Redmond", state = "WA", zipCode = "98502" };
                PartitionKey partitionKey = new PartitionKeyBuilder()
                    .Add(item.city)
                    .Add(item.state)
                    .Add(item.zipCode)
                    .Build();

                _ = await container.CreateItemAsync<dynamic>(item: item, partitionKey: partitionKey);

                partitionKey = new PartitionKeyBuilder()
                    .Add(item.city)
                    .Add(item.state)
                    .Build();

                FeedRange feedRange = await FeedRange.CreateFromPartitionKeyAsync(container: container, partitionKey: partitionKey);
                using (FeedIterator iterator = container.GetChangeFeedStreamIterator(ChangeFeedStartFrom.Beginning(feedRange), ChangeFeedMode.Incremental))
                {
                    ResponseMessage responseMessage = await iterator.ReadNextAsync();

                    using (StreamReader streamReader = new(responseMessage.Content))
                    {
                        string content = await streamReader.ReadToEndAsync();

                        JObject @object = JObject.Parse(content);
                        JToken token = @object["Documents"].First();

                        Assert.AreEqual(expected: item.id, actual: token["id"]);
                        Assert.AreEqual(expected: item.city, actual: token["city"]);
                        Assert.AreEqual(expected: item.state, actual: token["state"]);
                        Assert.AreEqual(expected: item.zipCode, actual: token["zipCode"]);
                    }
                }
            }
            finally
            {
                _ = await container.DeleteContainerAsync();
            }
        }

        /// <summary>
        /// Using <see cref="Database.CreateContainerIfNotExistsAsync(ContainerProperties, ThroughputProperties, RequestOptions, System.Threading.CancellationToken)"/> to create a new <see cref="Container"/> with Hierarchical Partition Keys.
        /// using <see cref="Container.GetItemQueryIterator{T}(FeedRange, QueryDefinition, string, QueryRequestOptions)"/> with a Prefix partition on a MultiHash V2 <see cref="Container"/>.
        /// </summary>
        /// <see href="https://github.com/AzureCosmosDB/HierarchicalPartitionKeysFeedbackGroup#net-v3-sdk-1"/>
        /// <see href="https://github.com/AzureCosmosDB/HierarchicalPartitionKeysFeedbackGroup#net-v3-sdk-3"/>
        [TestMethod]
        [Ignore("Query is returning 'Partition key provided either doesn't correspond to definition in the collection or doesn't match partition key field values specified in the document.' Investigation.")]
        [Owner("naga.naravamakula")]
        public async Task GetItemQueryIteratorWithPrefixPartitionKeyReturnsFeedIterator()
        {
            Container container = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(new(id: @"TestMultiHashedContainer", partitionKeyPaths: new List<string>() { "/city", "/state", "/zipCode" }));
            ContainerProperties containerProperties = await container.ReadContainerAsync();

            Assert.AreEqual(expected: PartitionKeyDefinitionVersion.V2, actual: containerProperties.PartitionKeyDefinitionVersion);
            Assert.AreEqual(expected: 3, actual: containerProperties.PartitionKey.Paths.Count);
            Assert.IsTrue(containerProperties.PartitionKey.Paths.Contains("/city"));
            Assert.IsTrue(containerProperties.PartitionKey.Paths.Contains("/state"));
            Assert.IsTrue(containerProperties.PartitionKey.Paths.Contains("/zipCode"));
            Assert.AreEqual(expected: Documents.PartitionKind.MultiHash, actual: containerProperties.PartitionKey.Kind);

            try
            {
                dynamic item = new { id = Guid.NewGuid().ToString(), city = "Redmond", state = "WA", zipCode = "98052" };
                PartitionKey partitionKey = new PartitionKeyBuilder()
                    .Add(item.city)
                    .Add(item.state)
                    .Add(item.zipCode)
                    .Build();

                _ = await container.CreateItemAsync<dynamic>(item: item, partitionKey: partitionKey);

                QueryDefinition queryDefinition = new QueryDefinition(query: "SELECT * FROM c WHERE c.city = @cityInput AND c.state = @stateInput")
                    .WithParameter("@cityInput", "Redmond")
                    .WithParameter("@stateInput", "WA");

                partitionKey = new PartitionKeyBuilder()
                    .Add(item.city)
                    .Add(item.state)
                    .Build();

                FeedRange feedRange = await FeedRange.CreateFromPartitionKeyAsync(container: container, partitionKey: partitionKey);

                Console.WriteLine(feedRange.ToJsonString());
                using (FeedIterator<dynamic> iterator = container.GetItemQueryIterator<dynamic>(feedRange: feedRange, queryDefinition: queryDefinition, requestOptions: new() { PartitionKey = partitionKey }))
                {
                    FeedResponse<dynamic> feedResponse = await iterator.ReadNextAsync();

                    string content = JsonConvert.SerializeObject(feedResponse.First());
                    JObject @object = JObject.Parse(content);

                    Assert.AreEqual(expected: item.id, actual: @object["id"]);
                    Assert.AreEqual(expected: item.city, actual: @object["city"]);
                    Assert.AreEqual(expected: item.state, actual: @object["state"]);
                    Assert.AreEqual(expected: item.zipCode, actual: @object["zipCode"]);
                }
            }
            finally
            {
                _ = await container.DeleteContainerAsync();
            }
        }

        /// <summary>
        /// Using <see cref="Database.CreateContainerIfNotExistsAsync(ContainerProperties, ThroughputProperties, RequestOptions, System.Threading.CancellationToken)"/> to create a new <see cref="Container"/> with Hierarchical Partition Keys.
        /// Using <see cref="Container.GetItemQueryStreamIterator(FeedRange, QueryDefinition, string, QueryRequestOptions)"/> with a Prefix Partition on a MultiHash V2 <see cref="Container"/>.
        /// </summary>
        /// <see href="https://github.com/AzureCosmosDB/HierarchicalPartitionKeysFeedbackGroup#net-v3-sdk-1"/>
        /// <see href="https://github.com/AzureCosmosDB/HierarchicalPartitionKeysFeedbackGroup#net-v3-sdk-3"/>
        [TestMethod]
        public async Task GetItemQueryStreamIteratorWithPrefixPartitionKeyReturnsFeedIterator()
        {
            Container container = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(new(id: @"TestMultiHashedContainer", partitionKeyPaths: new List<string>() { "/city", "/state", "/zipCode" }));
            ContainerProperties containerProperties = await container.ReadContainerAsync();

            Assert.AreEqual(expected: PartitionKeyDefinitionVersion.V2, actual: containerProperties.PartitionKeyDefinitionVersion);
            Assert.AreEqual(expected: 3, actual: containerProperties.PartitionKey.Paths.Count);
            Assert.IsTrue(containerProperties.PartitionKey.Paths.Contains("/city"));
            Assert.IsTrue(containerProperties.PartitionKey.Paths.Contains("/state"));
            Assert.IsTrue(containerProperties.PartitionKey.Paths.Contains("/zipCode"));
            Assert.AreEqual(expected: Documents.PartitionKind.MultiHash, actual: containerProperties.PartitionKey.Kind);

            try
            {
                dynamic item = new { id = Guid.NewGuid().ToString(), city = "Redmond", state = "WA", zipCode = "98052" };
                PartitionKey partitionKey = new PartitionKeyBuilder()
                    .Add(item.city)
                    .Add(item.state)
                    .Add(item.zipCode)
                    .Build();

                _ = await container.CreateItemAsync<dynamic>(item: item, partitionKey: partitionKey);

                QueryDefinition queryDefinition = new QueryDefinition(query: "SELECT * FROM c WHERE c.city = @cityInput AND c.state = @stateInput")
                    .WithParameter("@cityInput", "Redmond")
                    .WithParameter("@stateInput", "WA");

                using (FeedIterator iterator = container.GetItemQueryStreamIterator(queryDefinition: queryDefinition, requestOptions: new() { PartitionKey = partitionKey }))
                {
                    ResponseMessage responseMessage = await iterator.ReadNextAsync();

                    using (StreamReader streamReader = new(responseMessage.Content))
                    {
                        string content = await streamReader.ReadToEndAsync();

                        JObject @object = JObject.Parse(content);
                        JToken token = @object["Documents"].First();

                        Assert.AreEqual(expected: item.id, actual: token["id"]);
                        Assert.AreEqual(expected: item.city, actual: token["city"]);
                        Assert.AreEqual(expected: item.state, actual: token["state"]);
                        Assert.AreEqual(expected: item.zipCode, actual: token["zipCode"]);
                    }
                }
            }
            finally
            {
                _ = await container.DeleteContainerAsync();
            }
        }

        /// <summary>
        /// Using <see cref="Database.CreateContainerIfNotExistsAsync(ContainerProperties, ThroughputProperties, RequestOptions, System.Threading.CancellationToken)"/> to create a new <see cref="Container"/> with Hierarchical Partition Keys.
        /// Using <see cref="Container.ReadItemAsync{T}(string, PartitionKey, ItemRequestOptions, System.Threading.CancellationToken)"/> with a Full Partition on a MultiHash V2 <see cref="Container"/>.
        /// </summary>
        /// <see href="https://github.com/AzureCosmosDB/HierarchicalPartitionKeysFeedbackGroup#net-v3-sdk-1"/>
        /// <see href="https://github.com/AzureCosmosDB/HierarchicalPartitionKeysFeedbackGroup#net-v3-sdk-2"/>
        [TestMethod]
        public async Task ReadItemWithFullPartitionKeyReturnsFeedIterator()
        {
            Container container = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(new(id: @"TestMultiHashedContainer", partitionKeyPaths: new List<string>() { "/city", "/state", "/zipCode" }));
            ContainerProperties containerProperties = await container.ReadContainerAsync();

            Assert.AreEqual(expected: PartitionKeyDefinitionVersion.V2, actual: containerProperties.PartitionKeyDefinitionVersion);
            Assert.AreEqual(expected: 3, actual: containerProperties.PartitionKey.Paths.Count);
            Assert.IsTrue(containerProperties.PartitionKey.Paths.Contains("/city"));
            Assert.IsTrue(containerProperties.PartitionKey.Paths.Contains("/state"));
            Assert.IsTrue(containerProperties.PartitionKey.Paths.Contains("/zipCode"));
            Assert.AreEqual(expected: Documents.PartitionKind.MultiHash, actual: containerProperties.PartitionKey.Kind);

            try
            {
                dynamic item = new { id = Guid.NewGuid().ToString(), city = "Redmond", state = "WA", zipCode = "98052" };
                PartitionKey partitionKey = new PartitionKeyBuilder()
                    .Add(item.city)
                    .Add(item.state)
                    .Add(item.zipCode)
                    .Build();

                _ = await container.CreateItemAsync<dynamic>(item: item, partitionKey: partitionKey);

                ItemResponse<dynamic> itemResponse = await container.ReadItemAsync<dynamic>(id: item.id, partitionKey: partitionKey);

                string content = JsonConvert.SerializeObject(itemResponse.Resource);
                JObject @object = JObject.Parse(content);

                Assert.AreEqual(expected: 1, itemResponse.RequestCharge);
                Assert.AreEqual(expected: item.id, actual: @object["id"]);
                Assert.AreEqual(expected: item.city, actual: @object["city"]);
                Assert.AreEqual(expected: item.state, actual: @object["state"]);
                Assert.AreEqual(expected: item.zipCode, actual: @object["zipCode"]);
            }
            finally
            {
                _ = await container.DeleteContainerAsync();
            }
        }

        /// <summary>
        /// Using <see cref="Database.CreateContainerIfNotExistsAsync(ContainerProperties, ThroughputProperties, RequestOptions, System.Threading.CancellationToken)"/> to create a new <see cref="Container"/> with Hierarchical Partition Keys.
        /// Using <see cref="Container.CreateItemAsync{T}(T, PartitionKey?, ItemRequestOptions, System.Threading.CancellationToken)"/>
        /// Using <see cref="Container.ReadItemStreamAsync(string, PartitionKey, ItemRequestOptions, System.Threading.CancellationToken)"/> with a Full Partition on a MultiHash V2 <see cref="Container"/>.
        /// </summary>
        /// <see href="https://github.com/AzureCosmosDB/HierarchicalPartitionKeysFeedbackGroup#net-v3-sdk-1"/>
        /// <see href="https://github.com/AzureCosmosDB/HierarchicalPartitionKeysFeedbackGroup#net-v3-sdk-2"/>
        [TestMethod]
        public async Task ReadItemStreamWithFullPartitionKeyReturnsFeedIterator()
        {
            Container container = await this.cosmosDatabase.CreateContainerIfNotExistsAsync(new(id: @"TestMultiHashedContainer", partitionKeyPaths: new List<string>() { "/city", "/state", "/zipCode" }));
            ContainerProperties containerProperties = await container.ReadContainerAsync();

            Assert.AreEqual(expected: PartitionKeyDefinitionVersion.V2, actual: containerProperties.PartitionKeyDefinitionVersion);
            Assert.AreEqual(expected: 3, actual: containerProperties.PartitionKey.Paths.Count);
            Assert.IsTrue(containerProperties.PartitionKey.Paths.Contains("/city"));
            Assert.IsTrue(containerProperties.PartitionKey.Paths.Contains("/state"));
            Assert.IsTrue(containerProperties.PartitionKey.Paths.Contains("/zipCode"));
            Assert.AreEqual(expected: Documents.PartitionKind.MultiHash, actual: containerProperties.PartitionKey.Kind);

            try
            {
                dynamic item = new { id = Guid.NewGuid().ToString(), city = "Redmond", state = "WA", zipCode = "98052" };
                PartitionKey partitionKey = new PartitionKeyBuilder()
                    .Add(item.city)
                    .Add(item.state)
                    .Add(item.zipCode)
                    .Build();

                _ = await container.CreateItemAsync<dynamic>(item: item, partitionKey: partitionKey);

                using (ResponseMessage responseMessage = await container.ReadItemStreamAsync(id: item.id, partitionKey: partitionKey))
                {
                    using (StreamReader streamReader = new(responseMessage.Content))
                    {
                        string content = await streamReader.ReadToEndAsync();
                        JObject @object = JObject.Parse(content);

                        Assert.AreEqual(expected: item.id, actual: @object["id"]);
                        Assert.AreEqual(expected: item.city, actual: @object["city"]);
                        Assert.AreEqual(expected: item.state, actual: @object["state"]);
                        Assert.AreEqual(expected: item.zipCode, actual: @object["zipCode"]);
                    }
                }
            }
            finally
            {
                _ = await container.DeleteContainerAsync();
            }
        }
    }
}