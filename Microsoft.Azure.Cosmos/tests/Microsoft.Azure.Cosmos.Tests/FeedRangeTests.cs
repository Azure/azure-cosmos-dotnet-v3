namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class FromPartitionKeyAsyncTests
    {
        [TestMethod]
        public async Task FeedRange_GetFeedRangeAsync_Hash_Returns_FeedRangePartitionKey()
        {
            string connectionString = string.Empty;
            string databaseId = "sandboxDatabase";
            string containerId = "hashContainer";
            CosmosClient client = new(connectionString: connectionString);
            Database database = client.GetDatabase(id: databaseId);
            Collection<string> subpartitionKeyPaths = new() { @"/pk1" };
            Documents.PartitionKeyDefinition partitionKeyDefinition = new() { Paths = subpartitionKeyPaths, Kind = Documents.PartitionKind.Hash };
            ContainerProperties containerProperties = new(id: containerId, partitionKeyDefinition: partitionKeyDefinition);
            Container container = await database.CreateContainerIfNotExistsAsync(containerProperties: containerProperties, throughput: 400);
            PartitionKey partitionkey = new PartitionKeyBuilder()
                .Add("98052")
                .Build();

            Cosmos.FeedRange expected = Cosmos.FeedRange.FromPartitionKey(partitionKey: partitionkey);
            Cosmos.FeedRange actual = await Cosmos.FeedRange.CreateFromPartitionKeyAsync(container: container, partitionKey: partitionkey);

            Console.WriteLine(actual);
            Assert.IsNotNull(value: actual);
            Assert.AreEqual(expected: expected.ToJsonString(), actual: actual.ToJsonString());
            Assert.IsInstanceOfType(value: actual, expectedType: typeof(FeedRangePartitionKey));
        }

        [TestMethod]
        public async Task FeedRange_GetFeedRangeAsync_MultiHash_Returns_FeedRangeEpk()
        {

            string connectionString = string.Empty;
            string databaseId = "sandboxDatabase";
            string containerId = "multiHashContainer";
            CosmosClient client = new(connectionString: connectionString);
            Database database = client.GetDatabase(id: databaseId);
            Collection<string> subpartitionKeyPaths = new() { @"/pk1", @"/pk2", @"/pk3" };
            Documents.PartitionKeyDefinition partitionKeyDefinition = new() { Paths = subpartitionKeyPaths, Kind = Documents.PartitionKind.MultiHash };
            ContainerProperties containerProperties = new(id: containerId, partitionKeyDefinition: partitionKeyDefinition);
            Container container = await database.CreateContainerIfNotExistsAsync(containerProperties: containerProperties, throughput: 400);
            PartitionKey partitionkey = new PartitionKeyBuilder()
                .Add("WA")
                .Add("98052")
                .Add("Redmond")
                .Build();

            // Cosmos.FeedRange expected = Cosmos.FeedRange.FromPartitionKey(container: container, partitionKey: partitionkey);
            Cosmos.FeedRange actual = await Cosmos.FeedRange.CreateFromPartitionKeyAsync(container: container, partitionKey: partitionkey);

            Console.WriteLine(actual);
            Assert.IsNotNull(value: actual);
            Assert.AreEqual(expected: default, actual: actual.ToJsonString());
            Assert.IsInstanceOfType(value: actual, expectedType: typeof(FeedRangeEpk));
        }
    }
}