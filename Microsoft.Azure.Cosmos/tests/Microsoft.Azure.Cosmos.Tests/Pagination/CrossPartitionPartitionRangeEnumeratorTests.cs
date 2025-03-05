//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Tests.Query.Pipeline;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public sealed class CrossPartitionPartitionRangeEnumeratorTests
    {
        [TestMethod]
        public async Task Test429sAsync()
        {
            Implementation implementation = new Implementation(false);
            await implementation.Test429sAsync(false);
        }

        [TestMethod]
        public async Task Test429sWithContinuationsAsync()
        {
            Implementation implementation = new Implementation(false);
            await implementation.Test429sWithContinuationsAsync(false, false);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task TestEmptyPages(bool aggressivePrefetch)
        {
            Implementation implementation = new Implementation(false);
            await implementation.TestEmptyPages(aggressivePrefetch);
        }

        [TestMethod]
        public async Task TestMergeToSinglePartition()
        {
            Implementation implementation = new Implementation(true);
            await implementation.TestMergeToSinglePartition();
        }

        // Validates that on a merge (split with 1 result) we do not create new child enumerators for the merge result
        [TestMethod]
        public async Task OnMergeRequeueRange()
        {
            // We expect only creation of enumerators for the original ranges, not any child ranges
            List<EnumeratorThatSplits> createdEnumerators = new List<EnumeratorThatSplits>();
            PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> createEnumerator(
                FeedRangeState<ReadFeedState> feedRangeState)
            {
                EnumeratorThatSplits enumerator = new EnumeratorThatSplits(feedRangeState, createdEnumerators.Count == 0);
                createdEnumerators.Add(enumerator);
                return enumerator;
            }

            // We expect a request for children and we return the merged range
            Mock<IFeedRangeProvider> feedRangeProvider = new Mock<IFeedRangeProvider>();
            feedRangeProvider.Setup(p => p.GetChildRangeAsync(
                It.Is<FeedRangeInternal>(splitRange => ((FeedRangeEpk)splitRange).Range.Min == "" && ((FeedRangeEpk)splitRange).Range.Max == "A"),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<FeedRangeEpk>() {
                    FeedRangeEpk.FullRange});

            CrossPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> enumerator = new CrossPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>(
                feedRangeProvider: feedRangeProvider.Object,
                createPartitionRangeEnumerator: createEnumerator,
                comparer: null,
                maxConcurrency: 0,
                prefetchPolicy: PrefetchPolicy.PrefetchSinglePage,
                state: new CrossFeedRangeState<ReadFeedState>(
                    new FeedRangeState<ReadFeedState>[]
                    {
                        // start with 2 ranges
                        new FeedRangeState<ReadFeedState>(new FeedRangeEpk(new Documents.Routing.Range<string>("", "A", true, false)), ReadFeedState.Beginning()),
                        new FeedRangeState<ReadFeedState>(new FeedRangeEpk(new Documents.Routing.Range<string>("A", "FF", true, false)), ReadFeedState.Beginning())
                    }));

            // Trigger merge, should requeue and read second enumerator
            await enumerator.MoveNextAsync(NoOpTrace.Singleton, cancellationToken: default);

            // Should read first enumerator again
            await enumerator.MoveNextAsync(NoOpTrace.Singleton, cancellationToken: default);

            Assert.AreEqual(2, createdEnumerators.Count, "Should only create the original 2 enumerators");
            Assert.AreEqual("", ((FeedRangeEpk)createdEnumerators[0].FeedRangeState.FeedRange).Range.Min);
            Assert.AreEqual("A", ((FeedRangeEpk)createdEnumerators[0].FeedRangeState.FeedRange).Range.Max);
            Assert.AreEqual("A", ((FeedRangeEpk)createdEnumerators[1].FeedRangeState.FeedRange).Range.Min);
            Assert.AreEqual("FF", ((FeedRangeEpk)createdEnumerators[1].FeedRangeState.FeedRange).Range.Max);

            Assert.AreEqual(2, createdEnumerators[0].GetNextPageAsyncCounter, "First enumerator should have been requeued and called again");
            Assert.AreEqual(1, createdEnumerators[1].GetNextPageAsyncCounter, "Second enumerator should be used once");
        }

        // Validates that on a split we create children enumerators and use them
        [TestMethod]
        public async Task OnSplitQueueNewEnumerators()
        {
            // We expect creation of the initial full range enumerator and the 2 children
            List<EnumeratorThatSplits> createdEnumerators = new List<EnumeratorThatSplits>();
            PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> createEnumerator(
                FeedRangeState<ReadFeedState> feedRangeState)
            {
                EnumeratorThatSplits enumerator = new EnumeratorThatSplits(feedRangeState, createdEnumerators.Count == 0);
                createdEnumerators.Add(enumerator);
                return enumerator;
            }

            // We expect a request for children and we return the new children
            Mock<IFeedRangeProvider> feedRangeProvider = new Mock<IFeedRangeProvider>();
            feedRangeProvider.Setup(p => p.GetChildRangeAsync(
                It.Is<FeedRangeInternal>(splitRange => ((FeedRangeEpk)splitRange).Range.Min == FeedRangeEpk.FullRange.Range.Min && ((FeedRangeEpk)splitRange).Range.Max == FeedRangeEpk.FullRange.Range.Max),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<FeedRangeEpk>() {
                    new FeedRangeEpk(new Documents.Routing.Range<string>("", "A", true, false)),
                    new FeedRangeEpk(new Documents.Routing.Range<string>("A", "FF", true, false))});

            CrossPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> enumerator = new CrossPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>(
                feedRangeProvider: feedRangeProvider.Object,
                createPartitionRangeEnumerator: createEnumerator,
                comparer: null,
                prefetchPolicy: PrefetchPolicy.PrefetchSinglePage,
                maxConcurrency: 0,
                state: new CrossFeedRangeState<ReadFeedState>(
                    new FeedRangeState<ReadFeedState>[]
                    {
                        // start with 1 range
                        new FeedRangeState<ReadFeedState>(FeedRangeEpk.FullRange, ReadFeedState.Beginning())
                    }));

            // Trigger split, should create children and call first children
            await enumerator.MoveNextAsync(NoOpTrace.Singleton, cancellationToken: default);

            // Should read second children
            await enumerator.MoveNextAsync(NoOpTrace.Singleton, cancellationToken: default);

            Assert.AreEqual(3, createdEnumerators.Count, "Should have the original enumerator and the children");
            Assert.AreEqual(FeedRangeEpk.FullRange.Range.Min, ((FeedRangeEpk)createdEnumerators[0].FeedRangeState.FeedRange).Range.Min);
            Assert.AreEqual(FeedRangeEpk.FullRange.Range.Max, ((FeedRangeEpk)createdEnumerators[0].FeedRangeState.FeedRange).Range.Max);
            Assert.AreEqual("", ((FeedRangeEpk)createdEnumerators[1].FeedRangeState.FeedRange).Range.Min);
            Assert.AreEqual("A", ((FeedRangeEpk)createdEnumerators[1].FeedRangeState.FeedRange).Range.Max);
            Assert.AreEqual("A", ((FeedRangeEpk)createdEnumerators[2].FeedRangeState.FeedRange).Range.Min);
            Assert.AreEqual("FF", ((FeedRangeEpk)createdEnumerators[2].FeedRangeState.FeedRange).Range.Max);

            Assert.AreEqual(1, createdEnumerators[0].GetNextPageAsyncCounter, "First enumerator should have been called once");
            Assert.AreEqual(1, createdEnumerators[1].GetNextPageAsyncCounter, "Second enumerator should have been called once");
            Assert.AreEqual(1, createdEnumerators[2].GetNextPageAsyncCounter, "Second enumerator should not be used");
        }

        [TestMethod]
        public async Task TestParallelPrefetch()
        {
            List<FeedRangeEpk> feedRanges = new List<FeedRangeEpk>()
            {
                new FeedRangeEpk(new Documents.Routing.Range<string>(  "", "AA", isMinInclusive: true, isMaxInclusive: false)),
                new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "BB", isMinInclusive: true, isMaxInclusive: false)),
                new FeedRangeEpk(new Documents.Routing.Range<string>("BB", "CC", isMinInclusive: true, isMaxInclusive: false)),
                new FeedRangeEpk(new Documents.Routing.Range<string>("CC", "DD", isMinInclusive: true, isMaxInclusive: false)),
                new FeedRangeEpk(new Documents.Routing.Range<string>("DD", "EE", isMinInclusive: true, isMaxInclusive: false)),
                new FeedRangeEpk(new Documents.Routing.Range<string>("EE", "FF", isMinInclusive: true, isMaxInclusive: false)),
            };

            Mock<IFeedRangeProvider> mockFeedRangeProvider = new Mock<IFeedRangeProvider>();
            mockFeedRangeProvider.Setup(p => p.GetFeedRangesAsync(
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(feedRanges);

            foreach (int maxConcurrency in new[] { 0, 1, 2, 10, 100 })
            {
                List<MockEnumerator> rangeEnumerators = feedRanges
                    .Select(feedRange => new MockEnumerator(new FeedRangeState<ReadFeedState>(feedRange, null), 1))
                    .ToList();

                IEnumerator<MockEnumerator> enumerator = rangeEnumerators.GetEnumerator();

                MockEnumerator CreateMockEnumerator(FeedRangeState<ReadFeedState> feedRangeState)
                {
                    Assert.IsTrue(enumerator.MoveNext());
                    return enumerator.Current;
                };

                CrossPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> crossPartitionEnumerator = new CrossPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>(
                    feedRangeProvider: mockFeedRangeProvider.Object,
                    createPartitionRangeEnumerator: CreateMockEnumerator,
                    comparer: null,
                    prefetchPolicy: PrefetchPolicy.PrefetchSinglePage,
                    maxConcurrency: maxConcurrency,
                    state: null);

                await crossPartitionEnumerator.MoveNextAsync(NoOpTrace.Singleton, cancellationToken: default);

                if (maxConcurrency <= 1)
                {
                    Assert.AreEqual(1, rangeEnumerators.First().InvocationCount);
                    Assert.IsTrue(rangeEnumerators.Skip(1).All(x => x.InvocationCount == 0));
                }
                else
                {
                    Assert.IsTrue(rangeEnumerators.All(x => x.InvocationCount == 1));
                }
            }
        }

        private class MockEnumerator : PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>
        {
            private static readonly IReadOnlyDictionary<string, string> EmptyHeaders = new Dictionary<string, string>();

            private static readonly Stream EmptyStream = new MemoryStream(Encoding.UTF8.GetBytes("{\"Documents\": [], \"_count\": 0, \"_rid\": \"asdf\"}"));

            private readonly int pageCount;

            public int InvocationCount { get; private set; }

            public MockEnumerator(FeedRangeState<ReadFeedState> feedRangeState, int pageCount)
                : base(feedRangeState)
            {
                this.pageCount = pageCount;
            }

            public override ValueTask DisposeAsync()
            {
                return default;
            }

            protected override Task<TryCatch<ReadFeedPage>> GetNextPageAsync(ITrace trace, CancellationToken cancellationToken)
            {
                if (this.InvocationCount >= this.pageCount)
                {
                    return Task.FromResult(TryCatch<ReadFeedPage>.FromException(new InvalidOperationException(
                        "Trying to move next on an enumerator that is finished")));
                }

                ++this.InvocationCount;

                ReadFeedState state = null;
                if (this.InvocationCount < this.pageCount)
                {
                    CosmosElement continuationToken = CosmosString.Create("asdf");
                    state = new ReadFeedContinuationState(continuationToken);
                }

                return Task.FromResult(TryCatch<ReadFeedPage>.FromResult(
                    new ReadFeedPage(EmptyStream, 2.8, 0, "activityId", EmptyHeaders, state)));
            }
        }

        private class EnumeratorThatSplits : PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>
        {
            private readonly bool throwError;

            public EnumeratorThatSplits(
                FeedRangeState<ReadFeedState> feedRangeState,
                bool throwError = true)
                : base(feedRangeState)
            {
                this.throwError = throwError;
            }

            public override ValueTask DisposeAsync()
            {
                throw new NotImplementedException();
            }

            public int GetNextPageAsyncCounter { get; private set; }

            protected override Task<TryCatch<ReadFeedPage>> GetNextPageAsync(ITrace trace, CancellationToken cancellationToken)
            {
                this.GetNextPageAsyncCounter++;

                if (this.GetNextPageAsyncCounter == 1
                    && this.throwError)
                {
                    CosmosException splitError = new CosmosException("merge", System.Net.HttpStatusCode.Gone, (int)Documents.SubStatusCodes.PartitionKeyRangeGone, string.Empty, 0);
                    TryCatch<ReadFeedPage> state = TryCatch<ReadFeedPage>.FromException(splitError);
                    return Task.FromResult(state);
                }
                else
                {
                    return Task.FromResult(TryCatch<ReadFeedPage>.FromResult(
                        new ReadFeedPage(
                            new MemoryStream(Encoding.UTF8.GetBytes("{\"Documents\": [], \"_count\": 0, \"_rid\": \"asdf\"}")),
                            requestCharge: 1,
                            itemCount: 0,
                            activityId: Guid.NewGuid().ToString(),
                            additionalHeaders: null,
                            state: ReadFeedState.Beginning())));
                }
            }
        }

        [TestMethod]
        [DataRow(false, false, false, DisplayName = "Use State: false, Allow Splits: false, Allow Merges: false")]
        [DataRow(false, false, true, DisplayName = "Use State: false, Allow Splits: false, Allow Merges: true")]
        [DataRow(false, true, false, DisplayName = "Use State: false, Allow Splits: true, Allow Merges: false")]
        [DataRow(false, true, true, DisplayName = "Use State: false, Allow Splits: true, Allow Merges: true")]
        [DataRow(true, false, false, DisplayName = "Use State: true, Allow Splits: false, Allow Merges: false")]
        [DataRow(true, false, true, DisplayName = "Use State: true, Allow Splits: false, Allow Merges: true")]
        [DataRow(true, true, false, DisplayName = "Use State: true, Allow Splits: true, Allow Merges: false")]
        [DataRow(true, true, true, DisplayName = "Use State: true, Allow Splits: true, Allow Merges: true")]
        public async Task TestSplitAndMergeAsync(bool useState, bool allowSplits, bool allowMerges)
        {
            Implementation implementation = new Implementation(singlePartition: false);
            await implementation.TestSplitAndMergeImplementationAsync(useState, allowSplits, allowMerges);
        }

        private sealed class Implementation : PartitionRangeEnumeratorTests<CrossFeedRangePage<ReadFeedPage, ReadFeedState>, CrossFeedRangeState<ReadFeedState>>
        {
            enum TriState { NotReady, Ready, Done };

            public Implementation(bool singlePartition)
                : base(singlePartition)
            {
                this.ShouldMerge = TriState.NotReady;
            }

            private TriState ShouldMerge { get; set; }

            private IDocumentContainer DocumentContainer { get; set; }

            private async Task<Exception> ShouldReturnFailure()
            {
                if (this.ShouldMerge == TriState.Ready)
                {
                    await this.DocumentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
                    List<FeedRangeEpk> ranges = await this.DocumentContainer.GetFeedRangesAsync(
                        trace: NoOpTrace.Singleton,
                        cancellationToken: default);

                    await this.DocumentContainer.MergeAsync(ranges[0], ranges[1], default);
                    await this.DocumentContainer.RefreshProviderAsync(NoOpTrace.Singleton, default);
                    this.ShouldMerge = TriState.Done;

                    return new CosmosException(
                        message: "PKRange was split/merged",
                        statusCode: System.Net.HttpStatusCode.Gone,
                        subStatusCode: (int)Documents.SubStatusCodes.PartitionKeyRangeGone,
                        activityId: "BC0CCDA5-D378-4922-B8B0-D51D745B9139",
                        requestCharge: 0.0);
                }
                else
                {
                    return null;
                }
            }

            public async Task TestMergeToSinglePartition()
            {
                int numItems = 1000;
                FlakyDocumentContainer.FailureConfigs config = new FlakyDocumentContainer.FailureConfigs(
                    inject429s: false,
                    injectEmptyPages: false,
                    shouldReturnFailure: this.ShouldReturnFailure);

                this.DocumentContainer = await this.CreateDocumentContainerAsync(numItems: numItems, failureConfigs: config);

                await this.DocumentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
                List<FeedRangeEpk> ranges = await this.DocumentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);
                await this.DocumentContainer.SplitAsync(ranges.First(), cancellationToken: default);

                IAsyncEnumerator<TryCatch<CrossFeedRangePage<ReadFeedPage, ReadFeedState>>> enumerator = await this.CreateEnumeratorAsync(this.DocumentContainer);
                List<string> identifiers = new List<string>();
                int iteration = 0;
                while (await enumerator.MoveNextAsync())
                {
                    TryCatch<CrossFeedRangePage<ReadFeedPage, ReadFeedState>> tryGetPage = enumerator.Current;
                    tryGetPage.ThrowIfFailed();

                    IReadOnlyList<Record> records = this.GetRecordsFromPage(tryGetPage.Result);
                    foreach (Record record in records)
                    {
                        identifiers.Add(record.Payload["pk"].ToString());
                    }

                    ++iteration;
                    if (iteration == 1)
                    {
                        this.ShouldMerge = TriState.Ready;
                    }
                }

                Assert.AreEqual(numItems, identifiers.Count);
            }

            public async Task TestSplitAndMergeImplementationAsync(
                bool useState,
                bool allowSplits,
                bool allowMerges)
            {
                int numItems = 1000;
                IDocumentContainer inMemoryCollection = await this.CreateDocumentContainerAsync(numItems);
                IAsyncEnumerator<TryCatch<CrossFeedRangePage<ReadFeedPage, ReadFeedState>>> enumerator = await this.CreateEnumeratorAsync(inMemoryCollection);
                HashSet<string> identifiers = new HashSet<string>();
                Random random = new Random();
                while (await enumerator.MoveNextAsync())
                {
                    TryCatch<CrossFeedRangePage<ReadFeedPage, ReadFeedState>> tryGetPage = enumerator.Current;
                    tryGetPage.ThrowIfFailed();

                    IReadOnlyList<Record> records = this.GetRecordsFromPage(tryGetPage.Result);
                    foreach (Record record in records)
                    {
                        identifiers.Add(record.Payload["pk"].ToString());
                    }

                    if (useState)
                    {
                        if (tryGetPage.Result.State == null)
                        {
                            break;
                        }

                        enumerator = await this.CreateEnumeratorAsync(
                            inMemoryCollection,
                            false,
                            false,
                            tryGetPage.Result.State);
                    }

                    if (random.Next() % 2 == 0)
                    {
                        if (allowSplits && (random.Next() % 2 == 0))
                        {
                            // Split
                            await inMemoryCollection.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
                            List<FeedRangeEpk> ranges = await inMemoryCollection.GetFeedRangesAsync(
                                trace: NoOpTrace.Singleton,
                                cancellationToken: default);
                            FeedRangeInternal randomRangeToSplit = ranges[random.Next(0, ranges.Count)];
                            await inMemoryCollection.SplitAsync(randomRangeToSplit, cancellationToken: default);
                        }

                        if (allowMerges && (random.Next() % 2 == 0))
                        {
                            // Merge
                            await inMemoryCollection.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
                            List<FeedRangeEpk> ranges = await inMemoryCollection.GetFeedRangesAsync(
                                trace: NoOpTrace.Singleton,
                                cancellationToken: default);
                            if (ranges.Count > 1)
                            {
                                ranges = ranges.OrderBy(range => range.Range.Min).ToList();
                                int indexToMerge = random.Next(0, ranges.Count);
                                int adjacentIndex = indexToMerge == (ranges.Count - 1) ? indexToMerge - 1 : indexToMerge + 1;
                                await inMemoryCollection.MergeAsync(ranges[indexToMerge], ranges[adjacentIndex], cancellationToken: default);
                            }
                        }
                    }
                }

                Assert.AreEqual(numItems, identifiers.Count);
            }

            protected override IAsyncEnumerable<TryCatch<CrossFeedRangePage<ReadFeedPage, ReadFeedState>>> CreateEnumerable(
                IDocumentContainer inMemoryCollection,
                bool aggressivePrefetch = false,
                CrossFeedRangeState<ReadFeedState> state = null)
            {
                PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> createEnumerator(
                    FeedRangeState<ReadFeedState> feedRangeState)
                {
                    return new ReadFeedPartitionRangeEnumerator(
                        inMemoryCollection,
                        feedRangeState: feedRangeState,
                        readFeedPaginationOptions: new ReadFeedExecutionOptions(pageSizeHint: 10));
                }

                return new CrossPartitionRangePageAsyncEnumerable<ReadFeedPage, ReadFeedState>(
                    feedRangeProvider: inMemoryCollection,
                    createPartitionRangeEnumerator: createEnumerator,
                    comparer: PartitionRangePageAsyncEnumeratorComparer.Singleton,
                    maxConcurrency: 10,
                    prefetchPolicy: aggressivePrefetch ? PrefetchPolicy.PrefetchAll : PrefetchPolicy.PrefetchSinglePage,
                    trace: NoOpTrace.Singleton,
                    state: state ?? new CrossFeedRangeState<ReadFeedState>(
                        new FeedRangeState<ReadFeedState>[]
                        {
                            new FeedRangeState<ReadFeedState>(FeedRangeEpk.FullRange, ReadFeedState.Beginning())
                        }));
            }

            protected override Task<IAsyncEnumerator<TryCatch<CrossFeedRangePage<ReadFeedPage, ReadFeedState>>>> CreateEnumeratorAsync(
                IDocumentContainer inMemoryCollection,
                bool aggressivePrefetch = false,
                bool exercisePrefetch = false,
                CrossFeedRangeState<ReadFeedState> state = null,
                CancellationToken cancellationToken = default)
            {
                PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> createEnumerator(
                    FeedRangeState<ReadFeedState> feedRangeState)
                {
                    return new ReadFeedPartitionRangeEnumerator(
                        inMemoryCollection,
                        feedRangeState: feedRangeState,
                        readFeedPaginationOptions: new ReadFeedExecutionOptions(pageSizeHint: 10));
                }

                IAsyncEnumerator<TryCatch<CrossFeedRangePage<ReadFeedPage, ReadFeedState>>> enumerator = new TracingAsyncEnumerator<TryCatch<CrossFeedRangePage<ReadFeedPage, ReadFeedState>>>(
                    new CrossPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>(
                        feedRangeProvider: inMemoryCollection,
                        createPartitionRangeEnumerator: createEnumerator,
                        comparer: PartitionRangePageAsyncEnumeratorComparer.Singleton,
                        maxConcurrency: 10,
                        prefetchPolicy: PrefetchPolicy.PrefetchSinglePage,
                        state: state ?? new CrossFeedRangeState<ReadFeedState>(
                            new FeedRangeState<ReadFeedState>[]
                            {
                                new FeedRangeState<ReadFeedState>(FeedRangeEpk.FullRange, ReadFeedState.Beginning())
                            })),
                    NoOpTrace.Singleton,
                    cancellationToken: default);

                return Task.FromResult(enumerator);
            }

            public override IReadOnlyList<Record> GetRecordsFromPage(CrossFeedRangePage<ReadFeedPage, ReadFeedState> page)
            {
                return page.Page.GetRecords();
            }

            private sealed class PartitionRangePageAsyncEnumeratorComparer : IComparer<PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>>
            {
                public static readonly PartitionRangePageAsyncEnumeratorComparer Singleton = new PartitionRangePageAsyncEnumeratorComparer();

                public int Compare(
                    PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> partitionRangePageEnumerator1,
                    PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> partitionRangePageEnumerator2)
                {
                    if (object.ReferenceEquals(partitionRangePageEnumerator1, partitionRangePageEnumerator2))
                    {
                        return 0;
                    }

                    // Either both don't have results or both do.
                    return string.CompareOrdinal(
                        ((FeedRangeEpk)partitionRangePageEnumerator1.FeedRangeState.FeedRange).Range.Min,
                        ((FeedRangeEpk)partitionRangePageEnumerator2.FeedRangeState.FeedRange).Range.Min);
                }
            }
        }
    }
}