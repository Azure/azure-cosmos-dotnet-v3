//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class ReadFeedIteratorCoreTests
    {
        [TestMethod]
        public void ReadFeedIteratorCore_HasMoreResultsDefault()
        {
            FeedIteratorCore feedTokenIterator = FeedIteratorCore.CreateForPartitionedResource(Mock.Of<ContainerCore>(), new Uri("http://localhost"), Documents.ResourceType.Document, null, null, null, new QueryRequestOptions());
            Assert.IsTrue(feedTokenIterator.HasMoreResults);
        }

        [TestMethod]
        public void ReadFeedIteratorCore_FeedToken()
        {
            FeedTokenInternal feedToken = Mock.Of<FeedTokenInternal>();
            FeedIteratorCore feedTokenIterator = FeedIteratorCore.CreateForPartitionedResource(Mock.Of<ContainerCore>(), new Uri("http://localhost"), Documents.ResourceType.Document, null, null, feedToken, new QueryRequestOptions());
            Assert.AreEqual(feedToken, feedTokenIterator.FeedToken);
        }

        [TestMethod]
        public void ReadFeedIteratorCore_TryGetContinuation()
        {
            string continuation = Guid.NewGuid().ToString();
            FeedTokenInternal feedToken = Mock.Of<FeedTokenInternal>();
            Mock.Get(feedToken)
                .Setup(f => f.GetContinuation()).Returns(continuation);
            FeedIteratorCore feedTokenIterator = FeedIteratorCore.CreateForPartitionedResource(Mock.Of<ContainerCore>(), new Uri("http://localhost"), Documents.ResourceType.Document, null, null, feedToken, new QueryRequestOptions());
            Assert.AreEqual(continuation, feedTokenIterator.ContinuationToken);
        }

        [TestMethod]
        public async Task ReadFeedIteratorCore_ReadNextAsync()
        {
            string continuation = "TBD";
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK);
            responseMessage.Headers.ContinuationToken = continuation;
            responseMessage.Headers[Documents.HttpConstants.HttpHeaders.ItemCount] = "1";
            responseMessage.Content = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

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

            FeedIteratorCore feedTokenIterator = FeedIteratorCore.CreateForPartitionedResource(containerCore, new Uri("http://localhost"), Documents.ResourceType.Document, null, null, feedToken, new QueryRequestOptions());
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
        public async Task ReadFeedIteratorCore_OfT_ReadNextAsync()
        {
            string continuation = "TBD";
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK);
            responseMessage.Headers.ContinuationToken = continuation;
            responseMessage.Headers[Documents.HttpConstants.HttpHeaders.ItemCount] = "1";
            responseMessage.Content = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

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

            FeedIteratorCore feedTokenIterator = FeedIteratorCore.CreateForPartitionedResource(containerCore, new Uri("http://localhost"), Documents.ResourceType.Document, null, null, feedToken, new QueryRequestOptions());
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
        public async Task ReadFeedIteratorCore_UpdatesContinuation_OnOK()
        {
            string continuation = "TBD";
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK);
            responseMessage.Headers.ContinuationToken = continuation;
            responseMessage.Headers[Documents.HttpConstants.HttpHeaders.ItemCount] = "1";
            responseMessage.Content = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

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

            FeedIterator feedTokenIterator = FeedIteratorCore.CreateForPartitionedResource(containerCore, new Uri("http://localhost"), Documents.ResourceType.Document, null, null, feedToken, new QueryRequestOptions());
            ResponseMessage response = await feedTokenIterator.ReadNextAsync();

            Mock.Get(feedToken)
                .Verify(f => f.UpdateContinuation(It.Is<string>(ct => ct == continuation)), Times.Once);

            Mock.Get(feedToken)
                .Verify(f => f.ShouldRetryAsync(It.Is<ContainerCore>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()), Times.Once);

            Mock.Get(feedToken)
                .Verify(f => f.IsDone, Times.Once);
        }

        [TestMethod]
        public async Task ReadFeedIteratorCore_DoesNotUpdateContinuation_OnError()
        {
            string continuation = "TBD";
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.Gone);
            responseMessage.Headers.ContinuationToken = continuation;
            responseMessage.Headers[Documents.HttpConstants.HttpHeaders.ItemCount] = "1";
            responseMessage.Content = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

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

            FeedIterator feedTokenIterator = FeedIteratorCore.CreateForPartitionedResource(containerCore, new Uri("http://localhost"), Documents.ResourceType.Document, null, null, feedToken, new QueryRequestOptions());
            ResponseMessage response = await feedTokenIterator.ReadNextAsync();

            Mock.Get(feedToken)
                .Verify(f => f.UpdateContinuation(It.Is<string>(ct => ct == continuation)), Times.Never);

            Mock.Get(feedToken)
                .Verify(f => f.ShouldRetryAsync(It.Is<ContainerCore>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()), Times.Once);

            Mock.Get(feedToken)
                .Verify(f => f.IsDone, Times.Never);
        }

        [TestMethod]
        public async Task ReadFeedIteratorCore_WithNoInitialState_ReadNextAsync()
        {
            string continuation = "TBD";
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK);
            responseMessage.Headers.ContinuationToken = continuation;
            responseMessage.Headers[Documents.HttpConstants.HttpHeaders.ItemCount] = "1";
            responseMessage.Content = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

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

            FeedIteratorCore feedTokenIterator = FeedIteratorCore.CreateForPartitionedResource(containerCore, new Uri("http://localhost"), Documents.ResourceType.Document, null, null, null, new QueryRequestOptions());
            ResponseMessage response = await feedTokenIterator.ReadNextAsync();

            FeedToken feedTokenOut = feedTokenIterator.FeedToken;
            Assert.IsNotNull(feedTokenOut);

            FeedTokenEPKRange feedTokenEPKRange = feedTokenOut as FeedTokenEPKRange;
            // Assert that a FeedToken for the entire range is used
            Assert.AreEqual(Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey, feedTokenEPKRange.CompleteRange.Min);
            Assert.AreEqual(Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey, feedTokenEPKRange.CompleteRange.Max);
            Assert.AreEqual(continuation, feedTokenEPKRange.CompositeContinuationTokens.Peek().Token);
            Assert.IsFalse(feedTokenEPKRange.IsDone);
        }

        [TestMethod]
        public async Task ReadFeedIteratorCore_ForNonPartitionedResource_WithNoInitialState_ReadNextAsync()
        {
            string continuation = "TBD";
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK);
            responseMessage.Headers.ContinuationToken = continuation;
            responseMessage.Headers[Documents.HttpConstants.HttpHeaders.ItemCount] = "1";
            responseMessage.Content = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

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

            FeedIteratorCore feedTokenIterator = FeedIteratorCore.CreateForNonPartitionedResource(cosmosClientContext.Object, new Uri("http://localhost"), Documents.ResourceType.Document, null, null, new QueryRequestOptions());
            ResponseMessage response = await feedTokenIterator.ReadNextAsync();

            FeedToken feedTokenOut = feedTokenIterator.FeedToken;
            Assert.IsNotNull(feedTokenOut);

            FeedTokenEPKRange feedTokenEPKRange = feedTokenOut as FeedTokenEPKRange;
            // Assert that a FeedToken for the entire range is used
            Assert.AreEqual(Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey, feedTokenEPKRange.CompleteRange.Min);
            Assert.AreEqual(Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey, feedTokenEPKRange.CompleteRange.Max);
            Assert.AreEqual(continuation, feedTokenEPKRange.CompositeContinuationTokens.Peek().Token);
            Assert.IsFalse(feedTokenEPKRange.IsDone);
        }

        [TestMethod]
        public async Task ReadFeedIteratorCore_HandlesSplitsThroughPipeline()
        {
            int executionCount = 0;
            CosmosClientContext cosmosClientContext = GetMockedClientContext((RequestMessage requestMessage, CancellationToken cancellationToken) =>
            {
                // Force OnBeforeRequestActions call
                requestMessage.ToDocumentServiceRequest();
                if (executionCount++ == 0)
                {
                    return TestHandler.ReturnStatusCode(HttpStatusCode.Gone, Documents.SubStatusCodes.PartitionKeyRangeGone);
                }

                return TestHandler.ReturnStatusCode(HttpStatusCode.OK);
            });

            ContainerCore containerCore = Mock.Of<ContainerCore>();
            Mock.Get(containerCore)
                .Setup(c => c.ClientContext)
                .Returns(cosmosClientContext);
            Mock.Get(containerCore)
                .Setup(c => c.LinkUri)
                .Returns(new Uri($"/dbs/db/colls/colls", UriKind.Relative));
            FeedTokenInternal feedToken = Mock.Of<FeedTokenInternal>();
            Mock.Get(feedToken)
                .Setup(f => f.EnrichRequest(It.IsAny<RequestMessage>()));
            Mock.Get(feedToken)
                .Setup(f => f.ShouldRetryAsync(It.Is<ContainerCore>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));

            FeedIteratorCore changeFeedIteratorCore = FeedIteratorCore.CreateForPartitionedResource(containerCore, new Uri($"/dbs/db/colls/colls", UriKind.Relative), Documents.ResourceType.Document, null, null, feedToken, new QueryRequestOptions());

            ResponseMessage response = await changeFeedIteratorCore.ReadNextAsync();

            Assert.AreEqual(1, executionCount, "Pipeline handled the Split");
            Assert.AreEqual(HttpStatusCode.Gone, response.StatusCode);
        }

        private static CosmosClientContext GetMockedClientContext(
            Func<RequestMessage, CancellationToken, Task<ResponseMessage>> handlerFunc)
        {
            CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            CosmosClientContext clientContext = ClientContextCore.Create(
               client,
               new MockDocumentClient(),
               new CosmosClientOptions());
            Mock<PartitionRoutingHelper> partitionRoutingHelperMock = MockCosmosUtil.GetPartitionRoutingHelperMock("0");

            TestHandler testHandler = new TestHandler(handlerFunc);

            // Similar to FeedPipeline but with replaced transport
            RequestHandler[] feedPipeline = new RequestHandler[]
                {
                    new NamedCacheRetryHandler(),
                    new PartitionKeyRangeHandler(client),
                    testHandler,
                };

            RequestHandler feedHandler =  ClientPipelineBuilder.CreatePipeline(feedPipeline);

            RequestHandler handler = clientContext.RequestHandler.InnerHandler;
            while (handler != null)
            {
                if (handler.InnerHandler is RouterHandler)
                {
                    handler.InnerHandler = new RouterHandler(feedHandler, testHandler);
                    break;
                }

                handler = handler.InnerHandler;
            }

            return clientContext;
        }
    }
}
