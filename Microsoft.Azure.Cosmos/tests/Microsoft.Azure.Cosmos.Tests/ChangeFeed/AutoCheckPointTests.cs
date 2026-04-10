//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class AutoCheckPointTests
    {
        private readonly ChangeFeedObserver changeFeedObserver;
        private readonly ChangeFeedObserverContextCore observerContext;
        private readonly AutoCheckpointer sut;
        private readonly Stream stream;
        private readonly Mock<PartitionCheckpointer> partitionCheckpointer;

        public AutoCheckPointTests()
        {
            this.changeFeedObserver = Mock.Of<ChangeFeedObserver>();
            this.partitionCheckpointer = new Mock<PartitionCheckpointer>();
            this.partitionCheckpointer
                .Setup(checkPointer => checkPointer.CheckpointPartitionAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            this.sut = new AutoCheckpointer(this.changeFeedObserver);

            this.stream = Mock.Of<Stream>();

            ResponseMessage responseMessage = new ResponseMessage();
            responseMessage.Headers.ContinuationToken = Guid.NewGuid().ToString();
            this.observerContext = new ChangeFeedObserverContextCore(Guid.NewGuid().ToString(), feedResponse: responseMessage, this.partitionCheckpointer.Object, FeedRangeEpk.FullRange);
        }

        [TestMethod]
        public async Task OpenAsync_WhenCalled_ShouldOpenObserver()
        {
            await this.sut.OpenAsync(this.observerContext.LeaseToken);

            Mock.Get(this.changeFeedObserver)
                .Verify(observer => observer.OpenAsync(this.observerContext.LeaseToken), Times.Once);
        }

        [TestMethod]
        public async Task CloseAsync_WhenCalled_ShouldCloseObserver()
        {
            await this.sut.CloseAsync(this.observerContext.LeaseToken, ChangeFeedObserverCloseReason.ResourceGone);

            Mock.Get(this.changeFeedObserver)
                .Verify(observer => observer.CloseAsync(this.observerContext.LeaseToken, ChangeFeedObserverCloseReason.ResourceGone), Times.Once);
        }

        [TestMethod]
        public async Task ProcessChanges_WhenCalled_ShouldPassTheBatch()
        {
            await this.sut.ProcessChangesAsync(this.observerContext, this.stream, CancellationToken.None);

            Mock.Get(this.changeFeedObserver)
                .Verify(observer => observer.ProcessChangesAsync(this.observerContext, this.stream, CancellationToken.None), Times.Once);

            this.partitionCheckpointer.Verify(c => c.CheckpointPartitionAsync(It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        public async Task ProcessChanges_WhenCheckpointThrows_ShouldThrow()
        {
            CosmosException original = CosmosExceptionFactory.CreateThrottledException("throttled", new Headers());
            Mock<PartitionCheckpointer> checkpointer = new Mock<PartitionCheckpointer>();
            checkpointer.Setup(c => c.CheckpointPartitionAsync(It.IsAny<string>())).ThrowsAsync(original);

            ResponseMessage responseMessage = new ResponseMessage();
            responseMessage.Headers.ContinuationToken = Guid.NewGuid().ToString();
            ChangeFeedObserverContextCore observerContext = new ChangeFeedObserverContextCore(Guid.NewGuid().ToString(), feedResponse: responseMessage, checkpointer.Object, FeedRangeEpk.FullRange);

            CosmosException caught = await Assert.ThrowsExceptionAsync<CosmosException>(() => this.sut.ProcessChangesAsync(observerContext, this.stream, CancellationToken.None));
            Assert.AreEqual(original, caught);
        }
    }
}