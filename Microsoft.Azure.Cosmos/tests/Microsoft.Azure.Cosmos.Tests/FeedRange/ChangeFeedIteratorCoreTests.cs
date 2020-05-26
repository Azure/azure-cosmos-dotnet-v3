//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.FeedRange
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class ChangeFeedIteratorCoreTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ChangeFeedIteratorCore_Null_Container()
        {
            ChangeFeedIteratorCore.Create(null, null, null, new ChangeFeedRequestOptions());
        }

        [DataTestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        [DataRow(-1)]
        [DataRow(0)]
        public void ChangeFeedIteratorCore_ValidateOptions(int maxItemCount)
        {
            ChangeFeedIteratorCore.Create(Mock.Of<ContainerInternal>(), null, null, new ChangeFeedRequestOptions() { MaxItemCount = maxItemCount });
        }

        [TestMethod]
        public void ChangeFeedIteratorCore_HasMoreResultsDefault()
        {
            ChangeFeedIteratorCore changeFeedIteratorCore = ChangeFeedIteratorCore.Create(Mock.Of<ContainerInternal>(), null, null, null);
            Assert.IsTrue(changeFeedIteratorCore.HasMoreResults);
        }

        [TestMethod]
        public void ChangeFeedIteratorCore_FeedRange()
        {
            FeedRangeInternal feedToken = Mock.Of<FeedRangeInternal>();
            ChangeFeedIteratorCore changeFeedIteratorCore = ChangeFeedIteratorCore.Create(Mock.Of<ContainerInternal>(), feedToken, null, null);
            Assert.AreEqual(feedToken, changeFeedIteratorCore.FeedRangeInternal);
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_ReadNextAsync()
        {
            string continuation = "TBD";
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK);
            responseMessage.Headers.ETag = continuation;
            responseMessage.Headers[Documents.HttpConstants.HttpHeaders.ItemCount] = "1";

            Mock<CosmosClientContext> cosmosClientContext = CreateMockedClientContext(responseMessage);

            ContainerInternal containerCore = Mock.Of<ContainerInternal>();
            Mock.Get(containerCore)
                .Setup(c => c.ClientContext)
                .Returns(cosmosClientContext.Object);

            FeedRangeInternal range = Mock.Of<FeedRangeInternal>();
            Mock.Get(range)
                .Setup(f => f.Accept(It.IsAny<FeedRangeVisitor>()));
            FeedRangeContinuation feedToken = Mock.Of<FeedRangeContinuation>();
            Mock.Get(feedToken)
                .Setup(f => f.Accept(It.IsAny<FeedRangeVisitor>(), It.IsAny<Action<RequestMessage, string>>()));
            Mock.Get(feedToken)
                .Setup(f => f.FeedRange)
                .Returns(range);
            Mock.Get(feedToken)
                .Setup(f => f.HandleSplitAsync(It.Is<ContainerInternal>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Documents.ShouldRetryResult.NoRetry()));
            Mock.Get(feedToken)
                .Setup(f => f.HandleChangeFeedNotModified(It.IsAny<ResponseMessage>()))
                .Returns(Documents.ShouldRetryResult.NoRetry());

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(containerCore, feedToken, null);
            ResponseMessage response = await changeFeedIteratorCore.ReadNextAsync();

            Mock.Get(feedToken)
                .Verify(f => f.ReplaceContinuation(It.Is<string>(ct => ct == continuation)), Times.Once);

            Mock.Get(feedToken)
                .Verify(f => f.HandleSplitAsync(It.Is<ContainerInternal>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()), Times.Once);
            Mock.Get(feedToken)
                .Verify(f => f.HandleChangeFeedNotModified(It.IsAny<ResponseMessage>()), Times.Once);
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_OfT_ReadNextAsync()
        {
            string continuation = "TBD";
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK);
            responseMessage.Headers.ETag = continuation;
            responseMessage.Headers[Documents.HttpConstants.HttpHeaders.ItemCount] = "1";

            Mock<CosmosClientContext> cosmosClientContext = CreateMockedClientContext(responseMessage);

            ContainerInternal containerCore = Mock.Of<ContainerInternal>();
            Mock.Get(containerCore)
                .Setup(c => c.ClientContext)
                .Returns(cosmosClientContext.Object);
            FeedRangeInternal range = Mock.Of<FeedRangeInternal>();
            Mock.Get(range)
                .Setup(f => f.Accept(It.IsAny<FeedRangeVisitor>()));
            FeedRangeContinuation feedToken = Mock.Of<FeedRangeContinuation>();
            Mock.Get(feedToken)
                .Setup(f => f.Accept(It.IsAny<FeedRangeVisitor>(), It.IsAny<Action<RequestMessage, string>>()));
            Mock.Get(feedToken)
                .Setup(f => f.FeedRange)
                .Returns(range);
            Mock.Get(feedToken)
               .Setup(f => f.HandleSplitAsync(It.Is<ContainerInternal>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()))
               .Returns(Task.FromResult(Documents.ShouldRetryResult.NoRetry()));
            Mock.Get(feedToken)
                .Setup(f => f.HandleChangeFeedNotModified(It.IsAny<ResponseMessage>()))
                .Returns(Documents.ShouldRetryResult.NoRetry());

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(containerCore, feedToken, null);

            bool creatorCalled = false;
            Func<ResponseMessage, FeedResponse<dynamic>> creator = (ResponseMessage r) =>
            {
                creatorCalled = true;
                return Mock.Of<FeedResponse<dynamic>>();
            };

            FeedIteratorCore<dynamic> changeFeedIteratorCoreOfT = new FeedIteratorCore<dynamic>(changeFeedIteratorCore, creator);
            FeedResponse<dynamic> response = await changeFeedIteratorCoreOfT.ReadNextAsync();

            Mock.Get(feedToken)
                .Verify(f => f.ReplaceContinuation(It.Is<string>(ct => ct == continuation)), Times.Once);

            Mock.Get(feedToken)
                .Verify(f => f.HandleSplitAsync(It.Is<ContainerInternal>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()), Times.Once);
            Mock.Get(feedToken)
                .Verify(f => f.HandleChangeFeedNotModified(It.IsAny<ResponseMessage>()), Times.Once);

            Assert.IsTrue(creatorCalled, "Response creator not called");
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_UpdatesContinuation_On304()
        {
            string continuation = "TBD";
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.NotModified);
            responseMessage.Headers.ETag = continuation;

            Mock<CosmosClientContext> cosmosClientContext = CreateMockedClientContext(responseMessage);

            ContainerInternal containerCore = Mock.Of<ContainerInternal>();
            Mock.Get(containerCore)
                .Setup(c => c.ClientContext)
                .Returns(cosmosClientContext.Object);
            FeedRangeInternal range = Mock.Of<FeedRangeInternal>();
            Mock.Get(range)
                .Setup(f => f.Accept(It.IsAny<FeedRangeVisitor>()));
            FeedRangeContinuation feedToken = Mock.Of<FeedRangeContinuation>();
            Mock.Get(feedToken)
                .Setup(f => f.Accept(It.IsAny<FeedRangeVisitor>(), It.IsAny<Action<RequestMessage, string>>()));
            Mock.Get(feedToken)
                .Setup(f => f.FeedRange)
                .Returns(range);
            Mock.Get(feedToken)
                .Setup(f => f.HandleSplitAsync(It.Is<ContainerInternal>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Documents.ShouldRetryResult.NoRetry()));
            Mock.Get(feedToken)
                .Setup(f => f.HandleChangeFeedNotModified(It.IsAny<ResponseMessage>()))
                .Returns(Documents.ShouldRetryResult.NoRetry());

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(containerCore, feedToken, null);
            ResponseMessage response = await changeFeedIteratorCore.ReadNextAsync();

            Mock.Get(feedToken)
                .Verify(f => f.ReplaceContinuation(It.Is<string>(ct => ct == continuation)), Times.Once);

            Mock.Get(feedToken)
                .Verify(f => f.HandleSplitAsync(It.Is<ContainerInternal>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()), Times.Once);
            Mock.Get(feedToken)
                .Verify(f => f.HandleChangeFeedNotModified(It.IsAny<ResponseMessage>()), Times.Once);
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_DoesNotUpdateContinuation_OnError()
        {
            string continuation = "TBD";
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.Gone);
            responseMessage.Headers.ETag = continuation;

            Mock<CosmosClientContext> cosmosClientContext = CreateMockedClientContext(responseMessage);

            ContainerInternal containerCore = Mock.Of<ContainerInternal>();
            Mock.Get(containerCore)
                .Setup(c => c.ClientContext)
                .Returns(cosmosClientContext.Object);
            FeedRangeInternal range = Mock.Of<FeedRangeInternal>();
            Mock.Get(range)
                .Setup(f => f.Accept(It.IsAny<FeedRangeVisitor>()));
            FeedRangeContinuation feedToken = Mock.Of<FeedRangeContinuation>();
            Mock.Get(feedToken)
                .Setup(f => f.Accept(It.IsAny<FeedRangeVisitor>(), It.IsAny<Action<RequestMessage, string>>()));
            Mock.Get(feedToken)
                .Setup(f => f.FeedRange)
                .Returns(range);
            Mock.Get(feedToken)
                .Setup(f => f.HandleSplitAsync(It.Is<ContainerInternal>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Documents.ShouldRetryResult.NoRetry()));
            Mock.Get(feedToken)
                .Setup(f => f.HandleChangeFeedNotModified(It.IsAny<ResponseMessage>()))
                .Returns(Documents.ShouldRetryResult.NoRetry());

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(containerCore, feedToken, null);
            ResponseMessage response = await changeFeedIteratorCore.ReadNextAsync();

            Assert.IsFalse(changeFeedIteratorCore.HasMoreResults);

            Mock.Get(feedToken)
                .Verify(f => f.ReplaceContinuation(It.Is<string>(ct => ct == continuation)), Times.Never);

            Mock.Get(feedToken)
                .Verify(f => f.HandleSplitAsync(It.Is<ContainerInternal>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()), Times.Once);
            Mock.Get(feedToken)
                .Verify(f => f.HandleChangeFeedNotModified(It.IsAny<ResponseMessage>()), Times.Once);
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_Retries()
        {
            string continuation = "TBD";
            ResponseMessage responseMessage = new ResponseMessage(HttpStatusCode.OK);
            responseMessage.Headers.ETag = continuation;
            responseMessage.Headers[Documents.HttpConstants.HttpHeaders.ItemCount] = "1";

            Mock<CosmosClientContext> cosmosClientContext = CreateMockedClientContext(responseMessage);

            ContainerInternal containerCore = Mock.Of<ContainerInternal>();
            Mock.Get(containerCore)
                .Setup(c => c.ClientContext)
                .Returns(cosmosClientContext.Object);
            FeedRangeInternal range = Mock.Of<FeedRangeInternal>();
            Mock.Get(range)
                .Setup(f => f.Accept(It.IsAny<FeedRangeVisitor>()));
            FeedRangeContinuation feedToken = Mock.Of<FeedRangeContinuation>();
            Mock.Get(feedToken)
                .Setup(f => f.Accept(It.IsAny<FeedRangeVisitor>(), It.IsAny<Action<RequestMessage, string>>()));
            Mock.Get(feedToken)
                .Setup(f => f.FeedRange)
                .Returns(range);

            Mock.Get(feedToken)
                .SetupSequence(f => f.HandleSplitAsync(It.Is<ContainerInternal>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Documents.ShouldRetryResult.RetryAfter(TimeSpan.Zero)))
                .Returns(Task.FromResult(Documents.ShouldRetryResult.NoRetry()));
            Mock.Get(feedToken)
                .SetupSequence(f => f.HandleChangeFeedNotModified(It.IsAny<ResponseMessage>()))
                .Returns(Documents.ShouldRetryResult.RetryAfter(TimeSpan.Zero))
                .Returns(Documents.ShouldRetryResult.NoRetry())
                .Returns(Documents.ShouldRetryResult.NoRetry());

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(containerCore, feedToken, null);
            ResponseMessage response = await changeFeedIteratorCore.ReadNextAsync();

            Mock.Get(feedToken)
                .Verify(f => f.ReplaceContinuation(It.IsAny<string>()), Times.Once);

            Mock.Get(feedToken)
                .Verify(f => f.HandleSplitAsync(It.Is<ContainerInternal>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
            Mock.Get(feedToken)
                .Verify(f => f.HandleChangeFeedNotModified(It.IsAny<ResponseMessage>()), Times.Exactly(3));

            Mock.Get(cosmosClientContext.Object)
                .Verify(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<Documents.ResourceType>(),
                    It.IsAny<Documents.OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<CosmosDiagnosticsContext>(),
                    It.IsAny<CancellationToken>()), Times.Exactly(3));
        }

        [TestMethod]
        public async Task ChangeFeedIteratorCore_HandlesSplitsThroughPipeline()
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

            ContainerInternal containerCore = Mock.Of<ContainerInternal>();
            Mock.Get(containerCore)
                .Setup(c => c.ClientContext)
                .Returns(cosmosClientContext);
            Mock.Get(containerCore)
                .Setup(c => c.LinkUri)
                .Returns(new Uri("https://dummy.documents.azure.com:443/dbs"));
            FeedRangeInternal range = Mock.Of<FeedRangeInternal>();
            Mock.Get(range)
                .Setup(f => f.Accept(It.IsAny<FeedRangeVisitor>()));
            FeedRangeContinuation feedToken = Mock.Of<FeedRangeContinuation>();
            Mock.Get(feedToken)
                .Setup(f => f.Accept(It.IsAny<FeedRangeVisitor>(), It.IsAny<Action<RequestMessage, string>>()));
            Mock.Get(feedToken)
                .Setup(f => f.FeedRange)
                .Returns(range);
            Mock.Get(feedToken)
                .Setup(f => f.Accept(It.IsAny<FeedRangeVisitor>(), It.IsAny<Action<RequestMessage, string>>()));
            Mock.Get(feedToken)
                .Setup(f => f.HandleSplitAsync(It.Is<ContainerInternal>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Documents.ShouldRetryResult.NoRetry()));
            Mock.Get(feedToken)
                .Setup(f => f.HandleChangeFeedNotModified(It.IsAny<ResponseMessage>()))
                .Returns(Documents.ShouldRetryResult.NoRetry());

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(containerCore, feedToken, null);

            ResponseMessage response = await changeFeedIteratorCore.ReadNextAsync();

            Assert.AreEqual(1, executionCount, "PartitionKeyRangeGoneRetryHandler handled the Split");
            Assert.AreEqual(HttpStatusCode.Gone, response.StatusCode);

            Mock.Get(feedToken)
                .Verify(f => f.ReplaceContinuation(It.IsAny<string>()), Times.Never);

            Mock.Get(feedToken)
                .Verify(f => f.HandleSplitAsync(It.Is<ContainerInternal>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()), Times.Once);
            Mock.Get(feedToken)
                .Verify(f => f.HandleChangeFeedNotModified(It.IsAny<ResponseMessage>()), Times.Once);
        }

        private static Mock<CosmosClientContext> CreateMockedClientContext(
             ResponseMessage responseMessage)
        {
            Mock<CosmosClientContext> cosmosClientContext = new Mock<CosmosClientContext>();
            cosmosClientContext.Setup(c => c.ClientOptions).Returns(new CosmosClientOptions());

            cosmosClientContext.Setup(x => x.OperationHelperAsync<ResponseMessage>(
                It.IsAny<string>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<Func<CosmosDiagnosticsContext, Task<ResponseMessage>>>()))
               .Returns<string, RequestOptions, Func<CosmosDiagnosticsContext, Task<ResponseMessage>>>(
                (x, y, z) => z(new CosmosDiagnosticsContextCore(x, "MockUserAgentString")));

            cosmosClientContext.Setup(x => x.OperationHelperAsync<FeedResponse<dynamic>>(
                It.IsAny<string>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<Func<CosmosDiagnosticsContext, Task<FeedResponse<dynamic>>>>()))
               .Returns<string, RequestOptions, Func<CosmosDiagnosticsContext, Task<FeedResponse<dynamic>>>>(
                (x, y, z) => z(new CosmosDiagnosticsContextCore(x, "MockUserAgentString")));

            cosmosClientContext
                .Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<Uri>(),
                    It.IsAny<Documents.ResourceType>(),
                    It.IsAny<Documents.OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<CosmosDiagnosticsContext>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(responseMessage));

            return cosmosClientContext;
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

            RequestHandler feedHandler = ClientPipelineBuilder.CreatePipeline(feedPipeline);

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
