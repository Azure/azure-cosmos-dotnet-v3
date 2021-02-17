//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class ChangeFeedPartitionKeyResultSetIteratorCoreTests
    {
        /// <summary>
        /// Checks that Response's Etag is passed as caller Continuation
        /// </summary>
        [TestMethod]
        public async Task EtagPassesContinuation()
        {
            int itemCount = 5;
            string pkRangeId = "0";
            string etag = Guid.NewGuid().ToString();
            DateTime startTime = DateTime.UtcNow;
            DocumentServiceLeaseCore documentServiceLeaseCore = new DocumentServiceLeaseCore()
            {
                LeaseToken = pkRangeId
            };

            ResponseMessage responseMessage = new ResponseMessage(System.Net.HttpStatusCode.OK);
            responseMessage.Headers.ETag = etag;

            Mock<ContainerInternal> containerMock = new Mock<ContainerInternal>();
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(c => c.ProcessResourceOperationStreamAsync(
                It.IsAny<string>(),
                It.IsAny<Documents.ResourceType>(),
                It.IsAny<Documents.OperationType>(),
                It.IsAny<ChangeFeedRequestOptions>(),
                It.IsAny<ContainerInternal>(),
                It.IsAny<FeedRange>(),
                It.IsAny<Stream>(),
                It.IsAny<Action<RequestMessage>>(),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()
                )
            ).ReturnsAsync(responseMessage);
            containerMock.Setup(c => c.ClientContext).Returns(mockContext.Object);
            containerMock.Setup(c => c.LinkUri).Returns("http://localhot");

            ChangeFeedPartitionKeyResultSetIteratorCore iterator = ChangeFeedPartitionKeyResultSetIteratorCore.Create(
                lease: documentServiceLeaseCore,
                continuationToken: null,
                maxItemCount: itemCount,
                container: containerMock.Object,
                startTime: startTime,
                startFromBeginning: false);

            ResponseMessage response = await iterator.ReadNextAsync();
            Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
            Assert.AreEqual(etag, response.Headers.ContinuationToken);
        }

        /// <summary>
        /// Checks that a second ReadNextAsync uses the continuation of the first response.
        /// </summary>
        [TestMethod]
        public async Task NextReadHasUpdatedContinuation()
        {
            int itemCount = 5;
            string pkRangeId = "0";
            string etag = Guid.NewGuid().ToString();
            DateTime startTime = DateTime.UtcNow;
            DocumentServiceLeaseCore documentServiceLeaseCore = new DocumentServiceLeaseCore()
            {
                LeaseToken = pkRangeId
            };

            ResponseMessage firstResponse = new ResponseMessage(System.Net.HttpStatusCode.OK);
            firstResponse.Headers.ETag = etag;
            ResponseMessage secondResponse = new ResponseMessage(System.Net.HttpStatusCode.OK);

            int responseCount = 0;
            Func<Action<RequestMessage>, bool> validateEnricher = (Action<RequestMessage> enricher) =>
            {
                RequestMessage requestMessage = new RequestMessage();
                enricher(requestMessage);
                return responseCount++ == 0 || requestMessage.Headers.IfNoneMatch == etag;
            };

            Mock<ContainerInternal> containerMock = new Mock<ContainerInternal>();
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.SetupSequence(c => c.ProcessResourceOperationStreamAsync(
                It.IsAny<string>(),
                It.Is<Documents.ResourceType>(rt => rt == Documents.ResourceType.Document),
                It.Is<Documents.OperationType>(rt => rt == Documents.OperationType.ReadFeed),
                It.Is<ChangeFeedRequestOptions>(cfo => cfo.PageSizeHint == itemCount),
                It.Is<ContainerInternal>(o => o == containerMock.Object),
                It.IsAny<FeedRange>(),
                It.IsAny<Stream>(),
                It.Is<Action<RequestMessage>>(enricher => validateEnricher(enricher)),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(firstResponse)
            .ReturnsAsync(secondResponse);
            containerMock.Setup(c => c.ClientContext).Returns(mockContext.Object);
            containerMock.Setup(c => c.LinkUri).Returns("http://localhot");

            ChangeFeedPartitionKeyResultSetIteratorCore iterator = ChangeFeedPartitionKeyResultSetIteratorCore.Create(
                lease: documentServiceLeaseCore,
                continuationToken: null,
                maxItemCount: itemCount,
                container: containerMock.Object,
                startTime: startTime,
                startFromBeginning: false);

            await iterator.ReadNextAsync();
            await iterator.ReadNextAsync();
            Assert.AreEqual(2, responseCount);
        }

        /// <summary>
        /// Checks that for a PKRange based lease, the feed range is a FeedRangePartitionKeyRange
        /// </summary>
        [TestMethod]
        public async Task ShouldSetFeedRangePartitionKeyRange()
        {
            int itemCount = 5;
            string pkRangeId = "0";
            string etag = Guid.NewGuid().ToString();
            DateTime startTime = DateTime.UtcNow;
            DocumentServiceLeaseCore documentServiceLeaseCore = new DocumentServiceLeaseCore()
            {
                LeaseToken = pkRangeId
            };

            Mock<ContainerInternal> containerMock = new Mock<ContainerInternal>();
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(c => c.ProcessResourceOperationStreamAsync(
                It.IsAny<string>(),
                It.Is<Documents.ResourceType>(rt => rt == Documents.ResourceType.Document),
                It.Is<Documents.OperationType>(rt => rt == Documents.OperationType.ReadFeed),
                It.Is<ChangeFeedRequestOptions>(cfo => cfo.PageSizeHint == itemCount),
                It.Is<ContainerInternal>(o => o == containerMock.Object),
                It.Is<FeedRange>(fr => fr is FeedRangePartitionKeyRange),
                It.IsAny<Stream>(),
                It.IsAny<Action<RequestMessage>>(),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()
                )
            ).ReturnsAsync(new ResponseMessage(System.Net.HttpStatusCode.OK));
            containerMock.Setup(c => c.ClientContext).Returns(mockContext.Object);
            containerMock.Setup(c => c.LinkUri).Returns("http://localhot");

            ChangeFeedPartitionKeyResultSetIteratorCore iterator = ChangeFeedPartitionKeyResultSetIteratorCore.Create(
                lease: documentServiceLeaseCore,
                continuationToken: null,
                maxItemCount: itemCount,
                container: containerMock.Object,
                startTime: startTime,
                startFromBeginning: false);

            ResponseMessage response = await iterator.ReadNextAsync();
            Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);

            mockContext.Verify(c => c.ProcessResourceOperationStreamAsync(
                It.IsAny<string>(),
                It.Is<Documents.ResourceType>(rt => rt == Documents.ResourceType.Document),
                It.Is<Documents.OperationType>(rt => rt == Documents.OperationType.ReadFeed),
                It.Is<ChangeFeedRequestOptions>(cfo => cfo.PageSizeHint == itemCount),
                It.Is<ContainerInternal>(o => o == containerMock.Object),
                It.Is<FeedRange>(fr => fr is FeedRangePartitionKeyRange),
                It.IsAny<Stream>(),
                It.IsAny<Action<RequestMessage>>(),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()
                ), Times.Once);
        }

        /// <summary>
        /// Checks that for an EPK Range lease, the feedrange is EPK
        /// </summary>
        [TestMethod]
        public async Task ShouldUseFeedRangeEpk()
        {
            int itemCount = 5;
            string pkRangeId = "0";
            DateTime startTime = DateTime.UtcNow;
            Documents.Routing.Range<string> range = new Documents.Routing.Range<string>("AA", "BB", true, false);
            FeedRangeEpk feedRange = new FeedRangeEpk(range);
            DocumentServiceLeaseCoreEpk documentServiceLeaseCore = new DocumentServiceLeaseCoreEpk()
            {
                LeaseToken = pkRangeId,
                FeedRange = feedRange
            };

            Mock<ContainerInternal> containerMock = new Mock<ContainerInternal>();
            Mock<CosmosClientContext> mockContext = new Mock<CosmosClientContext>();
            mockContext.Setup(c => c.ProcessResourceOperationStreamAsync(
                It.IsAny<string>(),
                It.Is<Documents.ResourceType>(rt => rt == Documents.ResourceType.Document),
                It.Is<Documents.OperationType>(rt => rt == Documents.OperationType.ReadFeed),
                It.Is<ChangeFeedRequestOptions>(cfo => cfo.PageSizeHint == itemCount),
                It.Is<ContainerInternal>(o => o == containerMock.Object),
                It.Is<FeedRange>(fr => fr is FeedRangeEpk),
                It.IsAny<Stream>(),
                It.IsAny<Action<RequestMessage>>(),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()
                )
            ).ReturnsAsync(new ResponseMessage(System.Net.HttpStatusCode.OK));
            containerMock.Setup(c => c.ClientContext).Returns(mockContext.Object);
            containerMock.Setup(c => c.LinkUri).Returns("http://localhot");
            MockDocumentClient mockDocumentClient = new MockDocumentClient();
            mockContext.Setup(c => c.DocumentClient).Returns(mockDocumentClient);          

            ChangeFeedPartitionKeyResultSetIteratorCore iterator = ChangeFeedPartitionKeyResultSetIteratorCore.Create(
                lease: documentServiceLeaseCore,
                continuationToken: null,
                maxItemCount: itemCount,
                container: containerMock.Object,
                startTime: startTime,
                startFromBeginning: false);

            ResponseMessage response = await iterator.ReadNextAsync();
            Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);

            mockContext.Verify(c => c.ProcessResourceOperationStreamAsync(
                It.IsAny<string>(),
                It.Is<Documents.ResourceType>(rt => rt == Documents.ResourceType.Document),
                It.Is<Documents.OperationType>(rt => rt == Documents.OperationType.ReadFeed),
                It.Is<ChangeFeedRequestOptions>(cfo => cfo.PageSizeHint == itemCount),
                It.Is<ContainerInternal>(o => o == containerMock.Object),
                It.Is<FeedRange>(fr => fr is FeedRangeEpk),
                It.IsAny<Stream>(),
                It.IsAny<Action<RequestMessage>>(),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()
                ), Times.Once);
        }
    }
}
