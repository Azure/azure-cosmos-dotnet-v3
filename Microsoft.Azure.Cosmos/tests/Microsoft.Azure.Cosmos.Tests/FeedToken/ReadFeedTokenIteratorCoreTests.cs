//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class ReadFeedTokenIteratorCoreTests
    {
        [TestMethod]
        public void ReadFeedTokenIteratorCore_HasMoreResultsDefault()
        {
            FeedTokenIteratorCore feedTokenIterator = new FeedTokenIteratorCore(Mock.Of<ContainerCore>(), new Uri("http://localhost"), Documents.ResourceType.Document, null, null, null, new QueryRequestOptions());
            Assert.IsTrue(feedTokenIterator.HasMoreResults);
        }

        [TestMethod]
        public void ReadFeedTokenIteratorCore_FeedToken()
        {
            FeedTokenInternal feedToken = Mock.Of<FeedTokenInternal>();
            FeedTokenIteratorCore feedTokenIterator = new FeedTokenIteratorCore(Mock.Of<ContainerCore>(), new Uri("http://localhost"), Documents.ResourceType.Document, null, null, feedToken, new QueryRequestOptions());
            Assert.AreEqual(feedToken, feedTokenIterator.FeedToken);
        }

        [TestMethod]
        public void ReadFeedTokenIteratorCore_TryGetContinuation()
        {
            string continuation = Guid.NewGuid().ToString();
            FeedTokenInternal feedToken = Mock.Of<FeedTokenInternal>();
            Mock.Get(feedToken)
                .Setup(f => f.GetContinuation()).Returns(continuation);
            FeedTokenIteratorCore feedTokenIterator = new FeedTokenIteratorCore(Mock.Of<ContainerCore>(), new Uri("http://localhost"), Documents.ResourceType.Document, null, null, feedToken, new QueryRequestOptions());
            Assert.IsTrue(feedTokenIterator.TryGetContinuationToken(out string state));
            Assert.AreEqual(continuation, state);
        }

        [TestMethod]
        public async Task ReadFeedTokenIteratorCore_ReadNextAsync()
        {
            string continuation = "TBD";
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK);
            responseMessage.Headers.ContinuationToken = continuation;
            responseMessage.Headers[Documents.HttpConstants.HttpHeaders.ItemCount] = "1";

            Mock<CosmosClientContext> cosmosClientContext = new Mock<CosmosClientContext>();
            cosmosClientContext.Setup(c => c.ClientOptions).Returns(new CosmosClientOptions());
            cosmosClientContext
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<Documents.ResourceType>(),
                    It.IsAny<Documents.OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerCore>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<CosmosDiagnosticsContext>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(responseMessage));

            ContainerCore containerCore = Mock.Of<ContainerCore>();
            Mock.Get(containerCore)
                .Setup(c => c.ClientContext)
                .Returns(cosmosClientContext.Object);
            FeedTokenInternal feedToken = Mock.Of<FeedTokenInternal>();
            Mock.Get(feedToken)
                .Setup(f => f.EnrichRequest(It.IsAny<RequestMessage>()));
            Mock.Get(feedToken)
                .Setup(f => f.ShouldRetryAsync(It.Is<ContainerCore>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));
            Mock.Get(feedToken)
                .Setup(f => f.GetContinuation())
                .Returns(continuation);
            Mock.Get(feedToken)
                .Setup(f => f.IsDone)
                .Returns(true);

            FeedTokenIteratorCore feedTokenIterator = new FeedTokenIteratorCore(containerCore, new Uri("http://localhost"), Documents.ResourceType.Document, null, null, feedToken, new QueryRequestOptions());
            ResponseMessage response = await feedTokenIterator.ReadNextAsync();

            Assert.AreEqual(feedToken, feedTokenIterator.FeedToken);
            Mock.Get(feedToken)
                .Verify(f => f.UpdateContinuation(It.Is<string>(ct => ct == continuation)), Times.Once);

            Mock.Get(feedToken)
                .Verify(f => f.ShouldRetryAsync(It.Is<ContainerCore>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()), Times.Once);

            Mock.Get(feedToken)
                .Verify(f => f.IsDone, Times.Once);
        }

        [TestMethod]
        public async Task ReadFeedTokenIteratorCore_OfT_ReadNextAsync()
        {
            string continuation = "TBD";
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK);
            responseMessage.Headers.ContinuationToken = continuation;
            responseMessage.Headers[Documents.HttpConstants.HttpHeaders.ItemCount] = "1";

            Mock<CosmosClientContext> cosmosClientContext = new Mock<CosmosClientContext>();
            cosmosClientContext.Setup(c => c.ClientOptions).Returns(new CosmosClientOptions());
            cosmosClientContext
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<Documents.ResourceType>(),
                    It.IsAny<Documents.OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerCore>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<CosmosDiagnosticsContext>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(responseMessage));

            ContainerCore containerCore = Mock.Of<ContainerCore>();
            Mock.Get(containerCore)
                .Setup(c => c.ClientContext)
                .Returns(cosmosClientContext.Object);
            FeedTokenInternal feedToken = Mock.Of<FeedTokenInternal>();
            Mock.Get(feedToken)
                .Setup(f => f.EnrichRequest(It.IsAny<RequestMessage>()));
            Mock.Get(feedToken)
                .Setup(f => f.ShouldRetryAsync(It.Is<ContainerCore>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));
            Mock.Get(feedToken)
                .Setup(f => f.GetContinuation())
                .Returns(continuation);
            Mock.Get(feedToken)
                .Setup(f => f.IsDone)
                .Returns(true);

            FeedTokenIteratorCore feedTokenIterator = new FeedTokenIteratorCore(containerCore, new Uri("http://localhost"), Documents.ResourceType.Document, null, null, feedToken, new QueryRequestOptions());
            bool creatorCalled = false;
            Func<ResponseMessage, FeedResponse<dynamic>> creator = (ResponseMessage r) =>
            {
                creatorCalled = true;
                return Mock.Of<FeedResponse<dynamic>>();
            };

            FeedIteratorCore<dynamic> feedTokenIteratorOfT = new FeedIteratorCore<dynamic>(feedTokenIterator, creator);
            FeedResponse<dynamic> response = await feedTokenIteratorOfT.ReadNextAsync();

            Assert.IsTrue(creatorCalled, "Response creator not called");
            Mock.Get(feedToken)
                .Verify(f => f.UpdateContinuation(It.Is<string>(ct => ct == continuation)), Times.Once);

            Mock.Get(feedToken)
                .Verify(f => f.ShouldRetryAsync(It.Is<ContainerCore>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()), Times.Once);

            Mock.Get(feedToken)
                .Verify(f => f.IsDone, Times.Once);
        }

        [TestMethod]
        public async Task ReadFeedTokenIteratorCore_UpdatesContinuation_OnOK()
        {
            string continuation = "TBD";
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK);
            responseMessage.Headers.ContinuationToken = continuation;
            responseMessage.Headers[Documents.HttpConstants.HttpHeaders.ItemCount] = "1";

            Mock<CosmosClientContext> cosmosClientContext = new Mock<CosmosClientContext>();
            cosmosClientContext.Setup(c => c.ClientOptions).Returns(new CosmosClientOptions());
            cosmosClientContext
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<Documents.ResourceType>(),
                    It.IsAny<Documents.OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerCore>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<CosmosDiagnosticsContext>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(responseMessage));

            ContainerCore containerCore = Mock.Of<ContainerCore>();
            Mock.Get(containerCore)
                .Setup(c => c.ClientContext)
                .Returns(cosmosClientContext.Object);
            FeedTokenInternal feedToken = Mock.Of<FeedTokenInternal>();
            Mock.Get(feedToken)
                .Setup(f => f.EnrichRequest(It.IsAny<RequestMessage>()));
            Mock.Get(feedToken)
                .Setup(f => f.ShouldRetryAsync(It.Is<ContainerCore>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));
            Mock.Get(feedToken)
                .Setup(f => f.GetContinuation())
                .Returns(continuation);
            Mock.Get(feedToken)
                .Setup(f => f.IsDone)
                .Returns(true);

            FeedIterator feedTokenIterator = new FeedTokenIteratorCore(containerCore, new Uri("http://localhost"), Documents.ResourceType.Document, null, null, feedToken, new QueryRequestOptions());
            ResponseMessage response = await feedTokenIterator.ReadNextAsync();

            Mock.Get(feedToken)
                .Verify(f => f.UpdateContinuation(It.Is<string>(ct => ct == continuation)), Times.Once);

            Mock.Get(feedToken)
                .Verify(f => f.ShouldRetryAsync(It.Is<ContainerCore>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()), Times.Once);

            Mock.Get(feedToken)
                .Verify(f => f.IsDone, Times.Once);
        }

        [TestMethod]
        public async Task ReadFeedTokenIteratorCore_DoesNotUpdateContinuation_OnError()
        {
            string continuation = "TBD";
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.Gone);
            responseMessage.Headers.ContinuationToken = continuation;
            responseMessage.Headers[Documents.HttpConstants.HttpHeaders.ItemCount] = "1";

            Mock<CosmosClientContext> cosmosClientContext = new Mock<CosmosClientContext>();
            cosmosClientContext.Setup(c => c.ClientOptions).Returns(new CosmosClientOptions());
            cosmosClientContext
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<Documents.ResourceType>(),
                    It.IsAny<Documents.OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerCore>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<CosmosDiagnosticsContext>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(responseMessage));

            ContainerCore containerCore = Mock.Of<ContainerCore>();
            Mock.Get(containerCore)
                .Setup(c => c.ClientContext)
                .Returns(cosmosClientContext.Object);
            FeedTokenInternal feedToken = Mock.Of<FeedTokenInternal>();
            Mock.Get(feedToken)
                .Setup(f => f.EnrichRequest(It.IsAny<RequestMessage>()));
            Mock.Get(feedToken)
                .Setup(f => f.ShouldRetryAsync(It.Is<ContainerCore>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));
            Mock.Get(feedToken)
                .Setup(f => f.GetContinuation())
                .Returns(continuation);
            Mock.Get(feedToken)
                .Setup(f => f.IsDone)
                .Returns(true);

            FeedIterator feedTokenIterator = new FeedTokenIteratorCore(containerCore, new Uri("http://localhost"), Documents.ResourceType.Document, null, null, feedToken, new QueryRequestOptions());
            ResponseMessage response = await feedTokenIterator.ReadNextAsync();

            Mock.Get(feedToken)
                .Verify(f => f.UpdateContinuation(It.Is<string>(ct => ct == continuation)), Times.Never);

            Mock.Get(feedToken)
                .Verify(f => f.ShouldRetryAsync(It.Is<ContainerCore>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()), Times.Once);

            Mock.Get(feedToken)
                .Verify(f => f.IsDone, Times.Once);
        }
    }
}
