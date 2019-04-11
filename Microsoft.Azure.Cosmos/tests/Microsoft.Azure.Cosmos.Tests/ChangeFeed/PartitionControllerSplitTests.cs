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
    public class PartitionControllerSplitTests
    {
        private const string LastContinuationToken = "lastContinuation";
        private const string InitialContinuationToken = "initial token";
        private const string PartitionId = "partitionId";

        [TestMethod]
        public async Task Controller_ShouldSignalSynchronizerSplitPartition_IfPartitionSplitHappened()
        {
            //arrange
            var lease = CreateMockLease(PartitionId);
            var synchronizer = Mock.Of<PartitionSynchronizer>();
            Mock.Get(synchronizer)
                .Setup(s => s.SplitPartitionAsync(lease))
                .ReturnsAsync(new[] { CreateMockLease(), CreateMockLease() });

            var partitionSupervisor = Mock.Of<PartitionSupervisor>(o => o.RunAsync(It.IsAny<CancellationToken>()) == Task.FromException(new FeedSplitException("message", LastContinuationToken)));
            var partitionSupervisorFactory = Mock.Of<PartitionSupervisorFactory>(f => f.Create(lease) == partitionSupervisor);
            var leaseManager = Mock.Of<DocumentServiceLeaseManager>(manager => manager.AcquireAsync(lease) == Task.FromResult(lease));
            var leaseContainer = Mock.Of<DocumentServiceLeaseContainer>();

            var sut = new PartitionControllerCore(leaseContainer, leaseManager, partitionSupervisorFactory, synchronizer);

            //act
            await sut.AddOrUpdateLeaseAsync(lease).ConfigureAwait(false);

            //assert
            await sut.ShutdownAsync().ConfigureAwait(false);

            Mock.Get(synchronizer).VerifyAll();
        }

        [TestMethod]
        public async Task Controller_ShouldPassLastKnownContinuationTokenToSynchronizer_IfPartitionSplitHappened()
        {
            //arrange
            var lease = Mock.Of<DocumentServiceLease>(l => l.CurrentLeaseToken == PartitionId && l.ContinuationToken == InitialContinuationToken);
            var synchronizer = Mock.Of<PartitionSynchronizer>();
            Mock.Get(synchronizer)
                .Setup(s => s.SplitPartitionAsync(It.Is<DocumentServiceLease>(l => l.CurrentLeaseToken == PartitionId && l.ContinuationToken == LastContinuationToken)))
                .ReturnsAsync(new[] { CreateMockLease(), CreateMockLease() });

            var partitionSupervisor = Mock.Of<PartitionSupervisor>(o => o.RunAsync(It.IsAny<CancellationToken>()) == Task.FromException(new FeedSplitException("message", LastContinuationToken)));
            var partitionSupervisorFactory = Mock.Of<PartitionSupervisorFactory>(f => f.Create(lease) == partitionSupervisor);
            var leaseManager = Mock.Of<DocumentServiceLeaseManager>(manager => manager.AcquireAsync(lease) == Task.FromResult(lease));
            var leaseContainer = Mock.Of<DocumentServiceLeaseContainer>();

            var sut = new PartitionControllerCore(leaseContainer, leaseManager, partitionSupervisorFactory, synchronizer);

            //act
            await sut.AddOrUpdateLeaseAsync(lease).ConfigureAwait(false);

            //assert
            await sut.ShutdownAsync().ConfigureAwait(false);

            Mock.Get(synchronizer).VerifyAll();
        }

        [TestMethod]
        public async Task Controller_ShouldCopyParentLeaseProperties_IfPartitionSplitHappened()
        {
            //arrange
            var customProperties = new Dictionary<string, string> { { "key", "value" } };
            var lease = Mock.Of<DocumentServiceLease>(l => l.CurrentLeaseToken == PartitionId && l.Properties == customProperties);
            var synchronizer = Mock.Of<PartitionSynchronizer>();
            DocumentServiceLease leaseChild1 = CreateMockLease();
            DocumentServiceLease leaseChild2 = CreateMockLease();
            Mock.Get(synchronizer)
                .Setup(s => s.SplitPartitionAsync(It.Is<DocumentServiceLease>(l => l.CurrentLeaseToken == PartitionId && l.ContinuationToken == LastContinuationToken)))
                .ReturnsAsync(new[] { leaseChild1, leaseChild2 });

            var partitionSupervisor = Mock.Of<PartitionSupervisor>(o => o.RunAsync(It.IsAny<CancellationToken>()) == Task.FromException(new FeedSplitException("message", LastContinuationToken)));
            var partitionSupervisorFactory = Mock.Of<PartitionSupervisorFactory>(f => f.Create(lease) == partitionSupervisor);
            var leaseManager = Mock.Of<DocumentServiceLeaseManager>(manager => manager.AcquireAsync(lease) == Task.FromResult(lease));
            var leaseContainer = Mock.Of<DocumentServiceLeaseContainer>();

            var sut = new PartitionControllerCore(leaseContainer, leaseManager, partitionSupervisorFactory, synchronizer);

            //act
            await sut.AddOrUpdateLeaseAsync(lease).ConfigureAwait(false);

            await sut.ShutdownAsync().ConfigureAwait(false);
            Mock.Get(leaseChild1)
                    .VerifySet(l => l.Properties = customProperties, Times.Once);
            Mock.Get(leaseChild2)
                .VerifySet(l => l.Properties = customProperties, Times.Once);
        }

        [TestMethod]
        public async Task Controller_ShouldKeepParentLease_IfSplitThrows()
        {
            //arrange
            var lease = CreateMockLease(PartitionId);
            var synchronizer = Mock.Of<PartitionSynchronizer>(s => s.SplitPartitionAsync(lease) == Task.FromException<IEnumerable<DocumentServiceLease>>(new InvalidOperationException()));
            var partitionSupervisor = Mock.Of<PartitionSupervisor>(o => o.RunAsync(It.IsAny<CancellationToken>()) == Task.FromException(new FeedSplitException("message", LastContinuationToken)));
            var partitionSupervisorFactory = Mock.Of<PartitionSupervisorFactory>(f => f.Create(lease) == partitionSupervisor);
            var leaseManager = Mock.Of<DocumentServiceLeaseManager>();
            var leaseContainer = Mock.Of<DocumentServiceLeaseContainer>();

            var sut = new PartitionControllerCore(leaseContainer, leaseManager, partitionSupervisorFactory, synchronizer);

            //act
            await sut.AddOrUpdateLeaseAsync(lease).ConfigureAwait(false);

            //assert
            await sut.ShutdownAsync().ConfigureAwait(false);

            Mock.Get(leaseManager).Verify(manager => manager.DeleteAsync(lease), Times.Never);
        }

        [TestMethod]
        public async Task Controller_ShouldRunProcessingOnChildPartitions_IfHappyPath()
        {
            //arrange
            var lease = CreateMockLease(PartitionId);
            var synchronizer = Mock.Of<PartitionSynchronizer>();
            DocumentServiceLease leaseChild1 = CreateMockLease();
            DocumentServiceLease leaseChild2 = CreateMockLease();
            Mock.Get(synchronizer)
                .Setup(s => s.SplitPartitionAsync(It.Is<DocumentServiceLease>(l => l.CurrentLeaseToken == PartitionId && l.ContinuationToken == LastContinuationToken)))
                .ReturnsAsync(new[] { leaseChild1, leaseChild2 });

            var partitionSupervisor = Mock.Of<PartitionSupervisor>(o => o.RunAsync(It.IsAny<CancellationToken>()) == Task.FromException(new FeedSplitException("message", LastContinuationToken)));
            var partitionSupervisor1 = Mock.Of<PartitionSupervisor>();
            Mock.Get(partitionSupervisor1).Setup(o => o.RunAsync(It.IsAny<CancellationToken>())).Returns<CancellationToken>(token => Task.Delay(TimeSpan.FromHours(1), token));
            var partitionSupervisor2 = Mock.Of<PartitionSupervisor>();
            Mock.Get(partitionSupervisor2).Setup(o => o.RunAsync(It.IsAny<CancellationToken>())).Returns<CancellationToken>(token => Task.Delay(TimeSpan.FromHours(1), token));

            var partitionSupervisorFactory = Mock.Of<PartitionSupervisorFactory>(f =>
                f.Create(lease) == partitionSupervisor && f.Create(leaseChild1) == partitionSupervisor1 && f.Create(leaseChild2) == partitionSupervisor2);
            var leaseManager = Mock.Of<DocumentServiceLeaseManager>(manager => manager.AcquireAsync(lease) == Task.FromResult(lease));
            var leaseContainer = Mock.Of<DocumentServiceLeaseContainer>();

            var sut = new PartitionControllerCore(leaseContainer, leaseManager, partitionSupervisorFactory, synchronizer);

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
        }

        [TestMethod]
        public async Task Controller_ShouldIgnoreProcessingChildPartition_IfPartitionAlreadyAdded()
        {
            //arrange
            var lease = CreateMockLease(PartitionId);
            var synchronizer = Mock.Of<PartitionSynchronizer>();
            DocumentServiceLease leaseChild1 = CreateMockLease();
            DocumentServiceLease leaseChild2 = CreateMockLease();
            Mock.Get(synchronizer)
                .Setup(s => s.SplitPartitionAsync(It.Is<DocumentServiceLease>(l => l.CurrentLeaseToken == PartitionId && l.ContinuationToken == LastContinuationToken)))
                .ReturnsAsync(new[] { leaseChild1, leaseChild2 });

            var partitionSupervisor = Mock.Of<PartitionSupervisor>(o => o.RunAsync(It.IsAny<CancellationToken>()) == Task.FromException(new FeedSplitException("message", LastContinuationToken)));
            var partitionSupervisor1 = Mock.Of<PartitionSupervisor>();
            Mock.Get(partitionSupervisor1).Setup(o => o.RunAsync(It.IsAny<CancellationToken>())).Returns<CancellationToken>(token => Task.Delay(TimeSpan.FromHours(1), token));
            var partitionSupervisor2 = Mock.Of<PartitionSupervisor>();
            Mock.Get(partitionSupervisor2).Setup(o => o.RunAsync(It.IsAny<CancellationToken>())).Returns<CancellationToken>(token => Task.Delay(TimeSpan.FromHours(1), token));

            var partitionSupervisorFactory = Mock.Of<PartitionSupervisorFactory>(f =>
                f.Create(lease) == partitionSupervisor && f.Create(leaseChild1) == partitionSupervisor1 && f.Create(leaseChild2) == partitionSupervisor2);
            var leaseManager = Mock.Of<DocumentServiceLeaseManager>(manager => manager.AcquireAsync(lease) == Task.FromResult(lease));
            var leaseContainer = Mock.Of<DocumentServiceLeaseContainer>();

            var sut = new PartitionControllerCore(leaseContainer, leaseManager, partitionSupervisorFactory, synchronizer);

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
            var lease = CreateMockLease(PartitionId);
            var synchronizer = Mock.Of<PartitionSynchronizer>();
            Mock.Get(synchronizer)
                .Setup(s => s.SplitPartitionAsync(lease))
                .ReturnsAsync(new DocumentServiceLease[] { });

            var partitionSupervisor = Mock.Of<PartitionSupervisor>(o => o.RunAsync(It.IsAny<CancellationToken>()) == Task.FromException(new FeedSplitException("message", LastContinuationToken)));
            var partitionSupervisorFactory = Mock.Of<PartitionSupervisorFactory>(f => f.Create(lease) == partitionSupervisor);
            var leaseManager = Mock.Of<DocumentServiceLeaseManager>(manager =>
                manager.AcquireAsync(lease) == Task.FromResult(lease)
            );
            var leaseContainer = Mock.Of<DocumentServiceLeaseContainer>();

            var sut = new PartitionControllerCore(leaseContainer, leaseManager, partitionSupervisorFactory, synchronizer);

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
            var lease = CreateMockLease(PartitionId);
            var synchronizer = Mock.Of<PartitionSynchronizer>();
            DocumentServiceLease leaseChild2 = CreateMockLease();
            Mock.Get(synchronizer)
                .Setup(s => s.SplitPartitionAsync(lease))
                .ReturnsAsync(new[] { CreateMockLease(), leaseChild2 });

            var partitionSupervisor = Mock.Of<PartitionSupervisor>(o => o.RunAsync(It.IsAny<CancellationToken>()) == Task.FromException(new FeedSplitException("message", LastContinuationToken)));
            var partitionSupervisorFactory = Mock.Of<PartitionSupervisorFactory>(f => f.Create(lease) == partitionSupervisor);
            var leaseManager = Mock.Of<DocumentServiceLeaseManager>(manager =>
                manager.AcquireAsync(lease) == Task.FromResult(lease) &&
                manager.AcquireAsync(leaseChild2) == Task.FromException<DocumentServiceLease>(new LeaseLostException())
                );
            var leaseContainer = Mock.Of<DocumentServiceLeaseContainer>();

            var sut = new PartitionControllerCore(leaseContainer, leaseManager, partitionSupervisorFactory, synchronizer);

            //act
            await sut.AddOrUpdateLeaseAsync(lease).ConfigureAwait(false);

            //assert
            await sut.ShutdownAsync().ConfigureAwait(false);

            Mock.Get(leaseManager).Verify(manager => manager.DeleteAsync(lease), Times.Once);
        }

        private DocumentServiceLease CreateMockLease(string partitionId = null)
        {
            partitionId = partitionId ?? Guid.NewGuid().ToString();
            return Mock.Of<DocumentServiceLease>(l => l.CurrentLeaseToken == partitionId);
        }
    }
}
