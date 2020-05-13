//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.FeedRange
{
    using System;
    using System.Collections.Generic;
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
            FeedRangeIteratorCore iterator = FeedRangeIteratorCore.Create(Mock.Of<ContainerInternal>(), Mock.Of<FeedRangeInternal>(), null, null);
            Assert.IsTrue(iterator.HasMoreResults);
        }

        [TestMethod]
        public void ReadFeedIteratorCore_Create_Default()
        {
            FeedRangeIteratorCore feedTokenIterator = FeedRangeIteratorCore.Create(Mock.Of<ContainerInternal>(), null, null, null);
            FeedRangeEPK defaultRange = feedTokenIterator.FeedRangeInternal as FeedRangeEPK;
            Assert.AreEqual(FeedRangeEPK.ForCompleteRange().Range.Min, defaultRange.Range.Min);
            Assert.AreEqual(FeedRangeEPK.ForCompleteRange().Range.Max, defaultRange.Range.Max);
            Assert.IsNull(feedTokenIterator.FeedRangeContinuation);
        }

        [TestMethod]
        public void ReadFeedIteratorCore_Create_WithRange()
        {
            Documents.Routing.Range<string>  range = new Documents.Routing.Range<string>("A", "B", true, false);
            FeedRangeEPK feedRangeEPK = new FeedRangeEPK(range);
            FeedRangeIteratorCore feedTokenIterator = FeedRangeIteratorCore.Create(Mock.Of<ContainerInternal>(), feedRangeEPK, null, null);
            Assert.AreEqual(feedRangeEPK, feedTokenIterator.FeedRangeInternal);
            Assert.IsNull(feedTokenIterator.FeedRangeContinuation);
        }

        [TestMethod]
        public void ReadFeedIteratorCore_Create_WithContinuation()
        {

            string continuation = Guid.NewGuid().ToString();
            FeedRangeIteratorCore feedTokenIterator = FeedRangeIteratorCore.Create(Mock.Of<ContainerInternal>(), null, continuation, null);
            FeedRangeEPK defaultRange = feedTokenIterator.FeedRangeInternal as FeedRangeEPK;
            Assert.AreEqual(FeedRangeEPK.ForCompleteRange().Range.Min, defaultRange.Range.Min);
            Assert.AreEqual(FeedRangeEPK.ForCompleteRange().Range.Max, defaultRange.Range.Max);
            Assert.IsNotNull(feedTokenIterator.FeedRangeContinuation);
            Assert.AreEqual(continuation, feedTokenIterator.FeedRangeContinuation.GetContinuation());
        }

        [TestMethod]
        public void ReadFeedIteratorCore_Create_WithFeedContinuation()
        {

            string continuation = Guid.NewGuid().ToString();
            FeedRangeEPK feedRangeEPK = FeedRangeEPK.ForCompleteRange();
            FeedRangeCompositeContinuation feedRangeSimpleContinuation = new FeedRangeCompositeContinuation(Guid.NewGuid().ToString(), feedRangeEPK, new List<Documents.Routing.Range<string>>() { feedRangeEPK.Range }, continuation);
            FeedRangeIteratorCore feedTokenIterator = FeedRangeIteratorCore.Create(Mock.Of<ContainerInternal>(), null, feedRangeSimpleContinuation.ToString(), null);
            FeedRangeEPK defaultRange = feedTokenIterator.FeedRangeInternal as FeedRangeEPK;
            Assert.AreEqual(FeedRangeEPK.ForCompleteRange().Range.Min, defaultRange.Range.Min);
            Assert.AreEqual(FeedRangeEPK.ForCompleteRange().Range.Max, defaultRange.Range.Max);
            Assert.IsNotNull(feedTokenIterator.FeedRangeContinuation);
            Assert.AreEqual(continuation, feedTokenIterator.FeedRangeContinuation.GetContinuation());
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
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<CosmosDiagnosticsContext>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(responseMessage));

            ContainerInternal containerCore = Mock.Of<ContainerInternal>();
            Mock.Get(containerCore)
                .Setup(c => c.ClientContext)
                .Returns(cosmosClientContext.Object);
            FeedRangeInternal range = Mock.Of<FeedRangeInternal>();
            Mock.Get(range)
                .Setup(f => f.Accept(It.IsAny<FeedRangeVisitor>()));
            FeedRangeContinuation feedToken = Mock.Of<FeedRangeContinuation>();
            Mock.Get(feedToken)
               .Setup(f => f.FeedRange)
               .Returns(range);
            Mock.Get(feedToken)
                .Setup(f => f.Accept(It.IsAny<FeedRangeVisitor>()));
            Mock.Get(feedToken)
                .Setup(f => f.HandleSplitAsync(It.Is<ContainerInternal>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Documents.ShouldRetryResult.NoRetry()));
            Mock.Get(feedToken)
                .Setup(f => f.GetContinuation())
                .Returns(continuation);
            Mock.Get(feedToken)
                .Setup(f => f.IsDone)
                .Returns(true);

            FeedRangeIteratorCore feedTokenIterator = new FeedRangeIteratorCore(containerCore, feedToken, new QueryRequestOptions());
            ResponseMessage response = await feedTokenIterator.ReadNextAsync();

            Mock.Get(feedToken)
                .Verify(f => f.ReplaceContinuation(It.Is<string>(ct => ct == continuation)), Times.Once);

            Mock.Get(feedToken)
                .Verify(f => f.HandleSplitAsync(It.Is<ContainerInternal>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()), Times.Once);
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
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<CosmosDiagnosticsContext>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(responseMessage));

            ContainerInternal containerCore = Mock.Of<ContainerInternal>();
            Mock.Get(containerCore)
                .Setup(c => c.ClientContext)
                .Returns(cosmosClientContext.Object);
            FeedRangeInternal range = Mock.Of<FeedRangeInternal>();
            Mock.Get(range)
                .Setup(f => f.Accept(It.IsAny<FeedRangeVisitor>()));
            FeedRangeContinuation feedToken = Mock.Of<FeedRangeContinuation>();
            Mock.Get(feedToken)
               .Setup(f => f.FeedRange)
               .Returns(range);
            Mock.Get(feedToken)
                .Setup(f => f.Accept(It.IsAny<FeedRangeVisitor>()));
            Mock.Get(feedToken)
                .Setup(f => f.HandleSplitAsync(It.Is<ContainerInternal>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Documents.ShouldRetryResult.NoRetry()));
            Mock.Get(feedToken)
                .Setup(f => f.GetContinuation())
                .Returns(continuation);
            Mock.Get(feedToken)
                .Setup(f => f.IsDone)
                .Returns(true);

            FeedRangeIteratorCore feedTokenIterator = new FeedRangeIteratorCore(containerCore, feedToken, new QueryRequestOptions());
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
                .Verify(f => f.ReplaceContinuation(It.Is<string>(ct => ct == continuation)), Times.Once);

            Mock.Get(feedToken)
                .Verify(f => f.HandleSplitAsync(It.Is<ContainerInternal>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()), Times.Once);
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
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<CosmosDiagnosticsContext>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(responseMessage));

            ContainerInternal containerCore = Mock.Of<ContainerInternal>();
            Mock.Get(containerCore)
                .Setup(c => c.ClientContext)
                .Returns(cosmosClientContext.Object);
            FeedRangeInternal range = Mock.Of<FeedRangeInternal>();
            Mock.Get(range)
                .Setup(f => f.Accept(It.IsAny<FeedRangeVisitor>()));
            FeedRangeContinuation feedToken = Mock.Of<FeedRangeContinuation>();
            Mock.Get(feedToken)
               .Setup(f => f.FeedRange)
               .Returns(range);
            Mock.Get(feedToken)
                .Setup(f => f.Accept(It.IsAny<FeedRangeVisitor>()));
            Mock.Get(feedToken)
                .Setup(f => f.HandleSplitAsync(It.Is<ContainerInternal>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Documents.ShouldRetryResult.NoRetry()));
            Mock.Get(feedToken)
                .Setup(f => f.GetContinuation())
                .Returns(continuation);
            Mock.Get(feedToken)
                .Setup(f => f.IsDone)
                .Returns(true);

            FeedRangeIteratorCore feedTokenIterator = new FeedRangeIteratorCore(containerCore, feedToken, new QueryRequestOptions());
            ResponseMessage response = await feedTokenIterator.ReadNextAsync();

            Mock.Get(feedToken)
                .Verify(f => f.ReplaceContinuation(It.Is<string>(ct => ct == continuation)), Times.Once);

            Mock.Get(feedToken)
                .Verify(f => f.HandleSplitAsync(It.Is<ContainerInternal>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()), Times.Once);

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
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<PartitionKey?>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<CosmosDiagnosticsContext>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(responseMessage));

            ContainerInternal containerCore = Mock.Of<ContainerInternal>();
            Mock.Get(containerCore)
                .Setup(c => c.ClientContext)
                .Returns(cosmosClientContext.Object);
            FeedRangeInternal range = Mock.Of<FeedRangeInternal>();
            Mock.Get(range)
                .Setup(f => f.Accept(It.IsAny<FeedRangeVisitor>()));
            FeedRangeContinuation feedToken = Mock.Of<FeedRangeContinuation>();
            Mock.Get(feedToken)
                .Setup(f => f.FeedRange)
                .Returns(range);
            Mock.Get(feedToken)
                .Setup(f => f.Accept(It.IsAny<FeedRangeVisitor>()));
            Mock.Get(feedToken)
                .Setup(f => f.HandleSplitAsync(It.Is<ContainerInternal>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Documents.ShouldRetryResult.NoRetry()));
            Mock.Get(feedToken)
                .Setup(f => f.GetContinuation())
                .Returns(continuation);
            Mock.Get(feedToken)
                .Setup(f => f.IsDone)
                .Returns(true);

            FeedRangeIteratorCore feedTokenIterator = new FeedRangeIteratorCore(containerCore, feedToken, new QueryRequestOptions());
            ResponseMessage response = await feedTokenIterator.ReadNextAsync();

            Mock.Get(feedToken)
                .Verify(f => f.ReplaceContinuation(It.Is<string>(ct => ct == continuation)), Times.Never);

            Mock.Get(feedToken)
                .Verify(f => f.HandleSplitAsync(It.Is<ContainerInternal>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()), Times.Once);

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

            MultiRangeMockDocumentClient documentClient = new MultiRangeMockDocumentClient();

            Mock<CosmosClientContext> cosmosClientContext = new Mock<CosmosClientContext>();            
            cosmosClientContext.Setup(c => c.ClientOptions).Returns(new CosmosClientOptions());
            cosmosClientContext.Setup(c => c.DocumentClient).Returns(documentClient);
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

            ContainerInternal containerCore = Mock.Of<ContainerInternal>();
            Mock.Get(containerCore)
                .Setup(c => c.ClientContext)
                .Returns(cosmosClientContext.Object);
            Mock.Get(containerCore)
                .Setup(c => c.GetRIDAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Guid.NewGuid().ToString());

            FeedRangeIteratorCore feedTokenIterator = FeedRangeIteratorCore.Create(containerCore, null, null, new QueryRequestOptions());
            ResponseMessage response = await feedTokenIterator.ReadNextAsync();

            Assert.IsTrue(FeedRangeContinuation.TryParse(response.ContinuationToken, out FeedRangeContinuation parsedToken));
            FeedRangeCompositeContinuation feedRangeCompositeContinuation = parsedToken as FeedRangeCompositeContinuation;
            FeedRangeEPK feedTokenEPKRange = feedRangeCompositeContinuation.FeedRange as FeedRangeEPK;
            // Assert that a FeedToken for the entire range is used
            Assert.AreEqual(Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey, feedTokenEPKRange.Range.Min);
            Assert.AreEqual(Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey, feedTokenEPKRange.Range.Max);
            Assert.AreEqual(continuation, feedRangeCompositeContinuation.CompositeContinuationTokens.Peek().Token);
            Assert.IsFalse(feedRangeCompositeContinuation.IsDone);
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

            ContainerInternal containerCore = Mock.Of<ContainerInternal>();
            Mock.Get(containerCore)
                .Setup(c => c.ClientContext)
                .Returns(cosmosClientContext);
            Mock.Get(containerCore)
                .Setup(c => c.LinkUri)
                .Returns(new Uri($"/dbs/db/colls/colls", UriKind.Relative));
            FeedRangeInternal range = Mock.Of<FeedRangeInternal>();
            Mock.Get(range)
                .Setup(f => f.Accept(It.IsAny<FeedRangeVisitor>()));
            FeedRangeContinuation feedToken = Mock.Of<FeedRangeContinuation>();
            Mock.Get(feedToken)
                .Setup(f => f.FeedRange)
                .Returns(range);
            Mock.Get(feedToken)
                .Setup(f => f.Accept(It.IsAny<FeedRangeVisitor>()));
            Mock.Get(feedToken)
                .Setup(f => f.HandleSplitAsync(It.Is<ContainerInternal>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Documents.ShouldRetryResult.NoRetry()));

            FeedRangeIteratorCore changeFeedIteratorCore = new FeedRangeIteratorCore(containerCore, feedToken, new QueryRequestOptions());

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

        private class MultiRangeMockDocumentClient : MockDocumentClient
        {
            private List<Documents.PartitionKeyRange> availablePartitionKeyRanges = new List<Documents.PartitionKeyRange>() {
                new Documents.PartitionKeyRange() { MinInclusive = Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey, MaxExclusive = Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey, Id = "0" }
            };

            internal override IReadOnlyList<Documents.PartitionKeyRange> ResolveOverlapingPartitionKeyRanges(string collectionRid, Documents.Routing.Range<string> range, bool forceRefresh)
            {
                return this.availablePartitionKeyRanges;
            }
        }
    }
}
