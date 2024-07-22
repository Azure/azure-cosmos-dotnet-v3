//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;
    using Microsoft.Azure.Cosmos.Services.Management.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    [TestCategory("ChangeFeedProcessor")]
    public class GetChangeFeedProcessorBuilderWithAllVersionsAndDeletesTests : BaseChangeFeedClientHelper
    {
        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.ChangeFeedTestInit();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        [Timeout(300000)]
        [TestCategory("LongRunning")]
        [Owner("philipthomas-MSFT")]
        [Description("Scenario: When a document is created with ttl set, there should be 1 create and 1 delete that will appear for that " +
            "document when using ChangeFeedProcessor with AllVersionsAndDeletes set as the ChangeFeedMode.")]
        public async Task WhenADocumentIsCreatedWithTtlSetThenTheDocumentIsDeletedTestsAsync()
        {
            (ContainerInternal monitoredContainer, ContainerResponse containerResponse) = await this.CreateMonitoredContainer(ChangeFeedMode.AllVersionsAndDeletes);
            Exception exception = default;
            int ttlInSeconds = 5;
            Stopwatch stopwatch = new();
            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);

            ChangeFeedProcessor processor = monitoredContainer
                .GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes(processorName: "processor", onChangesDelegate: (ChangeFeedProcessorContext context, IReadOnlyCollection<ChangeFeedItem<dynamic>> docs, CancellationToken token) =>
                {
                    // NOTE(philipthomas-MSFT): Please allow these Logger.LogLine because TTL on items will purge at random times so I am using this to test when ran locally using emulator.

                    Logger.LogLine($"@ {DateTime.Now}, {nameof(stopwatch)} -> CFP AVAD took '{stopwatch.ElapsedMilliseconds}' to read document CRUD in feed.");
                    Logger.LogLine($"@ {DateTime.Now}, {nameof(docs)} -> {JsonConvert.SerializeObject(docs)}");

                    foreach (ChangeFeedItem<dynamic> change in docs)
                    {
                        if (change.Metadata.OperationType == ChangeFeedOperationType.Create)
                        {
                            // current
                            Assert.AreEqual(expected: "1", actual: change.Current.id.ToString());
                            Assert.AreEqual(expected: "1", actual: change.Current.pk.ToString());
                            Assert.AreEqual(expected: "Testing TTL on CFP.", actual: change.Current.description.ToString());
                            Assert.AreEqual(expected: ttlInSeconds, actual: change.Current.ttl.ToObject<int>());

                            // metadata
                            Assert.IsTrue(DateTime.TryParse(s: change.Metadata.ConflictResolutionTimestamp.ToString(), out _), message: "Invalid csrt must be a datetime value.");
                            Assert.IsTrue(change.Metadata.Lsn > 0, message: "Invalid lsn must be a long value.");
                            Assert.IsFalse(change.Metadata.IsTimeToLiveExpired);

                            // previous
                            Assert.IsNull(change.Previous);
                        }
                        else if (change.Metadata.OperationType == ChangeFeedOperationType.Delete)
                        {
                            // current
                            Assert.IsNull(change.Current.id);

                            // metadata
                            Assert.IsTrue(DateTime.TryParse(s: change.Metadata.ConflictResolutionTimestamp.ToString(), out _), message: "Invalid csrt must be a datetime value.");
                            Assert.IsTrue(change.Metadata.Lsn > 0, message: "Invalid lsn must be a long value.");
                            Assert.IsTrue(change.Metadata.IsTimeToLiveExpired);

                            // previous
                            Assert.AreEqual(expected: "1", actual: change.Previous.id.ToString());
                            Assert.AreEqual(expected: "1", actual: change.Previous.pk.ToString());
                            Assert.AreEqual(expected: "Testing TTL on CFP.", actual: change.Previous.description.ToString());
                            Assert.AreEqual(expected: ttlInSeconds, actual: change.Previous.ttl.ToObject<int>());

                            // stop after reading delete since it is the last document in feed.
                            stopwatch.Stop();
                            allDocsProcessed.Set();
                        }
                        else
                        {
                            Assert.Fail("Invalid operation.");
                        }
                    }

                    return Task.CompletedTask;
                })
                .WithInstanceName(Guid.NewGuid().ToString())
                .WithLeaseContainer(this.LeaseContainer)
                .WithErrorNotification((leaseToken, error) =>
                {
                    exception = error.InnerException;

                    return Task.CompletedTask;
                })
                .Build();

            stopwatch.Start();

            // NOTE(philipthomas-MSFT): Please allow these Logger.LogLine because TTL on items will purge at random times so I am using this to test when ran locally using emulator.

            Logger.LogLine($"@ {DateTime.Now}, CFProcessor starting...");

            await processor.StartAsync();

            try
            {
                await Task.Delay(GetChangeFeedProcessorBuilderWithAllVersionsAndDeletesTests.ChangeFeedSetupTime);
                await monitoredContainer.CreateItemAsync<dynamic>(new { id = "1", pk = "1", description = "Testing TTL on CFP.", ttl = ttlInSeconds }, partitionKey: new PartitionKey("1"));

                // NOTE(philipthomas-MSFT): Please allow these Logger.LogLine because TTL on items will purge at random times so I am using this to test when ran locally using emulator.

                Logger.LogLine($"@ {DateTime.Now}, Document created.");

                bool receivedDelete = allDocsProcessed.WaitOne(250000);
                Assert.IsTrue(receivedDelete, "Timed out waiting for docs to process");

                if (exception != default)
                {
                    Assert.Fail(exception.ToString());
                }
            }
            finally
            {
                await processor.StopAsync();
            }
        }

        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [Description("Scenario: When a document is created, then updated, and finally deleted, there should be 3 changes that will appear for that " +
            "document when using ChangeFeedProcessor with AllVersionsAndDeletes set as the ChangeFeedMode.")]
        public async Task WhenADocumentIsCreatedThenUpdatedThenDeletedTestsAsync()
        {
            (ContainerInternal monitoredContainer, ContainerResponse containerResponse) = await this.CreateMonitoredContainer(ChangeFeedMode.AllVersionsAndDeletes);
            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);
            Exception exception = default;

            ChangeFeedProcessor processor = monitoredContainer
                .GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes(processorName: "processor", onChangesDelegate: (ChangeFeedProcessorContext context, IReadOnlyCollection<ChangeFeedItem<dynamic>> docs, CancellationToken token) =>
                {
                    Assert.IsNotNull(context.LeaseToken);
                    Assert.IsNotNull(context.Diagnostics);
                    Assert.IsNotNull(context.Headers);
                    Assert.IsNotNull(context.Headers.Session);
                    Assert.IsTrue(context.Headers.RequestCharge > 0);
                    Assert.IsTrue(context.Diagnostics.ToString().Contains("Change Feed Processor Read Next Async"));
                    Assert.AreEqual(expected: 3, actual: docs.Count);

                    ChangeFeedItem<dynamic> createChange = docs.ElementAt(0);
                    Assert.IsNotNull(createChange.Current);
                    Assert.AreEqual(expected: "1", actual: createChange.Current.id.ToString());
                    Assert.AreEqual(expected: "1", actual: createChange.Current.pk.ToString());
                    Assert.AreEqual(expected: "original test", actual: createChange.Current.description.ToString());
                    Assert.AreEqual(expected: createChange.Metadata.OperationType, actual: ChangeFeedOperationType.Create);
                    Assert.AreEqual(expected: createChange.Metadata.PreviousLsn, actual: 0);
                    Assert.IsNull(createChange.Previous);

                    ChangeFeedItem<dynamic> replaceChange = docs.ElementAt(1);
                    Assert.IsNotNull(replaceChange.Current);
                    Assert.AreEqual(expected: "1", actual: replaceChange.Current.id.ToString());
                    Assert.AreEqual(expected: "1", actual: replaceChange.Current.pk.ToString());
                    Assert.AreEqual(expected: "test after replace", actual: replaceChange.Current.description.ToString());
                    Assert.AreEqual(expected: replaceChange.Metadata.OperationType, actual: ChangeFeedOperationType.Replace);
                    Assert.AreEqual(expected: createChange.Metadata.Lsn, actual: replaceChange.Metadata.PreviousLsn);
                    Assert.IsNull(replaceChange.Previous);

                    ChangeFeedItem<dynamic> deleteChange = docs.ElementAt(2);
                    Assert.IsNull(deleteChange.Current.id);
                    Assert.AreEqual(expected: deleteChange.Metadata.OperationType, actual: ChangeFeedOperationType.Delete);
                    Assert.AreEqual(expected: replaceChange.Metadata.Lsn, actual: deleteChange.Metadata.PreviousLsn);
                    Assert.IsNotNull(deleteChange.Previous);
                    Assert.AreEqual(expected: "1", actual: deleteChange.Previous.id.ToString());
                    Assert.AreEqual(expected: "1", actual: deleteChange.Previous.pk.ToString());
                    Assert.AreEqual(expected: "test after replace", actual: deleteChange.Previous.description.ToString());

                    Assert.IsTrue(condition: createChange.Metadata.ConflictResolutionTimestamp < replaceChange.Metadata.ConflictResolutionTimestamp, message: "The create operation must happen before the replace operation.");
                    Assert.IsTrue(condition: replaceChange.Metadata.ConflictResolutionTimestamp < deleteChange.Metadata.ConflictResolutionTimestamp, message: "The replace operation must happen before the delete operation.");
                    Assert.IsTrue(condition: createChange.Metadata.Lsn < replaceChange.Metadata.Lsn, message: "The create operation must happen before the replace operation.");
                    Assert.IsTrue(condition: createChange.Metadata.Lsn < replaceChange.Metadata.Lsn, message: "The replace operation must happen before the delete operation.");

                    return Task.CompletedTask;
                })
                .WithInstanceName(Guid.NewGuid().ToString())
                .WithLeaseContainer(this.LeaseContainer)
                .WithErrorNotification((leaseToken, error) =>
                {
                    exception = error.InnerException;

                    return Task.CompletedTask;
                })
                .Build();

            // Start the processor, insert 1 document to generate a checkpoint, modify it, and then delete it.
            // 1 second delay between operations to get different timestamps.

            await processor.StartAsync();
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);

            await monitoredContainer.CreateItemAsync<dynamic>(new { id = "1", pk = "1", description = "original test" }, partitionKey: new PartitionKey("1"));
            await Task.Delay(1000);

            await monitoredContainer.UpsertItemAsync<dynamic>(new { id = "1", pk = "1", description = "test after replace" }, partitionKey: new PartitionKey("1"));
            await Task.Delay(1000);

            await monitoredContainer.DeleteItemAsync<dynamic>(id: "1", partitionKey: new PartitionKey("1"));

            bool isStartOk = allDocsProcessed.WaitOne(10 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);

            await processor.StopAsync();

            if (exception != default)
            {
                Assert.Fail(exception.ToString());
            }
        }

        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [Description("Scenario: When a document is created, then updated, there should be 2 changes that will appear for that " +
            "document when using ChangeFeedProcessor with AllVersionsAndDeletes set as the ChangeFeedMode. The context header also now" +
            "has the FeedRange included. This is simulating a customer scenario using FindOverlappingRanges.")]
        public async Task WhenADocumentIsCreatedThenUpdatedHeaderHasFeedRangeTestsAsync()
        {
            (ContainerInternal monitoredContainer, ContainerResponse containerResponse) = await this.CreateMonitoredContainer(ChangeFeedMode.AllVersionsAndDeletes);
            ManualResetEvent allDocsProcessed = new (false);
            Exception exception = default;
            PartitionKey partitionKey = new PartitionKey("1");

            ChangeFeedProcessor processor = monitoredContainer
                .GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes(processorName: "processor", onChangesDelegate: async (ChangeFeedProcessorContext context, IReadOnlyCollection<ChangeFeedItem<dynamic>> docs, CancellationToken token) =>
                {
                    foreach (ChangeFeedItem<dynamic> change in docs)
                    {
                        FeedRange feedRange = context.FeedRange; // FeedRange
                        _ = long.TryParse(context.Headers.ContinuationToken.Trim('"'), out long lsnOfChange); // LSN

                        // Bookmarks would otherwise normaly be read from the customer's changed items, but I am generating this for test purposes.
                        List<(FeedRange range, long lsn)> bookmarks = await GetChangeFeedProcessorBuilderWithAllVersionsAndDeletesTests
                            .CreateTestBookmarksAsync(
                                cosmosClient: this.GetClient(),
                                containerRId: containerResponse.Resource.ResourceId);

                        // The customer will write their own HasChangeBeedProcessedAsync logic but can use this as a model.
                        bool hasChangedBeenProcessed = await GetChangeFeedProcessorBuilderWithAllVersionsAndDeletesTests
                            .HasChangeBeedProcessedAsync(
                                partitionKey: partitionKey,
                                monitoredContainer: monitoredContainer,
                                lsnOfChange: lsnOfChange,
                                bookmarks: bookmarks);

                        if (hasChangedBeenProcessed)
                        {
                            // Now? Up to customer to decide what to do.
                        }
                    }

                    Assert.IsNotNull(context.LeaseToken);
                    Assert.IsNotNull(context.Diagnostics);
                    Assert.IsNotNull(context.Headers);
                    Assert.IsNotNull(context.Headers.Session);
                    Assert.IsTrue(context.Headers.RequestCharge > 0);
                    Assert.IsTrue(context.Diagnostics.ToString().Contains("Change Feed Processor Read Next Async"));
                    Assert.AreEqual(expected: 2, actual: docs.Count);

                    ChangeFeedItem<dynamic> createChange = docs.ElementAt(0);
                    Assert.IsNotNull(createChange.Current);
                    Assert.AreEqual(expected: "1", actual: createChange.Current.id.ToString());
                    Assert.AreEqual(expected: "1", actual: createChange.Current.pk.ToString());
                    Assert.AreEqual(expected: "original test", actual: createChange.Current.description.ToString());
                    Assert.AreEqual(expected: createChange.Metadata.OperationType, actual: ChangeFeedOperationType.Create);
                    Assert.AreEqual(expected: createChange.Metadata.PreviousLsn, actual: 0);
                    Assert.IsNull(createChange.Previous);

                    ChangeFeedItem<dynamic> replaceChange = docs.ElementAt(1);
                    Assert.IsNotNull(replaceChange.Current);
                    Assert.AreEqual(expected: "1", actual: replaceChange.Current.id.ToString());
                    Assert.AreEqual(expected: "1", actual: replaceChange.Current.pk.ToString());
                    Assert.AreEqual(expected: "test after replace", actual: replaceChange.Current.description.ToString());
                    Assert.AreEqual(expected: replaceChange.Metadata.OperationType, actual: ChangeFeedOperationType.Replace);
                    Assert.AreEqual(expected: createChange.Metadata.Lsn, actual: replaceChange.Metadata.PreviousLsn);
                    Assert.IsNull(replaceChange.Previous);

                    Assert.IsTrue(condition: createChange.Metadata.ConflictResolutionTimestamp < replaceChange.Metadata.ConflictResolutionTimestamp, message: "The create operation must happen before the replace operation.");
                    Assert.IsTrue(condition: createChange.Metadata.Lsn < replaceChange.Metadata.Lsn, message: "The create operation must happen before the replace operation.");
                    Assert.IsTrue(condition: createChange.Metadata.Lsn < replaceChange.Metadata.Lsn, message: "The replace operation must happen before the delete operation.");

                    return; // Task.CompletedTask;
                })
                .WithInstanceName(Guid.NewGuid().ToString())
                .WithLeaseContainer(this.LeaseContainer)
                .WithErrorNotification((leaseToken, error) =>
                {
                    exception = error.InnerException;

                    return Task.CompletedTask;
                })
                .Build();

            // Start the processor, insert 1 document to generate a checkpoint, and modify it.
            // 1 second delay between operations to get different timestamps.

            await processor.StartAsync();
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);

            await monitoredContainer.CreateItemAsync<dynamic>(new { id = "1", pk = "1", description = "original test" }, partitionKey: new PartitionKey("1"));
            await Task.Delay(1000);

            await monitoredContainer.UpsertItemAsync<dynamic>(new { id = "1", pk = "1", description = "test after replace" }, partitionKey: new PartitionKey("1"));
            await Task.Delay(1000);

            bool isStartOk = allDocsProcessed.WaitOne(10 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);

            await processor.StopAsync();

            if (exception != default)
            {
                Assert.Fail(exception.ToString());
            }
        }

        private static async Task<List<(FeedRange range, long lsn)>> CreateTestBookmarksAsync(
            CosmosClient cosmosClient,
            string containerRId)
        {
            Routing.PartitionKeyRangeCache partitionKeyRangeCache = await cosmosClient.DocumentClient.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);
            IReadOnlyList<Documents.PartitionKeyRange> currentContainerRanges = await partitionKeyRangeCache.TryGetOverlappingRangesAsync(
                collectionRid: containerRId,
                range: FeedRangeEpk.FullRange.Range,
                trace: NoOpTrace.Singleton,
                forceRefresh: true);

            IEnumerable<FeedRange> bookmarkRanges = GetChangeFeedProcessorBuilderWithAllVersionsAndDeletesTests.CreateFeedRanges(
                minHexValue: currentContainerRanges.FirstOrDefault().MinInclusive,
                maxHexValue: currentContainerRanges.LastOrDefault().MaxExclusive,
                numberOfRanges: 3);

            return bookmarkRanges
                .Select((bookmarkRange, index) => (bookmarkRange, lsn: (long)(index + 1) * 25))
                .ToList();

        }

        /// <summary>
        /// This is based on an issue located at <see href="https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4308"/>.
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [Description("Scenario: When ChangeFeedMode on ChangeFeedProcessor, switches from LatestVersion to AllVersionsAndDeletes," +
            "an exception is expected. LatestVersion's WithStartFromBeginning can be set, or not set.")]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenLatestVersionSwitchToAllVersionsAndDeletesExpectsAexceptionTestAsync(bool withStartFromBeginning)
        {
            (ContainerInternal monitoredContainer, ContainerResponse _) = await this.CreateMonitoredContainer(ChangeFeedMode.LatestVersion);
            ManualResetEvent allDocsProcessed = new(false);

            await GetChangeFeedProcessorBuilderWithAllVersionsAndDeletesTests
                .BuildChangeFeedProcessorWithLatestVersionAsync(
                    monitoredContainer: monitoredContainer,
                    leaseContainer: this.LeaseContainer,
                    allDocsProcessed: allDocsProcessed,
                    withStartFromBeginning: withStartFromBeginning);

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => GetChangeFeedProcessorBuilderWithAllVersionsAndDeletesTests
                    .BuildChangeFeedProcessorWithAllVersionsAndDeletesAsync(
                        monitoredContainer: monitoredContainer,
                        leaseContainer: this.LeaseContainer,
                        allDocsProcessed: allDocsProcessed));

            Assert.AreEqual(expected: "Switching ChangeFeedMode Incremental Feed to Full-Fidelity Feed is not allowed.", actual: exception.Message);
        }


        /// <summary>
        /// This is based on an issue located at <see href="https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4308"/>.
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [Description("Scenario: For Legacy lease documents with no Mode property, When ChangeFeedMode on ChangeFeedProcessor, switches from LatestVersion to AllVersionsAndDeletes," +
            "an exception is expected. LatestVersion's WithStartFromBeginning can be set, or not set.")]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenLegacyLatestVersionSwitchToAllVersionsAndDeletesExpectsAexceptionTestAsync(bool withStartFromBeginning)
        {
            (ContainerInternal monitoredContainer, ContainerResponse _) = await this.CreateMonitoredContainer(ChangeFeedMode.LatestVersion);
            ManualResetEvent allDocsProcessed = new(false);

            await GetChangeFeedProcessorBuilderWithAllVersionsAndDeletesTests
                .BuildChangeFeedProcessorWithLatestVersionAsync(
                    monitoredContainer: monitoredContainer,
                    leaseContainer: this.LeaseContainer,
                    allDocsProcessed: allDocsProcessed,
                    withStartFromBeginning: withStartFromBeginning);

            // Read lease documents, remove the Mode, and update the lease documents, so that it mimics a legacy lease document.

            await GetChangeFeedProcessorBuilderWithAllVersionsAndDeletesTests
                .RevertLeaseDocumentsToLegacyWithNoMode(
                    leaseContainer: this.LeaseContainer,
                    leaseDocumentCount: 2);

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => GetChangeFeedProcessorBuilderWithAllVersionsAndDeletesTests
                    .BuildChangeFeedProcessorWithAllVersionsAndDeletesAsync(
                        monitoredContainer: monitoredContainer,
                        leaseContainer: this.LeaseContainer,
                        allDocsProcessed: allDocsProcessed));

            Assert.AreEqual(expected: "Switching ChangeFeedMode Incremental Feed to Full-Fidelity Feed is not allowed.", actual: exception.Message);
        }

        /// <summary>
        /// This is based on an issue located at <see href="https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4308"/>.
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [Description("Scenario: When ChangeFeedMode on ChangeFeedProcessor, switches from AllVersionsAndDeletes to LatestVersion," +
            "an exception is expected. LatestVersion's WithStartFromBeginning can be set, or not set.")]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenAllVersionsAndDeletesSwitchToLatestVersionExpectsAexceptionTestAsync(bool withStartFromBeginning)
        {
            (ContainerInternal monitoredContainer, ContainerResponse _) = await this.CreateMonitoredContainer(ChangeFeedMode.AllVersionsAndDeletes);
            ManualResetEvent allDocsProcessed = new(false);

            await GetChangeFeedProcessorBuilderWithAllVersionsAndDeletesTests
                .BuildChangeFeedProcessorWithAllVersionsAndDeletesAsync(
                    monitoredContainer: monitoredContainer,
                    leaseContainer: this.LeaseContainer,
                    allDocsProcessed: allDocsProcessed);

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => GetChangeFeedProcessorBuilderWithAllVersionsAndDeletesTests
                    .BuildChangeFeedProcessorWithLatestVersionAsync(
                        monitoredContainer: monitoredContainer,
                        leaseContainer: this.LeaseContainer,
                        allDocsProcessed: allDocsProcessed,
                        withStartFromBeginning: withStartFromBeginning));

            Assert.AreEqual(expected: "Switching ChangeFeedMode Full-Fidelity Feed to Incremental Feed is not allowed.", actual: exception.Message);
        }

        /// <summary>
        /// This is based on an issue located at <see href="https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4308"/>.
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [Description("Scenario: When ChangeFeedMode on ChangeFeedProcessor does not switch, AllVersionsAndDeletes," +
            "no exception is expected.")]
        public async Task WhenNoSwitchAllVersionsAndDeletesFDoesNotExpectAexceptionTestAsync()
        {
            (ContainerInternal monitoredContainer, ContainerResponse _) = await this.CreateMonitoredContainer(ChangeFeedMode.AllVersionsAndDeletes);
            ManualResetEvent allDocsProcessed = new(false);

            try
            {
                await GetChangeFeedProcessorBuilderWithAllVersionsAndDeletesTests
                    .BuildChangeFeedProcessorWithAllVersionsAndDeletesAsync(
                        monitoredContainer: monitoredContainer,
                        leaseContainer: this.LeaseContainer,
                        allDocsProcessed: allDocsProcessed);

                await GetChangeFeedProcessorBuilderWithAllVersionsAndDeletesTests
                    .BuildChangeFeedProcessorWithAllVersionsAndDeletesAsync(
                        monitoredContainer: monitoredContainer,
                        leaseContainer: this.LeaseContainer,
                        allDocsProcessed: allDocsProcessed);
            }
            catch
            {
                Assert.Fail("An exception occurred when one was not expceted."); ;
            }
        }

        /// <summary>
        /// This is based on an issue located at <see href="https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4308"/>.
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [Description("Scenario: When ChangeFeedMode on ChangeFeedProcessor does not switch, LatestVersion," +
            "no exception is expected. LatestVersion's WithStartFromBeginning can be set, or not set.")]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenNoSwitchLatestVersionDoesNotExpectAexceptionTestAsync(bool withStartFromBeginning)
        {
            (ContainerInternal monitoredContainer, ContainerResponse _) = await this.CreateMonitoredContainer(ChangeFeedMode.LatestVersion);
            ManualResetEvent allDocsProcessed = new(false);

            try
            {
                await GetChangeFeedProcessorBuilderWithAllVersionsAndDeletesTests
                    .BuildChangeFeedProcessorWithLatestVersionAsync(
                        monitoredContainer: monitoredContainer,
                        leaseContainer: this.LeaseContainer,
                        allDocsProcessed: allDocsProcessed,
                        withStartFromBeginning: withStartFromBeginning);

                await GetChangeFeedProcessorBuilderWithAllVersionsAndDeletesTests
                    .BuildChangeFeedProcessorWithLatestVersionAsync(
                        monitoredContainer: monitoredContainer,
                        leaseContainer: this.LeaseContainer,
                        allDocsProcessed: allDocsProcessed,
                        withStartFromBeginning: withStartFromBeginning);
            }
            catch
            {
                Assert.Fail("An exception occurred when one was not expceted."); ;
            }
        }

        /// <summary>
        /// This is based on an issue located at <see href="https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4423"/>.
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [Description("Scenario: For Legacy lease documents with no Mode property, When ChangeFeedMode on ChangeFeedProcessor " +
            "does not switch, LatestVersion, no exception is expected. LatestVersion's WithStartFromBeginning can be set, or not set.")]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenLegacyNoSwitchLatestVersionDoesNotExpectAnExceptionTestAsync(bool withStartFromBeginning)
        {
            (ContainerInternal monitoredContainer, ContainerResponse _) = await this.CreateMonitoredContainer(ChangeFeedMode.LatestVersion);
            ManualResetEvent allDocsProcessed = new(false);

            await GetChangeFeedProcessorBuilderWithAllVersionsAndDeletesTests
                .BuildChangeFeedProcessorWithLatestVersionAsync(
                    monitoredContainer: monitoredContainer,
                    leaseContainer: this.LeaseContainer,
                    allDocsProcessed: allDocsProcessed,
                    withStartFromBeginning: withStartFromBeginning);

            // Read lease documents, remove the Mode, and update the lease documents, so that it mimics a legacy lease document.

            await GetChangeFeedProcessorBuilderWithAllVersionsAndDeletesTests
                .RevertLeaseDocumentsToLegacyWithNoMode(
                    leaseContainer: this.LeaseContainer,
                    leaseDocumentCount: 2);

            await GetChangeFeedProcessorBuilderWithAllVersionsAndDeletesTests
                .BuildChangeFeedProcessorWithLatestVersionAsync(
                    monitoredContainer: monitoredContainer,
                    leaseContainer: this.LeaseContainer,
                    allDocsProcessed: allDocsProcessed,
                    withStartFromBeginning: withStartFromBeginning);
        }

        private static async Task RevertLeaseDocumentsToLegacyWithNoMode(
            Container leaseContainer,
            int leaseDocumentCount)
        {
            FeedIterator iterator = leaseContainer.GetItemQueryStreamIterator(
                queryText: "SELECT * FROM c",
                continuationToken: null);

            List<JObject> leases = new List<JObject>();
            while (iterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage = await iterator.ReadNextAsync().ConfigureAwait(false))
                {
                    responseMessage.EnsureSuccessStatusCode();
                    leases.AddRange(CosmosFeedResponseSerializer.FromFeedResponseStream<JObject>(
                        serializerCore: CosmosContainerExtensions.DefaultJsonSerializer,
                        streamWithServiceEnvelope: responseMessage.Content));
                }
            }

            int counter = 0;

            foreach (JObject lease in leases)
            {
                if (!lease.ContainsKey("Mode"))
                {
                    continue;
                }

                counter++;
                lease.Remove("Mode");

                _ = await leaseContainer.UpsertItemAsync(item: lease);
            }
                
            Assert.AreEqual(expected: leaseDocumentCount, actual: counter);
        }

        private static async Task BuildChangeFeedProcessorWithLatestVersionAsync(
            ContainerInternal monitoredContainer,
            Container leaseContainer,
            ManualResetEvent allDocsProcessed,
            bool withStartFromBeginning)
        {
            Exception exception = default;
            ChangeFeedProcessor latestVersionProcessorAtomic = null;

            ChangeFeedProcessorBuilder processorBuilder = monitoredContainer
                .GetChangeFeedProcessorBuilder(processorName: $"processorName", onChangesDelegate: (ChangeFeedProcessorContext context, IReadOnlyCollection<dynamic> documents, CancellationToken token) => Task.CompletedTask)
                .WithInstanceName(Guid.NewGuid().ToString())
                .WithLeaseContainer(leaseContainer)
                .WithErrorNotification((leaseToken, error) =>
                {
                    exception = error.InnerException;

                    return Task.CompletedTask;
                });

            if (withStartFromBeginning)
            {
                processorBuilder.WithStartFromBeginning();
            }

            ChangeFeedProcessor processor = processorBuilder.Build();
            Interlocked.Exchange(ref latestVersionProcessorAtomic, processor);

            await processor.StartAsync();
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            bool isStartOk = allDocsProcessed.WaitOne(10 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);

            if (exception != default)
            {
                Assert.Fail(exception.ToString());
            }
        }

        private static async Task BuildChangeFeedProcessorWithAllVersionsAndDeletesAsync(
            ContainerInternal monitoredContainer,
            Container leaseContainer,
            ManualResetEvent allDocsProcessed)
        {
            Exception exception = default;
            ChangeFeedProcessor allVersionsAndDeletesProcessorAtomic = null;

            ChangeFeedProcessorBuilder allVersionsAndDeletesProcessorBuilder = monitoredContainer
                .GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes(processorName: $"processorName", onChangesDelegate: (ChangeFeedProcessorContext context, IReadOnlyCollection<ChangeFeedItem<dynamic>> documents, CancellationToken token) => Task.CompletedTask)
                .WithInstanceName(Guid.NewGuid().ToString())
                .WithMaxItems(1)
                .WithLeaseContainer(leaseContainer)
                .WithErrorNotification((leaseToken, error) =>
                {
                    exception = error.InnerException;

                    return Task.FromResult(exception);
                });

            ChangeFeedProcessor processor = allVersionsAndDeletesProcessorBuilder.Build();
            Interlocked.Exchange(ref allVersionsAndDeletesProcessorAtomic, processor);

            await processor.StartAsync();
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            bool isStartOk = allDocsProcessed.WaitOne(10 * BaseChangeFeedClientHelper.ChangeFeedSetupTime);

            if (exception != default)
            {
                Assert.Fail(exception.ToString());
            }
        }

        private async Task<(ContainerInternal, ContainerResponse)> CreateMonitoredContainer(ChangeFeedMode changeFeedMode)
        {
            string PartitionKey = "/pk";
            ContainerProperties properties = new ContainerProperties(id: Guid.NewGuid().ToString(),
                partitionKeyPath: PartitionKey);

            if (changeFeedMode == ChangeFeedMode.AllVersionsAndDeletes)
            {
                properties.ChangeFeedPolicy.FullFidelityRetention = TimeSpan.FromMinutes(5);
                properties.DefaultTimeToLive = -1;
            }

            ContainerResponse response = await this.database.CreateContainerAsync(properties,
                throughput: 10000,
                cancellationToken: this.cancellationToken);

            return ((ContainerInternal)response, response);
        }

        private static Cosmos.FeedRange CreateFeedRange(string min, string max)
        {
            if (min == "0")
            {
                min = "";
            }

            Documents.Routing.Range<string> range = new(
                min: min,
                max: max,
                isMinInclusive: true,
                isMaxInclusive: false);

            FeedRangeEpk feedRangeEpk = new(range);

            return Cosmos.FeedRange.FromJsonString(feedRangeEpk.ToJsonString());
        }

        private static IEnumerable<Cosmos.FeedRange> CreateFeedRanges(
            string minHexValue,
            string maxHexValue,
            int numberOfRanges = 10)
        {
            if (minHexValue == string.Empty)
            {
                minHexValue = "0";
            }

            // Convert hex strings to ulong
            ulong minValue = ulong.Parse(minHexValue, System.Globalization.NumberStyles.HexNumber);
            ulong maxValue = ulong.Parse(maxHexValue, System.Globalization.NumberStyles.HexNumber);

            ulong range = maxValue - minValue + 1; // Include the upper boundary
            ulong stepSize = range / (ulong)numberOfRanges;

            // Generate the sub-ranges
            List<(string, string)> subRanges = new();
            ulong splitMaxValue = default;

            for (int i = 0; i < numberOfRanges; i++)
            {
                ulong splitMinValue = splitMaxValue;
                splitMaxValue = (i == numberOfRanges - 1) ? maxValue : splitMinValue + stepSize - 1;
                subRanges.Add((splitMinValue.ToString("X"), splitMaxValue.ToString("X")));
            }

            List<Cosmos.FeedRange> feedRanges = new List<Cosmos.FeedRange>();

            foreach ((string min, string max) in subRanges)
            {
                feedRanges.Add(GetChangeFeedProcessorBuilderWithAllVersionsAndDeletesTests.CreateFeedRange(
                    min: min,
                    max: max));
            }

            return feedRanges;
        }

        /// <summary>
        /// Checks bookmark ranges using partitionKey's range to see if that current changed lsn has been processed.
        /// </summary>
        /// <param name="monitoredContainer">Critical for invoking GetEPKRangeForPrefixPartitionKey.</param>
        /// <param name="lsnOfChange">Critical for determining if the current changed lsn has been processed.</param>
        /// <param name="bookmarks">Critical for feed ranges with lsn from the bookmarks. [{ min, max, lsn }]</param>
        private static bool HasChangeBeedProcessed(
            ContainerInternal monitoredContainer,
            FeedRange feedRange,
            long lsnOfChange,
            IReadOnlyList<(FeedRange range, long lsn)> bookmarks)
        {
            IReadOnlyList<FeedRange> overlappingRangesFromFeedRange = monitoredContainer.FindOverlappingRanges(
                feedRange: feedRange,
                feedRanges: bookmarks.Select(bookmark => bookmark.range).ToList());

            if (overlappingRangesFromFeedRange == null)
            {
                return false;
            }

            Logger.LogLine($"{nameof(feedRange)} -> {feedRange.ToJsonString()}");
            Logger.LogLine($"{nameof(lsnOfChange)} -> {lsnOfChange}");
            Logger.LogLine($"{nameof(bookmarks)} -> {JsonConvert.SerializeObject(bookmarks)}");

            foreach (FeedRange overlappingRange in overlappingRangesFromFeedRange)
            {
                foreach ((FeedRange range, long lsn) in bookmarks.Select(x => x))
                {
                    if (lsnOfChange <= lsn && overlappingRange.Equals(range))
                    {
                        Logger.LogLine($"The range '{range}' with lsn '{lsn}' has been processed.");

                        return true;
                    }
                }
            }

            return false;

        }

        private static async Task<bool> HasChangeBeedProcessedAsync(
            ContainerInternal monitoredContainer,
            PartitionKey partitionKey,
            long lsnOfChange,
            IReadOnlyList<(FeedRange range, long lsn)> bookmarks)
        {
            IReadOnlyList<FeedRange> overlappingRangesFromPartitionKey = await monitoredContainer.FindOverlappingRangesAsync(
                partitionKey,
                bookmarks.Select(bookmark => bookmark.range).ToList());

            if (overlappingRangesFromPartitionKey == null)
            {
                return false;
            }

            Logger.LogLine($"{nameof(partitionKey)} -> {partitionKey.ToJsonString()}");
            Logger.LogLine($"{nameof(lsnOfChange)} -> {lsnOfChange}");
            Logger.LogLine($"{nameof(bookmarks)} -> {JsonConvert.SerializeObject(bookmarks)}");

            foreach (FeedRange overlappingRange in overlappingRangesFromPartitionKey)
            {
                foreach ((FeedRange range, long lsn) in bookmarks.Select(x => x))
                {
                    if (lsnOfChange <= lsn && overlappingRange.Equals(range))
                    {
                        Logger.LogLine($"The range '{range}' with lsn '{lsn}' has been processed.");

                        return true;
                    }
                }
            }

            return false;

        }
    }
}
