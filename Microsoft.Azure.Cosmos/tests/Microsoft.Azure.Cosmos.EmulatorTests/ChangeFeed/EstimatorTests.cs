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
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class EstimatorTests : BaseCosmosClientHelper
    {
        private CosmosContainer Container = null;
        private CosmosContainer LeaseContainer = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/id";
            CosmosContainerResponse response = await this.database.Containers.CreateContainerAsync(
                new CosmosContainerSettings(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);
            this.Container = response;


            response = await this.database.Containers.CreateContainerAsync(
                new CosmosContainerSettings(id: "leases", partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);

            this.LeaseContainer = response;
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
            ChangeFeedProcessor estimator = this.Container.Items
                .CreateChangeFeedProcessorBuilder("noleases", (long estimation, CancellationToken token) =>
                {
                    receivedEstimation = estimation;
                    return Task.CompletedTask;
                }, TimeSpan.FromSeconds(1))
                .WithCosmosLeaseContainer(this.LeaseContainer).Build();

            await estimator.StartAsync();
            await Task.Delay(1000);
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
            ChangeFeedProcessor processor = this.Container.Items
                .CreateChangeFeedProcessorBuilder("continuationnull", (IReadOnlyList<dynamic> docs, CancellationToken token) =>
                {
                    return Task.CompletedTask;
                })
                .WithInstanceName("random")
                .WithCosmosLeaseContainer(this.LeaseContainer).Build();

            await processor.StartAsync();
            await processor.StopAsync();

            long? receivedEstimation = null;
            ChangeFeedProcessor estimator = this.Container.Items
                .CreateChangeFeedProcessorBuilder("continuationnull", (long estimation, CancellationToken token) =>
                {
                    receivedEstimation = estimation;
                    return Task.CompletedTask;
                }, TimeSpan.FromSeconds(1))
                .WithCosmosLeaseContainer(this.LeaseContainer).Build();

            await estimator.StartAsync();
            await Task.Delay(1000);
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
            ChangeFeedProcessor processor = this.Container.Items
                .CreateChangeFeedProcessorBuilder("counting", (IReadOnlyList<dynamic> docs, CancellationToken token) =>
                {
                    return Task.CompletedTask;
                })
                .WithInstanceName("random")
                .WithCosmosLeaseContainer(this.LeaseContainer).Build();

            await processor.StartAsync();
            // Letting processor initialize
            await Task.Delay(1000);
            // Inserting documents
            foreach (int id in Enumerable.Range(0, 10))
            {
                await this.Container.Items.CreateItemAsync<dynamic>(id.ToString(), new { id = id.ToString() });
            }

            // Waiting on all notifications to finish
            await Task.Delay(5000);
            await processor.StopAsync();

            long? receivedEstimation = null;
            ChangeFeedProcessor estimator = this.Container.Items
                .CreateChangeFeedProcessorBuilder("counting", (long estimation, CancellationToken token) =>
                {
                    receivedEstimation = estimation;
                    return Task.CompletedTask;
                }, TimeSpan.FromSeconds(1))
                .WithCosmosLeaseContainer(this.LeaseContainer).Build();

            
            // Inserting more documents
            foreach (int id in Enumerable.Range(11, 10))
            {
                await this.Container.Items.CreateItemAsync<dynamic>(id.ToString(), new { id = id.ToString() });
            }

            await estimator.StartAsync();
            await Task.Delay(1000); // Let the estimator delegate fire at least once
            await estimator.StopAsync();
            Assert.IsTrue(receivedEstimation.HasValue);
            Assert.AreEqual(10, receivedEstimation);
        }
    }
}
