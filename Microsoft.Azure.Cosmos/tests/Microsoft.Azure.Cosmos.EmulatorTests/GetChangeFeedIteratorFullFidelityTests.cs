namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Notes:
    /// 
    /// 1. Previous on Deletes
    ///     a. For local emulator (the one we ship to customers to play locally), to change config you can use as you already realized,
    ///         without restarting the emulator: /overrides=enablePreviousImageForReplaceInFFCF:true;
    ///     b. For DocumentDB.Emulator, type this in command window: putproperty fabric:/WinFabric/localhost enablePreviousImageForReplaceInFFCF
    ///         true forcedatastring widecharstring. o You can also use Import-module.\DocumentDBPSModule.psd1 then Connect-WindowsFabricCluster
    ///         then .\SetNamingConfiguration.ps1 -Federation -ConfigurationName enablePreviousImageForReplaceInFFCF -ConfigurationValue true
    ///
    /// 2. TimeToLiveExpired
    ///     a. For TTL expired docs please check that ExpiredResourcePurger is running. I don’t see it being disabled in local emulator. 
    ///         You would need to enable traces for local emulator: after you start local emulator, run its .exe again with /StartTraces,
    ///         after some time run again with /StopTraces, if you get an error, use /startwprtraces and /stopwprtraces. You should see 
    ///         .ETL file(s) created in emulator dir. You can open these with svcperf (you would need to point it to Server.man manifest 
    ///         – use recent one from enlistment and use MANIFEST->ADD ETW MANIFEST). Search for ExpiredResourcePurger in ETL traces. 
    ///         Let us know if that doesn’t work for you or you still have the issue with not seeing TTL-triggered deletes in FFCF.
    ///         
    /// 3. Deletes only appear for FullRange change feeds.
    ///     a. Backend team is addressing this issue.
    /// </summary>
        [TestClass]
    [TestCategory("ChangeFeed")]
    public class GetChangeFeedIteratorFullFidelityTests : BaseCosmosClientHelper
    {
        private static readonly string PartitionKey = "/id";

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        private async Task<ContainerInternal> InitializeLargeContainerAsync()
        {
            ContainerProperties containerProperties = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: GetChangeFeedIteratorFullFidelityTests.PartitionKey)
            {
                DefaultTimeToLive = 1
            };

            ContainerResponse response = await this.database.CreateContainerAsync(
                containerProperties,
                throughput: 20000,
                cancellationToken: this.cancellationToken);

            return (ContainerInternal)response;
        }

        private async Task<ContainerInternal> InitializeContainerAsync(TimeSpan timeToLive)
        {
            ContainerProperties containerProperties = new(id: Guid.NewGuid().ToString(), partitionKeyPath: GetChangeFeedIteratorFullFidelityTests.PartitionKey)
            {
                DefaultTimeToLive = Convert.ToInt32(timeToLive.TotalSeconds),
            };

            containerProperties.ChangeFeedPolicy.FullFidelityRetention = TimeSpan.FromMinutes(5);

            ContainerResponse response = await this.database.CreateContainerAsync(
                containerProperties,
                cancellationToken: this.cancellationToken);

            return (ContainerInternal)response;
        }

        /// <summary>
        /// This test will execute <see cref="Container.GetChangeFeedIterator{T}(ChangeFeedStartFrom, ChangeFeedMode, ChangeFeedRequestOptions)"/> 
        ///     in <see cref="ChangeFeedMode.FullFidelity"/> with a typed item.
        /// Using FeedRangeEpk.FromPartitionKey.
        /// </summary>
        [TestMethod]
        public async Task FeedRangeEpk_FromPartitionKey_VerifyingWireFormatTests()
        {
            TimeSpan ttl = TimeSpan.FromSeconds(1);
            TimeSpan waitAfterOperations = ttl.Add(TimeSpan.FromSeconds(5));

            ContainerInternal container = await this.InitializeContainerAsync(ttl);
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

                        PartitionKey otherPartitionKey = new(otherId);
                        PartitionKey partitionKey = new(id);

                        _ = await container.UpsertItemAsync<Item>(item: new(Id: otherId, Line1: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", City: "Bangkok", State: "Thailand", ZipCode: "10330"), partitionKey: otherPartitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "One Microsoft Way", City: "Redmond", State: "WA", ZipCode: "98052"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "205 16th St NW", City: "Atlanta", State: "GA", ZipCode: "30363"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.DeleteItemAsync<Item>(id: id, partitionKey: partitionKey);

                        Thread.Sleep(waitAfterOperations);
                    }
                    else
                    {
#if DEBUG
                        Console.WriteLine(JsonConvert.SerializeObject(feedResponse.Resource));
#endif
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
                        Assert.IsFalse(createOperation.Metadata.TimeToLiveExpired);

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
                        Assert.IsFalse(replaceOperation.Metadata.TimeToLiveExpired);

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
            TimeSpan ttl = TimeSpan.FromSeconds(1);
            TimeSpan waitAfterOperations = ttl.Add(TimeSpan.FromSeconds(5));

            ContainerInternal container = await this.InitializeContainerAsync(ttl);
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

                        PartitionKey partitionKey = new(id);
                        PartitionKey otherPartitionKey = new(otherId);

                        _ = await container.UpsertItemAsync<Item>(item: new(Id: otherId, Line1: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", City: "Bangkok", State: "Thailand", ZipCode: "10330"), partitionKey: otherPartitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "One Microsoft Way", City: "Redmond", State: "WA", ZipCode: "98052"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "205 16th St NW", City: "Atlanta", State: "GA", ZipCode: "30363"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.DeleteItemAsync<Item>(id: id, partitionKey: partitionKey);

                        Thread.Sleep(waitAfterOperations);
                    }
                    else
                    {
#if DEBUG
                        Console.WriteLine(JsonConvert.SerializeObject(feedResponse.Resource));
#endif
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
                        Assert.IsFalse(firstCreateOperation.Metadata.TimeToLiveExpired);

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
                        Assert.IsFalse(createOperation.Metadata.TimeToLiveExpired);

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
                        Assert.IsFalse(replaceOperation.Metadata.TimeToLiveExpired);

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
                        Assert.IsFalse(deleteOperation.Metadata.TimeToLiveExpired);
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
            TimeSpan ttl = TimeSpan.FromSeconds(1);
            TimeSpan waitAfterOperations = ttl.Add(TimeSpan.FromSeconds(5));

            ContainerInternal container = await this.InitializeContainerAsync(ttl);
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

                        PartitionKey partitionKey = new(id);
                        PartitionKey otherPartitionKey = new(otherId);

                        _ = await container.UpsertItemAsync<Item>(item: new(Id: otherId, Line1: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", City: "Bangkok", State: "Thailand", ZipCode: "10330"), partitionKey: otherPartitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "One Microsoft Way", City: "Redmond", State: "WA", ZipCode: "98052"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "205 16th St NW", City: "Atlanta", State: "GA", ZipCode: "30363"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.DeleteItemAsync<Item>(id: id, partitionKey: partitionKey);

                        Thread.Sleep(waitAfterOperations);
                    }
                    else
                    {
#if DEBUG
                        Console.WriteLine(JsonConvert.SerializeObject(feedResponse.Resource));
#endif
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
                        Assert.IsFalse(firstCreateOperation.Metadata.TimeToLiveExpired);

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
                        Assert.IsFalse(createOperation.Metadata.TimeToLiveExpired);

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
                        Assert.IsFalse(replaceOperation.Metadata.TimeToLiveExpired);

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
                        Assert.IsFalse(deleteOperation.Metadata.TimeToLiveExpired);
                        Assert.IsNotNull(deleteOperation.Previous);
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
            TimeSpan ttl = TimeSpan.FromSeconds(1);
            TimeSpan waitAfterOperations = ttl.Add(TimeSpan.FromSeconds(5));

            ContainerInternal container = await this.InitializeContainerAsync(ttl);
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

                        PartitionKey partitionKey = new(id);
                        PartitionKey otherPartitionKey = new(otherId);

                        _ = await container.UpsertItemAsync<Item>(item: new(Id: otherId, Line1: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", City: "Bangkok", State: "Thailand", ZipCode: "10330"), partitionKey: otherPartitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "One Microsoft Way", City: "Redmond", State: "WA", ZipCode: "98052"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "205 16th St NW", City: "Atlanta", State: "GA", ZipCode: "30363"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.DeleteItemAsync<Item>(id: id, partitionKey: partitionKey);

                        Thread.Sleep(waitAfterOperations);
                    }
                    else
                    {
#if DEBUG
                        Console.WriteLine(JsonConvert.SerializeObject(feedResponse.Resource));
#endif
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
                        Assert.IsFalse(createOperation.Metadata.TimeToLiveExpired);

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
                        Assert.IsFalse(replaceOperation.Metadata.TimeToLiveExpired);

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
            TimeSpan ttl = TimeSpan.FromSeconds(1);
            TimeSpan waitAfterOperations = ttl.Add(TimeSpan.FromSeconds(5));

            ContainerInternal container = await this.InitializeContainerAsync(ttl);
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

                        PartitionKey partitionKey = new(id);
                        PartitionKey otherPartitionKey = new(otherId);

                        _ = await container.UpsertItemAsync<Item>(item: new(Id: otherId, Line1: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", City: "Bangkok", State: "Thailand", ZipCode: "10330"), partitionKey: otherPartitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "One Microsoft Way", City: "Redmond", State: "WA", ZipCode: "98052"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "205 16th St NW", City: "Atlanta", State: "GA", ZipCode: "30363"), partitionKey: partitionKey).ConfigureAwait(false);

                        Thread.Sleep(waitAfterOperations);
                    }
                    else
                    {
#if DEBUG
                        Console.WriteLine(JsonConvert.SerializeObject(feedResponse.Resource));
#endif
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
                        Assert.IsFalse(createOperation.Metadata.TimeToLiveExpired);

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
                        Assert.IsFalse(replaceOperation.Metadata.TimeToLiveExpired);

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
            TimeSpan ttl = TimeSpan.FromSeconds(1);
            TimeSpan waitAfterOperations = ttl.Add(TimeSpan.FromSeconds(5));

            ContainerInternal container = await this.InitializeContainerAsync(ttl);
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

                        PartitionKey partitionKey = new(id);
                        PartitionKey otherPartitionKey = new(otherId);

                        _ = await container.UpsertItemAsync<Item>(item: new(Id: otherId, Line1: "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", City: "Bangkok", State: "Thailand", ZipCode: "10330"), partitionKey: otherPartitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "One Microsoft Way", City: "Redmond", State: "WA", ZipCode: "98052"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<Item>(item: new(Id: id, Line1: "205 16th St NW", City: "Atlanta", State: "GA", ZipCode: "30363"), partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.DeleteItemAsync<Item>(id: id, partitionKey: partitionKey);

                        Thread.Sleep(waitAfterOperations);
                    }
                    else
                    {
#if DEBUG
                        Console.WriteLine(JsonConvert.SerializeObject(feedResponse.Resource));
#endif
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
                        Assert.IsFalse(firstCreateOperation.Metadata.TimeToLiveExpired);

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
                        Assert.IsFalse(createOperation.Metadata.TimeToLiveExpired);

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
                        Assert.IsFalse(replaceOperation.Metadata.TimeToLiveExpired);

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
                        Assert.IsNotNull(deleteOperation.Previous);
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
        public async Task FeedRange_FromPartitionKey_Dynamic_VerifyingWireFormatTests()
        {
            TimeSpan ttl = TimeSpan.FromSeconds(1);
            TimeSpan waitAfterOperations = ttl.Add(TimeSpan.FromSeconds(5));

            ContainerInternal container = await this.InitializeContainerAsync(ttl);
            string id = Guid.NewGuid().ToString();
            string otherId = Guid.NewGuid().ToString();
            using (FeedIterator<dynamic> feedIterator = container.GetChangeFeedIterator<dynamic>(
                changeFeedStartFrom: ChangeFeedStartFrom.Now(FeedRange.FromPartitionKey(new PartitionKey(id))),
                changeFeedMode: ChangeFeedMode.FullFidelity))
            {
                string continuation = null;
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<dynamic> feedResponse = await feedIterator.ReadNextAsync();

                    if (feedResponse.StatusCode == System.Net.HttpStatusCode.NotModified)
                    {
                        continuation = feedResponse.ContinuationToken;
                        Assert.IsNotNull(continuation);

                        PartitionKey partitionKey = new(id);
                        PartitionKey otherPartitionKey = new(otherId);

                        _ = await container.UpsertItemAsync<dynamic>(item: new { id = otherId, line1 = "87 38floor, Witthayu Rd, Lumphini, Pathum Wan District", city = "Bangkok", state = "Thailand", zipCode = "10330" }, partitionKey: otherPartitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<dynamic>(item: new { id, line1 = "One Microsoft Way", city = "Redmond", state = "WA", zipCode = "98052" }, partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.UpsertItemAsync<dynamic>(item: new { id, line1 = "205 16th St NW", city = "Atlanta", state = "GA", zipCode = "30363" }, partitionKey: partitionKey).ConfigureAwait(false);
                        _ = await container.DeleteItemAsync<Item>(id: id, partitionKey: partitionKey);

                        Thread.Sleep(waitAfterOperations);
                    }
                    else
                    {
#if DEBUG
                        Console.WriteLine(JsonConvert.SerializeObject(feedResponse.Resource));
#endif
                        List<ChangeFeedItemChanges<Item>> itemChanges = JsonConvert.DeserializeObject<List<ChangeFeedItemChanges<Item>>>(
                            JsonConvert.SerializeObject(feedResponse.Resource));

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
                        Assert.IsFalse(createOperation.Metadata.TimeToLiveExpired);

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
                        Assert.IsFalse(replaceOperation.Metadata.TimeToLiveExpired);

                        break;
                    }
                }
            }
        }

        private static void AssertGatewayMode<T>(FeedResponse<T> feedResponse)
        {
            string diagnostics = feedResponse.Diagnostics.ToString();
            JToken jsonToken = JToken.Parse(diagnostics);

            Assert.IsNotNull(jsonToken["Summary"]["GatewayCalls"], "'GatewayCalls' is not found in diagnostics. UseGateMode is set to false.");
        }
    }

    public record Item(
        [property: JsonProperty("id")] string Id,
        [property: JsonProperty("line1")] string Line1,
        [property: JsonProperty("city")] string City,
        [property: JsonProperty("zipCode")] string ZipCode,
        [property: JsonProperty("state")] string State);
}
