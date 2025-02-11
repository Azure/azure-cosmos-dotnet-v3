//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
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
        private readonly ChangeFeedObserver observer;
        private readonly PartitionSynchronizer synchronizer;
        private readonly PartitionController sut;
        private readonly PartitionSupervisorFactory partitionSupervisorFactory;
        private readonly Mock<ChangeFeedProcessorHealthMonitor> healthMonitor;

        public PartitionControllerTests()
        {
            this.lease = Mock.Of<DocumentServiceLease>();
            Mock.Get(this.lease)
                .Setup(l => l.CurrentLeaseToken)
                .Returns("partitionId");

            this.partitionProcessor = MockPartitionProcessor();
            this.leaseRenewer = MockRenewer();
            this.observer = Mock.Of<ChangeFeedObserver>();
            this.partitionSupervisorFactory = Mock.Of<PartitionSupervisorFactory>(f => f.Create(this.lease) == new PartitionSupervisorCore(this.lease, this.observer, this.partitionProcessor, this.leaseRenewer));

            this.leaseManager = Mock.Of<DocumentServiceLeaseManager>();
            Mock.Get(this.leaseManager).Reset(); // Reset implicit/by default setup of properties.
            Mock.Get(this.leaseManager)
                .Setup(manager => manager.AcquireAsync(this.lease))
                .ReturnsAsync(this.lease);
            Mock.Get(this.leaseManager)
                .Setup(manager => manager.ReleaseAsync(this.lease))
                .Returns(Task.CompletedTask);
            DocumentServiceLeaseContainer leaseContainer = Mock.Of<DocumentServiceLeaseContainer>();

            this.synchronizer = Mock.Of<PartitionSynchronizer>();
            this.healthMonitor = new Mock<ChangeFeedProcessorHealthMonitor>();
            this.sut = new PartitionControllerCore(leaseContainer, this.leaseManager, this.partitionSupervisorFactory, this.synchronizer, this.healthMonitor.Object);
        }

        [TestInitialize]
        public async Task Setup()
        {
            await this.sut.InitializeAsync().ConfigureAwait(false);
        }

        [TestCleanup]
        public async Task CleanUp()
        {
            await this.sut.ShutdownAsync().ConfigureAwait(false);

            Mock.Get(this.leaseManager)
                .VerifyAll();

            Mock.Get(this.partitionProcessor)
                .VerifyAll();

            Mock.Get(this.synchronizer)
                .VerifyAll();
        }

        [TestMethod]
        public async Task AddLease_ShouldAcquireLease_WhenCalled()
        {
            await this.sut.AddOrUpdateLeaseAsync(this.lease).ConfigureAwait(false);

            Mock.Get(this.leaseManager)
                .Verify(manager => manager.AcquireAsync(this.lease), Times.Once);
        }

        [TestMethod]
        public async Task AddLease_ShouldNotify_Monitor()
        {
            await this.sut.AddOrUpdateLeaseAsync(this.lease).ConfigureAwait(false);

            this.healthMonitor
                .Verify(m => m.NotifyLeaseAcquireAsync(this.lease.CurrentLeaseToken), Times.Once);
        }

        [TestMethod]
        public async Task AddLease_ShouldRunObserver_WhenCalled()
        {
            await this.sut.AddOrUpdateLeaseAsync(this.lease).ConfigureAwait(false);

            Mock.Get(this.partitionProcessor)
                .Verify(p => p.RunAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task AddLease_ShouldntReleaseLease_WhenCalled()
        {
            await this.sut.AddOrUpdateLeaseAsync(this.lease).ConfigureAwait(false);

            Mock.Get(this.leaseManager)
                .Verify(manager => manager.ReleaseAsync(this.lease), Times.Never);
        }

        [TestMethod]
        public async Task AddLease_ShouldIgnorePartitionObserving_IfDuplicateLease()
        {
            await this.sut.AddOrUpdateLeaseAsync(this.lease).ConfigureAwait(false);

            FeedProcessor processorDuplicate = MockPartitionProcessor();
            Mock.Get(this.partitionSupervisorFactory)
                .Setup(f => f.Create(this.lease))
                .Returns(new PartitionSupervisorCore(this.lease, this.observer, processorDuplicate, this.leaseRenewer));

            await this.sut.AddOrUpdateLeaseAsync(this.lease).ConfigureAwait(false);

            Mock.Get(this.leaseManager)
                .Verify(manager => manager.AcquireAsync(this.lease), Times.Once);

            Mock.Get(this.leaseManager)
                .Verify(manager => manager.UpdatePropertiesAsync(this.lease), Times.Once);

            Mock.Get(this.leaseManager)
                .Verify(manager => manager.ReleaseAsync(It.IsAny<DocumentServiceLease>()), Times.Never);

            Mock.Get(this.partitionProcessor)
                .Verify(p => p.RunAsync(It.IsAny<CancellationToken>()), Times.Once);
            Mock.Get(processorDuplicate)
                .Verify(p => p.RunAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task AddLease_ShouldNotRelease_IfUpdateLeasePropertiesThrows()
        {
            await this.sut.AddOrUpdateLeaseAsync(this.lease).ConfigureAwait(false);

            Mock.Get(this.partitionProcessor)
                .Reset();

            Mock.Get(this.leaseManager)
                .Reset();

            Mock.Get(this.leaseManager)
                .Setup(manager => manager.UpdatePropertiesAsync(this.lease))
                .Throws(new NullReferenceException());

            Mock.Get(this.leaseManager)
                .Setup(manager => manager.ReleaseAsync(this.lease))
                .Returns(Task.CompletedTask);

            await Assert.ThrowsExceptionAsync<NullReferenceException>(() => this.sut.AddOrUpdateLeaseAsync(this.lease)).ConfigureAwait(false);

            Mock.Get(this.leaseManager)
                .Verify(manager => manager.ReleaseAsync(It.IsAny<DocumentServiceLease>()), Times.Never);
        }

        [TestMethod]
        public async Task AddLease_ShouldAcquireLease_IfSecondLeaseAdded()
        {
            DocumentServiceLease lease2 = Mock.Of<DocumentServiceLease>();
            Mock.Get(lease2)
                .Setup(l => l.CurrentLeaseToken)
                .Returns("partitionId2");

            Mock.Get(this.partitionSupervisorFactory)
                .Setup(f => f.Create(lease2))
                .Returns(new PartitionSupervisorCore(lease2, this.observer, MockPartitionProcessor(), this.leaseRenewer));

            await this.sut.AddOrUpdateLeaseAsync(this.lease).ConfigureAwait(false);
            await this.sut.AddOrUpdateLeaseAsync(lease2).ConfigureAwait(false);

            Mock.Get(this.leaseManager)
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
            Mock.Get(this.partitionSupervisorFactory)
                .Setup(f => f.Create(lease2))
                .Returns(new PartitionSupervisorCore(lease2, this.observer, partitionProcessor2, this.leaseRenewer));

            await this.sut.AddOrUpdateLeaseAsync(this.lease).ConfigureAwait(false);
            await this.sut.AddOrUpdateLeaseAsync(lease2).ConfigureAwait(false);

            Mock.Get(partitionProcessor2)
                .Verify(p => p.RunAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task Shutdown_ShouldWork_WithoutLeases()
        {
            Mock.Get(this.leaseManager)
                .Reset();

            Mock.Get(this.partitionProcessor)
                .Reset();

            await this.sut.ShutdownAsync().ConfigureAwait(false);

            Mock.Get(this.leaseManager)
                .Verify(manager => manager.ReleaseAsync(It.IsAny<DocumentServiceLease>()), Times.Never);
        }

        [TestMethod]
        public async Task Controller_ShouldReleasesLease_IfObserverExits()
        {
            Mock.Get(this.partitionProcessor)
                .Reset();

            Mock.Get(this.partitionSupervisorFactory)
                .Setup(f => f.Create(this.lease))
                .Returns(new PartitionSupervisorCore(this.lease, this.observer, this.partitionProcessor, this.leaseRenewer));

            await this.sut.AddOrUpdateLeaseAsync(this.lease).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);

            Mock.Get(this.leaseManager)
                .Verify(manager => manager.ReleaseAsync(It.IsAny<DocumentServiceLease>()), Times.Once);
        }

        [TestMethod]
        public async Task Controller_ShouldNotify_IfProcessingFails()
        {
            Mock.Get(this.partitionProcessor)
                .Reset();

            Mock<PartitionSupervisor> supervisor = new Mock<PartitionSupervisor>();

            Exception exception = new NotImplementedException();

            ManualResetEvent manualResetEvent = new ManualResetEvent(false);

            supervisor
                .Setup(s => s.RunAsync(It.IsAny<CancellationToken>()))
                .Callback((CancellationToken ct) =>
                {
                    manualResetEvent.Set();
                    throw exception;
                });

            Mock.Get(this.partitionSupervisorFactory)
                .Setup(f => f.Create(this.lease))
                .Returns(supervisor.Object);

            await this.sut.AddOrUpdateLeaseAsync(this.lease).ConfigureAwait(false);

            bool timeout = manualResetEvent.WaitOne(100);
            Assert.IsTrue(timeout, "Partition supervisor not started");

            this.healthMonitor
                .Verify(m => m.NotifyErrorAsync(this.lease.CurrentLeaseToken, exception), Times.Once);

            Mock.Get(this.leaseManager)
                .Verify(manager => manager.ReleaseAsync(this.lease), Times.Once);

            this.healthMonitor
                .Verify(m => m.NotifyLeaseReleaseAsync(this.lease.CurrentLeaseToken), Times.Once);
        }

        [TestMethod]
        public async Task Controller_ShouldNotify_IfProcessingFails_EvenOnLeaseLost()
        {
            Mock.Get(this.partitionProcessor)
                .Reset();

            Mock<PartitionSupervisor> supervisor = new Mock<PartitionSupervisor>();

            Exception exception = new NotImplementedException();

            ManualResetEvent manualResetEvent = new ManualResetEvent(false);

            // Fail on Release
            Mock.Get(this.leaseManager)
                .Setup(manager => manager.ReleaseAsync(this.lease))
                .ThrowsAsync(new Exceptions.LeaseLostException());

            supervisor
                .Setup(s => s.RunAsync(It.IsAny<CancellationToken>()))
                .Callback((CancellationToken ct) =>
                {
                    manualResetEvent.Set();
                    throw exception;
                });

            Mock.Get(this.partitionSupervisorFactory)
                .Setup(f => f.Create(this.lease))
                .Returns(supervisor.Object);

            await this.sut.AddOrUpdateLeaseAsync(this.lease).ConfigureAwait(false);

            bool timeout = manualResetEvent.WaitOne(100);
            Assert.IsTrue(timeout, "Partition supervisor not started");

            this.healthMonitor
                .Verify(m => m.NotifyErrorAsync(this.lease.CurrentLeaseToken, exception), Times.Once);

            Mock.Get(this.leaseManager)
                .Verify(manager => manager.ReleaseAsync(this.lease), Times.Once);

            this.healthMonitor
                .Verify(m => m.NotifyLeaseReleaseAsync(this.lease.CurrentLeaseToken), Times.Once);
        }

        [TestMethod]
        public async Task AddLease_ShouldNotNotify_IfLeaseAcquireFailsWithLeaseLost_WithoutInnerException()
        {
            Mock.Get(this.partitionProcessor)
                .Reset();

            Mock<PartitionSupervisor> supervisor = new Mock<PartitionSupervisor>();

            LeaseLostException exception = new LeaseLostException();

            // Fail on Acquire
            Mock.Get(this.leaseManager)
                .Setup(manager => manager.AcquireAsync(this.lease))
                .ThrowsAsync(exception);

            Exception thrownException = await Assert.ThrowsExceptionAsync<LeaseLostException>(() => this.sut.AddOrUpdateLeaseAsync(this.lease));

            Assert.AreEqual(exception, thrownException);

            this.healthMonitor
                .Verify(m => m.NotifyErrorAsync(this.lease.CurrentLeaseToken, exception), Times.Never);

            Mock.Get(this.leaseManager)
                .Verify(manager => manager.ReleaseAsync(this.lease), Times.Once);

            this.healthMonitor
                .Verify(m => m.NotifyLeaseReleaseAsync(this.lease.CurrentLeaseToken), Times.Once);
        }

        [TestMethod]
        public async Task AddLease_ShouldNotify_IfLeaseAcquireFailsWithLeaseLost_WithInnerException()
        {
            Mock.Get(this.partitionProcessor)
                .Reset();

            Mock<PartitionSupervisor> supervisor = new Mock<PartitionSupervisor>();

            Exception internalException = new Exception();

            LeaseLostException exception = new LeaseLostException("some error", internalException);

            // Fail on Acquire
            Mock.Get(this.leaseManager)
                .Setup(manager => manager.AcquireAsync(this.lease))
                .ThrowsAsync(exception);

            Exception thrownException = await Assert.ThrowsExceptionAsync<LeaseLostException>(() => this.sut.AddOrUpdateLeaseAsync(this.lease));

            Assert.AreEqual(exception, thrownException);

            this.healthMonitor
                .Verify(m => m.NotifyErrorAsync(this.lease.CurrentLeaseToken, internalException), Times.Once);

            Mock.Get(this.leaseManager)
                .Verify(manager => manager.ReleaseAsync(this.lease), Times.Once);

            this.healthMonitor
                .Verify(m => m.NotifyLeaseReleaseAsync(this.lease.CurrentLeaseToken), Times.Once);
        }

        [TestMethod]
        public async Task AddLease_ShouldReleaseLease_IfLeaseAcquireThrows()
        {
            Mock.Get(this.partitionProcessor)
                .Reset();

            Mock.Get(this.leaseManager)
                .Reset();

            Mock.Get(this.leaseManager)
                .Setup(manager => manager.AcquireAsync(this.lease))
                .Throws(new NullReferenceException());

            Mock.Get(this.leaseManager)
                .Setup(manager => manager.ReleaseAsync(this.lease))
                .Returns(Task.CompletedTask);

            await Assert.ThrowsExceptionAsync<NullReferenceException>(() => this.sut.AddOrUpdateLeaseAsync(this.lease)).ConfigureAwait(false);

            this.healthMonitor
                .Verify(m => m.NotifyLeaseAcquireAsync(this.lease.CurrentLeaseToken), Times.Never);

            this.healthMonitor
                .Verify(m => m.NotifyLeaseReleaseAsync(this.lease.CurrentLeaseToken), Times.Once);
        }

        [TestMethod]
        public async Task AddLease_ShouldntRunObserver_IfLeaseAcquireThrows()
        {
            Mock.Get(this.partitionProcessor)
                .Reset();

            Mock.Get(this.leaseManager)
                .Reset();

            Mock.Get(this.leaseManager)
                .Setup(manager => manager.AcquireAsync(this.lease))
                .Throws(new NullReferenceException());

            Mock.Get(this.leaseManager)
                .Setup(manager => manager.ReleaseAsync(this.lease))
                .Returns(Task.CompletedTask);

            await Assert.ThrowsExceptionAsync<NullReferenceException>(() => this.sut.AddOrUpdateLeaseAsync(this.lease)).ConfigureAwait(false);

            Mock.Get(this.partitionProcessor)
                .Verify(processor => processor.RunAsync(It.IsAny<CancellationToken>()), Times.Never);

            this.healthMonitor
                .Verify(m => m.NotifyErrorAsync(this.lease.CurrentLeaseToken, It.Is<Exception>(ex => ex is NullReferenceException)), Times.Once);

            this.healthMonitor
                .Verify(m => m.NotifyLeaseAcquireAsync(this.lease.CurrentLeaseToken), Times.Never);

            this.healthMonitor
                .Verify(m => m.NotifyLeaseReleaseAsync(this.lease.CurrentLeaseToken), Times.Once);
        }

        [TestMethod]
        public async Task AddLease_ShouldNotify_IfLeaseReleaseThrowsUnknown()
        {
            Mock.Get(this.partitionProcessor)
                .Reset();

            Mock.Get(this.leaseManager)
                .Reset();

            // Fail on Acquire to trigger Release
            Mock.Get(this.leaseManager)
                .Setup(manager => manager.AcquireAsync(this.lease))
                .ThrowsAsync(new NullReferenceException());

            // Fail on Release
            Mock.Get(this.leaseManager)
                .Setup(manager => manager.ReleaseAsync(this.lease))
                .ThrowsAsync(new ArgumentException());

            await Assert.ThrowsExceptionAsync<NullReferenceException>(() => this.sut.AddOrUpdateLeaseAsync(this.lease)).ConfigureAwait(false);

            Mock.Get(this.partitionProcessor)
                .Verify(processor => processor.RunAsync(It.IsAny<CancellationToken>()), Times.Never);

            // Notify the Acquire error
            this.healthMonitor
                .Verify(m => m.NotifyErrorAsync(this.lease.CurrentLeaseToken, It.Is<Exception>(ex => ex is NullReferenceException)), Times.Once);

            // Notify the Release error
            this.healthMonitor
                .Verify(m => m.NotifyErrorAsync(this.lease.CurrentLeaseToken, It.Is<Exception>(ex => ex is ArgumentException)), Times.Once);

            this.healthMonitor
                .Verify(m => m.NotifyLeaseAcquireAsync(this.lease.CurrentLeaseToken), Times.Never);

            this.healthMonitor
                .Verify(m => m.NotifyLeaseReleaseAsync(this.lease.CurrentLeaseToken), Times.Never);
        }

        [TestMethod]
        public async Task AddLease_ShouldNotify_IfLeaseReleaseThrowsLeaseLost()
        {
            Mock.Get(this.partitionProcessor)
                .Reset();

            Mock.Get(this.leaseManager)
                .Reset();

            // Fail on Acquire to trigger Release
            Mock.Get(this.leaseManager)
                .Setup(manager => manager.AcquireAsync(this.lease))
                .ThrowsAsync(new NullReferenceException());

            // Fail on Release
            Mock.Get(this.leaseManager)
                .Setup(manager => manager.ReleaseAsync(this.lease))
                .ThrowsAsync(new Exceptions.LeaseLostException());

            await Assert.ThrowsExceptionAsync<NullReferenceException>(() => this.sut.AddOrUpdateLeaseAsync(this.lease)).ConfigureAwait(false);

            Mock.Get(this.partitionProcessor)
                .Verify(processor => processor.RunAsync(It.IsAny<CancellationToken>()), Times.Never);

            // Notify the Acquire error
            this.healthMonitor
                .Verify(m => m.NotifyErrorAsync(this.lease.CurrentLeaseToken, It.Is<Exception>(ex => ex is NullReferenceException)), Times.Once);

            this.healthMonitor
                .Verify(m => m.NotifyLeaseAcquireAsync(this.lease.CurrentLeaseToken), Times.Never);

            this.healthMonitor
                .Verify(m => m.NotifyLeaseReleaseAsync(this.lease.CurrentLeaseToken), Times.Never);
        }

        [TestMethod]
        public async Task Shutdown_ShouldNotify_Monitor()
        {
            Mock.Get(this.leaseManager)
                .Reset();

            Mock.Get(this.partitionProcessor)
                .Reset();

            await this.sut.AddOrUpdateLeaseAsync(this.lease).ConfigureAwait(false);

            await Task.Delay(100);

            await this.sut.ShutdownAsync().ConfigureAwait(false);

            Mock.Get(this.leaseManager)
                .Verify(manager => manager.ReleaseAsync(this.lease), Times.Once);

            this.healthMonitor
                .Verify(m => m.NotifyLeaseReleaseAsync(this.lease.CurrentLeaseToken), Times.Once);
        }

        [TestMethod]
        public async Task Renewer_ShouldNotNotify_IfLeaseAcquireFailsWithLeaseLost_WithoutInnerException()
        {
            Mock.Get(this.partitionProcessor)
                .Reset();

            Mock<PartitionSupervisor> supervisor = new Mock<PartitionSupervisor>();

            LeaseLostException exception = new LeaseLostException();

            ManualResetEvent manualResetEvent = new ManualResetEvent(false);

            supervisor
                .Setup(s => s.RunAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception)
                .Callback((CancellationToken ct) =>
                {
                    manualResetEvent.Set();
                    throw exception;
                });

            Mock.Get(this.partitionSupervisorFactory)
                .Setup(f => f.Create(this.lease))
                .Returns(supervisor.Object);

            await this.sut.AddOrUpdateLeaseAsync(this.lease);

            bool timeout = manualResetEvent.WaitOne(100);
            Assert.IsTrue(timeout, "Partition supervisor not started");

            this.healthMonitor
                .Verify(m => m.NotifyErrorAsync(this.lease.CurrentLeaseToken, exception), Times.Never);

            Mock.Get(this.leaseManager)
                .Verify(manager => manager.ReleaseAsync(this.lease), Times.Once);

            this.healthMonitor
                .Verify(m => m.NotifyLeaseReleaseAsync(this.lease.CurrentLeaseToken), Times.Once);
        }

        [TestMethod]
        public async Task Renewer_ShouldNotify_IfLeaseAcquireFailsWithLeaseLost_WithInnerException()
        {
            Mock.Get(this.partitionProcessor)
                .Reset();

            Mock<PartitionSupervisor> supervisor = new Mock<PartitionSupervisor>();

            Exception internalException = new Exception();

            LeaseLostException exception = new LeaseLostException("some error", internalException);

            ManualResetEvent manualResetEvent = new ManualResetEvent(false);

            supervisor
                .Setup(s => s.RunAsync(It.IsAny<CancellationToken>()))
                .Callback((CancellationToken ct) =>
                {
                    manualResetEvent.Set();
                    throw exception;
                });

            Mock.Get(this.partitionSupervisorFactory)
                .Setup(f => f.Create(this.lease))
                .Returns(supervisor.Object);

            await this.sut.AddOrUpdateLeaseAsync(this.lease).ConfigureAwait(false);

            bool timeout = manualResetEvent.WaitOne(100);
            Assert.IsTrue(timeout, "Partition supervisor not started");

            this.healthMonitor
                .Verify(m => m.NotifyErrorAsync(this.lease.CurrentLeaseToken, internalException), Times.Once);

            Mock.Get(this.leaseManager)
                .Verify(manager => manager.ReleaseAsync(this.lease), Times.Once);

            this.healthMonitor
                .Verify(m => m.NotifyLeaseReleaseAsync(this.lease.CurrentLeaseToken), Times.Once);
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
    }
}