//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Tests;
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
            ChangeFeedProcessorCore<MyDocument> processor = ChangeFeedProcessorCoreTests.CreateProcessor(out _, out _);
            processor.ApplyBuildConfiguration(
                null,
                null,
                "something",
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ApplyBuildConfiguration_ValidatesNullInstance()
        {
            ChangeFeedProcessorCore<MyDocument> processor = ChangeFeedProcessorCoreTests.CreateProcessor(out _, out _);
            processor.ApplyBuildConfiguration(
                Mock.Of<DocumentServiceLeaseStoreManager>(),
                null,
                "something",
                null,
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ApplyBuildConfiguration_ValidatesNullMonitoredContainer()
        {
            ChangeFeedProcessorCore<MyDocument> processor = ChangeFeedProcessorCoreTests.CreateProcessor(out _, out _);
            processor.ApplyBuildConfiguration(
                Mock.Of<DocumentServiceLeaseStoreManager>(),
                null,
                "something",
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                null);
        }

        [TestMethod]
        public void ApplyBuildConfiguration_ValidCustomStore()
        {
            ChangeFeedProcessorCore<MyDocument> processor = ChangeFeedProcessorCoreTests.CreateProcessor(out _, out _);
            processor.ApplyBuildConfiguration(
                Mock.Of<DocumentServiceLeaseStoreManager>(),
                null,
                "something",
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));
        }

        [TestMethod]
        public void ApplyBuildConfiguration_ValidContainerStore()
        {
            ChangeFeedProcessorCore<MyDocument> processor = ChangeFeedProcessorCoreTests.CreateProcessor(out _, out _);
            processor.ApplyBuildConfiguration(
                null,
                ChangeFeedProcessorCoreTests.GetMockedContainer("leases"),
                "something",
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

            Mock<DocumentServiceLeaseStoreManager> leaseStoreManager = new Mock<DocumentServiceLeaseStoreManager>();
            leaseStoreManager.Setup(l => l.LeaseContainer).Returns(leaseContainer.Object);
            leaseStoreManager.Setup(l => l.LeaseManager).Returns(Mock.Of<DocumentServiceLeaseManager>);
            leaseStoreManager.Setup(l => l.LeaseStore).Returns(leaseStore.Object);
            leaseStoreManager.Setup(l => l.LeaseCheckpointer).Returns(Mock.Of<DocumentServiceLeaseCheckpointer>);
            ChangeFeedProcessorCore<MyDocument> processor = ChangeFeedProcessorCoreTests.CreateProcessor(out Mock<ChangeFeedObserverFactory<MyDocument>> factory, out Mock<ChangeFeedObserver<MyDocument>> observer);
            processor.ApplyBuildConfiguration(
                leaseStoreManager.Object,
                null,
                "something",
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

            Mock<DocumentServiceLeaseStoreManager> leaseStoreManager = new Mock<DocumentServiceLeaseStoreManager>();
            leaseStoreManager.Setup(l => l.LeaseContainer).Returns(leaseContainer.Object);
            leaseStoreManager.Setup(l => l.LeaseManager).Returns(Mock.Of<DocumentServiceLeaseManager>);
            leaseStoreManager.Setup(l => l.LeaseStore).Returns(leaseStore.Object);
            leaseStoreManager.Setup(l => l.LeaseCheckpointer).Returns(Mock.Of<DocumentServiceLeaseCheckpointer>);
            ChangeFeedProcessorCore<MyDocument> processor = ChangeFeedProcessorCoreTests.CreateProcessor(out Mock<ChangeFeedObserverFactory<MyDocument>> factory, out Mock<ChangeFeedObserver<MyDocument>> observer);
            processor.ApplyBuildConfiguration(
                leaseStoreManager.Object,
                null,
                "something",
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));

            await processor.StartAsync();

            Mock.Get(factory.Object)
                .Verify(mock => mock.CreateObserver(), Times.Once);

            Mock.Get(observer.Object)
                .Verify(mock => mock.OpenAsync(It.Is<ChangeFeedProcessorContext>((context) => context.LeaseToken == ownedLeases.First().CurrentLeaseToken)), Times.Once);
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
            ChangeFeedProcessorCore<MyDocument> processor = ChangeFeedProcessorCoreTests.CreateProcessor(out Mock<ChangeFeedObserverFactory<MyDocument>> factory, out Mock<ChangeFeedObserver<MyDocument>> observer);
            processor.ApplyBuildConfiguration(
                leaseStoreManager.Object,
                null,
                "something",
                "instanceName",
                new ChangeFeedLeaseOptions(),
                new ChangeFeedProcessorOptions(),
                ChangeFeedProcessorCoreTests.GetMockedContainer("monitored"));

            await processor.StartAsync();

            await processor.StopAsync();
            Mock.Get(leaseContainer.Object)
                .Verify(store => store.GetAllLeasesAsync(), Times.Once);
        }


        private static ChangeFeedProcessorCore<MyDocument> CreateProcessor(
            out Mock<ChangeFeedObserverFactory<MyDocument>> factory,
            out Mock<ChangeFeedObserver<MyDocument>> observer)
        {
            factory = new Mock<ChangeFeedObserverFactory<MyDocument>>();
            observer = new Mock<ChangeFeedObserver<MyDocument>>();
            factory.Setup(f => f.CreateObserver()).Returns(observer.Object);

            return new ChangeFeedProcessorCore<MyDocument>(factory.Object);
        }

        public class MyDocument
        {
            public string id { get; set; }
        }

        private static ContainerInternal GetMockedContainer(string containerName = null)
        {
            Mock<ContainerInternal> mockedContainer = MockCosmosUtil.CreateMockContainer(containerName: containerName);
            mockedContainer.Setup(c => c.ClientContext).Returns(ChangeFeedProcessorCoreTests.GetMockedClientContext());
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
