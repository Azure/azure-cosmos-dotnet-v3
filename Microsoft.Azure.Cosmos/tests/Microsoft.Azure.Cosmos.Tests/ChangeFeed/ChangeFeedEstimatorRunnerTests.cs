//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using static Microsoft.Azure.Cosmos.Container;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class ChangeFeedEstimatorRunnerTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void NullDelegateThrows()
        {
            ChangesEstimationHandler changesEstimationHandler = null;
            ChangeFeedEstimatorRunnerTests.CreateEstimator(changesEstimationHandler, out Mock<ChangeFeedEstimator> _);
        }

        [TestMethod]
        public void ApplyBuildConfiguration_ValidCustomStore()
        {
            static Task estimationDelegate(long estimation, CancellationToken token)
            {
                return Task.CompletedTask;
            }

            ChangeFeedEstimatorRunner estimator = ChangeFeedEstimatorRunnerTests.CreateEstimator(estimationDelegate, out Mock<ChangeFeedEstimator> remainingWorkEstimator);
            estimator.ApplyBuildConfiguration(
                Mock.Of<DocumentServiceLeaseStoreManager>(),
                null,
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedEstimatorRunnerTests.GetMockedContainer("monitored"));
        }

        [TestMethod]
        public void ApplyBuildConfiguration_ValidContainerStore()
        {
            static Task estimationDelegate(long estimation, CancellationToken token)
            {
                return Task.CompletedTask;
            }

            ChangeFeedEstimatorRunner estimator = ChangeFeedEstimatorRunnerTests.CreateEstimator(estimationDelegate, out Mock<ChangeFeedEstimator> remainingWorkEstimator);
            estimator.ApplyBuildConfiguration(
                null,
                ChangeFeedEstimatorRunnerTests.GetMockedContainer("leases"),
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedEstimatorRunnerTests.GetMockedContainer("monitored"));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ApplyBuildConfiguration_ValidatesNullStore()
        {
            static Task estimationDelegate(long estimation, CancellationToken token)
            {
                return Task.CompletedTask;
            }

            ChangeFeedEstimatorRunner estimator = ChangeFeedEstimatorRunnerTests.CreateEstimator(estimationDelegate, out Mock<ChangeFeedEstimator> remainingWorkEstimator);
            estimator.ApplyBuildConfiguration(
                null,
                null,
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedEstimatorRunnerTests.GetMockedContainer("monitored"));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ApplyBuildConfiguration_ValidatesNullMonitoredContainer()
        {
            static Task estimationDelegate(long estimation, CancellationToken token)
            {
                return Task.CompletedTask;
            }

            ChangeFeedEstimatorRunner estimator = ChangeFeedEstimatorRunnerTests.CreateEstimator(estimationDelegate, out Mock<ChangeFeedEstimator> remainingWorkEstimator);
            estimator.ApplyBuildConfiguration(
                null,
                ChangeFeedEstimatorRunnerTests.GetMockedContainer("leases"),
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
            Task estimationDelegate(long estimation, CancellationToken token)
            {
                estimationDelegateValue = estimation;
                receivedEstimation = true;
                return Task.CompletedTask;
            }

            Mock<DocumentServiceLeaseStoreManager> leaseStoreManager = new Mock<DocumentServiceLeaseStoreManager>();
            leaseStoreManager.Setup(l => l.LeaseContainer).Returns(Mock.Of<DocumentServiceLeaseContainer>);
            leaseStoreManager.Setup(l => l.LeaseManager).Returns(Mock.Of<DocumentServiceLeaseManager>);
            leaseStoreManager.Setup(l => l.LeaseStore).Returns(Mock.Of<DocumentServiceLeaseStore>);
            leaseStoreManager.Setup(l => l.LeaseCheckpointer).Returns(Mock.Of<DocumentServiceLeaseCheckpointer>);

            ChangeFeedEstimatorRunner estimator = null;
            try
            {
                estimator = ChangeFeedEstimatorRunnerTests.CreateEstimator(estimationDelegate, out Mock<ChangeFeedEstimator> remainingWorkEstimator);
                estimator.ApplyBuildConfiguration(
                    leaseStoreManager.Object,
                    null,
                    "instanceName",
                    new ChangeFeedLeaseOptions(),
                    new ChangeFeedProcessorOptions(),
                    ChangeFeedEstimatorRunnerTests.GetMockedContainer("monitored"));

                Mock<FeedResponse<ChangeFeedProcessorState>> mockedResponse = new Mock<FeedResponse<ChangeFeedProcessorState>>();
                mockedResponse.Setup(r => r.Count).Returns(1);
                mockedResponse.Setup(r => r.GetEnumerator()).Returns(new List<ChangeFeedProcessorState>() { new ChangeFeedProcessorState(string.Empty, remainingWork, string.Empty) }.GetEnumerator());

                Mock<FeedIterator<ChangeFeedProcessorState>> mockedIterator = new Mock<FeedIterator<ChangeFeedProcessorState>>();
                mockedIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);

                remainingWorkEstimator.Setup(e => e.GetCurrentStateIterator(It.IsAny<ChangeFeedEstimatorRequestOptions>())).Returns(mockedIterator.Object);

                await estimator.StartAsync();

                int waitIterations = 0; // Failsafe in case someone breaks the estimator so this does not run forever
                while (!receivedEstimation && waitIterations++ < 3)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(10));
                }

                Assert.AreEqual(remainingWork, estimationDelegateValue);
            }
            finally
            {
                if (estimator != null)
                {
                    await estimator.StopAsync();
                }
            }
        }

        [TestMethod]
        public async Task StartAsync_ThrowsIfStartedTwice()
        {
            static Task estimationDelegate(long estimation, CancellationToken token)
            {
                return Task.CompletedTask;
            }

            Mock<DocumentServiceLeaseStoreManager> leaseStoreManager = new Mock<DocumentServiceLeaseStoreManager>();
            leaseStoreManager.Setup(l => l.LeaseContainer).Returns(Mock.Of<DocumentServiceLeaseContainer>);
            leaseStoreManager.Setup(l => l.LeaseManager).Returns(Mock.Of<DocumentServiceLeaseManager>);
            leaseStoreManager.Setup(l => l.LeaseStore).Returns(Mock.Of<DocumentServiceLeaseStore>);
            leaseStoreManager.Setup(l => l.LeaseCheckpointer).Returns(Mock.Of<DocumentServiceLeaseCheckpointer>);

            ChangeFeedEstimatorRunner estimator = null;
            try
            {
                estimator = ChangeFeedEstimatorRunnerTests.CreateEstimator(estimationDelegate, out Mock<ChangeFeedEstimator> remainingWorkEstimator);
                estimator.ApplyBuildConfiguration(
                    leaseStoreManager.Object,
                    ChangeFeedEstimatorRunnerTests.GetMockedContainer("leases"),
                    "instanceName",
                    new ChangeFeedLeaseOptions(),
                    new ChangeFeedProcessorOptions(),
                    ChangeFeedEstimatorRunnerTests.GetMockedContainer("monitored"));

                Mock<FeedResponse<ChangeFeedProcessorState>> mockedResponse = new Mock<FeedResponse<ChangeFeedProcessorState>>();
                mockedResponse.Setup(r => r.Count).Returns(1);
                mockedResponse.Setup(r => r.GetEnumerator()).Returns(new List<ChangeFeedProcessorState>() { new ChangeFeedProcessorState(string.Empty, 0, string.Empty) }.GetEnumerator());

                Mock<FeedIterator<ChangeFeedProcessorState>> mockedIterator = new Mock<FeedIterator<ChangeFeedProcessorState>>();
                mockedIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);

                remainingWorkEstimator.Setup(e => e.GetCurrentStateIterator(It.IsAny<ChangeFeedEstimatorRequestOptions>())).Returns(mockedIterator.Object);

                await estimator.StartAsync();

                await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => estimator.StartAsync());
            }
            finally
            {
                if (estimator != null)
                {
                    await estimator.StopAsync();
                }
            }
        }

        [TestMethod]
        public async Task StartAsync_CanStopAndStart()
        {
            static Task estimationDelegate(long estimation, CancellationToken token)
            {
                return Task.CompletedTask;
            }

            Mock<DocumentServiceLeaseStoreManager> leaseStoreManager = new Mock<DocumentServiceLeaseStoreManager>();
            leaseStoreManager.Setup(l => l.LeaseContainer).Returns(Mock.Of<DocumentServiceLeaseContainer>);
            leaseStoreManager.Setup(l => l.LeaseManager).Returns(Mock.Of<DocumentServiceLeaseManager>);
            leaseStoreManager.Setup(l => l.LeaseStore).Returns(Mock.Of<DocumentServiceLeaseStore>);
            leaseStoreManager.Setup(l => l.LeaseCheckpointer).Returns(Mock.Of<DocumentServiceLeaseCheckpointer>);

            ChangeFeedEstimatorRunner estimator = null;
            try
            {
                estimator = ChangeFeedEstimatorRunnerTests.CreateEstimator(estimationDelegate, out Mock<ChangeFeedEstimator> remainingWorkEstimator);
                estimator.ApplyBuildConfiguration(
                    leaseStoreManager.Object,
                    null,
                    "instanceName",
                    new ChangeFeedLeaseOptions(),
                    new ChangeFeedProcessorOptions(),
                    ChangeFeedEstimatorRunnerTests.GetMockedContainer("monitored"));

                Mock<FeedResponse<ChangeFeedProcessorState>> mockedResponse = new Mock<FeedResponse<ChangeFeedProcessorState>>();
                mockedResponse.Setup(r => r.Count).Returns(1);
                mockedResponse.Setup(r => r.GetEnumerator()).Returns(new List<ChangeFeedProcessorState>() { new ChangeFeedProcessorState(string.Empty, 0, string.Empty) }.GetEnumerator());

                Mock<FeedIterator<ChangeFeedProcessorState>> mockedIterator = new Mock<FeedIterator<ChangeFeedProcessorState>>();
                mockedIterator.Setup(i => i.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(mockedResponse.Object);

                remainingWorkEstimator.Setup(e => e.GetCurrentStateIterator(It.IsAny<ChangeFeedEstimatorRequestOptions>())).Returns(mockedIterator.Object);

                await estimator.StartAsync();

                await estimator.StopAsync();

                await estimator.StartAsync();
            }
            finally
            {
                if (estimator != null)
                {
                    await estimator.StopAsync();
                }
            }
        }

        private static ChangeFeedEstimatorRunner CreateEstimator(ChangesEstimationHandler estimationDelegate, out Mock<ChangeFeedEstimator> remainingWorkEstimator)
        {
            remainingWorkEstimator = new Mock<ChangeFeedEstimator>();
            return new ChangeFeedEstimatorRunner(estimationDelegate, TimeSpan.FromSeconds(5), remainingWorkEstimator.Object);
        }

        private static ContainerInternal GetMockedContainer(string containerName = null)
        {
            Mock<ContainerInternal> mockedContainer = MockCosmosUtil.CreateMockContainer(containerName: containerName);
            mockedContainer.Setup(c => c.ClientContext).Returns(ChangeFeedEstimatorRunnerTests.GetMockedClientContext());
            return mockedContainer.Object;
        }

        private static CosmosClientContext GetMockedClientContext()
        {
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            mockClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));

            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(x => x.ClientOptions).Returns(MockCosmosUtil.GetDefaultConfiguration());
            mockContext.Setup(x => x.Client).Returns(mockClient.Object);
            return mockContext.Object;
        }
    }
}