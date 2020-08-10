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
        private readonly ChangeFeedObserver<dynamic> observer;
        private readonly CancellationTokenSource shutdownToken = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        private readonly PartitionSupervisor sut;

        public PartitionSupervisorTests()
        {
            lease = Mock.Of<DocumentServiceLease>();
            Mock.Get(lease)
                .Setup(l => l.CurrentLeaseToken)
                .Returns("partitionId");

            leaseRenewer = Mock.Of<LeaseRenewer>();
            partitionProcessor = Mock.Of<FeedProcessor>();
            observer = Mock.Of<ChangeFeedObserver<dynamic>>();

            sut = new PartitionSupervisorCore<dynamic>(lease, observer, partitionProcessor, leaseRenewer);
        }

        [TestMethod]
        public async Task RunObserver_ShouldCancelTasks_WhenTokenCanceled()
        {
            Task renewerTask = Task.FromResult(false);
            Mock.Get(leaseRenewer)
                .Setup(renewer => renewer.RunAsync(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(token => renewerTask = Task.Delay(TimeSpan.FromMinutes(1), token));

            Task processorTask = Task.FromResult(false);
            Mock.Get(partitionProcessor)
                .Setup(processor => processor.RunAsync(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(token => processorTask = Task.Delay(TimeSpan.FromMinutes(1), token));

            Task supervisorTask = sut.RunAsync(shutdownToken.Token);

            Task delay = Task.Delay(TimeSpan.FromMilliseconds(100));
            Task finished = await Task.WhenAny(supervisorTask, delay).ConfigureAwait(false);
            Assert.AreEqual(delay, finished);

            shutdownToken.Cancel();
            await supervisorTask.ConfigureAwait(false);

            Assert.IsTrue(renewerTask.IsCanceled);
            Assert.IsTrue(processorTask.IsCanceled);
            Mock.Get(partitionProcessor)
                .Verify(processor => processor.RunAsync(It.IsAny<CancellationToken>()), Times.Once);

            Mock.Get(observer)
                .Verify(feedObserver => feedObserver
                    .CloseAsync(It.Is<ChangeFeedProcessorContext>(context => context.LeaseToken == lease.CurrentLeaseToken),
                        ChangeFeedObserverCloseReason.Shutdown));
        }

        [TestMethod]
        public async Task RunObserver_ShouldCancelProcessor_IfRenewerFailed()
        {
            Task processorTask = Task.FromResult(false);
            Mock.Get(leaseRenewer)
                .Setup(renewer => renewer.RunAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new LeaseLostException());

            Mock.Get(partitionProcessor)
                .Setup(processor => processor.RunAsync(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(token => processorTask = Task.Delay(TimeSpan.FromMinutes(1), token));

            await Assert.ThrowsExceptionAsync<LeaseLostException>(() => sut.RunAsync(shutdownToken.Token)).ConfigureAwait(false);
            Assert.IsTrue(processorTask.IsCanceled);

            Mock.Get(observer)
                .Verify(feedObserver => feedObserver
                    .CloseAsync(It.Is<ChangeFeedProcessorContext>(context => context.LeaseToken == lease.CurrentLeaseToken),
                        ChangeFeedObserverCloseReason.LeaseLost));
        }

        [TestMethod]
        public async Task RunObserver_ShouldCancelRenewer_IfProcessorFailed()
        {
            Task renewerTask = Task.FromResult(false);
            Mock.Get(leaseRenewer)
                .Setup(renewer => renewer.RunAsync(It.IsAny<CancellationToken>()))
                .Returns<CancellationToken>(token => renewerTask = Task.Delay(TimeSpan.FromMinutes(1), token));

            Mock.Get(partitionProcessor)
                .Setup(processor => processor.RunAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("processorException"));

            await Assert.ThrowsExceptionAsync<Exception>(() => sut.RunAsync(shutdownToken.Token)).ConfigureAwait(false);
            Assert.IsTrue(renewerTask.IsCanceled);

            Mock.Get(observer)
                .Verify(feedObserver => feedObserver
                    .CloseAsync(It.Is<ChangeFeedProcessorContext>(context => context.LeaseToken == lease.CurrentLeaseToken),
                        ChangeFeedObserverCloseReason.Unknown));
        }

        [TestMethod]
        public async Task RunObserver_ShouldCloseWithObserverError_IfObserverFailed()
        {
            Mock.Get(partitionProcessor)
                .Setup(processor => processor.RunAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ObserverException(new Exception()));

            await Assert.ThrowsExceptionAsync<ObserverException>(() => sut.RunAsync(shutdownToken.Token)).ConfigureAwait(false);

            Mock.Get(observer)
                .Verify(feedObserver => feedObserver
                    .CloseAsync(It.Is<ChangeFeedProcessorContext>(context => context.LeaseToken == lease.CurrentLeaseToken),
                        ChangeFeedObserverCloseReason.ObserverError));
        }

        [TestMethod]
        public async Task RunObserver_ShouldPassPartitionToObserver_WhenExecuted()
        {
            Mock.Get(observer)
                .Setup(feedObserver => feedObserver.ProcessChangesAsync(It.IsAny<ChangeFeedProcessorContext>(), It.IsAny<IReadOnlyList<dynamic>>(), It.IsAny<CancellationToken>()))
                .Callback(() => shutdownToken.Cancel());

            await sut.RunAsync(shutdownToken.Token).ConfigureAwait(false);
            Mock.Get(observer)
                .Verify(feedObserver => feedObserver
                    .OpenAsync(It.Is<ChangeFeedProcessorContext>(context => context.LeaseToken == lease.CurrentLeaseToken)));
        }

        [TestMethod]
        public void Dispose_ShouldWork_WithoutRun()
        {
            try
            {
                sut.Dispose();
            }
            catch (Exception)
            {
                Assert.Fail();
            }
        }

        public void Dispose()
        {
            sut.Dispose();
            shutdownToken.Dispose();
        }
    }
}
