//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
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
        private readonly Mock<ChangeFeedObserver> observer;
        private readonly ChangeFeedObserverContext changeFeedObserverContext;
        private readonly ObserverExceptionWrappingChangeFeedObserverDecorator observerWrapper;
        private readonly IReadOnlyList<MyDocument> documents;
        private readonly CosmosSerializerCore serializerCore;

        public ObserverExceptionWrappingChangeFeedObserverDecoratorTests()
        {
            this.observer = new Mock<ChangeFeedObserver>();
            this.changeFeedObserverContext = Mock.Of<ChangeFeedObserverContext>();
            this.observerWrapper = new FeedProcessing.ObserverExceptionWrappingChangeFeedObserverDecorator(this.observer.Object);
            this.serializerCore = new CosmosSerializerCore();

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
                            It.Is<MemoryStream>(stream => this.ValidateStream(stream)),
                            It.IsAny<CancellationToken>()
                        ),
                    Times.Once);
        }

        [TestMethod]
        public async Task ProcessChangesAsync_ShouldThrow_IfObserverThrows()
        {
            Mock.Get(this.observer.Object)
                .SetupSequence(feedObserver => feedObserver
                    .ProcessChangesAsync(It.IsAny<ChangeFeedObserverContext>(), It.IsAny<MemoryStream>(), It.IsAny<CancellationToken>()))
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
                            It.Is<MemoryStream>(stream => this.ValidateStream(stream)),
                            It.IsAny<CancellationToken>()
                        ),
                    Times.Once);
        }

        [TestMethod]
        public async Task ProcessChangesAsync_ShouldThrow_IfObserverThrowsDocumentClientException()
        {
            Mock.Get(this.observer.Object)
                .SetupSequence(feedObserver => feedObserver
                    .ProcessChangesAsync(It.IsAny<ChangeFeedObserverContext>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Throws(new Documents.DocumentClientException("Some message", (HttpStatusCode)429, Documents.SubStatusCodes.Unknown));

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
                            It.Is<MemoryStream>(stream => this.ValidateStream(stream)),
                            It.IsAny<CancellationToken>()
                        ),
                    Times.Once);
        }

        private bool ValidateStream(Stream stream)
        {
            IEnumerable<MyDocument> asEnumerable = this.serializerCore.FromFeedStream<MyDocument>(stream);
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
