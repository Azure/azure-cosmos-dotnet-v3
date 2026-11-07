//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
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
    public class PartitionSupervisorTests : IDisposable
    {
        private readonly DocumentServiceLease lease;
        private readonly LeaseRenewer leaseRenewer;
        private readonly FeedProcessor partitionProcessor;
        private readonly ChangeFeedObserver observer;
        private readonly CancellationTokenSource shutdownToken = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        private readonly PartitionSupervisor sut;

        public PartitionSupervisorTests()
        {
            this.lease = Mock.Of<DocumentServiceLease>();
            Mock.Get(this.lease)
                .Setup(l => l.CurrentLeaseToken)
                .Returns("partitionId");

            this.leaseRenewer = Mock.Of<LeaseRenewer>();
            this.partitionProcessor = Mock.Of<FeedProcessor>();
            this.observer = Mock.Of<ChangeFeedObserver>();

            this.sut = new PartitionSupervisorCore(this.lease, this.observer, this.partitionProcessor, this.leaseRenewer);
        }

        [TestMethod]
        public async Task RunObserver_ShouldCancelTasks_WhenTokenCanceled()
        {
            Task renewerTask = Task.FromResult(false);
            Mock.Get(this.leaseRenewer)
                .Setup(renewer => renewer.RunAsync(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(token => renewerTask = Task.Delay(TimeSpan.FromMinutes(1), token));

            Task processorTask = Task.FromResult(false);
            Mock.Get(this.partitionProcessor)
                .Setup(processor => processor.RunAsync(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(token => processorTask = Task.Delay(TimeSpan.FromMinutes(1), token));

            Task supervisorTask = this.sut.RunAsync(this.shutdownToken.Token);

            Task delay = Task.Delay(TimeSpan.FromMilliseconds(100));
            Task finished = await Task.WhenAny(supervisorTask, delay).ConfigureAwait(false);
            Assert.AreEqual(delay, finished);

            this.shutdownToken.Cancel();
            await supervisorTask.ConfigureAwait(false);

            Assert.IsTrue(renewerTask.IsCanceled);
            Assert.IsTrue(processorTask.IsCanceled);
            Mock.Get(this.partitionProcessor)
                .Verify(processor => processor.RunAsync(It.IsAny<CancellationToken>()), Times.Once);

            Mock.Get(this.observer)
                .Verify(feedObserver => feedObserver
                    .CloseAsync(It.Is<string>(lt => lt == this.lease.CurrentLeaseToken),
                        ChangeFeedObserverCloseReason.Shutdown));
        }

        [TestMethod]
        public async Task RunObserver_ShouldCancelProcessor_IfRenewerFailed()
        {
            Task processorTask = Task.FromResult(false);
            Mock.Get(this.leaseRenewer)
                .Setup(renewer => renewer.RunAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new LeaseLostException());

            Mock.Get(this.partitionProcessor)
                .Setup(processor => processor.RunAsync(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(token => processorTask = Task.Delay(TimeSpan.FromMinutes(1), token));

            await Assert.ThrowsExceptionAsync<LeaseLostException>(() => this.sut.RunAsync(this.shutdownToken.Token)).ConfigureAwait(false);
            Assert.IsTrue(processorTask.IsCanceled);

            Mock.Get(this.observer)
                .Verify(feedObserver => feedObserver
                    .CloseAsync(It.Is<string>(lt => lt == this.lease.CurrentLeaseToken),
                        ChangeFeedObserverCloseReason.LeaseLost));
        }

        [TestMethod]
        public async Task RunObserver_ShouldCancelRenewer_IfProcessorFailed()
        {
            Task renewerTask = Task.FromResult(false);
            Mock.Get(this.leaseRenewer)
                .Setup(renewer => renewer.RunAsync(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(token => renewerTask = Task.Delay(TimeSpan.FromMinutes(1), token));

            Mock.Get(this.partitionProcessor)
                .Setup(processor => processor.RunAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("processorException"));

            await Assert.ThrowsExceptionAsync<Exception>(() => this.sut.RunAsync(this.shutdownToken.Token)).ConfigureAwait(false);
            Assert.IsTrue(renewerTask.IsCanceled);

            Mock.Get(this.observer)
                .Verify(feedObserver => feedObserver
                    .CloseAsync(It.Is<string>(lt => lt == this.lease.CurrentLeaseToken),
                        ChangeFeedObserverCloseReason.Unknown));
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task TransientRenewalFailureDoesNotCancelWorker(bool filtered)
        {
            DocumentServiceLease lease = new DocumentServiceLeaseCore
            {
                LeaseId = "partitionId",
                LeaseToken = "partitionId",
                QueryFilterHash = filtered ? "query-hash" : null
            };
            ConcurrentDictionary<string, DocumentServiceLease> leases = new ConcurrentDictionary<string, DocumentServiceLease>();
            leases[lease.Id] = lease;
            TaskCompletionSource<bool> renewed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int attempts = 0;
            Mock<DocumentServiceLeaseUpdater> updater = new Mock<DocumentServiceLeaseUpdater>();
            updater.Setup(u => u.UpdateLeaseAsync(
                It.IsAny<DocumentServiceLease>(), It.IsAny<string>(), It.IsAny<PartitionKey>(),
                It.IsAny<Func<DocumentServiceLease, DocumentServiceLease>>()))
                .Returns((DocumentServiceLease cached, string id, PartitionKey key, Func<DocumentServiceLease, DocumentServiceLease> update) =>
                {
                    if (Interlocked.Increment(ref attempts) == 1)
                    {
                        return Task.FromException<DocumentServiceLease>(
                            new CosmosException("Transient renewal failure", System.Net.HttpStatusCode.ServiceUnavailable, 0, "test", 0));
                    }

                    DocumentServiceLease result = update(lease);
                    renewed.TrySetResult(true);
                    return Task.FromResult(result);
                });
            DocumentServiceLeaseManager manager = new DocumentServiceLeaseManagerInMemory(updater.Object, leases);
            manager.ConfigureQueryFilter(lease.QueryFilterHash);
            LeaseRenewer renewer = new LeaseRenewerCore(lease, manager, TimeSpan.FromMilliseconds(20));
            Mock.Get(this.partitionProcessor)
                .Setup(p => p.RunAsync(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(token => Task.Delay(Timeout.Infinite, token));
            using PartitionSupervisor supervisor = new PartitionSupervisorCore(lease, this.observer, this.partitionProcessor, renewer);
            Task running = supervisor.RunAsync(this.shutdownToken.Token);
            try
            {
                await renewed.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.IsFalse(running.IsCompleted);
                Assert.IsTrue(attempts >= 2);
            }
            finally
            {
                this.shutdownToken.Cancel();
                await running;
            }

            Mock.Get(this.observer).Verify(o => o.CloseAsync("partitionId", ChangeFeedObserverCloseReason.Shutdown), Times.Once);
        }

        [TestMethod]
        public async Task RunObserver_ShouldCloseWithObserverError_IfObserverFailed()
        {
            Mock.Get(this.partitionProcessor)
                .Setup(processor => processor.RunAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ChangeFeedProcessorUserException(new Exception(), Mock.Of<ChangeFeedProcessorContext>()));

            await Assert.ThrowsExceptionAsync<ChangeFeedProcessorUserException>(() => this.sut.RunAsync(this.shutdownToken.Token)).ConfigureAwait(false);

            Mock.Get(this.observer)
                .Verify(feedObserver => feedObserver
                    .CloseAsync(It.Is<string>(lt => lt == this.lease.CurrentLeaseToken),
                        ChangeFeedObserverCloseReason.ObserverError));
        }

        [TestMethod]
        public async Task RunObserver_ShouldPassPartitionToObserver_WhenExecuted()
        {
            Mock.Get(this.observer)
                .Setup(feedObserver => feedObserver.ProcessChangesAsync(It.IsAny<ChangeFeedObserverContextCore>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback(() => this.shutdownToken.Cancel());

            await this.sut.RunAsync(this.shutdownToken.Token).ConfigureAwait(false);
            Mock.Get(this.observer)
                .Verify(feedObserver => feedObserver
                    .OpenAsync(It.Is<string>(lt => lt == this.lease.CurrentLeaseToken)));
        }

        [TestMethod]
        public async Task RunObserver_CosmosException_IfProcessorFailedWithCosmosException()
        {
            Mock.Get(this.partitionProcessor)
                .Setup(processor => processor.RunAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CosmosException("processorException", System.Net.HttpStatusCode.NotFound, 0, Guid.NewGuid().ToString(), 0));

            Exception exception = await Assert.ThrowsExceptionAsync<CosmosException>(() => this.sut.RunAsync(this.shutdownToken.Token)).ConfigureAwait(false);
            Assert.AreEqual("processorException", exception.Message);

            Mock.Get(this.observer)
                .Verify(feedObserver => feedObserver
                    .CloseAsync(It.Is<string>(lt => lt == this.lease.CurrentLeaseToken),
                        ChangeFeedObserverCloseReason.CosmosException));
        }

        [TestMethod]
        public void Dispose_ShouldWork_WithoutRun()
        {
            try
            {
                this.sut.Dispose();
            }
            catch (Exception)
            {
                Assert.Fail();
            }
        }

        public void Dispose()
        {
            this.sut.Dispose();
            this.shutdownToken.Dispose();
        }
    }
}