//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class AutoCheckPointTests
    {
        private readonly ChangeFeedObserver changeFeedObserver;
        private readonly ChangeFeedObserverContext observerContext;
        private readonly AutoCheckpointer sut;
        private readonly Stream stream;
        private readonly PartitionCheckpointer partitionCheckpointer;

        public AutoCheckPointTests()
        {
            this.changeFeedObserver = Mock.Of<ChangeFeedObserver>();
            this.partitionCheckpointer = Mock.Of<PartitionCheckpointer>();
            Mock.Get(this.partitionCheckpointer)
                .Setup(checkPointer => checkPointer.CheckpointPartitionAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            this.sut = new AutoCheckpointer(this.changeFeedObserver);

            this.stream = Mock.Of<Stream>();

            this.observerContext = Mock.Of<ChangeFeedObserverContext>();
            Mock.Get(this.observerContext)
                .Setup(context => context.CheckpointAsync())
                .Returns(this.partitionCheckpointer.CheckpointPartitionAsync("token"));
        }

        [TestMethod]
        public async Task OpenAsync_WhenCalled_ShouldOpenObserver()
        {
            await this.sut.OpenAsync(this.observerContext);

            Mock.Get(this.changeFeedObserver)
                .Verify(observer => observer.OpenAsync(this.observerContext), Times.Once);
        }

        [TestMethod]
        public async Task CloseAsync_WhenCalled_ShouldCloseObserver()
        {
            await this.sut.CloseAsync(this.observerContext, ChangeFeedObserverCloseReason.ResourceGone);

            Mock.Get(this.changeFeedObserver)
                .Verify(observer => observer.CloseAsync(this.observerContext, ChangeFeedObserverCloseReason.ResourceGone), Times.Once);
        }

        [TestMethod]
        public async Task ProcessChanges_WhenCalled_ShouldPassTheBatch()
        {
            await this.sut.ProcessChangesAsync(this.observerContext, this.stream, CancellationToken.None);

            Mock.Get(this.changeFeedObserver)
                .Verify(observer => observer.ProcessChangesAsync(this.observerContext, this.stream, CancellationToken.None), Times.Once);
        }

        [TestMethod]
        public async Task ProcessChanges_WhenCheckpointThrows_ShouldThrow()
        {
            ChangeFeedObserverContext observerContext = Mock.Of<ChangeFeedObserverContext>();
            Mock.Get(observerContext).Setup(abs => abs.CheckpointAsync()).Throws(new LeaseLostException());

            await Assert.ThrowsExceptionAsync<LeaseLostException>(() => this.sut.ProcessChangesAsync(observerContext, this.stream, CancellationToken.None));
        }
    }
}
