//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Bootstrapping;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class AutoCheckPointTests
    {
        private readonly ChangeFeedObserver<dynamic> changeFeedObserver;
        private readonly ChangeFeedObserverContext observerContext;
        private readonly CheckpointFrequency checkpointFrequency;
        private readonly AutoCheckpointer<dynamic> sut;
        private readonly IReadOnlyList<dynamic> documents;
        private readonly PartitionCheckpointer partitionCheckpointer;

        public AutoCheckPointTests()
        {
            changeFeedObserver = Mock.Of<ChangeFeedObserver<dynamic>>();
            partitionCheckpointer = Mock.Of<PartitionCheckpointer>();
            Mock.Get(partitionCheckpointer)
                .Setup(checkPointer => checkPointer.CheckpointPartitionAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            checkpointFrequency = new CheckpointFrequency();
            sut = new AutoCheckpointer<dynamic>(checkpointFrequency, changeFeedObserver);

            documents = Mock.Of<IReadOnlyList<dynamic>>();

            observerContext = Mock.Of<ChangeFeedObserverContext>();
            Mock.Get(observerContext)
                .Setup(context => context.CheckpointAsync())
                .Returns(partitionCheckpointer.CheckpointPartitionAsync("token"));
        }

        [TestMethod]
        public async Task OpenAsync_WhenCalled_ShouldOpenObserver()
        {
            await sut.OpenAsync(observerContext);

            Mock.Get(changeFeedObserver)
                .Verify(observer => observer.OpenAsync(observerContext), Times.Once);
        }

        [TestMethod]
        public async Task CloseAsync_WhenCalled_ShouldCloseObserver()
        {
            await sut.CloseAsync(observerContext, ChangeFeedObserverCloseReason.ResourceGone);

            Mock.Get(changeFeedObserver)
                .Verify(observer => observer.CloseAsync(observerContext, ChangeFeedObserverCloseReason.ResourceGone), Times.Once);
        }

        [TestMethod]
        public async Task ProcessChanges_WhenCalled_ShouldPassTheBatch()
        {
            await sut.ProcessChangesAsync(observerContext, documents, CancellationToken.None);

            Mock.Get(changeFeedObserver)
                .Verify(observer => observer.ProcessChangesAsync(observerContext, documents, CancellationToken.None), Times.Once);
        }

        [TestMethod]
        public async Task ProcessChanges_WhenCheckpointThrows_ShouldThrow()
        {
            checkpointFrequency.TimeInterval = TimeSpan.Zero;

            ChangeFeedObserverContext observerContext = Mock.Of<ChangeFeedObserverContext>();
            Mock.Get(observerContext).Setup(abs => abs.CheckpointAsync()).Throws(new LeaseLostException());

            await Assert.ThrowsExceptionAsync<LeaseLostException>(() => sut.ProcessChangesAsync(observerContext, documents, CancellationToken.None));
        }

        [TestMethod]
        public async Task ProcessChanges_WhenPeriodPass_ShouldCheckpoint()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            checkpointFrequency.TimeInterval = TimeSpan.FromHours(1);
            await sut.ProcessChangesAsync(observerContext, documents, CancellationToken.None);
            Mock.Get(observerContext)
                .Verify(context => context.CheckpointAsync(), Times.Never);

            await Task.Delay(TimeSpan.FromSeconds(1));

            checkpointFrequency.TimeInterval = stopwatch.Elapsed;
            await sut.ProcessChangesAsync(observerContext, documents, CancellationToken.None);
            Mock.Get(observerContext)
                .Verify(context => context.CheckpointAsync(), Times.Once);
        }

        [TestMethod]
        public async Task ProcessChanges_WithDocTrigger_ShouldCheckpointWhenAbove()
        {
            Mock.Get(documents)
                .Setup(list => list.Count)
                .Returns(1);

            checkpointFrequency.ProcessedDocumentCount = 2;

            await sut.ProcessChangesAsync(observerContext, documents, CancellationToken.None);
            Mock.Get(observerContext)
                .Verify(context => context.CheckpointAsync(), Times.Never);

            await sut.ProcessChangesAsync(observerContext, documents, CancellationToken.None);
            Mock.Get(observerContext)
                .Verify(context => context.CheckpointAsync(), Times.Once);

            await sut.ProcessChangesAsync(observerContext, documents, CancellationToken.None);
            Mock.Get(observerContext)
                .Verify(context => context.CheckpointAsync(), Times.Once);

            await sut.ProcessChangesAsync(observerContext, documents, CancellationToken.None);
            Mock.Get(observerContext)
                .Verify(context => context.CheckpointAsync(), Times.Exactly(2));
        }
    }
}
