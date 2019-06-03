//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class CosmosContainerChangeFeedTests : BaseChangeFeedClientHelper
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
        [ExpectedException(typeof(ArgumentNullException))]
        public void Processor_NullWorkflowName()
        {
            this.Container.CreateChangeFeedProcessor<dynamic>(
                null, 
                "instanceName", 
                this.LeaseContainer,
                (IReadOnlyCollection<dynamic> docs, CancellationToken token) =>
                {
                    return Task.CompletedTask;
                });
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Processor_NullInstanceName()
        {
            this.Container.CreateChangeFeedProcessor<dynamic>(
                "workFlowName",
                null,
                this.LeaseContainer,
                (IReadOnlyCollection<dynamic> docs, CancellationToken token) =>
                {
                    return Task.CompletedTask;
                });
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Processor_NullLeaseContainer()
        {
            this.Container.CreateChangeFeedProcessor<dynamic>(
                "workFlowName",
                "instanceName",
                null,
                (IReadOnlyCollection<dynamic> docs, CancellationToken token) =>
                {
                    return Task.CompletedTask;
                });
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Processor_NullDelegate()
        {
            this.Container.CreateChangeFeedProcessor<dynamic>(
                "workFlowName",
                "instanceName",
                this.LeaseContainer,
                null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Estimator_NullWorkflowName()
        {
            this.Container.CreateChangeFeedEstimator(
                null,
                this.LeaseContainer,
                (long estimation, CancellationToken token) =>
                {
                    return Task.CompletedTask;
                });
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Estimator_NullLeaseContainer()
        {
            this.Container.CreateChangeFeedEstimator(
                "workflowName",
                null,
                (long estimation, CancellationToken token) =>
                {
                    return Task.CompletedTask;
                });
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Estimator_NullDelegate()
        {
            this.Container.CreateChangeFeedEstimator(
                "workflowName",
                this.LeaseContainer,
                null);
        }

        [TestMethod]
        public void Processor_PassesOptions()
        {
            ChangeFeedProcessorOptions options = new ChangeFeedProcessorOptions()
                {
                    FeedPollDelay = TimeSpan.FromMinutes(15),
                    MaxItemCount = 20,
                    StartFromBeginning = true,
                    StartTime = DateTime.UtcNow
                };

            ChangeFeedProcessor processor = this.Container.CreateChangeFeedProcessor<dynamic>(
                "workFlowName",
                "instanceName",
                this.LeaseContainer,
                (IReadOnlyCollection<dynamic> docs, CancellationToken token) =>
                {
                    return Task.CompletedTask;
                },
                options);

            ChangeFeedProcessorCore<dynamic> processorAsCore = processor as ChangeFeedProcessorCore<dynamic>;

            Assert.AreEqual(options.FeedPollDelay, processorAsCore.changeFeedProcessorOptions.FeedPollDelay);
            Assert.AreEqual(options.MaxItemCount, processorAsCore.changeFeedProcessorOptions.MaxItemCount);
            Assert.AreEqual(options.StartFromBeginning, processorAsCore.changeFeedProcessorOptions.StartFromBeginning);
            Assert.AreEqual(options.StartTime, processorAsCore.changeFeedProcessorOptions.StartTime);
        }

        [TestMethod]
        public void Processor_PassesLeaseOptions()
        {
            ChangeFeedProcessorOptions options = new ChangeFeedProcessorOptions()
            {
                FeedPollDelay = TimeSpan.FromMinutes(15)
            };

            ChangeFeedLeaseOptions leaseOptions = new ChangeFeedLeaseOptions()
            {
                LeaseAcquireInterval = TimeSpan.FromSeconds(1),
                LeaseExpirationInterval = TimeSpan.FromSeconds(2),
                LeaseRenewInterval = TimeSpan.FromSeconds(3)
            };

            ChangeFeedProcessor processor = this.Container.CreateChangeFeedProcessor<dynamic>(
                "workFlowName",
                "instanceName",
                this.LeaseContainer,
                (IReadOnlyCollection<dynamic> docs, CancellationToken token) =>
                {
                    return Task.CompletedTask;
                },
                options,
                leaseOptions);

            ChangeFeedProcessorCore<dynamic> processorAsCore = processor as ChangeFeedProcessorCore<dynamic>;

            Assert.AreEqual(leaseOptions.LeaseAcquireInterval, processorAsCore.changeFeedLeaseOptions.LeaseAcquireInterval);
            Assert.AreEqual(leaseOptions.LeaseExpirationInterval, processorAsCore.changeFeedLeaseOptions.LeaseExpirationInterval);
            Assert.AreEqual(leaseOptions.LeaseRenewInterval, processorAsCore.changeFeedLeaseOptions.LeaseRenewInterval);
        }
    }
}