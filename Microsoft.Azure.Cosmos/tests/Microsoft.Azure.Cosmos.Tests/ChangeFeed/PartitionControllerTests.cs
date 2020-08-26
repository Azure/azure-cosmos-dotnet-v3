//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class PartitionControllerTests
    {
        private readonly DocumentServiceLease lease;
        private readonly DocumentServiceLeaseManager leaseManager;
        private readonly FeedProcessor partitionProcessor;
        private readonly LeaseRenewer leaseRenewer;
        private readonly ChangeFeedObserver<MyDocument> observer;
        private readonly PartitionSynchronizer synchronizer;
        private readonly PartitionController sut;
        private readonly PartitionSupervisorFactory partitionSupervisorFactory;

        public PartitionControllerTests()
        {
            lease = Mock.Of<DocumentServiceLease>();
            Mock.Get(lease)
                .Setup(l => l.CurrentLeaseToken)
                .Returns("partitionId");

            partitionProcessor = MockPartitionProcessor();
            leaseRenewer = MockRenewer();
            observer = MockObserver();
            partitionSupervisorFactory = Mock.Of<PartitionSupervisorFactory>(f => f.Create(lease) == new PartitionSupervisorCore<MyDocument>(lease, observer, partitionProcessor, leaseRenewer));

            leaseManager = Mock.Of<DocumentServiceLeaseManager>();
            Mock.Get(leaseManager).Reset(); // Reset implicit/by default setup of properties.
            Mock.Get(leaseManager)
                .Setup(manager => manager.AcquireAsync(lease))
                .ReturnsAsync(lease);
            Mock.Get(leaseManager)
                .Setup(manager => manager.ReleaseAsync(lease))
                .Returns(Task.CompletedTask);
            DocumentServiceLeaseContainer leaseContainer = Mock.Of<DocumentServiceLeaseContainer>();

            synchronizer = Mock.Of<PartitionSynchronizer>();
            sut = new PartitionControllerCore(leaseContainer, leaseManager, partitionSupervisorFactory, synchronizer);
        }

        [TestInitialize]
        public async Task Setup()
        {
            await sut.InitializeAsync().ConfigureAwait(false);
        }

        [TestCleanup]
        public async Task CleanUp()
        {
            await sut.ShutdownAsync().ConfigureAwait(false);

            Mock.Get(leaseManager)
                .VerifyAll();

            Mock.Get(partitionProcessor)
                .VerifyAll();

            Mock.Get(synchronizer)
                .VerifyAll();
        }

        [TestMethod]
        public async Task AddLease_ShouldAcquireLease_WhenCalled()
        {
            await sut.AddOrUpdateLeaseAsync(lease).ConfigureAwait(false);

            Mock.Get(leaseManager)
                .Verify(manager => manager.AcquireAsync(lease), Times.Once);
        }

        [TestMethod]
        public async Task AddLease_ShouldRunObserver_WhenCalled()
        {
            await sut.AddOrUpdateLeaseAsync(lease).ConfigureAwait(false);

            Mock.Get(partitionProcessor)
                .Verify(p => p.RunAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task AddLease_ShouldntReleaseLease_WhenCalled()
        {
            await sut.AddOrUpdateLeaseAsync(lease).ConfigureAwait(false);

            Mock.Get(partitionProcessor)
                .Verify(p => p.RunAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task AddLease_ShouldIgnorePartitionObserving_IfDuplicateLease()
        {
            await sut.AddOrUpdateLeaseAsync(lease).ConfigureAwait(false);

            FeedProcessor processorDuplicate = MockPartitionProcessor();
            Mock.Get(partitionSupervisorFactory)
                .Setup(f => f.Create(lease))
                .Returns(new PartitionSupervisorCore<MyDocument>(lease, observer, processorDuplicate, leaseRenewer));

            await sut.AddOrUpdateLeaseAsync(lease).ConfigureAwait(false);

            Mock.Get(leaseManager)
                .Verify(manager => manager.AcquireAsync(lease), Times.Once);

            Mock.Get(leaseManager)
                .Verify(manager => manager.UpdatePropertiesAsync(lease), Times.Once);

            Mock.Get(leaseManager)
                .Verify(manager => manager.ReleaseAsync(It.IsAny<DocumentServiceLease>()), Times.Never);

            Mock.Get(partitionProcessor)
                .Verify(p => p.RunAsync(It.IsAny<CancellationToken>()), Times.Once);
            Mock.Get(processorDuplicate)
                .Verify(p => p.RunAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task AddLease_ShouldNotRelease_IfUpdateLeasePropertiesThrows()
        {
            await sut.AddOrUpdateLeaseAsync(lease).ConfigureAwait(false);

            Mock.Get(partitionProcessor)
                .Reset();

            Mock.Get(leaseManager)
                .Reset();

            Mock.Get(leaseManager)
                .Setup(manager => manager.UpdatePropertiesAsync(lease))
                .Throws(new NullReferenceException());

            Mock.Get(leaseManager)
                .Setup(manager => manager.ReleaseAsync(lease))
                .Returns(Task.CompletedTask);

            await Assert.ThrowsExceptionAsync<NullReferenceException>(() => sut.AddOrUpdateLeaseAsync(lease)).ConfigureAwait(false);

            Mock.Get(leaseManager)
                .Verify(manager => manager.ReleaseAsync(It.IsAny<DocumentServiceLease>()), Times.Never);
        }

        [TestMethod]
        public async Task AddLease_ShouldAcquireLease_IfSecondLeaseAdded()
        {
            DocumentServiceLease lease2 = Mock.Of<DocumentServiceLease>();
            Mock.Get(lease2)
                .Setup(l => l.CurrentLeaseToken)
                .Returns("partitionId2");

            Mock.Get(partitionSupervisorFactory)
                .Setup(f => f.Create(lease2))
                .Returns(new PartitionSupervisorCore<MyDocument>(lease2, observer, MockPartitionProcessor(), leaseRenewer));

            await sut.AddOrUpdateLeaseAsync(lease).ConfigureAwait(false);
            await sut.AddOrUpdateLeaseAsync(lease2).ConfigureAwait(false);

            Mock.Get(leaseManager)
                .Verify(manager => manager.AcquireAsync(lease2), Times.Once);
        }

        [TestMethod]
        public async Task AddLease_ShouldRunObserver_IfSecondAdded()
        {
            DocumentServiceLease lease2 = Mock.Of<DocumentServiceLease>();
            Mock.Get(lease2)
                .Setup(l => l.CurrentLeaseToken)
                .Returns("partitionId2");

            FeedProcessor partitionProcessor2 = MockPartitionProcessor();
            Mock.Get(partitionSupervisorFactory)
                .Setup(f => f.Create(lease2))
                .Returns(new PartitionSupervisorCore<MyDocument>(lease2, observer, partitionProcessor2, leaseRenewer));

            await sut.AddOrUpdateLeaseAsync(lease).ConfigureAwait(false);
            await sut.AddOrUpdateLeaseAsync(lease2).ConfigureAwait(false);

            Mock.Get(partitionProcessor2)
                .Verify(p => p.RunAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task Shutdown_ShouldWork_WithoutLeases()
        {
            Mock.Get(leaseManager)
                .Reset();

            Mock.Get(partitionProcessor)
                .Reset();

            await sut.ShutdownAsync().ConfigureAwait(false);

            Mock.Get(leaseManager)
                .Verify(manager => manager.ReleaseAsync(It.IsAny<DocumentServiceLease>()), Times.Never);
        }

        [TestMethod]
        public async Task Controller_ShouldReleasesLease_IfObserverExits()
        {
            Mock.Get(partitionProcessor)
                .Reset();

            Mock.Get(partitionSupervisorFactory)
                .Setup(f => f.Create(lease))
                .Returns(new PartitionSupervisorCore<MyDocument>(lease, observer, partitionProcessor, leaseRenewer));

            await sut.AddOrUpdateLeaseAsync(lease).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);

            Mock.Get(leaseManager)
                .Verify(manager => manager.ReleaseAsync(It.IsAny<DocumentServiceLease>()), Times.Once);
        }

        [TestMethod]
        public async Task AddLease_ShouldFail_IfLeaseAcquireThrows()
        {
            Mock.Get(partitionProcessor)
                .Reset();

            Mock.Get(leaseManager)
                .Reset();

            Mock.Get(leaseManager)
                .Setup(manager => manager.AcquireAsync(lease))
                .Throws(new NullReferenceException());

            Mock.Get(leaseManager)
                .Setup(manager => manager.ReleaseAsync(lease))
                .Returns(Task.CompletedTask);

            await Assert.ThrowsExceptionAsync<NullReferenceException>(() => sut.AddOrUpdateLeaseAsync(lease)).ConfigureAwait(false);
        }

        [TestMethod]
        public async Task AddLease_ShouldReleaseLease_IfLeaseAcquireThrows()
        {
            Mock.Get(partitionProcessor)
                .Reset();

            Mock.Get(leaseManager)
                .Reset();

            Mock.Get(leaseManager)
                .Setup(manager => manager.AcquireAsync(lease))
                .Throws(new NullReferenceException());

            Mock.Get(leaseManager)
                .Setup(manager => manager.ReleaseAsync(lease))
                .Returns(Task.CompletedTask);

            await Assert.ThrowsExceptionAsync<NullReferenceException>(() => sut.AddOrUpdateLeaseAsync(lease)).ConfigureAwait(false);

            Mock.Get(leaseManager)
                .Verify(manager => manager.ReleaseAsync(It.IsAny<DocumentServiceLease>()), Times.Once);
        }

        [TestMethod]
        public async Task AddLease_ShouldntRunObserver_IfLeaseAcquireThrows()
        {
            Mock.Get(partitionProcessor)
                .Reset();

            Mock.Get(leaseManager)
                .Reset();

            Mock.Get(leaseManager)
                .Setup(manager => manager.AcquireAsync(lease))
                .Throws(new NullReferenceException());

            Mock.Get(leaseManager)
                .Setup(manager => manager.ReleaseAsync(lease))
                .Returns(Task.CompletedTask);

            await Assert.ThrowsExceptionAsync<NullReferenceException>(() => sut.AddOrUpdateLeaseAsync(lease)).ConfigureAwait(false);

            Mock.Get(partitionProcessor)
                .Verify(processor => processor.RunAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        public Task InitializeAsync()
        {
            return Task.FromResult(false);
        }

        private static FeedProcessor MockPartitionProcessor()
        {
            Mock<FeedProcessor> mock = new Mock<FeedProcessor>();
            mock
                .Setup(p => p.RunAsync(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(token => Task.Delay(TimeSpan.FromHours(1), token));
            return mock.Object;
        }

        private static LeaseRenewer MockRenewer()
        {
            Mock<LeaseRenewer> mock = new Mock<LeaseRenewer>();
            mock
                .Setup(renewer => renewer.RunAsync(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(token => Task.Delay(TimeSpan.FromMinutes(1), token));
            return mock.Object;
        }

        private static ChangeFeedObserver<MyDocument> MockObserver()
        {
            Mock<ChangeFeedObserver<MyDocument>> mock = new Mock<ChangeFeedObserver<MyDocument>>();
            return mock.Object;
        }

        public class MyDocument
        {
            public string id { get; set; }
        }
    }
}
