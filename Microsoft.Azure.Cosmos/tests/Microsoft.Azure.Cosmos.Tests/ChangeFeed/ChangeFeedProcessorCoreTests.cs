//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class ChangeFeedProcessorCoreTests
    {

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ApplyBuildConfiguration_ValidatesNullStore()
        {
            ChangeFeedProcessorCore processor = ChangeFeedProcessorCoreTests.CreateProcessor(out _, out _);
            processor.ApplyBuildConfiguration(
                null,
                null,
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ApplyBuildConfiguration_ValidatesNullInstance()
        {
            ChangeFeedProcessorCore processor = ChangeFeedProcessorCoreTests.CreateProcessor(out _, out _);
            processor.ApplyBuildConfiguration(
                Mock.Of<DocumentServiceLeaseStoreManager>(),
                null,
                null,
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ApplyBuildConfiguration_ValidatesNullMonitoredContainer()
        {
            ChangeFeedProcessorCore processor = ChangeFeedProcessorCoreTests.CreateProcessor(out _, out _);
            processor.ApplyBuildConfiguration(
                Mock.Of<DocumentServiceLeaseStoreManager>(),
                null,
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                null);
        }

        [TestMethod]
        public void ApplyBuildConfiguration_ValidCustomStore()
        {
            ChangeFeedProcessorCore processor = ChangeFeedProcessorCoreTests.CreateProcessor(out _, out _);
            processor.ApplyBuildConfiguration(
                Mock.Of<DocumentServiceLeaseStoreManager>(),
                null,
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));
        }

        [TestMethod]
        public void ApplyBuildConfiguration_ValidContainerStore()
        {
            ChangeFeedProcessorCore processor = ChangeFeedProcessorCoreTests.CreateProcessor(out _, out _);
            processor.ApplyBuildConfiguration(
                null,
                ChangeFeedProcessorCoreTests.GetMockedContainer("leases"),
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));
        }

        [TestMethod]
        public async Task StartAsync()
        {
            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(true);

            Mock<DocumentServiceLeaseContainer> leaseContainer = new Mock<DocumentServiceLeaseContainer>();
            leaseContainer.Setup(l => l.GetOwnedLeasesAsync()).Returns(Task.FromResult(Enumerable.Empty<DocumentServiceLease>()));
            leaseContainer.Setup(l => l.GetAllLeasesAsync()).ReturnsAsync(new List<DocumentServiceLease>());

            Mock<DocumentServiceLeaseStoreManager> leaseStoreManager = new Mock<DocumentServiceLeaseStoreManager>();
            leaseStoreManager.Setup(l => l.LeaseContainer).Returns(leaseContainer.Object);
            leaseStoreManager.Setup(l => l.LeaseManager).Returns(Mock.Of<DocumentServiceLeaseManager>);
            leaseStoreManager.Setup(l => l.LeaseStore).Returns(leaseStore.Object);
            leaseStoreManager.Setup(l => l.LeaseCheckpointer).Returns(Mock.Of<DocumentServiceLeaseCheckpointer>);
            ChangeFeedProcessorCore processor = null;
            try
            {
                processor = ChangeFeedProcessorCoreTests.CreateProcessor(out Mock<ChangeFeedObserverFactory> factory, out Mock<ChangeFeedObserver> observer);
                processor.ApplyBuildConfiguration(
                leaseStoreManager.Object,
                null,
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));

                await processor.StartAsync();
                Mock.Get(leaseStore.Object)
                    .Verify(store => store.IsInitializedAsync(), Times.Once);
                Mock.Get(leaseContainer.Object)
                    .Verify(store => store.GetOwnedLeasesAsync(), Times.Once);
            }
            finally
            {
                if (processor != null)
                {
                    await processor.StopAsync();
                }
            }
        }

        [TestMethod]
        public async Task ObserverIsCreated()
        {
            IEnumerable<DocumentServiceLease> ownedLeases = new List<DocumentServiceLease>()
            {
                new DocumentServiceLeaseCore()
                {
                    LeaseId = "0",
                    LeaseToken = "0"
                }
            };

            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(true);

            Mock<DocumentServiceLeaseContainer> leaseContainer = new Mock<DocumentServiceLeaseContainer>();
            leaseContainer.Setup(l => l.GetOwnedLeasesAsync()).Returns(Task.FromResult(ownedLeases));
            leaseContainer.Setup(l => l.GetAllLeasesAsync()).ReturnsAsync(new List<DocumentServiceLease>());

            Mock<DocumentServiceLeaseStoreManager> leaseStoreManager = new Mock<DocumentServiceLeaseStoreManager>();
            leaseStoreManager.Setup(l => l.LeaseContainer).Returns(leaseContainer.Object);
            leaseStoreManager.Setup(l => l.LeaseManager).Returns(Mock.Of<DocumentServiceLeaseManager>);
            leaseStoreManager.Setup(l => l.LeaseStore).Returns(leaseStore.Object);
            leaseStoreManager.Setup(l => l.LeaseCheckpointer).Returns(Mock.Of<DocumentServiceLeaseCheckpointer>);
            ChangeFeedProcessorCore processor = null;
            try
            {
                processor = ChangeFeedProcessorCoreTests.CreateProcessor(out Mock<ChangeFeedObserverFactory> factory, out Mock<ChangeFeedObserver> observer);
                processor.ApplyBuildConfiguration(
                leaseStoreManager.Object,
                null,
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));

                await processor.StartAsync();

                Mock.Get(factory.Object)
                    .Verify(mock => mock.CreateObserver(), Times.Once);

                Mock.Get(observer.Object)
                    .Verify(mock => mock.OpenAsync(It.Is<string>((lt) => lt == ownedLeases.First().CurrentLeaseToken)), Times.Once);
            }
            finally
            {
                if (processor != null)
                {
                    await processor.StopAsync();
                }
            }
        }

        [TestMethod]
        public async Task StopAsync()
        {
            Mock<DocumentServiceLeaseStore> leaseStore = new Mock<DocumentServiceLeaseStore>();
            leaseStore.Setup(l => l.IsInitializedAsync()).ReturnsAsync(true);

            Mock<DocumentServiceLeaseContainer> leaseContainer = new Mock<DocumentServiceLeaseContainer>();
            leaseContainer.Setup(l => l.GetOwnedLeasesAsync()).Returns(Task.FromResult(Enumerable.Empty<DocumentServiceLease>()));
            leaseContainer.Setup(l => l.GetAllLeasesAsync()).ReturnsAsync(new List<DocumentServiceLease>());

            Mock<DocumentServiceLeaseStoreManager> leaseStoreManager = new Mock<DocumentServiceLeaseStoreManager>();
            leaseStoreManager.Setup(l => l.LeaseContainer).Returns(leaseContainer.Object);
            leaseStoreManager.Setup(l => l.LeaseManager).Returns(Mock.Of<DocumentServiceLeaseManager>);
            leaseStoreManager.Setup(l => l.LeaseStore).Returns(leaseStore.Object);
            leaseStoreManager.Setup(l => l.LeaseCheckpointer).Returns(Mock.Of<DocumentServiceLeaseCheckpointer>);
            ChangeFeedProcessorCore processor = ChangeFeedProcessorCoreTests.CreateProcessor(out Mock<ChangeFeedObserverFactory> factory, out Mock<ChangeFeedObserver> observer);
            processor.ApplyBuildConfiguration(
                leaseStoreManager.Object,
                null,
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));

            await processor.StartAsync();

            await processor.StopAsync();
            Mock.Get(leaseContainer.Object)
                .Verify(store => store.GetAllLeasesAsync(), Times.Exactly(2));
        }


        private static ChangeFeedProcessorCore CreateProcessor(
            out Mock<ChangeFeedObserverFactory> factory,
            out Mock<ChangeFeedObserver> observer)
        {
            factory = new Mock<ChangeFeedObserverFactory>();
            observer = new Mock<ChangeFeedObserver>();
            factory.Setup(f => f.CreateObserver()).Returns(observer.Object);

            return new ChangeFeedProcessorCore(factory.Object);
        }

        public class MyDocument
        {
            public string id { get; set; }
        }

        private static ContainerInternal GetMockedContainer(string containerName = null)
        {
            Mock<ContainerInternal> mockedContainer = MockCosmosUtil.CreateMockContainer(containerName: containerName);
            mockedContainer.Setup(c => c.ClientContext).Returns(ChangeFeedProcessorCoreTests.GetMockedClientContext());
            string monitoredContainerRid = "V4lVAMl0wuQ=";
            mockedContainer.Setup(c => c.GetCachedRIDAsync(It.IsAny<bool>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>())).ReturnsAsync(monitoredContainerRid);
            Mock<DatabaseInternal> mockedDatabase = MockCosmosUtil.CreateMockDatabase();
            mockedContainer.Setup(c => c.Database).Returns(mockedDatabase.Object);
            return mockedContainer.Object;
        }

        private static CosmosClientContext GetMockedClientContext()
        {
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            mockClient.Setup(x => x.Endpoint).Returns(new Uri("http://localhost"));

            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(x => x.ClientOptions).Returns(MockCosmosUtil.GetDefaultConfiguration());
            mockContext.Setup(x => x.DocumentClient).Returns(new MockDocumentClient());
            mockContext.Setup(x => x.SerializerCore).Returns(MockCosmosUtil.Serializer);
            mockContext.Setup(x => x.Client).Returns(mockClient.Object);
            return mockContext.Object;
        }
    }
}