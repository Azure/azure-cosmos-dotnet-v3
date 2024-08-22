//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.CFP.AllVersionsAndDeletes
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests.ChangeFeed;
    using Microsoft.Azure.Cosmos.Services.Management.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    [TestCategory("ChangeFeedProcessor")]
    public class BuilderTests : BaseChangeFeedClientHelper
    {
        [TestInitialize]
        public async Task TestInitialize()
        {
            await this.ChangeFeedTestInit();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await this.TestCleanup();
        }

        [TestMethod]
        [Timeout(300000)]
        [TestCategory("LongRunning")]
        [Owner("philipthomas-MSFT")]
        [Description("Scenario: When a document is created with ttl set, there should be 1 create and 1 delete that will appear for that " +
            "document when using ChangeFeedProcessor with AllVersionsAndDeletes set as the ChangeFeedMode.")]
        public async Task WhenADocumentIsCreatedWithTtlSetThenTheDocumentIsDeletedTestsAsync()
        {
            ContainerInternal monitoredContainer = await this.CreateMonitoredContainer(ChangeFeedMode.AllVersionsAndDeletes);
            Exception exception = default;
            int ttlInSeconds = 5;
            Stopwatch stopwatch = new();
            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);

            ChangeFeedProcessor processor = monitoredContainer
                .GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes(processorName: "processor", onChangesDelegate: (ChangeFeedProcessorContext context, IReadOnlyCollection<ChangeFeedItem<ToDoActivity>> docs, CancellationToken token) =>
                {
                    // NOTE(philipthomas-MSFT): Please allow these Logger.LogLine because TTL on items will purge at random times so I am using this to test when ran locally using emulator.

                    Logger.LogLine($"@ {DateTime.Now}, {nameof(stopwatch)} -> CFP AVAD took '{stopwatch.ElapsedMilliseconds}' to read document CRUD in feed.");

                    foreach (ChangeFeedItem<ToDoActivity> change in docs)
                    {
                        if (change.Metadata.OperationType == ChangeFeedOperationType.Create)
                        {
                            // current
                            Assert.AreEqual(expected: "1", actual: change.Current.id.ToString());
                            Assert.AreEqual(expected: "1", actual: change.Current.pk.ToString());
                            Assert.AreEqual(expected: "Testing TTL on CFP.", actual: change.Current.description.ToString());
                            Assert.AreEqual(expected: ttlInSeconds, actual: change.Current.ttl);

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
                            Assert.AreEqual(expected: ttlInSeconds, actual: change.Previous.ttl);

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
                await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);
                await monitoredContainer.CreateItemAsync<ToDoActivity>(new ToDoActivity  { id = "1", pk = "1", description = "Testing TTL on CFP.", ttl = ttlInSeconds }, partitionKey: new PartitionKey("1"));

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
            ContainerInternal monitoredContainer = await this.CreateMonitoredContainer(ChangeFeedMode.AllVersionsAndDeletes);
            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);
            Exception exception = default;

            ChangeFeedProcessor processor = monitoredContainer
                .GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes(processorName: "processor", onChangesDelegate: (ChangeFeedProcessorContext context, IReadOnlyCollection<ChangeFeedItem<dynamic>> docs, CancellationToken token) =>
                {
                    string id = default;
                    string pk = default;
                    string description = default;

                    Console.WriteLine(JsonConvert.SerializeObject(docs));

                    foreach (ChangeFeedItem<dynamic> change in docs)
                    {
                        if (change.Metadata.OperationType != ChangeFeedOperationType.Delete)
                        {
                            id = change.Current.id.ToString();
                            pk = change.Current.pk.ToString();
                            description = change.Current.description.ToString();
                        }
                        else
                        {
                            id = change.Previous.id.ToString();
                            pk = change.Previous.pk.ToString();
                            description = change.Previous.description.ToString();
                        }

                        ChangeFeedOperationType operationType = change.Metadata.OperationType;
                        long previousLsn = change.Metadata.PreviousLsn;
                        DateTime m = change.Metadata.ConflictResolutionTimestamp;
                        long lsn = change.Metadata.Lsn;
                        bool isTimeToLiveExpired = change.Metadata.IsTimeToLiveExpired;
                    }

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
            ContainerInternal monitoredContainer = await this.CreateMonitoredContainer(ChangeFeedMode.LatestVersion);
            ManualResetEvent allDocsProcessed = new(false);

            await
                BuildChangeFeedProcessorWithLatestVersionAsync(
                    monitoredContainer: monitoredContainer,
                    leaseContainer: this.LeaseContainer,
                    allDocsProcessed: allDocsProcessed,
                    withStartFromBeginning: withStartFromBeginning);

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(
                () =>
                    BuildChangeFeedProcessorWithAllVersionsAndDeletesAsync(
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
            ContainerInternal monitoredContainer = await this.CreateMonitoredContainer(ChangeFeedMode.LatestVersion);
            ManualResetEvent allDocsProcessed = new(false);

            await
                BuildChangeFeedProcessorWithLatestVersionAsync(
                    monitoredContainer: monitoredContainer,
                    leaseContainer: this.LeaseContainer,
                    allDocsProcessed: allDocsProcessed,
                    withStartFromBeginning: withStartFromBeginning);

            // Read lease documents, remove the Mode, and update the lease documents, so that it mimics a legacy lease document.

            await
                RevertLeaseDocumentsToLegacyWithNoMode(
                    leaseContainer: this.LeaseContainer,
                    leaseDocumentCount: 2);

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(
                () =>
                    BuildChangeFeedProcessorWithAllVersionsAndDeletesAsync(
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
            ContainerInternal monitoredContainer = await this.CreateMonitoredContainer(ChangeFeedMode.AllVersionsAndDeletes);
            ManualResetEvent allDocsProcessed = new(false);

            await
                BuildChangeFeedProcessorWithAllVersionsAndDeletesAsync(
                    monitoredContainer: monitoredContainer,
                    leaseContainer: this.LeaseContainer,
                    allDocsProcessed: allDocsProcessed);

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(
                () =>
                    BuildChangeFeedProcessorWithLatestVersionAsync(
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
            ContainerInternal monitoredContainer = await this.CreateMonitoredContainer(ChangeFeedMode.AllVersionsAndDeletes);
            ManualResetEvent allDocsProcessed = new(false);

            try
            {
                await
                    BuildChangeFeedProcessorWithAllVersionsAndDeletesAsync(
                        monitoredContainer: monitoredContainer,
                        leaseContainer: this.LeaseContainer,
                        allDocsProcessed: allDocsProcessed);

                await
                    BuildChangeFeedProcessorWithAllVersionsAndDeletesAsync(
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
            ContainerInternal monitoredContainer = await this.CreateMonitoredContainer(ChangeFeedMode.LatestVersion);
            ManualResetEvent allDocsProcessed = new(false);

            try
            {
                await
                    BuildChangeFeedProcessorWithLatestVersionAsync(
                        monitoredContainer: monitoredContainer,
                        leaseContainer: this.LeaseContainer,
                        allDocsProcessed: allDocsProcessed,
                        withStartFromBeginning: withStartFromBeginning);

                await
                    BuildChangeFeedProcessorWithLatestVersionAsync(
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
            ContainerInternal monitoredContainer = await this.CreateMonitoredContainer(ChangeFeedMode.LatestVersion);
            ManualResetEvent allDocsProcessed = new(false);

            await
                BuildChangeFeedProcessorWithLatestVersionAsync(
                    monitoredContainer: monitoredContainer,
                    leaseContainer: this.LeaseContainer,
                    allDocsProcessed: allDocsProcessed,
                    withStartFromBeginning: withStartFromBeginning);

            // Read lease documents, remove the Mode, and update the lease documents, so that it mimics a legacy lease document.

            await
                RevertLeaseDocumentsToLegacyWithNoMode(
                    leaseContainer: this.LeaseContainer,
                    leaseDocumentCount: 2);

            await
                BuildChangeFeedProcessorWithLatestVersionAsync(
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

        private async Task<ContainerInternal> CreateMonitoredContainer(ChangeFeedMode changeFeedMode)
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

            return (ContainerInternal)response;
        }

        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [Description("Scenario: WithStartTime should throw an exception when used in AVAD mode.")]
        public async Task WhenACFPInAVADModeUsesWithStartTimeExpectExceptionTestsAsync()
        {
            ContainerInternal monitoredContainer = await this.CreateMonitoredContainer(ChangeFeedMode.AllVersionsAndDeletes);

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() =>
            {
                ChangeFeedProcessor processor = monitoredContainer
                    .GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes(
                        processorName: "processor",
                        onChangesDelegate: (ChangeFeedProcessorContext context, IReadOnlyCollection<ChangeFeedItem<dynamic>> docs, CancellationToken cancellationToken) => Task.CompletedTask)
                    .WithStartTime(DateTime.Now)
                    .WithInstanceName(Guid.NewGuid().ToString())
                    .WithLeaseContainer(this.LeaseContainer)
                    .Build();
            });

            Assert.AreEqual(
                expected: "Using the 'WithStartTime' option with ChangeFeedProcessor is not supported with Microsoft.Azure.Cosmos.ChangeFeed.ChangeFeedModeFullFidelity mode.",
                actual: exception.Message);
        }

        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [Description("Scenario: WithStartFromBeginning should throw an exception when used in AVAD mode.")]
        public async Task WhenACFPInAVADModeUsesWithStartFromBeginningExpectExceptionTestsAsync()
        {
            ContainerInternal monitoredContainer = await this.CreateMonitoredContainer(ChangeFeedMode.AllVersionsAndDeletes);

            InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(() =>
            {
                ChangeFeedProcessor processor = monitoredContainer
                    .GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes(
                        processorName: "processor",
                        onChangesDelegate: (ChangeFeedProcessorContext context, IReadOnlyCollection<ChangeFeedItem<dynamic>> docs, CancellationToken cancellationToken) => Task.CompletedTask)
                    .WithStartFromBeginning()
                    .WithInstanceName(Guid.NewGuid().ToString())
                    .WithLeaseContainer(this.LeaseContainer)
                    .Build();
            });

            Assert.AreEqual(
                expected: "Using the 'WithStartFromBeginning' option with ChangeFeedProcessor is not supported with Microsoft.Azure.Cosmos.ChangeFeed.ChangeFeedModeFullFidelity mode.",
                actual: exception.Message);
        }
    }
}
