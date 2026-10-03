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
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class NetworkAttachedDocumentContainerTests
    {
        [TestMethod]
        public async Task TestKeyRangeCacheRefresh()
        {
            const string resourceId = "resourceId";
            Mock<CosmosClientContext> context = new Mock<CosmosClientContext>();
            Mock<ContainerInternal> container = new Mock<ContainerInternal>();
            container.Setup(c => c.ClientContext).Returns(context.Object);
            container.Setup(c => c.GetCachedRIDAsync(false, It.IsAny<ITrace>(), It.IsAny<CancellationToken>())).ReturnsAsync(resourceId);

            Mock<CosmosQueryClient> client = new Mock<CosmosQueryClient>();
            NetworkAttachedDocumentContainer networkAttachedDocumentContainer = new NetworkAttachedDocumentContainer(
                container.Object,
                client.Object,
                Guid.NewGuid());

            TryCatch result = await networkAttachedDocumentContainer.MonadicRefreshProviderAsync(
                trace: NoOpTrace.Singleton,
                cancellationToken: default);

            Assert.IsTrue(result.Succeeded);
            client.Verify(c => c.TryGetOverlappingRangesAsync(resourceId, FeedRangeEpk.FullRange.Range, true), Times.Once);
        }

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
            response.Headers[HttpConstants.HttpHeaders.ItemCount] = "0";

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
                Mock.Of<CosmosQueryClient>(),
                Guid.NewGuid());

            await networkAttachedDocumentContainer.MonadicChangeFeedAsync(
                feedRangeState: new FeedRangeState<ChangeFeedState>(new FeedRangePartitionKeyRange("0"), ChangeFeedState.Beginning()),
                changeFeedPaginationOptions: new ChangeFeedExecutionOptions(ChangeFeedMode.Incremental, pageSizeHint: 10),
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
                Assert.AreEqual(HttpConstants.A_IMHeaderValues.FullFidelityFeed, requestMessage.Headers[HttpConstants.HttpHeaders.A_IM]);
                return true;
            };

            ResponseMessage response = new ResponseMessage(System.Net.HttpStatusCode.NotModified);
            response.Headers.ETag = Guid.NewGuid().ToString();
            response.Headers.ActivityId = Guid.NewGuid().ToString();
            response.Headers.RequestCharge = 1;
            response.Headers[HttpConstants.HttpHeaders.ItemCount] = "0";

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
                Mock.Of<CosmosQueryClient>(),
                Guid.NewGuid());

            await networkAttachedDocumentContainer.MonadicChangeFeedAsync(
                feedRangeState: new FeedRangeState<ChangeFeedState>(new FeedRangePartitionKeyRange("0"), ChangeFeedState.Beginning()),
                changeFeedPaginationOptions: new ChangeFeedExecutionOptions(ChangeFeedMode.AllVersionsAndDeletes, pageSizeHint: 10),
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

        /// <summary>
        /// Pins the fix at <c>NetworkAttachedDocumentContainer.MonadicGetChildRangeAsync</c> (the
        /// line-145 call site): the caller-supplied <see cref="CancellationToken"/> must be
        /// propagated verbatim to <see cref="CosmosQueryClient.GetTargetPartitionKeyRangeByFeedRangeAsync"/>,
        /// not silently replaced with <c>default</c>.
        /// </summary>
        [TestMethod]
        [Owner("tvaron3")]
        public async Task MonadicGetChildRangeAsync_PropagatesCancellationToken()
        {
            const string resourceId = "resourceId";
            Mock<CosmosClientContext> context = new ();
            Mock<ContainerInternal> container = new ();
            ContainerProperties containerProperties = new ("c", "/pk");
            container.Setup(c => c.ClientContext).Returns(context.Object);
            container.Setup(c => c.GetCachedRIDAsync(false, It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(resourceId);
            container.Setup(c => c.LinkUri).Returns("dbs/db/colls/c");
            context.Setup(c => c.GetCachedContainerPropertiesAsync(It.IsAny<string>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(containerProperties);

            using CancellationTokenSource cts = new ();
            CancellationToken expected = cts.Token;
            CancellationToken observed = default;

            Mock<CosmosQueryClient> client = new ();
            client
                .Setup(c => c.GetTargetPartitionKeyRangeByFeedRangeAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Documents.PartitionKeyDefinition>(),
                    It.IsAny<FeedRangeInternal>(),
                    It.IsAny<bool>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Returns<string, string, Documents.PartitionKeyDefinition, FeedRangeInternal, bool, ITrace, CancellationToken>(
                    (_, _, _, _, _, _, ct) =>
                    {
                        observed = ct;
                        return Task.FromResult(new System.Collections.Generic.List<Documents.PartitionKeyRange>());
                    });

            NetworkAttachedDocumentContainer networkAttachedDocumentContainer = new (
                container.Object,
                client.Object,
                Guid.NewGuid());

            TryCatch<System.Collections.Generic.List<FeedRangeEpk>> result = await networkAttachedDocumentContainer.MonadicGetChildRangeAsync(
                feedRange: FeedRangeEpk.FullRange,
                trace: NoOpTrace.Singleton,
                cancellationToken: expected);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(expected, observed, "CancellationToken passed to MonadicGetChildRangeAsync was not propagated to GetTargetPartitionKeyRangeByFeedRangeAsync.");
        }
    }
}
