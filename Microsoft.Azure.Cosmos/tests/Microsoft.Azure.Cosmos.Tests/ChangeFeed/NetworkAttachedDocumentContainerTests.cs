//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.ChangeFeed
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class NetworkAttachedDocumentContainerTests
    {
        [TestMethod]
        public async Task MonadicChangeFeedAsync_ChangeFeedMode_Incremental()
        {
            Mock<ContainerInternal> container = new Mock<ContainerInternal>();
            Mock<CosmosClientContext> context = new Mock<CosmosClientContext>();
            container.Setup(m => m.ClientContext).Returns(context.Object);

            Func<Action<RequestMessage>, bool> validateEnricher = (Action<RequestMessage> enricher) =>
            {
                RequestMessage requestMessage = new RequestMessage();
                enricher(requestMessage);
                Assert.AreEqual(HttpConstants.A_IMHeaderValues.IncrementalFeed, requestMessage.Headers[HttpConstants.HttpHeaders.A_IM]);
                return true;
            };

            ResponseMessage response = new ResponseMessage(System.Net.HttpStatusCode.NotModified);
            response.Headers.ETag = Guid.NewGuid().ToString();
            response.Headers.ActivityId = Guid.NewGuid().ToString();
            response.Headers.RequestCharge = 1;

            context.SetupSequence(c => c.ProcessResourceOperationStreamAsync(
                It.IsAny<string>(),
                It.Is<ResourceType>(rt => rt == ResourceType.Document),
                It.Is<OperationType>(rt => rt == OperationType.ReadFeed),
                It.IsAny<RequestOptions>(),
                It.Is<ContainerInternal>(o => o == container.Object),
                It.IsAny<FeedRangeInternal>(),
                It.IsAny<Stream>(),
                It.Is<Action<RequestMessage>>(enricher => validateEnricher(enricher)),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()
                )
            ).ReturnsAsync(response);

            NetworkAttachedDocumentContainer networkAttachedDocumentContainer = new NetworkAttachedDocumentContainer(
                container.Object,
                Mock.Of<CosmosQueryClient>());

            await networkAttachedDocumentContainer.MonadicChangeFeedAsync(
                feedRangeState: new FeedRangeState<ChangeFeedState>(new FeedRangePartitionKeyRange("0"), ChangeFeedState.Beginning()),
                changeFeedPaginationOptions: new ChangeFeedPaginationOptions(ChangeFeedMode.Incremental, pageSizeHint: 10),
                trace: NoOpTrace.Singleton,
                cancellationToken: default);

            context.Verify(c => c.ProcessResourceOperationStreamAsync(
                It.IsAny<string>(),
                It.Is<ResourceType>(rt => rt == ResourceType.Document),
                It.Is<OperationType>(rt => rt == OperationType.ReadFeed),
                It.IsAny<RequestOptions>(),
                It.Is<ContainerInternal>(o => o == container.Object),
                It.IsAny<FeedRangeInternal>(),
                It.IsAny<Stream>(),
                It.Is<Action<RequestMessage>>(enricher => validateEnricher(enricher)),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()
                ), Times.Once);
        }

        [TestMethod]
        public async Task MonadicChangeFeedAsync_ChangeFeedMode_FullFidelity()
        {
            Mock<ContainerInternal> container = new Mock<ContainerInternal>();
            Mock<CosmosClientContext> context = new Mock<CosmosClientContext>();
            container.Setup(m => m.ClientContext).Returns(context.Object);

            Func<Action<RequestMessage>, bool> validateEnricher = (Action<RequestMessage> enricher) =>
            {
                RequestMessage requestMessage = new RequestMessage();
                enricher(requestMessage);
                Assert.AreEqual(ChangeFeedModeFullFidelity.FullFidelityHeader, requestMessage.Headers[HttpConstants.HttpHeaders.A_IM]);
                return true;
            };

            ResponseMessage response = new ResponseMessage(System.Net.HttpStatusCode.NotModified);
            response.Headers.ETag = Guid.NewGuid().ToString();
            response.Headers.ActivityId = Guid.NewGuid().ToString();
            response.Headers.RequestCharge = 1;

            context.SetupSequence(c => c.ProcessResourceOperationStreamAsync(
                It.IsAny<string>(),
                It.Is<ResourceType>(rt => rt == ResourceType.Document),
                It.Is<OperationType>(rt => rt == OperationType.ReadFeed),
                It.IsAny<RequestOptions>(),
                It.Is<ContainerInternal>(o => o == container.Object),
                It.IsAny<FeedRangeInternal>(),
                It.IsAny<Stream>(),
                It.Is<Action<RequestMessage>>(enricher => validateEnricher(enricher)),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()
                )
            ).ReturnsAsync(response);

            NetworkAttachedDocumentContainer networkAttachedDocumentContainer = new NetworkAttachedDocumentContainer(
                container.Object,
                Mock.Of<CosmosQueryClient>());

            await networkAttachedDocumentContainer.MonadicChangeFeedAsync(
                feedRangeState: new FeedRangeState<ChangeFeedState>(new FeedRangePartitionKeyRange("0"), ChangeFeedState.Beginning()),
                changeFeedPaginationOptions: new ChangeFeedPaginationOptions(ChangeFeedMode.FullFidelity, pageSizeHint: 10),
                trace: NoOpTrace.Singleton,
                cancellationToken: default);

            context.Verify(c => c.ProcessResourceOperationStreamAsync(
                It.IsAny<string>(),
                It.Is<ResourceType>(rt => rt == ResourceType.Document),
                It.Is<OperationType>(rt => rt == OperationType.ReadFeed),
                It.IsAny<RequestOptions>(),
                It.Is<ContainerInternal>(o => o == container.Object),
                It.IsAny<FeedRangeInternal>(),
                It.IsAny<Stream>(),
                It.Is<Action<RequestMessage>>(enricher => validateEnricher(enricher)),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()
                ), Times.Once);
        }
    }
}
