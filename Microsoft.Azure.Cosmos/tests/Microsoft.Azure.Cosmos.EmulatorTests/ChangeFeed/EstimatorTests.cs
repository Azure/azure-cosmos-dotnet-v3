//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class EstimatorTests : BaseChangeFeedClientHelper
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
        public async Task WhenNoLeasesExist_Pull()
        {
            ChangeFeedEstimator estimator = this.Container
                .GetChangeFeedEstimator(
                    processorName: "test",
                    this.LeaseContainer);

            long receivedEstimation = 0;
            using FeedIterator<RemainingLeaseWork> feedIterator = estimator.GetRemainingLeaseWorkIterator();
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<RemainingLeaseWork> response = await feedIterator.ReadNextAsync();
                receivedEstimation += response.Sum(r => r.RemainingWork);
            }

            Assert.AreEqual(0, receivedEstimation);
        }

        /// <summary>
        /// This test checks that when the ContinuationToken is null, we send the StartFromBeginning flag, but since there is no documents, it returns 0
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task WhenLeasesHaveContinuationTokenNullReturn0()
        {
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder("test", (IReadOnlyCollection<dynamic> docs, CancellationToken token) =>
                {
                    return Task.CompletedTask;
                })
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
            Assert.AreEqual(0, receivedEstimation);
        }

        [TestMethod]
        public async Task WhenLeasesHaveContinuationTokenNullReturn0_Pull()
        {
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder("test", (IReadOnlyCollection<dynamic> docs, CancellationToken token) =>
                {
                    return Task.CompletedTask;
                })
                .WithInstanceName("random")
                .WithLeaseContainer(this.LeaseContainer).Build();

            await processor.StartAsync();
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedCleanupTime);
            await processor.StopAsync();

            long receivedEstimation = 0;
            ChangeFeedEstimator estimator = this.Container
                .GetChangeFeedEstimator(
                    processorName: "test",
                    this.LeaseContainer);

            using FeedIterator<RemainingLeaseWork> feedIterator = estimator.GetRemainingLeaseWorkIterator();
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<RemainingLeaseWork> response = await feedIterator.ReadNextAsync();
                receivedEstimation += response.Sum(r => r.RemainingWork);
            }

            Assert.AreEqual(0, receivedEstimation);
        }

        /// <summary>
        /// This test checks that when the ContinuationToken is null, we send the StartFromBeginning flag, but since there is no documents, it returns 0
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task CountPendingDocuments()
        {
            ChangeFeedProcessor processor = this.Container
                .GetChangeFeedProcessorBuilder(
                    processorName: "test",
                    onChangesDelegate: (IReadOnlyCollection<dynamic> docs, CancellationToken token) =>
                    {
                        return Task.CompletedTask;
                    })
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
                    onChangesDelegate: (IReadOnlyCollection<dynamic> docs, CancellationToken token) =>
                    {
                        return Task.CompletedTask;
                    })
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
            ChangeFeedEstimator estimator = this.Container
                .GetChangeFeedEstimator(
                    processorName: "test",
                    this.LeaseContainer);

            // Inserting more documents
            foreach (int id in Enumerable.Range(11, 10))
            {
                await this.Container.CreateItemAsync<dynamic>(new { id = id.ToString() });
            }

            using FeedIterator<RemainingLeaseWork> feedIterator = estimator.GetRemainingLeaseWorkIterator();
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<RemainingLeaseWork> response = await feedIterator.ReadNextAsync();
                receivedEstimation += response.Sum(r => r.RemainingWork);
                Assert.IsTrue(response.Headers.RequestCharge > 0);
                Assert.IsNotNull(response.Diagnostics);
                Assert.IsTrue(response.Diagnostics.ToString().Length > 0);
            }

            Assert.AreEqual(10, receivedEstimation);
        }
    }
}
