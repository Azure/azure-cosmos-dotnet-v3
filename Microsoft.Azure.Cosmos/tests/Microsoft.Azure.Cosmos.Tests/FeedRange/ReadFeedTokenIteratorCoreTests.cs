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
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class ReadFeedIteratorCoreTests
    {
        [TestMethod]
        public void ReadFeedIteratorCore_HasMoreResultsDefault()
        {
            FeedRangeIteratorCore iterator = new FeedRangeIteratorCore(
                Mock.Of<IDocumentContainer>(),
                default,
                default,
                default);
            Assert.IsTrue(iterator.HasMoreResults);
        }

        [TestMethod]
        public async Task ReadFeedIteratorCore_ReadNextAsync()
        {
            int numItems = 100;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);
            FeedRangeIteratorCore iterator = new FeedRangeIteratorCore(
                documentContainer,
                continuationToken: null,
                pageSize: 10,
                cancellationToken: default);

            int count = 0;
            while (iterator.HasMoreResults)
            {
                ResponseMessage message = await iterator.ReadNextAsync();
                CosmosArray documents = GetDocuments(message.Content);
                count += documents.Count;
            }

            Assert.AreEqual(numItems, count);
        }

        [TestMethod]
        public async Task ReadFeedIteratorCore_ReadNextAsync_WithContinuationToken()
        {
            int numItems = 100;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);

            int count = 0;
            string continuationToken = null;
            do
            {
                FeedRangeIteratorCore iterator = new FeedRangeIteratorCore(
                    documentContainer,
                    continuationToken: continuationToken,
                    pageSize: 10,
                    cancellationToken: default);
                ResponseMessage message = await iterator.ReadNextAsync();
                CosmosArray documents = GetDocuments(message.Content);
                count += documents.Count;
                continuationToken = message.ContinuationToken;
            } while (continuationToken != null);

            Assert.AreEqual(numItems, count);
        }

        [TestMethod]
        public async Task ReadFeedIteratorCore_DoesNotUpdateContinuation_OnError()
        {
            int numItems = 100;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(
                numItems,
                failureConfigs: new FlakyDocumentContainer.FailureConfigs(inject429s: true, injectEmptyPages: true);

            int count = 0;
            string continuationToken = null;
            do
            {
                FeedRangeIteratorCore iterator = new FeedRangeIteratorCore(
                    documentContainer,
                    continuationToken: continuationToken,
                    pageSize: 10,
                    cancellationToken: default);
                ResponseMessage message = await iterator.ReadNextAsync();
                if (message.IsSuccessStatusCode)
                {
                    CosmosArray documents = GetDocuments(message.Content);
                    count += documents.Count;
                    continuationToken = message.ContinuationToken;
                }
                else
                {
                    Assert.IsNull(message.ContinuationToken);
                }
            } while (continuationToken != null);

            Assert.AreEqual(numItems, count);
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
                    It.IsAny<string>(),
                    It.Is<Documents.ResourceType>(rt => rt == Documents.ResourceType.Document),
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
            FeedRangeEpk feedTokenEPKRange = feedRangeCompositeContinuation.FeedRange as FeedRangeEpk;
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
                .Returns("/dbs/db/colls/colls");
            FeedRangeInternal range = Mock.Of<FeedRangeInternal>();
            Mock.Get(range)
                .Setup(f => f.Accept(It.IsAny<FeedRangeRequestMessagePopulatorVisitor>()));
            FeedRangeContinuation feedToken = Mock.Of<FeedRangeContinuation>();
            Mock.Get(feedToken)
                .Setup(f => f.FeedRange)
                .Returns(range);
            Mock.Get(feedToken)
                .Setup(f => f.HandleSplitAsync(It.Is<ContainerInternal>(c => c == containerCore), It.IsAny<ResponseMessage>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(Documents.ShouldRetryResult.NoRetry()));

            FeedRangeIteratorCore changeFeedIteratorCore = new FeedRangeIteratorCore(containerCore, feedToken, new QueryRequestOptions(), Documents.ResourceType.Document, queryDefinition: null);

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

        private static CosmosArray GetDocuments(Stream stream)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                CosmosObject element = CosmosObject.CreateFromBuffer(memoryStream.ToArray());
                if (!element.TryGetValue("Documents", out CosmosArray value))
                {
                    Assert.Fail();
                }

                return value;
            }
        }

        private static async Task<IDocumentContainer> CreateDocumentContainerAsync(
            int numItems,
            FlakyDocumentContainer.FailureConfigs failureConfigs = default)
        {
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition()
            {
                Paths = new System.Collections.ObjectModel.Collection<string>()
                    {
                        "/pk"
                    },
                Kind = PartitionKind.Hash,
                Version = PartitionKeyDefinitionVersion.V2,
            };

            IMonadicDocumentContainer monadicDocumentContainer = new InMemoryContainer(partitionKeyDefinition);
            if (failureConfigs != null)
            {
                monadicDocumentContainer = new FlakyDocumentContainer(monadicDocumentContainer, failureConfigs);
            }

            DocumentContainer documentContainer = new DocumentContainer(monadicDocumentContainer);

            for (int i = 0; i < 3; i++)
            {
                IReadOnlyList<FeedRangeInternal> ranges = await documentContainer.GetFeedRangesAsync(cancellationToken: default);
                foreach (FeedRangeInternal range in ranges)
                {
                    await documentContainer.SplitAsync(range, cancellationToken: default);
                }
            }

            for (int i = 0; i < numItems; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                while (true)
                {
                    TryCatch<Record> monadicCreateRecord = await documentContainer.MonadicCreateItemAsync(item, cancellationToken: default);
                    if (monadicCreateRecord.Succeeded)
                    {
                        break;
                    }
                }
            }

            return documentContainer;
        }
    }
}
