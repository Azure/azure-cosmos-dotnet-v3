//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class ObserverExceptionWrappingChangeFeedObserverDecoratorTests
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly Mock<ChangeFeedObserver<MyDocument>> observer;
        private readonly ChangeFeedObserverContext changeFeedObserverContext;
        private readonly FeedProcessing.ObserverExceptionWrappingChangeFeedObserverDecorator<MyDocument> observerWrapper;
        private readonly IReadOnlyList<MyDocument> documents;

        public ObserverExceptionWrappingChangeFeedObserverDecoratorTests()
        {
            this.observer = new Mock<ChangeFeedObserver<MyDocument>>();
            this.changeFeedObserverContext = Mock.Of<ChangeFeedObserverContext>();
            this.observerWrapper = new FeedProcessing.ObserverExceptionWrappingChangeFeedObserverDecorator<MyDocument>(this.observer.Object);

            MyDocument document = new MyDocument();
            this.documents = new List<MyDocument> { document };
        }

        [TestMethod]
        public async Task OpenAsync_ShouldCallOpenAsync()
        {
            await this.observerWrapper.OpenAsync(this.changeFeedObserverContext);

            Mock.Get(this.observer.Object)
                .Verify(feedObserver => feedObserver.OpenAsync(It.IsAny<ChangeFeedObserverContext>()),
                    Times.Once);
        }

        [TestMethod]
        public async Task CloseAsync_ShouldCallCloseAsync()
        {
            await this.observerWrapper.CloseAsync(this.changeFeedObserverContext, ChangeFeedObserverCloseReason.Shutdown);

            Mock.Get(this.observer.Object)
                .Verify(feedObserver => feedObserver
                        .CloseAsync(It.IsAny<ChangeFeedObserverContext>(),
                        It.Is<ChangeFeedObserverCloseReason>(reason => reason == ChangeFeedObserverCloseReason.Shutdown)),
                    Times.Once);
        }

        [TestMethod]
        public async Task ProcessChangesAsync_ShouldPassDocumentsToProcessChangesAsync()
        {
            await this.observerWrapper.ProcessChangesAsync(this.changeFeedObserverContext, this.documents, this.cancellationTokenSource.Token);

            Mock.Get(this.observer.Object)
                .Verify(feedObserver => feedObserver
                        .ProcessChangesAsync(It.IsAny<ChangeFeedObserverContext>(),
                            It.Is<IReadOnlyList<MyDocument>>(list => this.documents.SequenceEqual(list)),
                            It.IsAny<CancellationToken>()
                        ),
                    Times.Once);
        }

        [TestMethod]
        public async Task ProcessChangesAsync_ShouldThrow_IfObserverThrows()
        {
            Mock.Get(this.observer.Object)
                .SetupSequence(feedObserver => feedObserver
                    .ProcessChangesAsync(It.IsAny<ChangeFeedObserverContext>(), It.IsAny<IReadOnlyList<MyDocument>>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception());

            try
            {
                await this.observerWrapper.ProcessChangesAsync(this.changeFeedObserverContext, this.documents, this.cancellationTokenSource.Token);
                Assert.Fail("Should had thrown");
            }
            catch (ObserverException ex)
            {
                Assert.IsInstanceOfType(ex.InnerException, typeof(Exception));
            }

            Mock.Get(this.observer.Object)
                .Verify(feedObserver => feedObserver
                        .ProcessChangesAsync(It.IsAny<ChangeFeedObserverContext>(),
                            It.Is<IReadOnlyList<MyDocument>>(list => this.documents.SequenceEqual(list)),
                            It.IsAny<CancellationToken>()
                        ),
                    Times.Once);
        }

        [TestMethod]
        public async Task ProcessChangesAsync_ShouldThrow_IfObserverThrowsDocumentClientException()
        {
            Mock.Get(this.observer.Object)
                .SetupSequence(feedObserver => feedObserver
                    .ProcessChangesAsync(It.IsAny<ChangeFeedObserverContext>(), It.IsAny<IReadOnlyList<MyDocument>>(), It.IsAny<CancellationToken>()))
                .Throws(new Documents.DocumentClientException("Some message", (HttpStatusCode) 429, Documents.SubStatusCodes.Unknown));

            try
            {
                await this.observerWrapper.ProcessChangesAsync(this.changeFeedObserverContext, this.documents, this.cancellationTokenSource.Token);
                Assert.Fail("Should had thrown");
            }
            catch (ObserverException ex)
            {
                Assert.IsInstanceOfType(ex.InnerException, typeof(Documents.DocumentClientException));
            }

            Mock.Get(this.observer.Object)
                .Verify(feedObserver => feedObserver
                        .ProcessChangesAsync(It.IsAny<ChangeFeedObserverContext>(),
                            It.Is<IReadOnlyList<MyDocument>>(list => this.documents.SequenceEqual(list)),
                            It.IsAny<CancellationToken>()
                        ),
                    Times.Once);
        }

        public class MyDocument
        {
            public string id { get; set; }
        }
    }
}
