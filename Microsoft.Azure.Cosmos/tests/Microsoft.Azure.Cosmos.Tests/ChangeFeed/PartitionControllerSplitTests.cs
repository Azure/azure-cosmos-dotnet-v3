//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class PartitionControllerSplitTests
    {
        private const string LastContinuationToken = "lastContinuation";
        private const string InitialContinuationToken = "initial token";
        private const string PartitionId = "partitionId";

        [TestMethod]
        public async Task Controller_ShouldSignalSynchronizerSplitPartition_IfPartitionSplitHappened()
        {
            //arrange
            DocumentServiceLease lease = this.CreateMockLease(PartitionId);
            PartitionSynchronizer synchronizer = Mock.Of<PartitionSynchronizer>();
            Mock.Get(synchronizer)
                .Setup(s => s.HandlePartitionGoneAsync(lease))
                .ReturnsAsync((new[] { this.CreateMockLease(), this.CreateMockLease() }, true));

            PartitionSupervisor partitionSupervisor = Mock.Of<PartitionSupervisor>(o => o.RunAsync(It.IsAny<CancellationToken>()) == Task.FromException(new FeedRangeGoneException("message", LastContinuationToken)));
            PartitionSupervisorFactory partitionSupervisorFactory = Mock.Of<PartitionSupervisorFactory>(f => f.Create(lease) == partitionSupervisor);
            DocumentServiceLeaseManager leaseManager = Mock.Of<DocumentServiceLeaseManager>(manager => manager.AcquireAsync(lease) == Task.FromResult(lease));
            DocumentServiceLeaseContainer leaseContainer = Mock.Of<DocumentServiceLeaseContainer>();

            PartitionControllerCore sut = new PartitionControllerCore(leaseContainer, leaseManager, partitionSupervisorFactory, synchronizer, Mock.Of<ChangeFeedProcessorHealthMonitor>());

            await sut.InitializeAsync().ConfigureAwait(false);

            //act
            await sut.AddOrUpdateLeaseAsync(lease).ConfigureAwait(false);

            //assert
            await sut.ShutdownAsync().ConfigureAwait(false);

            Mock.Get(synchronizer).VerifyAll();

            Mock.Get(partitionSupervisor)
                .Verify(s => s.Dispose(), Times.Once);
        }

        [TestMethod]
        public async Task Controller_ShouldPassLastKnownContinuationTokenToSynchronizer_IfPartitionSplitHappened()
        {
            //arrange
            DocumentServiceLease lease = Mock.Of<DocumentServiceLease>(l => l.CurrentLeaseToken == PartitionId && l.ContinuationToken == InitialContinuationToken);
            PartitionSynchronizer synchronizer = Mock.Of<PartitionSynchronizer>();
            Mock.Get(synchronizer)
                .Setup(s => s.HandlePartitionGoneAsync(It.Is<DocumentServiceLease>(l => l.CurrentLeaseToken == PartitionId && l.ContinuationToken == LastContinuationToken)))
                .ReturnsAsync((new[] { this.CreateMockLease(), this.CreateMockLease() }, true));

            PartitionSupervisor partitionSupervisor = Mock.Of<PartitionSupervisor>(o => o.RunAsync(It.IsAny<CancellationToken>()) == Task.FromException(new FeedRangeGoneException("message", LastContinuationToken)));
            PartitionSupervisorFactory partitionSupervisorFactory = Mock.Of<PartitionSupervisorFactory>(f => f.Create(lease) == partitionSupervisor);
            DocumentServiceLeaseManager leaseManager = Mock.Of<DocumentServiceLeaseManager>(manager => manager.AcquireAsync(lease) == Task.FromResult(lease));
            DocumentServiceLeaseContainer leaseContainer = Mock.Of<DocumentServiceLeaseContainer>();

            PartitionControllerCore sut = new PartitionControllerCore(leaseContainer, leaseManager, partitionSupervisorFactory, synchronizer, Mock.Of<ChangeFeedProcessorHealthMonitor>());

            await sut.InitializeAsync().ConfigureAwait(false);

            //act
            await sut.AddOrUpdateLeaseAsync(lease).ConfigureAwait(false);

            //assert
            await sut.ShutdownAsync().ConfigureAwait(false);

            Mock.Get(synchronizer).VerifyAll();

            Mock.Get(partitionSupervisor)
                .Verify(s => s.Dispose(), Times.Once);
        }

        [TestMethod]
        public async Task Controller_ShouldCopyParentLeaseProperties_IfPartitionSplitHappened()
        {
            //arrange
            Dictionary<string, string> customProperties = new Dictionary<string, string> { { "key", "value" } };
            DocumentServiceLease lease = Mock.Of<DocumentServiceLease>(l => l.CurrentLeaseToken == PartitionId && l.Properties == customProperties);
            PartitionSynchronizer synchronizer = Mock.Of<PartitionSynchronizer>();
            DocumentServiceLease leaseChild1 = this.CreateMockLease();
            DocumentServiceLease leaseChild2 = this.CreateMockLease();
            Mock.Get(synchronizer)
                .Setup(s => s.HandlePartitionGoneAsync(It.Is<DocumentServiceLease>(l => l.CurrentLeaseToken == PartitionId && l.ContinuationToken == LastContinuationToken)))
                .ReturnsAsync((new[] { leaseChild1, leaseChild2 }, true));

            PartitionSupervisor partitionSupervisor = Mock.Of<PartitionSupervisor>(o => o.RunAsync(It.IsAny<CancellationToken>()) == Task.FromException(new FeedRangeGoneException("message", LastContinuationToken)));
            PartitionSupervisorFactory partitionSupervisorFactory = Mock.Of<PartitionSupervisorFactory>(f => f.Create(lease) == partitionSupervisor);
            DocumentServiceLeaseManager leaseManager = Mock.Of<DocumentServiceLeaseManager>(manager => manager.AcquireAsync(lease) == Task.FromResult(lease));
            DocumentServiceLeaseContainer leaseContainer = Mock.Of<DocumentServiceLeaseContainer>();

            PartitionControllerCore sut = new PartitionControllerCore(leaseContainer, leaseManager, partitionSupervisorFactory, synchronizer, Mock.Of<ChangeFeedProcessorHealthMonitor>());

            await sut.InitializeAsync().ConfigureAwait(false);

            //act
            await sut.AddOrUpdateLeaseAsync(lease).ConfigureAwait(false);

            await sut.ShutdownAsync().ConfigureAwait(false);
            Mock.Get(leaseChild1)
                    .VerifySet(l => l.Properties = customProperties, Times.Once);
            Mock.Get(leaseChild2)
                .VerifySet(l => l.Properties = customProperties, Times.Once);

            Mock.Get(partitionSupervisor)
                .Verify(s => s.Dispose(), Times.Once);
        }

        [TestMethod]
        public async Task Controller_ShouldKeepParentLease_IfSplitThrows()
        {
            //arrange
            DocumentServiceLease lease = this.CreateMockLease(PartitionId);
            PartitionSynchronizer synchronizer = Mock.Of<PartitionSynchronizer>(s => s.HandlePartitionGoneAsync(lease) == Task.FromException<(IEnumerable<DocumentServiceLease>, bool)>(new InvalidOperationException()));
            PartitionSupervisor partitionSupervisor = Mock.Of<PartitionSupervisor>(o => o.RunAsync(It.IsAny<CancellationToken>()) == Task.FromException(new FeedRangeGoneException("message", LastContinuationToken)));
            PartitionSupervisorFactory partitionSupervisorFactory = Mock.Of<PartitionSupervisorFactory>(f => f.Create(lease) == partitionSupervisor);
            DocumentServiceLeaseManager leaseManager = Mock.Of<DocumentServiceLeaseManager>();
            DocumentServiceLeaseContainer leaseContainer = Mock.Of<DocumentServiceLeaseContainer>();

            PartitionControllerCore sut = new PartitionControllerCore(leaseContainer, leaseManager, partitionSupervisorFactory, synchronizer, Mock.Of<ChangeFeedProcessorHealthMonitor>());

            await sut.InitializeAsync().ConfigureAwait(false);

            //act
            await sut.AddOrUpdateLeaseAsync(lease).ConfigureAwait(false);

            //assert
            await sut.ShutdownAsync().ConfigureAwait(false);

            Mock.Get(leaseManager).Verify(manager => manager.DeleteAsync(lease), Times.Never);

            Mock.Get(partitionSupervisor)
                .Verify(s => s.Dispose(), Times.Once);
        }

        [TestMethod]
        public async Task Controller_ShouldRunProcessingOnChildPartitions_IfHappyPath()
        {
            //arrange
            DocumentServiceLease lease = this.CreateMockLease(PartitionId);
            PartitionSynchronizer synchronizer = Mock.Of<PartitionSynchronizer>();
            DocumentServiceLease leaseChild1 = this.CreateMockLease();
            DocumentServiceLease leaseChild2 = this.CreateMockLease();
            Mock.Get(synchronizer)
                .Setup(s => s.HandlePartitionGoneAsync(It.Is<DocumentServiceLease>(l => l.CurrentLeaseToken == PartitionId && l.ContinuationToken == LastContinuationToken)))
                .ReturnsAsync((new[] { leaseChild1, leaseChild2 }, true));

            PartitionSupervisor partitionSupervisor = Mock.Of<PartitionSupervisor>(o => o.RunAsync(It.IsAny<CancellationToken>()) == Task.FromException(new FeedRangeGoneException("message", LastContinuationToken)));
            PartitionSupervisor partitionSupervisor1 = Mock.Of<PartitionSupervisor>();
            Mock.Get(partitionSupervisor1).Setup(o => o.RunAsync(It.IsAny<CancellationToken>())).Returns<CancellationToken>(token => Task.Delay(TimeSpan.FromHours(1), token));
            PartitionSupervisor partitionSupervisor2 = Mock.Of<PartitionSupervisor>();
            Mock.Get(partitionSupervisor2).Setup(o => o.RunAsync(It.IsAny<CancellationToken>())).Returns<CancellationToken>(token => Task.Delay(TimeSpan.FromHours(1), token));

            PartitionSupervisorFactory partitionSupervisorFactory = Mock.Of<PartitionSupervisorFactory>(f =>
                f.Create(lease) == partitionSupervisor && f.Create(leaseChild1) == partitionSupervisor1 && f.Create(leaseChild2) == partitionSupervisor2);
            DocumentServiceLeaseManager leaseManager = Mock.Of<DocumentServiceLeaseManager>(manager => manager.AcquireAsync(lease) == Task.FromResult(lease));
            DocumentServiceLeaseContainer leaseContainer = Mock.Of<DocumentServiceLeaseContainer>();

            Mock<ChangeFeedProcessorHealthMonitor> monitor = new Mock<ChangeFeedProcessorHealthMonitor>();

            PartitionControllerCore sut = new PartitionControllerCore(leaseContainer, leaseManager, partitionSupervisorFactory, synchronizer, monitor.Object);

            await sut.InitializeAsync().ConfigureAwait(false);

            //act
            await sut.AddOrUpdateLeaseAsync(lease).ConfigureAwait(false);

            //assert
            await sut.ShutdownAsync().ConfigureAwait(false);

            Mock.Get(leaseManager).Verify(manager => manager.AcquireAsync(leaseChild1), Times.Once);
            Mock.Get(leaseManager).Verify(manager => manager.AcquireAsync(leaseChild2), Times.Once);

            Mock.Get(partitionSupervisorFactory).Verify(f => f.Create(leaseChild1), Times.Once);
            Mock.Get(partitionSupervisorFactory).Verify(f => f.Create(leaseChild2), Times.Once);

            Mock.Get(partitionSupervisor1).Verify(p => p.RunAsync(It.IsAny<CancellationToken>()), Times.Once);
            Mock.Get(partitionSupervisor2).Verify(p => p.RunAsync(It.IsAny<CancellationToken>()), Times.Once);

            monitor.Verify(m => m.NotifyLeaseAcquireAsync(leaseChild1.CurrentLeaseToken), Times.Once);
            monitor.Verify(m => m.NotifyLeaseReleaseAsync(leaseChild2.CurrentLeaseToken), Times.Once);

            Mock.Get(partitionSupervisor)
                .Verify(s => s.Dispose(), Times.Once);
            Mock.Get(partitionSupervisor1)
                .Verify(s => s.Dispose(), Times.Once);
            Mock.Get(partitionSupervisor2)
                .Verify(s => s.Dispose(), Times.Once);
        }

        [TestMethod]
        public async Task Controller_ShouldRunNotDeleteLease()
        {
            //arrange
            DocumentServiceLease lease = this.CreateMockLease(PartitionId);
            PartitionSynchronizer synchronizer = Mock.Of<PartitionSynchronizer>();
            Mock.Get(synchronizer)
                .Setup(s => s.HandlePartitionGoneAsync(It.Is<DocumentServiceLease>(l => l.CurrentLeaseToken == PartitionId && l.ContinuationToken == LastContinuationToken)))
                .ReturnsAsync((new[] { lease }, false));

            PartitionSupervisor partitionSupervisor = Mock.Of<PartitionSupervisor>(o => o.RunAsync(It.IsAny<CancellationToken>()) == Task.FromException(new FeedRangeGoneException("message", LastContinuationToken)));

            PartitionSupervisorFactory partitionSupervisorFactory = Mock.Of<PartitionSupervisorFactory>(f =>
                f.Create(lease) == partitionSupervisor);
            DocumentServiceLeaseManager leaseManager = Mock.Of<DocumentServiceLeaseManager>(manager => manager.AcquireAsync(lease) == Task.FromResult(lease));
            DocumentServiceLeaseContainer leaseContainer = Mock.Of<DocumentServiceLeaseContainer>();

            PartitionControllerCore sut = new PartitionControllerCore(leaseContainer, leaseManager, partitionSupervisorFactory, synchronizer, Mock.Of<ChangeFeedProcessorHealthMonitor>());

            await sut.InitializeAsync().ConfigureAwait(false);

            //act
            await sut.AddOrUpdateLeaseAsync(lease).ConfigureAwait(false);

            //assert
            await sut.ShutdownAsync().ConfigureAwait(false);

            Mock.Get(leaseManager).Verify(manager => manager.DeleteAsync(lease), Times.Never);
        }

        [TestMethod]
        public async Task Controller_ShouldIgnoreProcessingChildPartition_IfPartitionAlreadyAdded()
        {
            //arrange
            DocumentServiceLease lease = this.CreateMockLease(PartitionId);
            PartitionSynchronizer synchronizer = Mock.Of<PartitionSynchronizer>();
            DocumentServiceLease leaseChild1 = this.CreateMockLease();
            DocumentServiceLease leaseChild2 = this.CreateMockLease();
            Mock.Get(synchronizer)
                .Setup(s => s.HandlePartitionGoneAsync(It.Is<DocumentServiceLease>(l => l.CurrentLeaseToken == PartitionId && l.ContinuationToken == LastContinuationToken)))
                .ReturnsAsync((new[] { leaseChild1, leaseChild2 }, true));

            PartitionSupervisor partitionSupervisor = Mock.Of<PartitionSupervisor>(o => o.RunAsync(It.IsAny<CancellationToken>()) == Task.FromException(new FeedRangeGoneException("message", LastContinuationToken)));
            PartitionSupervisor partitionSupervisor1 = Mock.Of<PartitionSupervisor>();
            Mock.Get(partitionSupervisor1).Setup(o => o.RunAsync(It.IsAny<CancellationToken>())).Returns<CancellationToken>(token => Task.Delay(TimeSpan.FromHours(1), token));
            PartitionSupervisor partitionSupervisor2 = Mock.Of<PartitionSupervisor>();
            Mock.Get(partitionSupervisor2).Setup(o => o.RunAsync(It.IsAny<CancellationToken>())).Returns<CancellationToken>(token => Task.Delay(TimeSpan.FromHours(1), token));

            PartitionSupervisorFactory partitionSupervisorFactory = Mock.Of<PartitionSupervisorFactory>(f =>
                f.Create(lease) == partitionSupervisor && f.Create(leaseChild1) == partitionSupervisor1 && f.Create(leaseChild2) == partitionSupervisor2);
            DocumentServiceLeaseManager leaseManager = Mock.Of<DocumentServiceLeaseManager>(manager => manager.AcquireAsync(lease) == Task.FromResult(lease));
            DocumentServiceLeaseContainer leaseContainer = Mock.Of<DocumentServiceLeaseContainer>();

            PartitionControllerCore sut = new PartitionControllerCore(leaseContainer, leaseManager, partitionSupervisorFactory, synchronizer, Mock.Of<ChangeFeedProcessorHealthMonitor>());

            await sut.InitializeAsync().ConfigureAwait(false);

            //act
            await sut.AddOrUpdateLeaseAsync(lease).ConfigureAwait(false);
            await sut.AddOrUpdateLeaseAsync(leaseChild2).ConfigureAwait(false);
            await sut.AddOrUpdateLeaseAsync(leaseChild2).ConfigureAwait(false);
            await sut.AddOrUpdateLeaseAsync(leaseChild2).ConfigureAwait(false);
            await sut.AddOrUpdateLeaseAsync(leaseChild2).ConfigureAwait(false);
            await sut.AddOrUpdateLeaseAsync(leaseChild2).ConfigureAwait(false);

            //assert
            await sut.ShutdownAsync().ConfigureAwait(false);

            Mock.Get(leaseManager)
                .Verify(manager => manager.AcquireAsync(leaseChild2), Times.Once);

            Mock.Get(leaseManager)
                .Verify(manager => manager.UpdatePropertiesAsync(leaseChild2), Times.Exactly(5));

            Mock.Get(partitionSupervisorFactory)
                .Verify(f => f.Create(leaseChild2), Times.Once);
        }

        [TestMethod]
        public async Task Controller_ShouldDeleteParentLease_IfChildLeasesCreatedByAnotherHost()
        {
            //arrange
            DocumentServiceLease lease = this.CreateMockLease(PartitionId);
            PartitionSynchronizer synchronizer = Mock.Of<PartitionSynchronizer>();
            Mock.Get(synchronizer)
                .Setup(s => s.HandlePartitionGoneAsync(lease))
                .ReturnsAsync((new DocumentServiceLease[] { }, true));

            PartitionSupervisor partitionSupervisor = Mock.Of<PartitionSupervisor>(o => o.RunAsync(It.IsAny<CancellationToken>()) == Task.FromException(new FeedRangeGoneException("message", LastContinuationToken)));
            PartitionSupervisorFactory partitionSupervisorFactory = Mock.Of<PartitionSupervisorFactory>(f => f.Create(lease) == partitionSupervisor);
            DocumentServiceLeaseManager leaseManager = Mock.Of<DocumentServiceLeaseManager>(manager =>
                manager.AcquireAsync(lease) == Task.FromResult(lease)
            );
            DocumentServiceLeaseContainer leaseContainer = Mock.Of<DocumentServiceLeaseContainer>();

            PartitionControllerCore sut = new PartitionControllerCore(leaseContainer, leaseManager, partitionSupervisorFactory, synchronizer, Mock.Of<ChangeFeedProcessorHealthMonitor>());

            await sut.InitializeAsync().ConfigureAwait(false);

            //act
            await sut.AddOrUpdateLeaseAsync(lease).ConfigureAwait(false);

            //assert
            await sut.ShutdownAsync().ConfigureAwait(false);

            Mock.Get(leaseManager).Verify(manager => manager.DeleteAsync(lease), Times.Once);
        }

        [TestMethod]
        public async Task Controller_ShouldDeleteParentLease_IfChildLeaseAcquireThrows()
        {
            //arrange
            DocumentServiceLease lease = this.CreateMockLease(PartitionId);
            PartitionSynchronizer synchronizer = Mock.Of<PartitionSynchronizer>();
            DocumentServiceLease leaseChild2 = this.CreateMockLease();
            Mock.Get(synchronizer)
                .Setup(s => s.HandlePartitionGoneAsync(lease))
                .ReturnsAsync((new[] { this.CreateMockLease(), leaseChild2 }, true));

            PartitionSupervisor partitionSupervisor = Mock.Of<PartitionSupervisor>(o => o.RunAsync(It.IsAny<CancellationToken>()) == Task.FromException(new FeedRangeGoneException("message", LastContinuationToken)));
            PartitionSupervisorFactory partitionSupervisorFactory = Mock.Of<PartitionSupervisorFactory>(f => f.Create(lease) == partitionSupervisor);
            DocumentServiceLeaseManager leaseManager = Mock.Of<DocumentServiceLeaseManager>(manager =>
                manager.AcquireAsync(lease) == Task.FromResult(lease) &&
                manager.AcquireAsync(leaseChild2) == Task.FromException<DocumentServiceLease>(new LeaseLostException())
                );
            DocumentServiceLeaseContainer leaseContainer = Mock.Of<DocumentServiceLeaseContainer>();

            Mock<ChangeFeedProcessorHealthMonitor> monitor = new Mock<ChangeFeedProcessorHealthMonitor>();

            PartitionControllerCore sut = new PartitionControllerCore(leaseContainer, leaseManager, partitionSupervisorFactory, synchronizer, monitor.Object);

            await sut.InitializeAsync().ConfigureAwait(false);

            //act
            await sut.AddOrUpdateLeaseAsync(lease).ConfigureAwait(false);

            //assert
            await sut.ShutdownAsync().ConfigureAwait(false);

            Mock.Get(leaseManager).Verify(manager => manager.DeleteAsync(lease), Times.Once);

            monitor.Verify(m => m.NotifyLeaseAcquireAsync(lease.CurrentLeaseToken), Times.Once);
            monitor.Verify(m => m.NotifyLeaseReleaseAsync(lease.CurrentLeaseToken), Times.Once);
        }

        private DocumentServiceLease CreateMockLease(string partitionId = null)
        {
            partitionId ??= Guid.NewGuid().ToString();
            return Mock.Of<DocumentServiceLease>(l => l.CurrentLeaseToken == partitionId);
        }
    }
}