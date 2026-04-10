//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class ObserverExceptionWrappingChangeFeedObserverDecoratorTests
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly Mock<ChangeFeedObserver> observer;
        private readonly ChangeFeedObserverContextCore changeFeedObserverContext;
        private readonly ObserverExceptionWrappingChangeFeedObserverDecorator observerWrapper;
        private readonly IReadOnlyList<MyDocument> documents;
        private readonly CosmosSerializerCore serializerCore;

        public ObserverExceptionWrappingChangeFeedObserverDecoratorTests()
        {
            this.observer = new Mock<ChangeFeedObserver>();
            this.changeFeedObserverContext = new ChangeFeedObserverContextCore(Guid.NewGuid().ToString(), feedResponse: null, Mock.Of<PartitionCheckpointer>(), FeedRangeEpk.FullRange); ;
            this.observerWrapper = new ObserverExceptionWrappingChangeFeedObserverDecorator(this.observer.Object);

            this.serializerCore = new CosmosSerializerCore();

            MyDocument document = new MyDocument();
            this.documents = new List<MyDocument> { document };
        }

        [TestMethod]
        public async Task OpenAsync_ShouldCallOpenAsync()
        {
            await this.observerWrapper.OpenAsync(this.changeFeedObserverContext.LeaseToken);

            Mock.Get(this.observer.Object)
                .Verify(feedObserver => feedObserver.OpenAsync(It.IsAny<string>()),
                    Times.Once);
        }

        [TestMethod]
        public async Task CloseAsync_ShouldCallCloseAsync()
        {
            await this.observerWrapper.CloseAsync(this.changeFeedObserverContext.LeaseToken, ChangeFeedObserverCloseReason.Shutdown);

            Mock.Get(this.observer.Object)
                .Verify(feedObserver => feedObserver
                        .CloseAsync(It.IsAny<string>(),
                        It.Is<ChangeFeedObserverCloseReason>(reason => reason == ChangeFeedObserverCloseReason.Shutdown)),
                    Times.Once);
        }

        [TestMethod]
        public async Task ProcessChangesAsync_ShouldPassDocumentsToProcessChangesAsync()
        {
            using Stream stream = this.serializerCore.ToStream(this.documents);
            await this.observerWrapper.ProcessChangesAsync(this.changeFeedObserverContext, stream, this.cancellationTokenSource.Token);

            Mock.Get(this.observer.Object)
                .Verify(feedObserver => feedObserver
                        .ProcessChangesAsync(It.IsAny<ChangeFeedObserverContextCore>(),
                            It.Is<MemoryStream>(stream => this.ValidateStream(stream)),
                            It.IsAny<CancellationToken>()
                        ),
                    Times.Once);
        }

        [TestMethod]
        public async Task ProcessChangesAsync_ShouldThrow_IfObserverThrows()
        {
            using Stream stream = this.serializerCore.ToStream(this.documents);
            Mock.Get(this.observer.Object)
                .SetupSequence(feedObserver => feedObserver
                    .ProcessChangesAsync(It.IsAny<ChangeFeedObserverContextCore>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception());

            try
            {
                await this.observerWrapper.ProcessChangesAsync(this.changeFeedObserverContext, stream, this.cancellationTokenSource.Token);
                Assert.Fail("Should had thrown");
            }
            catch (ChangeFeedProcessorUserException ex)
            {
                Assert.IsInstanceOfType(ex.InnerException, typeof(Exception));
            }

            Mock.Get(this.observer.Object)
                .Verify(feedObserver => feedObserver
                        .ProcessChangesAsync(It.IsAny<ChangeFeedObserverContextCore>(),
                            It.Is<MemoryStream>(stream => this.ValidateStream(stream)),
                            It.IsAny<CancellationToken>()
                        ),
                    Times.Once);
        }

        [TestMethod]
        public async Task ProcessChangesAsync_ShouldThrow_IfObserverThrowsDocumentClientException()
        {
            using Stream stream = this.serializerCore.ToStream(this.documents);
            Mock.Get(this.observer.Object)
                .SetupSequence(feedObserver => feedObserver
                    .ProcessChangesAsync(It.IsAny<ChangeFeedObserverContextCore>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Throws(new CosmosException("Some message", (HttpStatusCode)429, (int)Documents.SubStatusCodes.Unknown, Guid.NewGuid().ToString(), 0));

            try
            {
                await this.observerWrapper.ProcessChangesAsync(this.changeFeedObserverContext, stream, this.cancellationTokenSource.Token);
                Assert.Fail("Should had thrown");
            }
            catch (ChangeFeedProcessorUserException ex)
            {
                Assert.IsInstanceOfType(ex.InnerException, typeof(CosmosException));
            }

            Mock.Get(this.observer.Object)
                .Verify(feedObserver => feedObserver
                        .ProcessChangesAsync(It.IsAny<ChangeFeedObserverContextCore>(),
                            It.Is<MemoryStream>(stream => this.ValidateStream(stream)),
                            It.IsAny<CancellationToken>()
                        ),
                    Times.Once);
        }

        private bool ValidateStream(Stream stream)
        {
            IEnumerable<MyDocument> asEnumerable = CosmosFeedResponseSerializer.FromFeedResponseStream<MyDocument>(this.serializerCore, stream);
            return this.documents.SequenceEqual(asEnumerable, new MyDocument.Comparer());
        }

        public class MyDocument
        {
            public string id { get; set; }

            public class Comparer : IEqualityComparer<MyDocument>
            {
                public bool Equals(MyDocument x, MyDocument y)
                {
                    return x.id?.Equals(y?.id) ?? y.id == null;
                }

                public int GetHashCode(MyDocument obj)
                {
                    return obj.id.GetHashCode();
                }
            }
        }
    }
}