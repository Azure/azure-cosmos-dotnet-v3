//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
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
        public async Task MonadicGetChildRangeAsync_ClipsPartialHpkForReadAndChangeFeed()
        {
            const string resourceId = "resourceId";
            const string linkUri = "dbs/db/colls/coll";
            Documents.PartitionKeyDefinition partitionKeyDefinition =
                new Documents.PartitionKeyDefinition
                {
                    Kind = Documents.PartitionKind.MultiHash,
                    Version = Documents.PartitionKeyDefinitionVersion.V2,
                };
            partitionKeyDefinition.Paths.Add("/tenant");
            partitionKeyDefinition.Paths.Add("/user");
            Microsoft.Azure.Cosmos.PartitionKey partialPartitionKey =
                new PartitionKeyBuilder()
                .Add("tenant-a")
                .Build();
            Documents.Routing.Range<string> prefixRange =
                partialPartitionKey.InternalKey.GetEPKRangeForPrefixPartitionKey(
                    partitionKeyDefinition);
            string splitPoint = prefixRange.Min + "80";
            List<Documents.PartitionKeyRange> physicalRanges =
                new List<Documents.PartitionKeyRange>
                {
                    new Documents.PartitionKeyRange
                    {
                        Id = "0",
                        MinInclusive = Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                        MaxExclusive = splitPoint,
                    },
                    new Documents.PartitionKeyRange
                    {
                        Id = "1",
                        MinInclusive = splitPoint,
                        MaxExclusive = Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                    },
                };

            QueryRequestOptions queryRequestOptions = new QueryRequestOptions();
            ChangeFeedRequestOptions changeFeedRequestOptions = new ChangeFeedRequestOptions();
            Mock<CosmosClientContext> context = new Mock<CosmosClientContext>();
            context.Setup(c => c.ClientOptions).Returns(
                new CosmosClientOptions
                {
                    UseLengthAwareRangeComparer = true,
                });
            context.Setup(c => c.GetCachedContainerPropertiesAsync(
                    linkUri,
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContainerProperties
                {
                    PartitionKey = partitionKeyDefinition,
                });
            context.Setup(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<FeedRangeInternal>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => NetworkAttachedDocumentContainerTests.CreateFeedResponse());

            Mock<ContainerInternal> container = new Mock<ContainerInternal>();
            container.Setup(c => c.LinkUri).Returns(linkUri);
            container.Setup(c => c.ClientContext).Returns(context.Object);
            container.Setup(c => c.GetCachedRIDAsync(
                    false,
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(resourceId);

            Mock<CosmosQueryClient> client = new Mock<CosmosQueryClient>();
            client.Setup(c => c.GetTargetPartitionKeyRangeByFeedRangeAsync(
                    linkUri,
                    resourceId,
                    partitionKeyDefinition,
                    It.IsAny<FeedRangeInternal>(),
                    false,
                    It.IsAny<ITrace>()))
                .ReturnsAsync(physicalRanges);

            NetworkAttachedDocumentContainer documentContainer =
                new NetworkAttachedDocumentContainer(
                    container.Object,
                    client.Object,
                    Guid.NewGuid(),
                    queryRequestOptions,
                    changeFeedRequestOptions);
            TryCatch<List<FeedRangeEpk>> childRanges =
                await documentContainer.MonadicGetChildRangeAsync(
                    new FeedRangePartitionKey(partialPartitionKey),
                    NoOpTrace.Singleton,
                    cancellationToken: default);

            Assert.IsTrue(childRanges.Succeeded);
            Assert.AreEqual(2, childRanges.Result.Count);
            Assert.AreEqual(prefixRange.Min, childRanges.Result[0].Range.Min);
            Assert.AreEqual(splitPoint, childRanges.Result[0].Range.Max);
            Assert.AreEqual(splitPoint, childRanges.Result[1].Range.Min);
            Assert.AreEqual(prefixRange.Max, childRanges.Result[1].Range.Max);

            foreach (FeedRangeEpk childRange in childRanges.Result)
            {
                TryCatch<ReadFeedPage> readFeedPage =
                    await documentContainer.MonadicReadFeedAsync(
                        new FeedRangeState<ReadFeedState>(
                            childRange,
                            ReadFeedState.Beginning()),
                        new ReadFeedExecutionOptions(pageSizeHint: 1),
                        NoOpTrace.Singleton,
                        cancellationToken: default);
                Assert.IsTrue(readFeedPage.Succeeded);

                TryCatch<Microsoft.Azure.Cosmos.ChangeFeed.Pagination.ChangeFeedPage>
                    changeFeedPage =
                    await documentContainer.MonadicChangeFeedAsync(
                        new FeedRangeState<ChangeFeedState>(
                            childRange,
                            ChangeFeedState.Beginning()),
                        new ChangeFeedExecutionOptions(
                            ChangeFeedMode.Incremental,
                            pageSizeHint: 1),
                        NoOpTrace.Singleton,
                        cancellationToken: default);
                Assert.IsTrue(changeFeedPage.Succeeded);

                context.Verify(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.Is<RequestOptions>(options => ReferenceEquals(
                        options,
                        queryRequestOptions)),
                    It.IsAny<ContainerInternal>(),
                    It.Is<FeedRangeInternal>(feedRange =>
                        NetworkAttachedDocumentContainerTests.MatchesFeedRange(
                            feedRange,
                            childRange)),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()),
                    Times.Once);
                context.Verify(c => c.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.Is<RequestOptions>(options => ReferenceEquals(
                        options,
                        changeFeedRequestOptions)),
                    It.IsAny<ContainerInternal>(),
                    It.Is<FeedRangeInternal>(feedRange =>
                        NetworkAttachedDocumentContainerTests.MatchesFeedRange(
                            feedRange,
                            childRange)),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()),
                    Times.Once);
            }
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

        private static ResponseMessage CreateFeedResponse()
        {
            ResponseMessage response = new ResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new MemoryStream(),
            };
            response.Headers.ActivityId = Guid.NewGuid().ToString();
            response.Headers.RequestCharge = 1;
            response.Headers.ETag = Guid.NewGuid().ToString();
            response.Headers[HttpConstants.HttpHeaders.ItemCount] = "0";
            return response;
        }

        private static bool MatchesFeedRange(
            FeedRangeInternal actual,
            FeedRangeEpk expected)
        {
            FeedRangeEpk actualEpk = actual as FeedRangeEpk;
            return actualEpk != null &&
                actualEpk.Range.Equals(expected.Range);
        }
    }
}
