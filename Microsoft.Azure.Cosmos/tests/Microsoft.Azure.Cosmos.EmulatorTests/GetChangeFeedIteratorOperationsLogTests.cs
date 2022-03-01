namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.FullFidelity;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class GetChangeFeedIteratorOperationsLogTests
    {
        private readonly string connectionString = @"AccountEndpoint=;AccountKey=";
        private readonly string databaseId = "";
        private readonly string containerId = "";
        private Container container;

        [TestInitialize]
        public async Task Initialize()
        {
            CosmosClient cosmosClient = new(connectionString: this.connectionString, clientOptions: new CosmosClientOptions { ConnectionMode = ConnectionMode.Direct });
            Database database = cosmosClient.GetDatabase(this.databaseId);
            ContainerProperties containerProperties = new(id: this.containerId, partitionKeyPath: "/id");
            containerProperties.ChangeFeedPolicy.FullFidelityRetention = TimeSpan.FromMinutes(5);
            this.container = await database.CreateContainerIfNotExistsAsync(containerProperties: containerProperties);
        }

        //[TestCleanup]
        //public async Task Cleanup()
        //{
        //    _ = await this.container.DeleteContainerAsync();
        //}

        /// <summary>
        /// This test will execute <see cref="Container.GetChangeFeedIterator{T}(ChangeFeedStartFrom, ChangeFeedMode, ChangeFeedRequestOptions)"/> in <see cref="ChangeFeedMode.OperationsLog"/> (FullFidelity) with a typed item.
        /// Using FeedRangeEpk.FromPartitionKey.
        /// </summary>
        [TestMethod]
        public async Task FeedRangeEpk_FromPartitionKey_VerifyingWireFormatTests()
        {
            string id = Guid.NewGuid().ToString();
            string otherId = Guid.NewGuid().ToString();
            using (FeedIterator<ChangeFeedItem<Item>> feedIterator = this.container.GetChangeFeedIterator<ChangeFeedItem<Item>>(
                changeFeedStartFrom: ChangeFeedStartFrom.Now(FeedRangeEpk.FromPartitionKey(new PartitionKey(id))),
            changeFeedMode: ChangeFeedMode.FullFidelity))
            {
                string continuation = null;
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<ChangeFeedItem<Item>> feedResponse = await feedIterator.ReadNextAsync();

                    if (feedResponse.StatusCode == System.Net.HttpStatusCode.NotModified)
                    {
                        continuation = feedResponse.ContinuationToken;
                        Assert.IsNotNull(continuation);
                        Console.WriteLine(continuation);

                        PartitionKey otherPartitionKey = new(otherId);
                        PartitionKey partitionKey = new(id);

                        _ = await this.container.UpsertItemAsync<Item>(item: new(Id: otherId, Line1: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", City: "Bangkok", State: "Thailand", ZipCode: "10330"), partitionKey: otherPartitionKey).ConfigureAwait(false);
                        _ = await this.container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "One Microsoft Way", City: "Redmond", State: "WA", ZipCode: "98052"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await this.container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "205 16th St NW", City: "Atlanta", State: "GA", ZipCode: "30363"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await this.container.DeleteItemAsync<Item>(id: id, partitionKey: partitionKey);

                        Console.WriteLine("delete happened.");
                    }
                    else
                    {
                        List<ChangeFeedItem<Item>> resources = feedResponse.Resource.ToList();

                        resources.ForEach(x => Console.WriteLine(x.Metadata.OperationType));
                        Assert.AreEqual(expected: 3, actual: resources.Count);

                        ChangeFeedItem<Item> createOperation = resources[0];

                        Assert.AreEqual(expected: id, actual: createOperation.Current.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: createOperation.Current.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: createOperation.Current.City);
                        Assert.AreEqual(expected: "WA", actual: createOperation.Current.State);
                        Assert.AreEqual(expected: "98052", actual: createOperation.Current.ZipCode);
                        Assert.AreEqual(expected: OperationType.Create, actual: createOperation.Metadata.OperationType);
                        Assert.IsNull(createOperation.Previous);

                        ChangeFeedItem<Item> replaceOperation = resources[1];

                        Assert.AreEqual(expected: id, actual: replaceOperation.Current.Id);
                        Assert.AreEqual(expected: "205 16th St NW", actual: replaceOperation.Current.Line1);
                        Assert.AreEqual(expected: "Atlanta", actual: replaceOperation.Current.City);
                        Assert.AreEqual(expected: "GA", actual: replaceOperation.Current.State);
                        Assert.AreEqual(expected: "30363", actual: replaceOperation.Current.ZipCode);
                        Assert.AreEqual(expected: OperationType.Replace, actual: replaceOperation.Metadata.OperationType);
                        Assert.AreEqual(expected: id, actual: replaceOperation.Previous.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: replaceOperation.Previous.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: replaceOperation.Previous.City);
                        Assert.AreEqual(expected: "WA", actual: replaceOperation.Previous.State);
                        Assert.AreEqual(expected: "98052", actual: replaceOperation.Previous.ZipCode);

                        ChangeFeedItem<Item> deleteOperation = resources[2];

                        Assert.IsNull(deleteOperation.Current.Id);
                        Assert.IsNull(deleteOperation.Current.Line1);
                        Assert.IsNull(deleteOperation.Current.City);
                        Assert.IsNull(deleteOperation.Current.State);
                        Assert.IsNull(deleteOperation.Current.ZipCode);
                        Assert.AreEqual(expected: OperationType.Delete, actual: deleteOperation.Metadata.OperationType);
                        Assert.AreEqual(expected: id, actual: deleteOperation.Previous.Id);
                        Assert.AreEqual(expected: "205 16th St NW", actual: deleteOperation.Previous.Line1);
                        Assert.AreEqual(expected: "Atlanta", actual: deleteOperation.Previous.City);
                        Assert.AreEqual(expected: "GA", actual: deleteOperation.Previous.State);
                        Assert.AreEqual(expected: "30363", actual: deleteOperation.Previous.ZipCode);

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// This test will execute <see cref="Container.GetChangeFeedIterator{T}(ChangeFeedStartFrom, ChangeFeedMode, ChangeFeedRequestOptions)"/> in <see cref="ChangeFeedMode.OperationsLog"/> (FullFidelity) with a typed item.
        /// Using FeedRangeEpk.FullRange explicity.
        /// </summary>
        [TestMethod]
        public async Task FeedRangeEpk_Explicit_FullRange_VerifyingWireFormatTests()
        {
            string id = Guid.NewGuid().ToString();
            string otherId = Guid.NewGuid().ToString();
            using (FeedIterator<ChangeFeedItem<Item>> feedIterator = this.container.GetChangeFeedIterator<ChangeFeedItem<Item>>(
                changeFeedStartFrom: ChangeFeedStartFrom.Now(FeedRangeEpk.FullRange),
                changeFeedMode: ChangeFeedMode.FullFidelity))
            {
                string continuation = null;
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<ChangeFeedItem<Item>> feedResponse = await feedIterator.ReadNextAsync();

                    if (feedResponse.StatusCode == System.Net.HttpStatusCode.NotModified)
                    {
                        continuation = feedResponse.ContinuationToken;
                        Assert.IsNotNull(continuation);
                        Console.WriteLine(continuation);

                        PartitionKey partitionKey = new(id);
                        PartitionKey otherPartitionKey = new(otherId);

                        _ = await this.container.UpsertItemAsync<Item>(item: new(Id: otherId, Line1: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", City: "Bangkok", State: "Thailand", ZipCode: "10330"), partitionKey: otherPartitionKey).ConfigureAwait(false);
                        _ = await this.container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "One Microsoft Way", City: "Redmond", State: "WA", ZipCode: "98052"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await this.container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "205 16th St NW", City: "Atlanta", State: "GA", ZipCode: "30363"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await this.container.DeleteItemAsync<Item>(id: id, partitionKey: partitionKey);
                    }
                    else
                    {
                        List<ChangeFeedItem<Item>> resources = feedResponse.Resource.ToList();

                        resources.ForEach(x => Console.WriteLine(x.Metadata.OperationType));
                        Assert.AreEqual(expected: 4, actual: resources.Count);

                        ChangeFeedItem<Item> firstCreateOperation = resources[0];

                        Assert.AreEqual(expected: otherId, actual: firstCreateOperation.Current.Id);
                        Assert.AreEqual(expected: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", actual: firstCreateOperation.Current.Line1);
                        Assert.AreEqual(expected: "Bangkok", actual: firstCreateOperation.Current.City);
                        Assert.AreEqual(expected: "Thailand", actual: firstCreateOperation.Current.State);
                        Assert.AreEqual(expected: "10330", actual: firstCreateOperation.Current.ZipCode);
                        Assert.AreEqual(expected: OperationType.Create, actual: firstCreateOperation.Metadata.OperationType);
                        Assert.IsNull(firstCreateOperation.Previous);

                        ChangeFeedItem<Item> createOperation = resources[1];

                        Assert.AreEqual(expected: id, actual: createOperation.Current.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: createOperation.Current.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: createOperation.Current.City);
                        Assert.AreEqual(expected: "WA", actual: createOperation.Current.State);
                        Assert.AreEqual(expected: "98052", actual: createOperation.Current.ZipCode);
                        Assert.AreEqual(expected: OperationType.Create, actual: createOperation.Metadata.OperationType);
                        Assert.IsNull(createOperation.Previous);

                        ChangeFeedItem<Item> replaceOperation = resources[2];

                        Assert.AreEqual(expected: id, actual: replaceOperation.Current.Id);
                        Assert.AreEqual(expected: "205 16th St NW", actual: replaceOperation.Current.Line1);
                        Assert.AreEqual(expected: "Atlanta", actual: replaceOperation.Current.City);
                        Assert.AreEqual(expected: "GA", actual: replaceOperation.Current.State);
                        Assert.AreEqual(expected: "30363", actual: replaceOperation.Current.ZipCode);
                        Assert.AreEqual(expected: OperationType.Replace, actual: replaceOperation.Metadata.OperationType);
                        Assert.AreEqual(expected: id, actual: replaceOperation.Previous.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: replaceOperation.Previous.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: replaceOperation.Previous.City);
                        Assert.AreEqual(expected: "WA", actual: replaceOperation.Previous.State);
                        Assert.AreEqual(expected: "98052", actual: replaceOperation.Previous.ZipCode);

                        ChangeFeedItem<Item> deleteOperation = resources[3];

                        Assert.IsNull(deleteOperation.Current.Id);
                        Assert.IsNull(deleteOperation.Current.Line1);
                        Assert.IsNull(deleteOperation.Current.City);
                        Assert.IsNull(deleteOperation.Current.State);
                        Assert.IsNull(deleteOperation.Current.ZipCode);
                        Assert.AreEqual(expected: OperationType.Delete, actual: deleteOperation.Metadata.OperationType);
                        Assert.AreEqual(expected: id, actual: deleteOperation.Previous.Id);
                        Assert.AreEqual(expected: "205 16th St NW", actual: deleteOperation.Previous.Line1);
                        Assert.AreEqual(expected: "Atlanta", actual: deleteOperation.Previous.City);
                        Assert.AreEqual(expected: "GA", actual: deleteOperation.Previous.State);
                        Assert.AreEqual(expected: "30363", actual: deleteOperation.Previous.ZipCode);

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// This test will execute <see cref="Container.GetChangeFeedIterator{T}(ChangeFeedStartFrom, ChangeFeedMode, ChangeFeedRequestOptions)"/> in <see cref="ChangeFeedMode.OperationsLog"/> (FullFidelity) with a typed item.
        /// Using FeedRangeEpk.FullRange implicity.
        /// </summary>
        [TestMethod]
        public async Task FeedRangeEpk_Implicit_FullRange_VerifyingWireFormatTests()
        {
            string id = Guid.NewGuid().ToString();
            string otherId = Guid.NewGuid().ToString();
            using (FeedIterator<ChangeFeedItem<Item>> feedIterator = this.container.GetChangeFeedIterator<ChangeFeedItem<Item>>(
                changeFeedStartFrom: ChangeFeedStartFrom.Now(),
                changeFeedMode: ChangeFeedMode.FullFidelity))
            {
                string continuation = null;
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<ChangeFeedItem<Item>> feedResponse = await feedIterator.ReadNextAsync();

                    if (feedResponse.StatusCode == System.Net.HttpStatusCode.NotModified)
                    {
                        continuation = feedResponse.ContinuationToken;
                        Assert.IsNotNull(continuation);
                        Console.WriteLine(continuation);

                        PartitionKey partitionKey = new(id);
                        PartitionKey otherPartitionKey = new(otherId);

                        _ = await this.container.UpsertItemAsync<Item>(item: new(Id: otherId, Line1: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", City: "Bangkok", State: "Thailand", ZipCode: "10330"), partitionKey: otherPartitionKey).ConfigureAwait(false);

                        _ = await this.container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "One Microsoft Way", City: "Redmond", State: "WA", ZipCode: "98052"), partitionKey: partitionKey).ConfigureAwait(false);

                        _ = await this.container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "205 16th St NW", City: "Atlanta", State: "GA", ZipCode: "30363"), partitionKey: partitionKey).ConfigureAwait(false);

                        _ = await this.container.DeleteItemAsync<Item>(id: id, partitionKey: partitionKey);
                    }
                    else
                    {
                        List<ChangeFeedItem<Item>> resources = feedResponse.Resource.ToList();

                        resources.ForEach(x => Console.WriteLine(x.Metadata.OperationType));
                        Assert.AreEqual(expected: 4, actual: resources.Count);

                        ChangeFeedItem<Item> firstCreateOperation = resources[0];

                        Assert.AreEqual(expected: otherId, actual: firstCreateOperation.Current.Id);
                        Assert.AreEqual(expected: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", actual: firstCreateOperation.Current.Line1);
                        Assert.AreEqual(expected: "Bangkok", actual: firstCreateOperation.Current.City);
                        Assert.AreEqual(expected: "Thailand", actual: firstCreateOperation.Current.State);
                        Assert.AreEqual(expected: "10330", actual: firstCreateOperation.Current.ZipCode);
                        Assert.AreEqual(expected: OperationType.Create, actual: firstCreateOperation.Metadata.OperationType);
                        Assert.IsNull(firstCreateOperation.Previous);

                        ChangeFeedItem<Item> createOperation = resources[1];

                        Assert.AreEqual(expected: id, actual: createOperation.Current.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: createOperation.Current.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: createOperation.Current.City);
                        Assert.AreEqual(expected: "WA", actual: createOperation.Current.State);
                        Assert.AreEqual(expected: "98052", actual: createOperation.Current.ZipCode);
                        Assert.AreEqual(expected: OperationType.Create, actual: createOperation.Metadata.OperationType);
                        Assert.IsNull(createOperation.Previous);

                        ChangeFeedItem<Item> replaceOperation = resources[2];

                        Assert.AreEqual(expected: id, actual: replaceOperation.Current.Id);
                        Assert.AreEqual(expected: "205 16th St NW", actual: replaceOperation.Current.Line1);
                        Assert.AreEqual(expected: "Atlanta", actual: replaceOperation.Current.City);
                        Assert.AreEqual(expected: "GA", actual: replaceOperation.Current.State);
                        Assert.AreEqual(expected: "30363", actual: replaceOperation.Current.ZipCode);
                        Assert.AreEqual(expected: OperationType.Replace, actual: replaceOperation.Metadata.OperationType);
                        Assert.AreEqual(expected: id, actual: replaceOperation.Previous.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: replaceOperation.Previous.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: replaceOperation.Previous.City);
                        Assert.AreEqual(expected: "WA", actual: replaceOperation.Previous.State);
                        Assert.AreEqual(expected: "98052", actual: replaceOperation.Previous.ZipCode);

                        ChangeFeedItem<Item> deleteOperation = resources[3];

                        Assert.IsNull(deleteOperation.Current.Id);
                        Assert.IsNull(deleteOperation.Current.Line1);
                        Assert.IsNull(deleteOperation.Current.City);
                        Assert.IsNull(deleteOperation.Current.State);
                        Assert.IsNull(deleteOperation.Current.ZipCode);
                        Assert.AreEqual(expected: OperationType.Delete, actual: deleteOperation.Metadata.OperationType);
                        Assert.AreEqual(expected: id, actual: deleteOperation.Previous.Id);
                        Assert.AreEqual(expected: "205 16th St NW", actual: deleteOperation.Previous.Line1);
                        Assert.AreEqual(expected: "Atlanta", actual: deleteOperation.Previous.City);
                        Assert.AreEqual(expected: "GA", actual: deleteOperation.Previous.State);
                        Assert.AreEqual(expected: "30363", actual: deleteOperation.Previous.ZipCode);

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// This test will execute <see cref="Container.GetChangeFeedIterator{T}(ChangeFeedStartFrom, ChangeFeedMode, ChangeFeedRequestOptions)"/> in <see cref="ChangeFeedMode.OperationsLog"/> (FullFidelity) with a typed item.
        /// Using FeedRangePartitionKeyRange.FromPartitionKey
        /// </summary>
        [TestMethod]
        public async Task FeedRangePartitionKeyRange_FromPartitionKey_VerifyingWireFormatTests()
        {
            string id = Guid.NewGuid().ToString();
            string otherId = Guid.NewGuid().ToString();
            using (FeedIterator<ChangeFeedItem<Item>> feedIterator = this.container.GetChangeFeedIterator<ChangeFeedItem<Item>>(
                changeFeedStartFrom: ChangeFeedStartFrom.Now(FeedRangePartitionKeyRange.FromPartitionKey(new PartitionKey(id))),
                changeFeedMode: ChangeFeedMode.FullFidelity))
            {
                string continuation = null;
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<ChangeFeedItem<Item>> feedResponse = await feedIterator.ReadNextAsync();

                    Console.WriteLine($"feed response status code: {feedResponse.ContinuationToken}, {feedResponse.StatusCode}, {feedIterator.HasMoreResults}");

                    if (feedResponse.StatusCode == System.Net.HttpStatusCode.NotModified)
                    {
                        continuation = feedResponse.ContinuationToken;
                        Assert.IsNotNull(continuation);
                        Console.WriteLine(continuation);

                        PartitionKey partitionKey = new(id);
                        PartitionKey otherPartitionKey = new(otherId);

                        _ = await this.container.UpsertItemAsync<Item>(item: new(Id: otherId, Line1: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", City: "Bangkok", State: "Thailand", ZipCode: "10330"), partitionKey: otherPartitionKey).ConfigureAwait(false);

                        _ = await this.container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "One Microsoft Way", City: "Redmond", State: "WA", ZipCode: "98052"), partitionKey: partitionKey).ConfigureAwait(false);

                        _ = await this.container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "205 16th St NW", City: "Atlanta", State: "GA", ZipCode: "30363"), partitionKey: partitionKey).ConfigureAwait(false);

                        _ = await this.container.DeleteItemAsync<Item>(id: id, partitionKey: partitionKey);

                        Console.WriteLine("after operations!");
                    }
                    else
                    {
                        List<ChangeFeedItem<Item>> resources = feedResponse.Resource.ToList();

                        resources.ForEach(x => Console.WriteLine(x.Metadata.OperationType));

                        Assert.AreEqual(expected: 3, actual: resources.Count);

                        ChangeFeedItem<Item> createOperation = resources[0];

                        Assert.AreEqual(expected: id, actual: createOperation.Current.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: createOperation.Current.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: createOperation.Current.City);
                        Assert.AreEqual(expected: "WA", actual: createOperation.Current.State);
                        Assert.AreEqual(expected: "98052", actual: createOperation.Current.ZipCode);
                        Assert.AreEqual(expected: OperationType.Create, actual: createOperation.Metadata.OperationType);
                        Assert.IsNull(createOperation.Previous);

                        ChangeFeedItem<Item> replaceOperation = resources[1];

                        Assert.AreEqual(expected: id, actual: replaceOperation.Current.Id);
                        Assert.AreEqual(expected: "205 16th St NW", actual: replaceOperation.Current.Line1);
                        Assert.AreEqual(expected: "Atlanta", actual: replaceOperation.Current.City);
                        Assert.AreEqual(expected: "GA", actual: replaceOperation.Current.State);
                        Assert.AreEqual(expected: "30363", actual: replaceOperation.Current.ZipCode);
                        Assert.AreEqual(expected: OperationType.Replace, actual: replaceOperation.Metadata.OperationType);
                        Assert.AreEqual(expected: id, actual: replaceOperation.Previous.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: replaceOperation.Previous.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: replaceOperation.Previous.City);
                        Assert.AreEqual(expected: "WA", actual: replaceOperation.Previous.State);
                        Assert.AreEqual(expected: "98052", actual: replaceOperation.Previous.ZipCode);

                        ChangeFeedItem<Item> deleteOperation = resources[2];

                        Assert.IsNull(deleteOperation.Current.Id);
                        Assert.IsNull(deleteOperation.Current.Line1);
                        Assert.IsNull(deleteOperation.Current.City);
                        Assert.IsNull(deleteOperation.Current.State);
                        Assert.IsNull(deleteOperation.Current.ZipCode);
                        Assert.AreEqual(expected: OperationType.Delete, actual: deleteOperation.Metadata.OperationType);
                        Assert.AreEqual(expected: id, actual: deleteOperation.Previous.Id);
                        Assert.AreEqual(expected: "205 16th St NW", actual: deleteOperation.Previous.Line1);
                        Assert.AreEqual(expected: "Atlanta", actual: deleteOperation.Previous.City);
                        Assert.AreEqual(expected: "GA", actual: deleteOperation.Previous.State);
                        Assert.AreEqual(expected: "30363", actual: deleteOperation.Previous.ZipCode);

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// This test will execute <see cref="Container.GetChangeFeedIterator{T}(ChangeFeedStartFrom, ChangeFeedMode, ChangeFeedRequestOptions)"/> in <see cref="ChangeFeedMode.OperationsLog"/> (FullFidelity) with a typed item.
        /// Using FeedRange.FromPartitionKey.
        /// </summary>
        [TestMethod]
        public async Task FeedRange_FromPartitionKey_VerifyingWireFormatTests()
        {
            string id = Guid.NewGuid().ToString();
            string otherId = Guid.NewGuid().ToString();
            using (FeedIterator<ChangeFeedItem<Item>> feedIterator = this.container.GetChangeFeedIterator<ChangeFeedItem<Item>>(
                changeFeedStartFrom: ChangeFeedStartFrom.Now(FeedRange.FromPartitionKey(new PartitionKey(id))),
                changeFeedMode: ChangeFeedMode.FullFidelity))
            {
                string continuation = null;
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<ChangeFeedItem<Item>> feedResponse = await feedIterator.ReadNextAsync();

                    if (feedResponse.StatusCode == System.Net.HttpStatusCode.NotModified)
                    {
                        continuation = feedResponse.ContinuationToken;
                        Assert.IsNotNull(continuation);
                        Console.WriteLine(continuation);

                        PartitionKey partitionKey = new(id);
                        PartitionKey otherPartitionKey = new(otherId);

                        _ = await this.container.UpsertItemAsync<Item>(item: new(Id: otherId, Line1: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", City: "Bangkok", State: "Thailand", ZipCode: "10330"), partitionKey: otherPartitionKey).ConfigureAwait(false);

                        _ = await this.container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "One Microsoft Way", City: "Redmond", State: "WA", ZipCode: "98052"), partitionKey: partitionKey).ConfigureAwait(false);

                        _ = await this.container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "205 16th St NW", City: "Atlanta", State: "GA", ZipCode: "30363"), partitionKey: partitionKey).ConfigureAwait(false);

                        _ = await this.container.DeleteItemStreamAsync(id: id, partitionKey: partitionKey);
                    }
                    else
                    {
                        List<ChangeFeedItem<Item>> resources = feedResponse.Resource.ToList();

                        resources.ForEach(x => Console.WriteLine(x.Metadata.OperationType));
                        Assert.AreEqual(expected: 3, actual: resources.Count);

                        ChangeFeedItem<Item> createOperation = resources[0];

                        Assert.AreEqual(expected: id, actual: createOperation.Current.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: createOperation.Current.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: createOperation.Current.City);
                        Assert.AreEqual(expected: "WA", actual: createOperation.Current.State);
                        Assert.AreEqual(expected: "98052", actual: createOperation.Current.ZipCode);
                        Assert.AreEqual(expected: OperationType.Create, actual: createOperation.Metadata.OperationType);
                        Assert.IsNull(createOperation.Previous);

                        ChangeFeedItem<Item> replaceOperation = resources[1];

                        Assert.AreEqual(expected: id, actual: replaceOperation.Current.Id);
                        Assert.AreEqual(expected: "205 16th St NW", actual: replaceOperation.Current.Line1);
                        Assert.AreEqual(expected: "Atlanta", actual: replaceOperation.Current.City);
                        Assert.AreEqual(expected: "GA", actual: replaceOperation.Current.State);
                        Assert.AreEqual(expected: "30363", actual: replaceOperation.Current.ZipCode);
                        Assert.AreEqual(expected: OperationType.Replace, actual: replaceOperation.Metadata.OperationType);
                        Assert.AreEqual(expected: id, actual: replaceOperation.Previous.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: replaceOperation.Previous.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: replaceOperation.Previous.City);
                        Assert.AreEqual(expected: "WA", actual: replaceOperation.Previous.State);
                        Assert.AreEqual(expected: "98052", actual: replaceOperation.Previous.ZipCode);

                        ChangeFeedItem<Item> deleteOperation = resources[2];

                        Assert.IsNull(deleteOperation.Current.Id);
                        Assert.IsNull(deleteOperation.Current.Line1);
                        Assert.IsNull(deleteOperation.Current.City);
                        Assert.IsNull(deleteOperation.Current.State);
                        Assert.IsNull(deleteOperation.Current.ZipCode);
                        Assert.AreEqual(expected: OperationType.Delete, actual: deleteOperation.Metadata.OperationType);
                        Assert.AreEqual(expected: id, actual: deleteOperation.Previous.Id);
                        Assert.AreEqual(expected: "205 16th St NW", actual: deleteOperation.Previous.Line1);
                        Assert.AreEqual(expected: "Atlanta", actual: deleteOperation.Previous.City);
                        Assert.AreEqual(expected: "GA", actual: deleteOperation.Previous.State);
                        Assert.AreEqual(expected: "30363", actual: deleteOperation.Previous.ZipCode);

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// This test will execute <see cref="Container.GetChangeFeedIterator{T}(ChangeFeedStartFrom, ChangeFeedMode, ChangeFeedRequestOptions)"/> in <see cref="ChangeFeedMode.OperationsLog"/> (FullFidelity) with a typed item.
        /// Using FeedRangeEpk.FromPartitionKey.
        /// </summary>
        [TestMethod]
        public async Task FeedRange_VerifyingWireFormatTests()
        {
            IReadOnlyList<FeedRange> ranges = await this.container.GetFeedRangesAsync();
            string id = Guid.NewGuid().ToString();
            string otherId = Guid.NewGuid().ToString();
            
            using (FeedIterator<ChangeFeedItem<Item>> feedIterator = this.container.GetChangeFeedIterator<ChangeFeedItem<Item>>(
                changeFeedStartFrom: ChangeFeedStartFrom.Now(ranges[0]),
                changeFeedMode: ChangeFeedMode.FullFidelity))
            {
                string continuation = null;
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<ChangeFeedItem<Item>> feedResponse = await feedIterator.ReadNextAsync();

                    if (feedResponse.StatusCode == System.Net.HttpStatusCode.NotModified)
                    {
                        continuation = feedResponse.ContinuationToken;
                        Assert.IsNotNull(continuation);
                        Console.WriteLine(continuation);

                        PartitionKey partitionKey = new(id);
                        PartitionKey otherPartitionKey = new(otherId);

                        _ = await this.container.UpsertItemAsync<Item>(item: new(Id: otherId, Line1: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", City: "Bangkok", State: "Thailand", ZipCode: "10330"), partitionKey: otherPartitionKey).ConfigureAwait(false);
                        
                        _ = await this.container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "One Microsoft Way", City: "Redmond", State: "WA", ZipCode: "98052"), partitionKey: partitionKey).ConfigureAwait(false);
                        
                        _ = await this.container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "205 16th St NW", City: "Atlanta", State: "GA", ZipCode: "30363"), partitionKey: partitionKey).ConfigureAwait(false);
                        
                        _ = await this.container.DeleteItemAsync<Item>(id: id, partitionKey: partitionKey);
                    }
                    else
                    {
                        List<ChangeFeedItem<Item>> resources = feedResponse.Resource.ToList();

                        resources.ForEach(x => Console.WriteLine(x.Metadata.OperationType));
                        Assert.AreEqual(expected: 4, actual: resources.Count);


                        ChangeFeedItem<Item> firstCreateOperation = resources[0];

                        Assert.AreEqual(expected: otherId, actual: firstCreateOperation.Current.Id);
                        Assert.AreEqual(expected: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", actual: firstCreateOperation.Current.Line1);
                        Assert.AreEqual(expected: "Bangkok", actual: firstCreateOperation.Current.City);
                        Assert.AreEqual(expected: "Thailand", actual: firstCreateOperation.Current.State);
                        Assert.AreEqual(expected: "10330", actual: firstCreateOperation.Current.ZipCode);
                        Assert.AreEqual(expected: OperationType.Create, actual: firstCreateOperation.Metadata.OperationType);
                        Assert.IsNull(firstCreateOperation.Previous);

                        ChangeFeedItem<Item> createOperation = resources[1];

                        Assert.AreEqual(expected: id, actual: createOperation.Current.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: createOperation.Current.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: createOperation.Current.City);
                        Assert.AreEqual(expected: "WA", actual: createOperation.Current.State);
                        Assert.AreEqual(expected: "98052", actual: createOperation.Current.ZipCode);
                        Assert.AreEqual(expected: OperationType.Create, actual: createOperation.Metadata.OperationType);
                        Assert.IsNull(createOperation.Previous);

                        ChangeFeedItem<Item> replaceOperation = resources[2];

                        Assert.AreEqual(expected: id, actual: replaceOperation.Current.Id);
                        Assert.AreEqual(expected: "205 16th St NW", actual: replaceOperation.Current.Line1);
                        Assert.AreEqual(expected: "Atlanta", actual: replaceOperation.Current.City);
                        Assert.AreEqual(expected: "GA", actual: replaceOperation.Current.State);
                        Assert.AreEqual(expected: "30363", actual: replaceOperation.Current.ZipCode);
                        Assert.AreEqual(expected: OperationType.Replace, actual: replaceOperation.Metadata.OperationType);
                        Assert.AreEqual(expected: id, actual: replaceOperation.Previous.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: replaceOperation.Previous.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: replaceOperation.Previous.City);
                        Assert.AreEqual(expected: "WA", actual: replaceOperation.Previous.State);
                        Assert.AreEqual(expected: "98052", actual: replaceOperation.Previous.ZipCode);

                        ChangeFeedItem<Item> deleteOperation = resources[3];

                        Assert.IsNull(deleteOperation.Current.Id);
                        Assert.IsNull(deleteOperation.Current.Line1);
                        Assert.IsNull(deleteOperation.Current.City);
                        Assert.IsNull(deleteOperation.Current.State);
                        Assert.IsNull(deleteOperation.Current.ZipCode);
                        Assert.AreEqual(expected: OperationType.Delete, actual: deleteOperation.Metadata.OperationType);
                        Assert.AreEqual(expected: id, actual: deleteOperation.Previous.Id);
                        Assert.AreEqual(expected: "205 16th St NW", actual: deleteOperation.Previous.Line1);
                        Assert.AreEqual(expected: "Atlanta", actual: deleteOperation.Previous.City);
                        Assert.AreEqual(expected: "GA", actual: deleteOperation.Previous.State);
                        Assert.AreEqual(expected: "30363", actual: deleteOperation.Previous.ZipCode);

                        break;
                    }
                }
            }
        }
    }

    public record Item(
        [property: JsonProperty("id")] string Id,
        [property: JsonProperty("line1")] string Line1,
        [property: JsonProperty("city")] string City,
        [property: JsonProperty("state")] string State,
        [property: JsonProperty("zipCode")] string ZipCode);
}
