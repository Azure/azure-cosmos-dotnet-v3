//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using System.IO;
    using Microsoft.Azure.Cosmos.Tracing;
    using Moq;
    using System.Threading;
    using System.Text;

    [TestClass]
    public sealed class CrossPartitionChangeFeedAsyncEnumeratorTests
    {
        [TestMethod]
        public async Task NoChangesAsync()
        {
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems: 0);
            CrossPartitionChangeFeedAsyncEnumerator enumerator = CrossPartitionChangeFeedAsyncEnumerator.Create(
                documentContainer,
                new CrossFeedRangeState<ChangeFeedState>(
                    new FeedRangeState<ChangeFeedState>[]
                    {
                        new FeedRangeState<ChangeFeedState>(FeedRangeEpk.FullRange, ChangeFeedState.Beginning())
                    }),
                ChangeFeedPaginationOptions.Default,
                cancellationToken: default);

            Assert.IsTrue(await enumerator.MoveNextAsync());
            Assert.IsTrue(enumerator.Current.Succeeded);
            Assert.IsTrue(enumerator.Current.Result.Page is ChangeFeedNotModifiedPage);
            Assert.IsNotNull(enumerator.Current.Result.State);
        }

        [TestMethod]
        public async Task SomeChangesAsync()
        {
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems: 1);
            CrossPartitionChangeFeedAsyncEnumerator enumerator = CrossPartitionChangeFeedAsyncEnumerator.Create(
                documentContainer,
                new CrossFeedRangeState<ChangeFeedState>(
                    new FeedRangeState<ChangeFeedState>[]
                    {
                        new FeedRangeState<ChangeFeedState>(FeedRangeEpk.FullRange, ChangeFeedState.Beginning())
                    }),
                ChangeFeedPaginationOptions.Default,
                cancellationToken: default);

            // First page should be true and skip the 304 not modified
            Assert.IsTrue(await enumerator.MoveNextAsync());
            Assert.IsTrue(enumerator.Current.Succeeded);
            Assert.IsTrue(enumerator.Current.Result.Page is ChangeFeedSuccessPage);

            // Second page should surface up the 304
            Assert.IsTrue(await enumerator.MoveNextAsync());
            Assert.IsTrue(enumerator.Current.Succeeded);
            Assert.IsTrue(enumerator.Current.Result.Page is ChangeFeedNotModifiedPage);
        }

        [TestMethod]
        [DataRow(false, DisplayName = "Use Continuations: false")]
        [DataRow(true, DisplayName = "Use Continuations: true")]
        public async Task StartFromBeginningAsync(bool useContinuations)
        {
            int numItems = 100;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);
            CrossPartitionChangeFeedAsyncEnumerator enumerator = CrossPartitionChangeFeedAsyncEnumerator.Create(
                documentContainer,
                new CrossFeedRangeState<ChangeFeedState>(
                    new FeedRangeState<ChangeFeedState>[]
                    {
                        new FeedRangeState<ChangeFeedState>(FeedRangeEpk.FullRange, ChangeFeedState.Beginning())
                    }),
                ChangeFeedPaginationOptions.Default,
                cancellationToken: default);

            (int globalCount, double _) = await (useContinuations
                ? DrainWithUntilNotModifiedWithContinuationTokens(documentContainer, enumerator)
                : DrainUntilNotModifedAsync(enumerator));
            Assert.AreEqual(numItems, globalCount);
        }

        [TestMethod]
        [DataRow(false, DisplayName = "Use Continuations: false")]
        [DataRow(true, DisplayName = "Use Continuations: true")]
        public async Task StartFromTimeAsync(bool useContinuations)
        {
            int numItems = 100;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);
            CrossPartitionChangeFeedAsyncEnumerator enumerator = CrossPartitionChangeFeedAsyncEnumerator.Create(
                documentContainer,
                new CrossFeedRangeState<ChangeFeedState>(
                    new FeedRangeState<ChangeFeedState>[]
                    {
                        new FeedRangeState<ChangeFeedState>(FeedRangeEpk.FullRange, ChangeFeedState.Time(DateTime.UtcNow))
                    }),
                ChangeFeedPaginationOptions.Default,
                cancellationToken: default);

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

            (int globalCount, double _) = await (useContinuations
                ? DrainWithUntilNotModifiedWithContinuationTokens(documentContainer, enumerator)
                : DrainUntilNotModifedAsync(enumerator));

            Assert.AreEqual(numItems, globalCount);
        }

        [TestMethod]
        [DataRow(false, DisplayName = "Use Continuations: false")]
        [DataRow(true, DisplayName = "Use Continuations: true")]
        public async Task StartFromNowAsync(bool useContinuations)
        {
            int numItems = 100;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);
            CrossPartitionChangeFeedAsyncEnumerator enumerator = CrossPartitionChangeFeedAsyncEnumerator.Create(
                documentContainer,
                new CrossFeedRangeState<ChangeFeedState>(
                    new FeedRangeState<ChangeFeedState>[]
                    {
                        new FeedRangeState<ChangeFeedState>(FeedRangeEpk.FullRange, ChangeFeedState.Now())
                    }),
                ChangeFeedPaginationOptions.Default,
                cancellationToken: default);

            (int globalCount, double _) = await (useContinuations
                ? DrainWithUntilNotModifiedWithContinuationTokens(documentContainer, enumerator)
                : DrainUntilNotModifedAsync(enumerator));

            Assert.AreEqual(0, globalCount);

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

            (int globalCountAfter, double _) = await (useContinuations
                ? DrainWithUntilNotModifiedWithContinuationTokens(documentContainer, enumerator)
                : DrainUntilNotModifedAsync(enumerator));
            Assert.AreEqual(numItems, globalCountAfter);
        }

        // Verifies that it cycles on all the internal ranges before returning a 304
        // The number of requests should be equal to the number of internal ranges
        [DataTestMethod]
        [Timeout(5000)]
        [DataRow(1, DisplayName = "FeedRange spanning 1 partition")]
        [DataRow(2, DisplayName = "FeedRange spanning 2 partitions")]
        [DataRow(3, DisplayName = "FeedRange spanning 3 partitions")]
        public async Task ShouldReturnNotModifiedAfterCyclingOnAllRanges(int partitions)
        {
            ReadOnlyMemory<FeedRangeState<ChangeFeedState>> rangeStates = null;

            if (partitions == 1)
            {
                rangeStates = new FeedRangeState<ChangeFeedState>[]{
                    new FeedRangeState<ChangeFeedState>(FeedRangeEpk.FullRange, ChangeFeedState.Now())
                };
            }
            if (partitions == 2)
            {
                rangeStates = new FeedRangeState<ChangeFeedState>[]{
                    new FeedRangeState<ChangeFeedState>(new FeedRangeEpk(new Documents.Routing.Range<string>("", "AA", true, false)), ChangeFeedState.Now()),
                    new FeedRangeState<ChangeFeedState>(new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "FF", true, false)), ChangeFeedState.Now()),
                };
            }
            if (partitions == 3)
            {
                rangeStates = new FeedRangeState<ChangeFeedState>[]{
                    new FeedRangeState<ChangeFeedState>(new FeedRangeEpk(new Documents.Routing.Range<string>("", "AA", true, false)), ChangeFeedState.Now()),
                    new FeedRangeState<ChangeFeedState>(new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "BB", true, false)), ChangeFeedState.Now()),
                    new FeedRangeState<ChangeFeedState>(new FeedRangeEpk(new Documents.Routing.Range<string>("BB", "FF", true, false)), ChangeFeedState.Now()),
                };
            }

            Assert.IsNotNull(rangeStates, $"Range states not initialized for {partitions} partitions");

            CrossFeedRangeState<ChangeFeedState> state = new CrossFeedRangeState<ChangeFeedState>(rangeStates);
            Mock<IDocumentContainer> documentContainer = new Mock<IDocumentContainer>();

            // Returns a 304 with 1RU charge
            documentContainer.Setup(c => c.MonadicChangeFeedAsync(
                It.IsAny<FeedRangeState<ChangeFeedState>>(),
                It.IsAny<ChangeFeedPaginationOptions>(),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(
                (FeedRangeState<ChangeFeedState> state, ChangeFeedPaginationOptions options, ITrace trace, CancellationToken token) 
                    => TryCatch<ChangeFeedPage>.FromResult(new ChangeFeedNotModifiedPage(requestCharge: 1, activityId: string.Empty, additionalHeaders: default, state.State)));
            CrossPartitionChangeFeedAsyncEnumerator enumerator = CrossPartitionChangeFeedAsyncEnumerator.Create(
                documentContainer.Object,
                state,
                ChangeFeedPaginationOptions.Default,
                cancellationToken: default);

            (int _, double requestCharge) = await DrainUntilNotModifedAsync(enumerator);

            // Verify the number of calls were the expected
            documentContainer.Verify(c => c.MonadicChangeFeedAsync(
                It.IsAny<FeedRangeState<ChangeFeedState>>(),
                It.IsAny<ChangeFeedPaginationOptions>(),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()), Times.Exactly(partitions));

            // Verify the RU is being summarized
            Assert.AreEqual(partitions, requestCharge, "Should sum requestcharge of all notmodified pages");

            // Verify the calls match the ranges
            if (partitions == 1)
            {
                documentContainer.Verify(c => c.MonadicChangeFeedAsync(
                    It.Is<FeedRangeState<ChangeFeedState>>(s => s.FeedRange.Equals(FeedRangeEpk.FullRange)),
                    It.IsAny<ChangeFeedPaginationOptions>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()), Times.Once);
            }
            if (partitions == 2)
            {
                documentContainer.Verify(c => c.MonadicChangeFeedAsync(
                    It.Is<FeedRangeState<ChangeFeedState>>(s => s.FeedRange.Equals(new FeedRangeEpk(new Documents.Routing.Range<string>("", "AA", true, false)))),
                    It.IsAny<ChangeFeedPaginationOptions>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()), Times.Once);

                documentContainer.Verify(c => c.MonadicChangeFeedAsync(
                    It.Is<FeedRangeState<ChangeFeedState>>(s => s.FeedRange.Equals(new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "FF", true, false)))),
                    It.IsAny<ChangeFeedPaginationOptions>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()), Times.Once);
            }
            if (partitions == 3)
            {
                documentContainer.Verify(c => c.MonadicChangeFeedAsync(
                    It.Is<FeedRangeState<ChangeFeedState>>(s => s.FeedRange.Equals(new FeedRangeEpk(new Documents.Routing.Range<string>("", "AA", true, false)))),
                    It.IsAny<ChangeFeedPaginationOptions>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()), Times.Once);

                documentContainer.Verify(c => c.MonadicChangeFeedAsync(
                    It.Is<FeedRangeState<ChangeFeedState>>(s => s.FeedRange.Equals(new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "BB", true, false)))),
                    It.IsAny<ChangeFeedPaginationOptions>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()), Times.Once);

                documentContainer.Verify(c => c.MonadicChangeFeedAsync(
                    It.Is<FeedRangeState<ChangeFeedState>>(s => s.FeedRange.Equals(new FeedRangeEpk(new Documents.Routing.Range<string>("BB", "FF", true, false)))),
                    It.IsAny<ChangeFeedPaginationOptions>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        // Verifies that in a FeedRange with any partitions, the not modified are skipped until we find one that contains results
        [TestMethod]
        [Timeout(5000)]
        public async Task ShouldSkipNotModifiedAndReturnResults()
        {
            ReadOnlyMemory<FeedRangeState<ChangeFeedState>> rangeStates = new FeedRangeState<ChangeFeedState>[]{
                    new FeedRangeState<ChangeFeedState>(new FeedRangeEpk(new Documents.Routing.Range<string>("", "AA", true, false)), ChangeFeedState.Now()),
                    new FeedRangeState<ChangeFeedState>(new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "BB", true, false)), ChangeFeedState.Now()),
                    new FeedRangeState<ChangeFeedState>(new FeedRangeEpk(new Documents.Routing.Range<string>("BB", "CC", true, false)), ChangeFeedState.Now()),
                    new FeedRangeState<ChangeFeedState>(new FeedRangeEpk(new Documents.Routing.Range<string>("CC", "FF", true, false)), ChangeFeedState.Now()),
                };

            CrossFeedRangeState<ChangeFeedState> state = new CrossFeedRangeState<ChangeFeedState>(rangeStates);
            Mock<IDocumentContainer> documentContainer = new Mock<IDocumentContainer>();

            const double expectedTotalRU = 5 + 1 + 1; // 2x 304s + OK

            // Returns a 304 with 1RU charge on <>-AA
            documentContainer.Setup(c => c.MonadicChangeFeedAsync(
                It.Is<FeedRangeState<ChangeFeedState>>(s => s.FeedRange.Equals(new FeedRangeEpk(new Documents.Routing.Range<string>("", "AA", true, false)))),
                It.IsAny<ChangeFeedPaginationOptions>(),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(
                (FeedRangeState<ChangeFeedState> state, ChangeFeedPaginationOptions options, ITrace trace, CancellationToken token)
                    => TryCatch<ChangeFeedPage>.FromResult(new ChangeFeedNotModifiedPage(requestCharge: 1, activityId: string.Empty, additionalHeaders: default, state.State)));

            // Returns a 304 with 1RU charge on AA-BB
            documentContainer.Setup(c => c.MonadicChangeFeedAsync(
                It.Is<FeedRangeState<ChangeFeedState>>(s => s.FeedRange.Equals(new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "BB", true, false)))),
                It.IsAny<ChangeFeedPaginationOptions>(),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(
                (FeedRangeState<ChangeFeedState> state, ChangeFeedPaginationOptions options, ITrace trace, CancellationToken token)
                    => TryCatch<ChangeFeedPage>.FromResult(new ChangeFeedNotModifiedPage(requestCharge: 1, activityId: string.Empty, additionalHeaders: default, state.State)));

            // Returns a 200 with 5RU charge on BB-CC
            documentContainer.Setup(c => c.MonadicChangeFeedAsync(
                It.Is<FeedRangeState<ChangeFeedState>>(s => s.FeedRange.Equals(new FeedRangeEpk(new Documents.Routing.Range<string>("BB", "CC", true, false)))),
                It.IsAny<ChangeFeedPaginationOptions>(),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(
                (FeedRangeState<ChangeFeedState> state, ChangeFeedPaginationOptions options, ITrace trace, CancellationToken token)
                    => TryCatch<ChangeFeedPage>.FromResult(new ChangeFeedSuccessPage(content: new MemoryStream(Encoding.UTF8.GetBytes("{\"Documents\": [], \"_count\": 0, \"_rid\": \"asdf\"}")), requestCharge: 5, activityId: string.Empty, additionalHeaders: default, state.State)));

            // Returns a 304 with 1RU charge on CC-FF
            documentContainer.Setup(c => c.MonadicChangeFeedAsync(
                It.Is<FeedRangeState<ChangeFeedState>>(s => s.FeedRange.Equals(new FeedRangeEpk(new Documents.Routing.Range<string>("CC", "FF", true, false)))),
                It.IsAny<ChangeFeedPaginationOptions>(),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(
                (FeedRangeState<ChangeFeedState> state, ChangeFeedPaginationOptions options, ITrace trace, CancellationToken token)
                    => TryCatch<ChangeFeedPage>.FromResult(new ChangeFeedNotModifiedPage(requestCharge: 1, activityId: string.Empty, additionalHeaders: default, state.State)));

            CrossPartitionChangeFeedAsyncEnumerator enumerator = CrossPartitionChangeFeedAsyncEnumerator.Create(
                documentContainer.Object,
                state,
                ChangeFeedPaginationOptions.Default,
                cancellationToken: default);

            (int _, double requestCharge) = await DrainUntilSuccessAsync(enumerator);

            // Verify the number of calls were the expected
            documentContainer.Verify(c => c.MonadicChangeFeedAsync(
                It.IsAny<FeedRangeState<ChangeFeedState>>(),
                It.IsAny<ChangeFeedPaginationOptions>(),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()), Times.Exactly(3));

            // Verify the RU is being summarized
            Assert.AreEqual(expectedTotalRU, requestCharge, "Should sum requestcharge of all notmodified pages");

            // Verify the calls match the ranges
            documentContainer.Verify(c => c.MonadicChangeFeedAsync(
                    It.Is<FeedRangeState<ChangeFeedState>>(s => s.FeedRange.Equals(new FeedRangeEpk(new Documents.Routing.Range<string>("", "AA", true, false)))),
                    It.IsAny<ChangeFeedPaginationOptions>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()), Times.Once);

            documentContainer.Verify(c => c.MonadicChangeFeedAsync(
                It.Is<FeedRangeState<ChangeFeedState>>(s => s.FeedRange.Equals(new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "BB", true, false)))),
                It.IsAny<ChangeFeedPaginationOptions>(),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()), Times.Once);

            documentContainer.Verify(c => c.MonadicChangeFeedAsync(
                It.Is<FeedRangeState<ChangeFeedState>>(s => s.FeedRange.Equals(new FeedRangeEpk(new Documents.Routing.Range<string>("BB", "CC", true, false)))),
                It.IsAny<ChangeFeedPaginationOptions>(),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()), Times.Once);

            documentContainer.Verify(c => c.MonadicChangeFeedAsync(
                It.Is<FeedRangeState<ChangeFeedState>>(s => s.FeedRange.Equals(new FeedRangeEpk(new Documents.Routing.Range<string>("CC", "FF", true, false)))),
                It.IsAny<ChangeFeedPaginationOptions>(),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        private static async Task<(int, double)> DrainUntilNotModifedAsync(CrossPartitionChangeFeedAsyncEnumerator enumerator)
        {
            int globalCount = 0;
            double requestCharge = 0;
            while (await enumerator.MoveNextAsync())
            {
                Assert.IsTrue(enumerator.Current.Succeeded);
                requestCharge += enumerator.Current.Result.Page.RequestCharge;
                if (!(enumerator.Current.Result.Page is ChangeFeedSuccessPage changeFeedSuccessPage))
                {
                    break;
                }

                globalCount += GetResponseCount(changeFeedSuccessPage.Content);
            }

            return (globalCount, requestCharge);
        }

        private static async Task<(int, double)> DrainUntilSuccessAsync(CrossPartitionChangeFeedAsyncEnumerator enumerator)
        {
            int globalCount = 0;
            double requestCharge = 0;
            while (await enumerator.MoveNextAsync())
            {
                Assert.IsTrue(enumerator.Current.Succeeded);
                requestCharge += enumerator.Current.Result.Page.RequestCharge;
                if (enumerator.Current.Result.Page is ChangeFeedSuccessPage changeFeedSuccessPage)
                {
                    globalCount += GetResponseCount(changeFeedSuccessPage.Content);
                    break;
                }
            }

            return (globalCount, requestCharge);
        }

        private static async Task<(int, double)> DrainWithUntilNotModifiedWithContinuationTokens(
            IDocumentContainer documentContainer, 
            CrossPartitionChangeFeedAsyncEnumerator enumerator)
        {
            double requestCharge = 0;
            List<CosmosElement> globalChanges = new List<CosmosElement>();
            while (true)
            {
                if (!await enumerator.MoveNextAsync())
                {
                    throw new InvalidOperationException();
                }

                Assert.IsTrue(enumerator.Current.Succeeded);

                if (!(enumerator.Current.Result.Page is ChangeFeedSuccessPage changeFeedSuccessPage))
                {
                    break;
                }

                requestCharge += changeFeedSuccessPage.RequestCharge;
                CosmosArray changes = GetChanges(changeFeedSuccessPage.Content);
                globalChanges.AddRange(changes);

                enumerator = CrossPartitionChangeFeedAsyncEnumerator.Create(
                    documentContainer,
                    enumerator.Current.Result.State,
                    ChangeFeedPaginationOptions.Default,
                    cancellationToken: default);
            }

            return (globalChanges.Count, requestCharge);
        }

        private static int GetResponseCount(Stream stream)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                CosmosObject element = CosmosObject.CreateFromBuffer(memoryStream.ToArray());
                if (!element.TryGetValue("_count", out CosmosElement value))
                {
                    Assert.Fail();
                }

                return (int)Number64.ToLong(((CosmosNumber)value).Value);
            }
        }

        private static CosmosArray GetChanges(Stream stream)
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
                IReadOnlyList<FeedRangeInternal> ranges = await documentContainer.GetFeedRangesAsync(trace: NoOpTrace.Singleton, cancellationToken: default);
                foreach (FeedRangeInternal range in ranges)
                {
                    await documentContainer.SplitAsync(range, cancellationToken: default);
                }

                await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
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
