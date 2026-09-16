//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class EstimatorTests : BaseChangeFeedClientHelper
    {
        private Container Container;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.ChangeFeedTestInit();
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: "monitored", partitionKeyPath: "/pk"),
                cancellationToken: this.cancellationToken);
            this.Container = response;

        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task WhenNoLeasesExist()
        {
            ManualResetEvent manualResetEvent = new ManualResetEvent(false);
            long receivedEstimation = 0;
            ChangeFeedProcessor estimator = this.Container
                .GetChangeFeedEstimatorBuilder("test", (long estimation, CancellationToken token) =>
                {
                    receivedEstimation = estimation;
                    manualResetEvent.Set();
                    return Task.CompletedTask;
                }, TimeSpan.FromSeconds(1))
                .WithLeaseContainer(this.LeaseContainer).Build();

            await estimator.StartAsync();
            Assert.IsTrue(manualResetEvent.WaitOne(BaseChangeFeedClientHelper.ChangeFeedCleanupTime), "Not received estimation in the expected time");
            await estimator.StopAsync();
            Assert.AreEqual(1, receivedEstimation);
        }

        [TestMethod]
        public async Task StartAsync_ShouldThrowIfContainerDoesNotExist()
        {
            ChangeFeedProcessor estimator = this.GetClient().GetContainer(this.database.Id, "DoesNotExist")
                .GetChangeFeedEstimatorBuilder("test", (long estimation, CancellationToken token) =>
                {
                    return Task.CompletedTask;
                }, TimeSpan.FromSeconds(1))
                .WithLeaseContainer(this.LeaseContainer).Build();

            CosmosException exception = await Assert.ThrowsExceptionAsync<CosmosException>(() => estimator.StartAsync());
            Assert.AreEqual(HttpStatusCode.NotFound, exception.StatusCode);
        }

        [TestMethod]
        public async Task WhenNoLeasesExist_Pull()
        {
            ChangeFeedEstimator estimator = ((ContainerInternal)this.Container)
                .GetChangeFeedEstimator(
                    processorName: "test",
                    this.LeaseContainer);

            long receivedEstimation = 0;
            using FeedIterator<ChangeFeedProcessorState> feedIterator = estimator.GetCurrentStateIterator();
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<ChangeFeedProcessorState> response = await feedIterator.ReadNextAsync();
                receivedEstimation += response.Sum(r => r.EstimatedLag);
            }

            Assert.AreEqual(0, receivedEstimation);
        }

        /// <summary>
        /// This test checks that when the ContinuationToken is null (an uncheckpointed lease, e.g. on
        /// fresh deploy), the estimator returns the sentinel lag of 1 per lease — preserving the
        /// non-zero wake signal that downstream listeners (Azure Functions Scale Controller, KEDA Cosmos
        /// scaler) rely on. Once the lease checkpoints for the first time, subsequent estimations
        /// report the real lag (see <see cref="CountPendingDocuments"/>).
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task WhenLeasesHaveContinuationTokenNullReturnSentinel()
        {
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder("test", (IReadOnlyCollection<dynamic> docs, CancellationToken token) => Task.CompletedTask)
                .WithInstanceName("random")
                .WithLeaseContainer(this.LeaseContainer).Build();

            await processor.StartAsync();
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedCleanupTime);
            await processor.StopAsync();

            ManualResetEvent manualResetEvent = new ManualResetEvent(false);
            long receivedEstimation = 0;
            ChangeFeedProcessor estimator = this.Container
                .GetChangeFeedEstimatorBuilder("test", (long estimation, CancellationToken token) =>
                {
                    receivedEstimation = estimation;
                    manualResetEvent.Set();
                    return Task.CompletedTask;
                }, TimeSpan.FromSeconds(1))
                .WithLeaseContainer(this.LeaseContainer).Build();

            await estimator.StartAsync();
            Assert.IsTrue(manualResetEvent.WaitOne(BaseChangeFeedClientHelper.ChangeFeedCleanupTime), "Not received estimation in the expected time");
            await estimator.StopAsync();

            // Sentinel: leases without a continuation token report EstimatedLag = 1 each so that
            // downstream listeners can wake the processor. There is at least one lease here.
            Assert.IsTrue(receivedEstimation >= 1, $"Expected at least sentinel lag of 1, got {receivedEstimation}.");
        }

        [TestMethod]
        public async Task WhenLeasesHaveContinuationTokenNullReturnSentinel_Pull()
        {
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder("test", (IReadOnlyCollection<dynamic> docs, CancellationToken token) => Task.CompletedTask)
                .WithInstanceName("random")
                .WithLeaseContainer(this.LeaseContainer).Build();

            await processor.StartAsync();
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedCleanupTime);
            await processor.StopAsync();

            long receivedEstimation = 0;
            ChangeFeedEstimator estimator = ((ContainerInternal)this.Container)
                .GetChangeFeedEstimator(
                    processorName: "test",
                    this.LeaseContainer);

            using FeedIterator<ChangeFeedProcessorState> feedIterator = estimator.GetCurrentStateIterator();
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<ChangeFeedProcessorState> response = await feedIterator.ReadNextAsync();
                receivedEstimation += response.Sum(r => r.EstimatedLag);
            }

            // Sentinel: leases without a continuation token report EstimatedLag = 1 each.
            Assert.IsTrue(receivedEstimation >= 1, $"Expected at least sentinel lag of 1, got {receivedEstimation}.");
        }

        /// <summary>
        /// This test checks that the estimator counts documents inserted after the processor first
        /// checkpoints a lease (i.e. once the lease has a non-null ContinuationToken).
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task CountPendingDocuments()
        {
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder(
                    processorName: "test",
                    onChangesDelegate: (IReadOnlyCollection<dynamic> docs, CancellationToken token) => Task.CompletedTask)
                .WithInstanceName("random")
                .WithLeaseContainer(this.LeaseContainer)
                .Build();

            await processor.StartAsync();

            // Letting processor initialize
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);

            // Inserting documents
            foreach (int id in Enumerable.Range(0, 10))
            {
                await this.Container.CreateItemAsync<dynamic>(new { id = id.ToString() });
            }

            // Waiting on all notifications to finish
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedCleanupTime);
            await processor.StopAsync();

            ManualResetEvent manualResetEvent = new ManualResetEvent(false);
            long? receivedEstimation = null;
            ChangeFeedProcessor estimator = this.Container
                .GetChangeFeedEstimatorBuilder(
                    processorName: "test",
                    estimationDelegate: (long estimation, CancellationToken token) =>
                    {
                        receivedEstimation = estimation;
                        manualResetEvent.Set();
                        return Task.CompletedTask;
                    }, TimeSpan.FromSeconds(1))
                .WithLeaseContainer(this.LeaseContainer)
                .Build();

            // Inserting more documents
            foreach (int id in Enumerable.Range(11, 10))
            {
                await this.Container.CreateItemAsync<dynamic>(new { id = id.ToString() });
            }

            await estimator.StartAsync();
            Assert.IsTrue(manualResetEvent.WaitOne(BaseChangeFeedClientHelper.ChangeFeedCleanupTime), "Not received estimation in the expected time");
            await estimator.StopAsync();
            Assert.AreEqual(10, receivedEstimation);
        }

        [TestMethod]
        public async Task CountPendingDocuments_Pull()
        {
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder(
                    processorName: "test",
                    onChangesDelegate: (IReadOnlyCollection<dynamic> docs, CancellationToken token) => Task.CompletedTask)
                .WithInstanceName("random")
                .WithLeaseContainer(this.LeaseContainer)
                .Build();

            await processor.StartAsync();

            // Letting processor initialize
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);

            // Inserting documents
            foreach (int id in Enumerable.Range(0, 10))
            {
                await this.Container.CreateItemAsync<dynamic>(new { id = id.ToString() });
            }

            // Waiting on all notifications to finish
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedCleanupTime);
            await processor.StopAsync();

            ManualResetEvent manualResetEvent = new ManualResetEvent(false);
            long receivedEstimation = 0;
            ChangeFeedEstimator estimator = ((ContainerInternal)this.Container)
                .GetChangeFeedEstimator(
                    processorName: "test",
                    this.LeaseContainer);

            // Inserting more documents
            foreach (int id in Enumerable.Range(11, 10))
            {
                await this.Container.CreateItemAsync<dynamic>(new { id = id.ToString() });
            }

            using FeedIterator<ChangeFeedProcessorState> feedIterator = estimator.GetCurrentStateIterator();
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<ChangeFeedProcessorState> response = await feedIterator.ReadNextAsync();
                receivedEstimation += response.Sum(r => r.EstimatedLag);
                Assert.IsTrue(response.Headers.RequestCharge > 0);
                Assert.IsNotNull(response.Diagnostics);
                string asString = response.Diagnostics.ToString();
                Assert.IsTrue(asString.Length > 0);
                Assert.IsTrue(asString.Contains("cosmos-netstandard-sdk"));
            }

            Assert.AreEqual(10, receivedEstimation);
        }
    }
}
