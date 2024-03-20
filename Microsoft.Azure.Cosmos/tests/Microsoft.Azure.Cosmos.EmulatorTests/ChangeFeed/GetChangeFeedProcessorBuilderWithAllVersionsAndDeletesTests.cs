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
    using Microsoft.VisualStudio.TestTools.UnitTesting;

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
        [Owner("philipthomas-MSFT")]
        [Description("Scenario: When a document is created, then updated, and finally deleted, there should be 3 changes that will appear for that " +
            "document when using ChangeFeedProcessor with AllVersionsAndDeletes set as the ChangeFeedMode.")]
        public async Task WhenADocumentIsCreatedThenUpdatedThenDeletedTestsAsync()
        {
            ContainerInternal monitoredContainer = await this.CreateMonitoredContainer(ChangeFeedMode.AllVersionsAndDeletes);
            ManualResetEvent allDocsProcessed = new ManualResetEvent(false);
            Exception exception = default;

            ChangeFeedProcessor processor = monitoredContainer
                .GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes(processorName: "processor", onChangesDelegate: (ChangeFeedProcessorContext context, IReadOnlyCollection<ChangeFeedItemChange<dynamic>> docs, CancellationToken token) =>
                {
                    string id = default;
                    string pk = default;
                    string description = default;

                    foreach (ChangeFeedItemChange<dynamic> change in docs)
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

                    ChangeFeedItemChange<dynamic> createChange = docs.ElementAt(0);
                    Assert.IsNotNull(createChange.Current);
                    Assert.AreEqual(expected: "1", actual: createChange.Current.id.ToString());
                    Assert.AreEqual(expected: "1", actual: createChange.Current.pk.ToString());
                    Assert.AreEqual(expected: "original test", actual: createChange.Current.description.ToString());
                    Assert.AreEqual(expected: createChange.Metadata.OperationType, actual: ChangeFeedOperationType.Create);
                    Assert.AreEqual(expected: createChange.Metadata.PreviousLsn, actual: 0);
                    Assert.IsNull(createChange.Previous);

                    ChangeFeedItemChange<dynamic> replaceChange = docs.ElementAt(1);
                    Assert.IsNotNull(replaceChange.Current);
                    Assert.AreEqual(expected: "1", actual: replaceChange.Current.id.ToString());
                    Assert.AreEqual(expected: "1", actual: replaceChange.Current.pk.ToString());
                    Assert.AreEqual(expected: "test after replace", actual: replaceChange.Current.description.ToString());
                    Assert.AreEqual(expected: replaceChange.Metadata.OperationType, actual: ChangeFeedOperationType.Replace);
                    Assert.AreEqual(expected: createChange.Metadata.Lsn, actual: replaceChange.Metadata.PreviousLsn);
                    Assert.IsNull(replaceChange.Previous);

                    ChangeFeedItemChange<dynamic> deleteChange = docs.ElementAt(2);
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

                    Debug.WriteLine("Assertions completed.");

                    return Task.CompletedTask;
                })
                .WithInstanceName(Guid.NewGuid().ToString())
                .WithLeaseContainer(this.LeaseContainer)
                .WithErrorNotification((leaseToken, error) =>
                {
                    exception = error.InnerException;

                    Debug.WriteLine("WithErrorNotification");
                    Debug.WriteLine(error.ToString());

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
            "a CosmosException is expected. LatestVersion's WithStartFromBeginning can be set, or not set.")]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenLatestVersionSwitchToAllVersionsAndDeletesExpectsACosmosExceptionTestAsync(bool withStartFromBeginning)
        {
            ContainerInternal monitoredContainer = await this.CreateMonitoredContainer(ChangeFeedMode.LatestVersion);
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

            Debug.WriteLine(exception.ToString());

            Assert.AreEqual(expected: "Switching ChangeFeedMode Incremental Feed to Full-Fidelity Feed is not allowed.", actual: exception.Message);

            Debug.WriteLine("Assertions completed.");
        }

        /// <summary>
        /// This is based on an issue located at <see href="https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4308"/>.
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [Description("Scenario: When ChangeFeedMode on ChangeFeedProcessor, switches from AllVersionsAndDeletes to LatestVersion," +
            "a CosmosException is expected. LatestVersion's WithStartFromBeginning can be set, or not set.")]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenAllVersionsAndDeletesSwitchToLatestVersionExpectsACosmosExceptionTestAsync(bool withStartFromBeginning)
        {
            ContainerInternal monitoredContainer = await this.CreateMonitoredContainer(ChangeFeedMode.AllVersionsAndDeletes);
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

            Debug.WriteLine(exception.ToString());

            Assert.AreEqual(expected: "Switching ChangeFeedMode Full-Fidelity Feed to Incremental Feed is not allowed.", actual: exception.Message);

            Debug.WriteLine("Assertions completed.");
        }

        /// <summary>
        /// This is based on an issue located at <see href="https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4308"/>.
        /// </summary>
        [TestMethod]
        [Owner("philipthomas-MSFT")]
        [Description("Scenario: When ChangeFeedMode on ChangeFeedProcessor does not switch, AllVersionsAndDeletes," +
            "no CosmosException is expected.")]
        public async Task WhenNoSwitchAllVersionsAndDeletesFDoesNotExpectACosmosExceptionTestAsync()
        {
            ContainerInternal monitoredContainer = await this.CreateMonitoredContainer(ChangeFeedMode.AllVersionsAndDeletes);
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

                Debug.WriteLine("No exceptions occurred.");
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
            "no CosmosException is expected. LatestVersion's WithStartFromBeginning can be set, or not set.")]
        [DataRow(false)]
        [DataRow(true)]
        public async Task WhenNoSwitchLatestVersionDoesNotExpectACosmosExceptionTestAsync(bool withStartFromBeginning)
        {
            ContainerInternal monitoredContainer = await this.CreateMonitoredContainer(ChangeFeedMode.LatestVersion);
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

                Debug.WriteLine("No exceptions occurred.");
            }
            catch
            {
                Assert.Fail("An exception occurred when one was not expceted."); ;
            }
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

                    Debug.WriteLine("WithErrorNotification");
                    Debug.WriteLine(error.ToString());

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
                .GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes(processorName: $"processorName", onChangesDelegate: (ChangeFeedProcessorContext context, IReadOnlyCollection<ChangeFeedItemChange<dynamic>> documents, CancellationToken token) => Task.CompletedTask)
                .WithInstanceName(Guid.NewGuid().ToString())
                .WithMaxItems(1)
                .WithLeaseContainer(leaseContainer)
                .WithErrorNotification((leaseToken, error) =>
                {
                    exception = error.InnerException;

                    Debug.WriteLine("WithErrorNotification");
                    Debug.WriteLine(error.ToString());

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
                Debug.WriteLine($"{nameof(properties.ChangeFeedPolicy.FullFidelityRetention)} initialized.");

                properties.ChangeFeedPolicy.FullFidelityRetention = TimeSpan.FromMinutes(5);
            }

            ContainerResponse response = await this.database.CreateContainerAsync(properties,
                throughput: 10000,
                cancellationToken: this.cancellationToken);

            return (ContainerInternal)response;
        }
    }
}
