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
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using static Microsoft.Azure.Cosmos.Container;

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
            ChangesEstimationHandler estimationDelegate = (long estimation, CancellationToken token) =>
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
            ChangesEstimationHandler estimationDelegate = (long estimation, CancellationToken token) =>
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
            ChangesEstimationHandler estimationDelegate = (long estimation, CancellationToken token) =>
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
            ChangesEstimationHandler estimationDelegate = (long estimation, CancellationToken token) =>
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
            ChangesEstimationHandler estimationDelegate = (long estimation, CancellationToken token) =>
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

        private static ChangeFeedEstimatorCore CreateEstimator(ChangesEstimationHandler estimationDelegate, out Mock<RemainingWorkEstimator> remainingWorkEstimator)
        {
            remainingWorkEstimator = new Mock<RemainingWorkEstimator>();
            return new ChangeFeedEstimatorCore(estimationDelegate, TimeSpan.FromSeconds(5), remainingWorkEstimator.Object);
        }

        private static ContainerInternal GetMockedContainer(string containerName = null)
        {
            Mock<ContainerInternal> mockedContainer = MockCosmosUtil.CreateMockContainer(containerName: containerName);
            mockedContainer.Setup(c => c.ClientContext).Returns(ChangeFeedEstimatorCoreTests.GetMockedClientContext());
            return mockedContainer.Object;
        }

        private static CosmosClientContext GetMockedClientContext()
        {
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            mockClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));

            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(x => x.ClientOptions).Returns(MockCosmosUtil.GetDefaultConfiguration());
            mockContext.Setup(x => x.Client).Returns(mockClient.Object);
            //mockContext.Setup(x => x.DocumentClient).Returns(new MockDocumentClient());
            return mockContext.Object;
        }
    }
}
