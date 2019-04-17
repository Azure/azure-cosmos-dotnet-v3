//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Client.Core.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class ChangeFeedEstimatorCoreTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void NullDelegateThrows()
        {
            ChangeFeedEstimatorCoreTests.CreateEstimator(null, out Mock<RemainingWorkEstimator> remainingWorkEstimator);
        }

        [TestMethod]
        public void ApplyBuildConfiguration_ValidCustomStore()
        {
            Func<long, CancellationToken, Task> estimationDelegate = (long estimation, CancellationToken token) =>
            {
                return Task.CompletedTask;
            };

            ChangeFeedEstimatorCore estimator = ChangeFeedEstimatorCoreTests.CreateEstimator(estimationDelegate, out Mock<RemainingWorkEstimator> remainingWorkEstimator);
            estimator.ApplyBuildConfiguration(
                Mock.Of<DocumentServiceLeaseStoreManager>(),
                null,
                "something",
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedEstimatorCoreTests.GetMockedContainer("monitored"));
        }

        [TestMethod]
        public void ApplyBuildConfiguration_ValidContainerStore()
        {
            Func<long, CancellationToken, Task> estimationDelegate = (long estimation, CancellationToken token) =>
            {
                return Task.CompletedTask;
            };

            ChangeFeedEstimatorCore estimator = ChangeFeedEstimatorCoreTests.CreateEstimator(estimationDelegate, out Mock<RemainingWorkEstimator> remainingWorkEstimator);
            estimator.ApplyBuildConfiguration(
                null,
                ChangeFeedEstimatorCoreTests.GetMockedContainer("leases"),
                "something",
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedEstimatorCoreTests.GetMockedContainer("monitored"));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ApplyBuildConfiguration_ValidatesNullStore()
        {
            Func<long, CancellationToken, Task> estimationDelegate = (long estimation, CancellationToken token) =>
            {
                return Task.CompletedTask;
            };

            ChangeFeedEstimatorCore estimator = ChangeFeedEstimatorCoreTests.CreateEstimator(estimationDelegate, out Mock<RemainingWorkEstimator> remainingWorkEstimator);
            estimator.ApplyBuildConfiguration(
                null,
                null,
                "something",
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedEstimatorCoreTests.GetMockedContainer("monitored"));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ApplyBuildConfiguration_ValidatesNullMonitoredContainer()
        {
            Func<long, CancellationToken, Task> estimationDelegate = (long estimation, CancellationToken token) =>
            {
                return Task.CompletedTask;
            };

            ChangeFeedEstimatorCore estimator = ChangeFeedEstimatorCoreTests.CreateEstimator(estimationDelegate, out Mock<RemainingWorkEstimator> remainingWorkEstimator);
            estimator.ApplyBuildConfiguration(
                null,
                ChangeFeedEstimatorCoreTests.GetMockedContainer("leases"),
                "something",
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                null);
        }

        [TestMethod]
        public async Task StartAsync_TriggersDelegate()
        {
            const long remainingWork = 15;
            long estimationDelegateValue = 0;
            bool receivedEstimation = false;
            Func<long, CancellationToken, Task> estimationDelegate = (long estimation, CancellationToken token) =>
            {
                estimationDelegateValue = estimation;
                receivedEstimation = true;
                return Task.CompletedTask;
            };

            Mock<DocumentServiceLeaseStoreManager> leaseStoreManager = new Mock<DocumentServiceLeaseStoreManager>();
            leaseStoreManager.Setup(l => l.LeaseContainer).Returns(Mock.Of<DocumentServiceLeaseContainer>);
            leaseStoreManager.Setup(l => l.LeaseManager).Returns(Mock.Of<DocumentServiceLeaseManager>);
            leaseStoreManager.Setup(l => l.LeaseStore).Returns(Mock.Of<DocumentServiceLeaseStore>);
            leaseStoreManager.Setup(l => l.LeaseCheckpointer).Returns(Mock.Of<DocumentServiceLeaseCheckpointer>);

            ChangeFeedEstimatorCore estimator = ChangeFeedEstimatorCoreTests.CreateEstimator(estimationDelegate, out Mock<RemainingWorkEstimator> remainingWorkEstimator);
            estimator.ApplyBuildConfiguration(
                leaseStoreManager.Object,
                null,
                "something",
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedEstimatorCoreTests.GetMockedContainer("monitored"));

            remainingWorkEstimator.Setup(r => r.GetEstimatedRemainingWorkAsync(It.IsAny<CancellationToken>())).ReturnsAsync(remainingWork);

            await estimator.StartAsync();

            int waitIterations = 0; // Failsafe in case someone breaks the estimator so this does not run forever
            while (!receivedEstimation && waitIterations++ < 3)
            {
                Thread.Sleep(TimeSpan.FromSeconds(10));
            }

            Assert.AreEqual(remainingWork, estimationDelegateValue);
        }

        private static ChangeFeedEstimatorCore CreateEstimator(Func<long, CancellationToken, Task> estimationDelegate, out Mock<RemainingWorkEstimator> remainingWorkEstimator)
        {
            remainingWorkEstimator = new Mock<RemainingWorkEstimator>();
            return new ChangeFeedEstimatorCore(estimationDelegate, TimeSpan.FromSeconds(5), remainingWorkEstimator.Object);
        }

        private static CosmosContainer GetMockedContainer(string containerName = "myColl")
        {
            Mock<CosmosContainer> mockedContainer = new Mock<CosmosContainer>();
            mockedContainer.Setup(c => c.LinkUri).Returns(new Uri("/dbs/myDb/colls/" + containerName, UriKind.Relative));
            mockedContainer.Setup(c => c.Client).Returns(ChangeFeedEstimatorCoreTests.GetMockedClient());
            return mockedContainer.Object;
        }

        private static CosmosClient GetMockedClient()
        {
            return MockDocumentClient.CreateMockCosmosClient();
        }
    }
}
