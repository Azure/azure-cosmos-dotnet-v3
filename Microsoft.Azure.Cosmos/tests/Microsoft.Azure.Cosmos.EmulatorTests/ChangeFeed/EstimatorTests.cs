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
        public async Task WhenNoLeasesExistReturn1()
        {
            long? receivedEstimation = 0;
            ChangeFeedProcessor estimator = this.Container
                .GetChangeFeedEstimatorBuilder("test", (long estimation, CancellationToken token) =>
                {
                    receivedEstimation = estimation;
                    return Task.CompletedTask;
                }, TimeSpan.FromSeconds(1))
                .WithLeaseContainer(this.LeaseContainer).Build();

            await estimator.StartAsync();
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedSetupTime);
            await estimator.StopAsync();
            Assert.IsTrue(receivedEstimation.HasValue);
            Assert.AreEqual(1, receivedEstimation);
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

            long? receivedEstimation = null;
            ChangeFeedProcessor estimator = this.Container
                .GetChangeFeedEstimatorBuilder("test", (long estimation, CancellationToken token) =>
                {
                    receivedEstimation = estimation;
                    return Task.CompletedTask;
                }, TimeSpan.FromSeconds(1))
                .WithLeaseContainer(this.LeaseContainer).Build();

            await estimator.StartAsync();
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedCleanupTime);
            await estimator.StopAsync();
            Assert.IsTrue(receivedEstimation.HasValue);
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
                .GetChangeFeedProcessorBuilder("test", (IReadOnlyCollection<dynamic> docs, CancellationToken token) =>
                {
                    return Task.CompletedTask;
                })
                .WithInstanceName("random")
                .WithLeaseContainer(this.LeaseContainer).Build();

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

            long? receivedEstimation = null;
            ChangeFeedProcessor estimator = this.Container
                .GetChangeFeedEstimatorBuilder("test", (long estimation, CancellationToken token) =>
                {
                    receivedEstimation = estimation;
                    return Task.CompletedTask;
                }, TimeSpan.FromSeconds(1))
                .WithLeaseContainer(this.LeaseContainer).Build();

            
            // Inserting more documents
            foreach (int id in Enumerable.Range(11, 10))
            {
                await this.Container.CreateItemAsync<dynamic>(new { id = id.ToString() });
            }

            await estimator.StartAsync();
            await Task.Delay(BaseChangeFeedClientHelper.ChangeFeedCleanupTime);
            await estimator.StopAsync();
            Assert.IsTrue(receivedEstimation.HasValue);
            Assert.AreEqual(10, receivedEstimation);
        }
    }
}
