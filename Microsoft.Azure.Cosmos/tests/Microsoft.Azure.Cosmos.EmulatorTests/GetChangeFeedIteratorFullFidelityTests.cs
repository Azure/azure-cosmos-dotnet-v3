namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class GetChangeFeedIteratorFullFidelityTests : BaseCosmosClientHelper
    {
        private static readonly string PartitionKey = "/id";

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit(customizeClientBuilder: (b) => b.WithCustomSerializer(new TestItemSerializer()));
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        private async Task<ContainerInternal> InitializeLargeContainerAsync()
        {
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: GetChangeFeedIteratorFullFidelityTests.PartitionKey),
                throughput: 20000,
                cancellationToken: this.cancellationToken);

            return (ContainerInternal)response;
        }

        private async Task<ContainerInternal> InitializeContainerAsync()
        {
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: GetChangeFeedIteratorFullFidelityTests.PartitionKey),
                cancellationToken: this.cancellationToken);

            return (ContainerInternal)response;
        }

        private async Task<ContainerInternal> InitializeLargeContainerAsync()
        {
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: GetChangeFeedIteratorOperationsLogTests.PartitionKey),
                throughput: 20000,
                cancellationToken: this.cancellationToken);

            return (ContainerInternal)response;
        }

        private async Task<ContainerInternal> InitializeContainerAsync()
        {
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: GetChangeFeedIteratorOperationsLogTests.PartitionKey),
                cancellationToken: this.cancellationToken);

            return (ContainerInternal)response;
        }

        /// <summary>
        /// This test will execute <see cref="Container.GetChangeFeedIterator{T}(ChangeFeedStartFrom, ChangeFeedMode, ChangeFeedRequestOptions)"/> in <see cref="ChangeFeedMode.OperationsLog"/> (FullFidelity) with a typed item.
        /// Using FeedRangeEpk.FromPartitionKey.
        /// </summary>
        [TestMethod]
        public async Task FeedRangeEpk_FromPartitionKey_VerifyingWireFormatTests()
        {
            ContainerProperties properties = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: GetChangeFeedIteratorFullFidelityTests.PartitionKey);
            properties.ChangeFeedPolicy.FullFidelityRetention = TimeSpan.FromMinutes(5);
            ContainerResponse response = await this.database.CreateContainerAsync(
                properties,
                cancellationToken: this.cancellationToken);
            ContainerInternal container = (ContainerInternal)response;
            string id = Guid.NewGuid().ToString();
            string otherId = Guid.NewGuid().ToString();
            using (FeedIterator<ChangeFeedItemChanges<Item>> feedIterator = container.GetChangeFeedIterator<ChangeFeedItemChanges<Item>>(
                changeFeedStartFrom: ChangeFeedStartFrom.Now(FeedRangeEpk.FromPartitionKey(new PartitionKey(id))),
            changeFeedMode: ChangeFeedMode.FullFidelity))
            {
                string continuation = null;
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<ChangeFeedItemChanges<Item>> feedResponse = await feedIterator.ReadNextAsync();

                    if (feedResponse.StatusCode == System.Net.HttpStatusCode.NotModified)
                    {
                        continuation = feedResponse.ContinuationToken;
                        Assert.IsNotNull(continuation);
                        Console.WriteLine(continuation);

                        PartitionKey otherPartitionKey = new(otherId);
                        PartitionKey partitionKey = new(id);

                        _ = await container.UpsertItemAsync<Item>(item: new(Id: otherId, Line1: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", City: "Bangkok", State: "Thailand", ZipCode: "10330"), partitionKey: otherPartitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "One Microsoft Way", City: "Redmond", State: "WA", ZipCode: "98052"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "205 16th St NW", City: "Atlanta", State: "GA", ZipCode: "30363"), partitionKey: partitionKey).ConfigureAwait(false);
                    }
                    else
                    {
                        List<ChangeFeedItemChanges<Item>> resources = feedResponse.Resource.ToList();

                        GetChangeFeedIteratorFullFidelityTests.AssertGatewayMode(feedResponse);
                        
                        Assert.AreEqual(expected: 2, actual: resources.Count);

                        ChangeFeedItemChanges<Item> createOperation = resources[0];

                        Assert.AreEqual(expected: id, actual: createOperation.Current.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: createOperation.Current.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: createOperation.Current.City);
                        Assert.AreEqual(expected: "WA", actual: createOperation.Current.State);
                        Assert.AreEqual(expected: "98052", actual: createOperation.Current.ZipCode);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Create, actual: createOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: createOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: createOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreEqual(expected: default, actual: createOperation.Metadata.PreviousLogSequenceNumber);
                        Assert.IsNull(createOperation.Previous);

                        ChangeFeedItemChanges<Item> replaceOperation = resources[1];

                        Assert.AreEqual(expected: id, actual: replaceOperation.Current.Id);
                        Assert.AreEqual(expected: "205 16th St NW", actual: replaceOperation.Current.Line1);
                        Assert.AreEqual(expected: "Atlanta", actual: replaceOperation.Current.City);
                        Assert.AreEqual(expected: "GA", actual: replaceOperation.Current.State);
                        Assert.AreEqual(expected: "30363", actual: replaceOperation.Current.ZipCode);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Replace, actual: replaceOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.PreviousLogSequenceNumber);
                        Assert.AreEqual(expected: id, actual: replaceOperation.Previous.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: replaceOperation.Previous.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: replaceOperation.Previous.City);
                        Assert.AreEqual(expected: "WA", actual: replaceOperation.Previous.State);
                        Assert.AreEqual(expected: "98052", actual: replaceOperation.Previous.ZipCode);

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
            ContainerProperties properties = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: GetChangeFeedIteratorFullFidelityTests.PartitionKey);
            properties.ChangeFeedPolicy.FullFidelityRetention = TimeSpan.FromMinutes(5);
            ContainerResponse response = await this.database.CreateContainerAsync(
                properties,
                cancellationToken: this.cancellationToken);
            ContainerInternal container = (ContainerInternal)response;
            string id = Guid.NewGuid().ToString();
            string otherId = Guid.NewGuid().ToString();
            using (FeedIterator<ChangeFeedItemChanges<Item>> feedIterator = container.GetChangeFeedIterator<ChangeFeedItemChanges<Item>>(
                changeFeedStartFrom: ChangeFeedStartFrom.Now(FeedRangeEpk.FullRange),
                changeFeedMode: ChangeFeedMode.FullFidelity))
            {
                string continuation = null;
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<ChangeFeedItemChanges<Item>> feedResponse = await feedIterator.ReadNextAsync();

                    if (feedResponse.StatusCode == System.Net.HttpStatusCode.NotModified)
                    {
                        continuation = feedResponse.ContinuationToken;
                        Assert.IsNotNull(continuation);
                        Console.WriteLine(continuation);

                        PartitionKey partitionKey = new(id);
                        PartitionKey otherPartitionKey = new(otherId);

                        _ = await container.UpsertItemAsync<Item>(item: new(Id: otherId, Line1: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", City: "Bangkok", State: "Thailand", ZipCode: "10330"), partitionKey: otherPartitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "One Microsoft Way", City: "Redmond", State: "WA", ZipCode: "98052"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "205 16th St NW", City: "Atlanta", State: "GA", ZipCode: "30363"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.DeleteItemAsync<Item>(id: id, partitionKey: partitionKey);
                    }
                    else
                    {
                        List<ChangeFeedItemChanges<Item>> resources = feedResponse.Resource.ToList();

                        GetChangeFeedIteratorFullFidelityTests.AssertGatewayMode(feedResponse);

                        Assert.AreEqual(expected: 4, actual: resources.Count);

                        ChangeFeedItemChanges<Item> firstCreateOperation = resources[0];

                        Assert.AreEqual(expected: otherId, actual: firstCreateOperation.Current.Id);
                        Assert.AreEqual(expected: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", actual: firstCreateOperation.Current.Line1);
                        Assert.AreEqual(expected: "Bangkok", actual: firstCreateOperation.Current.City);
                        Assert.AreEqual(expected: "Thailand", actual: firstCreateOperation.Current.State);
                        Assert.AreEqual(expected: "10330", actual: firstCreateOperation.Current.ZipCode);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Create, actual: firstCreateOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: firstCreateOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: firstCreateOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreEqual(expected: default, actual: firstCreateOperation.Metadata.PreviousLogSequenceNumber);
                        Assert.IsNull(firstCreateOperation.Previous);

                        ChangeFeedItemChanges<Item> createOperation = resources[1];

                        Assert.AreEqual(expected: id, actual: createOperation.Current.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: createOperation.Current.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: createOperation.Current.City);
                        Assert.AreEqual(expected: "WA", actual: createOperation.Current.State);
                        Assert.AreEqual(expected: "98052", actual: createOperation.Current.ZipCode);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Create, actual: createOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: createOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: createOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreEqual(expected: default, actual: createOperation.Metadata.PreviousLogSequenceNumber);
                        Assert.IsNull(createOperation.Previous);

                        ChangeFeedItemChanges<Item> replaceOperation = resources[2];

                        Assert.AreEqual(expected: id, actual: replaceOperation.Current.Id);
                        Assert.AreEqual(expected: "205 16th St NW", actual: replaceOperation.Current.Line1);
                        Assert.AreEqual(expected: "Atlanta", actual: replaceOperation.Current.City);
                        Assert.AreEqual(expected: "GA", actual: replaceOperation.Current.State);
                        Assert.AreEqual(expected: "30363", actual: replaceOperation.Current.ZipCode);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Replace, actual: replaceOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.PreviousLogSequenceNumber);
                        Assert.AreEqual(expected: id, actual: replaceOperation.Previous.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: replaceOperation.Previous.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: replaceOperation.Previous.City);
                        Assert.AreEqual(expected: "WA", actual: replaceOperation.Previous.State);
                        Assert.AreEqual(expected: "98052", actual: replaceOperation.Previous.ZipCode);

                        ChangeFeedItemChanges<Item> deleteOperation = resources[3];

                        Assert.IsNull(deleteOperation.Current.Id);
                        Assert.IsNull(deleteOperation.Current.Line1);
                        Assert.IsNull(deleteOperation.Current.City);
                        Assert.IsNull(deleteOperation.Current.State);
                        Assert.IsNull(deleteOperation.Current.ZipCode);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Delete, actual: deleteOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: deleteOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: deleteOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreNotEqual(notExpected: default, actual: deleteOperation.Metadata.PreviousLogSequenceNumber);
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
            ContainerProperties properties = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: GetChangeFeedIteratorFullFidelityTests.PartitionKey);
            properties.ChangeFeedPolicy.FullFidelityRetention = TimeSpan.FromMinutes(5);
            ContainerResponse response = await this.database.CreateContainerAsync(
                properties,
                cancellationToken: this.cancellationToken);
            ContainerInternal container = (ContainerInternal)response;
            string id = Guid.NewGuid().ToString();
            string otherId = Guid.NewGuid().ToString();
            using (FeedIterator<ChangeFeedItemChanges<Item>> feedIterator = container.GetChangeFeedIterator<ChangeFeedItemChanges<Item>>(
                changeFeedStartFrom: ChangeFeedStartFrom.Now(),
                changeFeedMode: ChangeFeedMode.FullFidelity))
            {
                string continuation = null;
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<ChangeFeedItemChanges<Item>> feedResponse = await feedIterator.ReadNextAsync();

                    if (feedResponse.StatusCode == System.Net.HttpStatusCode.NotModified)
                    {
                        continuation = feedResponse.ContinuationToken;
                        Assert.IsNotNull(continuation);
                        Console.WriteLine(continuation);

                        PartitionKey partitionKey = new(id);
                        PartitionKey otherPartitionKey = new(otherId);

                        _ = await container.UpsertItemAsync<Item>(item: new(Id: otherId, Line1: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", City: "Bangkok", State: "Thailand", ZipCode: "10330"), partitionKey: otherPartitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "One Microsoft Way", City: "Redmond", State: "WA", ZipCode: "98052"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "205 16th St NW", City: "Atlanta", State: "GA", ZipCode: "30363"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.DeleteItemAsync<Item>(id: id, partitionKey: partitionKey);
                    }
                    else
                    {
                        List<ChangeFeedItemChanges<Item>> resources = feedResponse.Resource.ToList();

                        GetChangeFeedIteratorFullFidelityTests.AssertGatewayMode(feedResponse);

                        Assert.AreEqual(expected: 4, actual: resources.Count);

                        ChangeFeedItemChanges<Item> firstCreateOperation = resources[0];

                        Assert.AreEqual(expected: otherId, actual: firstCreateOperation.Current.Id);
                        Assert.AreEqual(expected: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", actual: firstCreateOperation.Current.Line1);
                        Assert.AreEqual(expected: "Bangkok", actual: firstCreateOperation.Current.City);
                        Assert.AreEqual(expected: "Thailand", actual: firstCreateOperation.Current.State);
                        Assert.AreEqual(expected: "10330", actual: firstCreateOperation.Current.ZipCode);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Create, actual: firstCreateOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: firstCreateOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: firstCreateOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreEqual(expected: default, actual: firstCreateOperation.Metadata.PreviousLogSequenceNumber);
                        Assert.IsNull(firstCreateOperation.Previous);

                        ChangeFeedItemChanges<Item> createOperation = resources[1];

                        Assert.AreEqual(expected: id, actual: createOperation.Current.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: createOperation.Current.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: createOperation.Current.City);
                        Assert.AreEqual(expected: "WA", actual: createOperation.Current.State);
                        Assert.AreEqual(expected: "98052", actual: createOperation.Current.ZipCode);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Create, actual: createOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: createOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: createOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreEqual(expected: default, actual: createOperation.Metadata.PreviousLogSequenceNumber);
                        Assert.IsNull(createOperation.Previous);

                        ChangeFeedItemChanges<Item> replaceOperation = resources[2];

                        Assert.AreEqual(expected: id, actual: replaceOperation.Current.Id);
                        Assert.AreEqual(expected: "205 16th St NW", actual: replaceOperation.Current.Line1);
                        Assert.AreEqual(expected: "Atlanta", actual: replaceOperation.Current.City);
                        Assert.AreEqual(expected: "GA", actual: replaceOperation.Current.State);
                        Assert.AreEqual(expected: "30363", actual: replaceOperation.Current.ZipCode);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Replace, actual: replaceOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.PreviousLogSequenceNumber);
                        Assert.AreEqual(expected: id, actual: replaceOperation.Previous.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: replaceOperation.Previous.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: replaceOperation.Previous.City);
                        Assert.AreEqual(expected: "WA", actual: replaceOperation.Previous.State);
                        Assert.AreEqual(expected: "98052", actual: replaceOperation.Previous.ZipCode);

                        ChangeFeedItemChanges<Item> deleteOperation = resources[3];

                        Assert.IsNull(deleteOperation.Current.Id);
                        Assert.IsNull(deleteOperation.Current.Line1);
                        Assert.IsNull(deleteOperation.Current.City);
                        Assert.IsNull(deleteOperation.Current.State);
                        Assert.IsNull(deleteOperation.Current.ZipCode);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Delete, actual: deleteOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: deleteOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: deleteOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreNotEqual(notExpected: default, actual: deleteOperation.Metadata.PreviousLogSequenceNumber);
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
            ContainerProperties properties = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: GetChangeFeedIteratorFullFidelityTests.PartitionKey);
            properties.ChangeFeedPolicy.FullFidelityRetention = TimeSpan.FromMinutes(5);
            ContainerResponse response = await this.database.CreateContainerAsync(
                properties,
                cancellationToken: this.cancellationToken);
            ContainerInternal container = (ContainerInternal)response;
            string id = Guid.NewGuid().ToString();
            string otherId = Guid.NewGuid().ToString();
            using (FeedIterator<ChangeFeedItemChanges<Item>> feedIterator = container.GetChangeFeedIterator<ChangeFeedItemChanges<Item>>(
                changeFeedStartFrom: ChangeFeedStartFrom.Now(FeedRangePartitionKeyRange.FromPartitionKey(new PartitionKey(id))),
                changeFeedMode: ChangeFeedMode.FullFidelity))
            {
                string continuation = null;
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<ChangeFeedItemChanges<Item>> feedResponse = await feedIterator.ReadNextAsync();

                    if (feedResponse.StatusCode == System.Net.HttpStatusCode.NotModified)
                    {
                        continuation = feedResponse.ContinuationToken;
                        Assert.IsNotNull(continuation);
                        Console.WriteLine(continuation);

                        PartitionKey partitionKey = new(id);
                        PartitionKey otherPartitionKey = new(otherId);

                        _ = await container.UpsertItemAsync<Item>(item: new(Id: otherId, Line1: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", City: "Bangkok", State: "Thailand", ZipCode: "10330"), partitionKey: otherPartitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "One Microsoft Way", City: "Redmond", State: "WA", ZipCode: "98052"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "205 16th St NW", City: "Atlanta", State: "GA", ZipCode: "30363"), partitionKey: partitionKey).ConfigureAwait(false);
                    }
                    else
                    {
                        List<ChangeFeedItemChanges<Item>> resources = feedResponse.Resource.ToList();

                        GetChangeFeedIteratorFullFidelityTests.AssertGatewayMode(feedResponse);

                        Assert.AreEqual(expected: 2, actual: resources.Count);

                        ChangeFeedItemChanges<Item> createOperation = resources[0];

                        Assert.AreEqual(expected: id, actual: createOperation.Current.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: createOperation.Current.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: createOperation.Current.City);
                        Assert.AreEqual(expected: "WA", actual: createOperation.Current.State);
                        Assert.AreEqual(expected: "98052", actual: createOperation.Current.ZipCode);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Create, actual: createOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: createOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: createOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreEqual(expected: default, actual: createOperation.Metadata.PreviousLogSequenceNumber);
                        Assert.IsNull(createOperation.Previous);

                        ChangeFeedItemChanges<Item> replaceOperation = resources[1];

                        Assert.AreEqual(expected: id, actual: replaceOperation.Current.Id);
                        Assert.AreEqual(expected: "205 16th St NW", actual: replaceOperation.Current.Line1);
                        Assert.AreEqual(expected: "Atlanta", actual: replaceOperation.Current.City);
                        Assert.AreEqual(expected: "GA", actual: replaceOperation.Current.State);
                        Assert.AreEqual(expected: "30363", actual: replaceOperation.Current.ZipCode);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Replace, actual: replaceOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.PreviousLogSequenceNumber);
                        Assert.AreEqual(expected: id, actual: replaceOperation.Previous.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: replaceOperation.Previous.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: replaceOperation.Previous.City);
                        Assert.AreEqual(expected: "WA", actual: replaceOperation.Previous.State);
                        Assert.AreEqual(expected: "98052", actual: replaceOperation.Previous.ZipCode);

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
            ContainerProperties properties = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: GetChangeFeedIteratorFullFidelityTests.PartitionKey);
            properties.ChangeFeedPolicy.FullFidelityRetention = TimeSpan.FromMinutes(5);
            ContainerResponse response = await this.database.CreateContainerAsync(
                properties,
                cancellationToken: this.cancellationToken);
            ContainerInternal container = (ContainerInternal)response;
            string id = Guid.NewGuid().ToString();
            string otherId = Guid.NewGuid().ToString();
            using (FeedIterator<ChangeFeedItemChanges<Item>> feedIterator = container.GetChangeFeedIterator<ChangeFeedItemChanges<Item>>(
                changeFeedStartFrom: ChangeFeedStartFrom.Now(FeedRange.FromPartitionKey(new PartitionKey(id))),
                changeFeedMode: ChangeFeedMode.FullFidelity))
            {
                string continuation = null;
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<ChangeFeedItemChanges<Item>> feedResponse = await feedIterator.ReadNextAsync();

                    if (feedResponse.StatusCode == System.Net.HttpStatusCode.NotModified)
                    {
                        continuation = feedResponse.ContinuationToken;
                        Assert.IsNotNull(continuation);
                        Console.WriteLine(continuation);

                        PartitionKey partitionKey = new(id);
                        PartitionKey otherPartitionKey = new(otherId);

                        _ = await container.UpsertItemAsync<Item>(item: new(Id: otherId, Line1: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", City: "Bangkok", State: "Thailand", ZipCode: "10330"), partitionKey: otherPartitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "One Microsoft Way", City: "Redmond", State: "WA", ZipCode: "98052"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "205 16th St NW", City: "Atlanta", State: "GA", ZipCode: "30363"), partitionKey: partitionKey).ConfigureAwait(false);
                    }
                    else
                    {
                        List<ChangeFeedItemChanges<Item>> itemChanges = feedResponse.Resource.ToList();

                        GetChangeFeedIteratorFullFidelityTests.AssertGatewayMode(feedResponse);

                        Assert.AreEqual(expected: 2, actual: itemChanges.Count);

                        ChangeFeedItemChanges<Item> createOperation = itemChanges[0];

                        Assert.AreEqual(expected: id, actual: createOperation.Current.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: createOperation.Current.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: createOperation.Current.City);
                        Assert.AreEqual(expected: "WA", actual: createOperation.Current.State);
                        Assert.AreEqual(expected: "98052", actual: createOperation.Current.ZipCode);
                        Assert.IsNotNull(createOperation.Metadata);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Create, actual: createOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: createOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: createOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreEqual(expected: default, actual: createOperation.Metadata.PreviousLogSequenceNumber);
                        Assert.IsNull(createOperation.Previous);

                        ChangeFeedItemChanges<Item> replaceOperation = itemChanges[1];

                        Assert.AreEqual(expected: id, actual: replaceOperation.Current.Id);
                        Assert.AreEqual(expected: "205 16th St NW", actual: replaceOperation.Current.Line1);
                        Assert.AreEqual(expected: "Atlanta", actual: replaceOperation.Current.City);
                        Assert.AreEqual(expected: "GA", actual: replaceOperation.Current.State);
                        Assert.AreEqual(expected: "30363", actual: replaceOperation.Current.ZipCode);
                        Assert.IsNotNull(createOperation.Metadata);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Replace, actual: replaceOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.PreviousLogSequenceNumber);
                        Assert.IsNotNull(replaceOperation.Previous);
                        Assert.AreEqual(expected: id, actual: replaceOperation.Previous.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: replaceOperation.Previous.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: replaceOperation.Previous.City);
                        Assert.AreEqual(expected: "WA", actual: replaceOperation.Previous.State);
                        Assert.AreEqual(expected: "98052", actual: replaceOperation.Previous.ZipCode);

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
            ContainerProperties properties = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: GetChangeFeedIteratorFullFidelityTests.PartitionKey);
            properties.ChangeFeedPolicy.FullFidelityRetention = TimeSpan.FromMinutes(5);
            ContainerResponse response = await this.database.CreateContainerAsync(
                properties,
                cancellationToken: this.cancellationToken);
            ContainerInternal container = (ContainerInternal)response;
            IReadOnlyList<FeedRange> ranges = await container.GetFeedRangesAsync();
            string id = Guid.NewGuid().ToString();
            string otherId = Guid.NewGuid().ToString();
            
            using (FeedIterator<ChangeFeedItemChanges<Item>> feedIterator = container.GetChangeFeedIterator<ChangeFeedItemChanges<Item>>(
                changeFeedStartFrom: ChangeFeedStartFrom.Now(ranges[0]),
                changeFeedMode: ChangeFeedMode.FullFidelity))
            {
                string continuation = null;
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<ChangeFeedItemChanges<Item>> feedResponse = await feedIterator.ReadNextAsync();

                    if (feedResponse.StatusCode == System.Net.HttpStatusCode.NotModified)
                    {
                        continuation = feedResponse.ContinuationToken;
                        Assert.IsNotNull(continuation);
                        Console.WriteLine(continuation);

                        PartitionKey partitionKey = new(id);
                        PartitionKey otherPartitionKey = new(otherId);

                        _ = await container.UpsertItemAsync<Item>(item: new(Id: otherId, Line1: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", City: "Bangkok", State: "Thailand", ZipCode: "10330"), partitionKey: otherPartitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "One Microsoft Way", City: "Redmond", State: "WA", ZipCode: "98052"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "205 16th St NW", City: "Atlanta", State: "GA", ZipCode: "30363"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.DeleteItemAsync<Item>(id: id, partitionKey: partitionKey);
                    }
                    else
                    {
                        List<ChangeFeedItemChanges<Item>> resources = feedResponse.Resource.ToList();

                        GetChangeFeedIteratorFullFidelityTests.AssertGatewayMode(feedResponse);

                        Assert.AreEqual(expected: 4, actual: resources.Count);

                        ChangeFeedItemChanges<Item> firstCreateOperation = resources[0];

                        Assert.AreEqual(expected: otherId, actual: firstCreateOperation.Current.Id);
                        Assert.AreEqual(expected: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", actual: firstCreateOperation.Current.Line1);
                        Assert.AreEqual(expected: "Bangkok", actual: firstCreateOperation.Current.City);
                        Assert.AreEqual(expected: "Thailand", actual: firstCreateOperation.Current.State);
                        Assert.AreEqual(expected: "10330", actual: firstCreateOperation.Current.ZipCode);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Create, actual: firstCreateOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: firstCreateOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: firstCreateOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreEqual(expected: default, actual: firstCreateOperation.Metadata.PreviousLogSequenceNumber);
                        Assert.IsNull(firstCreateOperation.Previous);

                        ChangeFeedItemChanges<Item> createOperation = resources[1];

                        Assert.AreEqual(expected: id, actual: createOperation.Current.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: createOperation.Current.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: createOperation.Current.City);
                        Assert.AreEqual(expected: "WA", actual: createOperation.Current.State);
                        Assert.AreEqual(expected: "98052", actual: createOperation.Current.ZipCode);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Create, actual: createOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: createOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: createOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreEqual(expected: default, actual: createOperation.Metadata.PreviousLogSequenceNumber);
                        Assert.IsNull(createOperation.Previous);

                        ChangeFeedItemChanges<Item> replaceOperation = resources[2];

                        Assert.AreEqual(expected: id, actual: replaceOperation.Current.Id);
                        Assert.AreEqual(expected: "205 16th St NW", actual: replaceOperation.Current.Line1);
                        Assert.AreEqual(expected: "Atlanta", actual: replaceOperation.Current.City);
                        Assert.AreEqual(expected: "GA", actual: replaceOperation.Current.State);
                        Assert.AreEqual(expected: "30363", actual: replaceOperation.Current.ZipCode);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Replace, actual: replaceOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreNotEqual(notExpected: default, actual: replaceOperation.Metadata.PreviousLogSequenceNumber);
                        Assert.AreEqual(expected: id, actual: replaceOperation.Previous.Id);
                        Assert.AreEqual(expected: "One Microsoft Way", actual: replaceOperation.Previous.Line1);
                        Assert.AreEqual(expected: "Redmond", actual: replaceOperation.Previous.City);
                        Assert.AreEqual(expected: "WA", actual: replaceOperation.Previous.State);
                        Assert.AreEqual(expected: "98052", actual: replaceOperation.Previous.ZipCode);

                        ChangeFeedItemChanges<Item> deleteOperation = resources[3];

                        Assert.IsNull(deleteOperation.Current.Id);
                        Assert.IsNull(deleteOperation.Current.Line1);
                        Assert.IsNull(deleteOperation.Current.City);
                        Assert.IsNull(deleteOperation.Current.State);
                        Assert.IsNull(deleteOperation.Current.ZipCode);
                        Assert.AreEqual(expected: ChangeFeedOperationType.Delete, actual: deleteOperation.Metadata.OperationType);
                        Assert.AreNotEqual(notExpected: default, actual: deleteOperation.Metadata.ConflictResolutionTimestamp);
                        Assert.AreNotEqual(notExpected: default, actual: deleteOperation.Metadata.CurrentLogSequenceNumber);
                        Assert.AreNotEqual(notExpected: default, actual: deleteOperation.Metadata.PreviousLogSequenceNumber);
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

        private static void AssertGatewayMode(FeedResponse<ChangeFeedItemChanges<Item>> feedResponse)
        {
            string diagnostics = feedResponse.Diagnostics.ToString();
            JToken jToken = JToken.Parse(diagnostics);

            Assert.IsNotNull(jToken["Summary"]["GatewayCalls"], "'GatewayCalls' is not found in diagnostics. UseGateMode is set to false.");
        }
    }

    public record Item(
        [property: JsonProperty("id")] string Id,
        [property: JsonProperty("line1")] string Line1,
        [property: JsonProperty("city")] string City,
        [property: JsonProperty("zipCode")] string ZipCode,
        [property: JsonProperty("state")] string State);

    public class TestItemSerializer : CosmosSerializer
    {
        public override T FromStream<T>(Stream stream)
        {
            using (StreamReader reader = new StreamReader(stream))
            {
                string jsonString = reader.ReadToEnd();

#if DEBUG
                StackTrace stackTrace = new StackTrace(new StackFrame(true));
                Console.WriteLine($"type => {typeof(T)} => {Environment.NewLine}{stackTrace}");
#endif
                T item = JsonConvert.DeserializeObject<T>(jsonString);

                return item;
            }
        }

        public override Stream ToStream<T>(T input)
        {
            string jsonString = JsonConvert.SerializeObject(input);
            Stream stream = new MemoryStream();

            using (StreamWriter writer = new StreamWriter(
                stream: stream,
                leaveOpen: true))
            {
#if DEBUG
                StackTrace stackTrace = new StackTrace(new StackFrame(true));
                Console.WriteLine($"type => {typeof(T)} => {Environment.NewLine}{stackTrace}");
#endif
                writer.Write(jsonString);
                writer.Flush();
            }

            stream.Position = 0;

            return stream;
        }
    }
}
