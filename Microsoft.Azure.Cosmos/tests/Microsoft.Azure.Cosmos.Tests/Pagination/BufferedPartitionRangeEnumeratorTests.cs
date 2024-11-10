namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Threading;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tests.Query.Pipeline;

    [TestClass]
    public sealed class BufferedPartitionPartitionRangeEnumeratorTests
    {
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task Test429sAsync(bool aggressivePrefetch)
        {
            Implementation implementation = new Implementation();
            await implementation.Test429sAsync(aggressivePrefetch);
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task Test429sWithContinuationsAsync(bool aggressivePrefetch, bool exercisePrefetch)
        {
            Implementation implementation = new Implementation();
            await implementation.Test429sWithContinuationsAsync(aggressivePrefetch, exercisePrefetch);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task TestDrainFullyAsync(bool aggressivePrefetch)
        {
            Implementation implementation = new Implementation();
            await implementation.TestDrainFullyAsync(aggressivePrefetch);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task TestEmptyPages(bool aggressivePrefetch)
        {
            Implementation implementation = new Implementation();
            await implementation.TestEmptyPages(aggressivePrefetch);
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task TestResumingFromStateAsync(bool aggressivePrefetch, bool exercisePrefetch)
        {
            Implementation implementation = new Implementation();
            await implementation.TestResumingFromStateAsync(aggressivePrefetch, exercisePrefetch);
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task TestSplitAsync(bool aggressivePrefetch, bool exercisePrefetch)
        {
            Implementation implementation = new Implementation();
            await implementation.TestSplitAsync(aggressivePrefetch, exercisePrefetch);
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task TestBufferPageAsync(bool aggressivePrefetch, bool exercisePrefetch)
        {
            Implementation implementation = new Implementation();
            await implementation.TestBufferPageAsync(aggressivePrefetch, exercisePrefetch);
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task TestMoveNextAndBufferPageAsync(bool aggressivePrefetch, bool exercisePrefetch)
        {
            Implementation implementation = new Implementation();
            await implementation.TestMoveNextAndBufferPageAsync(aggressivePrefetch, exercisePrefetch);
        }

        [TestClass]
        private sealed class Implementation : PartitionRangeEnumeratorTests<ReadFeedPage, ReadFeedState>
        {
            private const int Iterations = 1;

            public Implementation()
                : base(singlePartition: true)
            {
            }

            [TestMethod]
            public async Task TestSplitAsync(bool aggressivePrefetch, bool exercisePrefetch)
            {
                int numItems = 100;
                IDocumentContainer inMemoryCollection = await this.CreateDocumentContainerAsync(numItems);
                IAsyncEnumerator<TryCatch<ReadFeedPage>> enumerator = await this.CreateEnumeratorAsync(inMemoryCollection, aggressivePrefetch, exercisePrefetch);

                (HashSet<string> parentIdentifiers, ReadFeedState state) = await this.PartialDrainAsync(enumerator, numIterations: 3);

                // Split the partition
                await inMemoryCollection.SplitAsync(new FeedRangePartitionKeyRange(partitionKeyRangeId: "0"), cancellationToken: default);

                // Try To read from the partition that is gone.
                await enumerator.MoveNextAsync();
                Assert.IsTrue((aggressivePrefetch && exercisePrefetch) || enumerator.Current.Failed);

                // Resume on the children using the parent continuation token
                HashSet<string> childIdentifiers = new HashSet<string>();
                foreach (int partitionKeyRangeId in new int[] { 1, 2 })
                {
                    PartitionRangePageAsyncEnumerable<ReadFeedPage, ReadFeedState> enumerable = new PartitionRangePageAsyncEnumerable<ReadFeedPage, ReadFeedState>(
                        feedRangeState: new FeedRangeState<ReadFeedState>(
                            new FeedRangePartitionKeyRange(partitionKeyRangeId: partitionKeyRangeId.ToString()),
                            state),
                        (feedRangeState) => new ReadFeedPartitionRangeEnumerator(
                            inMemoryCollection,
                            feedRangeState: feedRangeState,
                            readFeedPaginationOptions: new ReadFeedExecutionOptions(pageSizeHint: 10)),
                        trace: NoOpTrace.Singleton);

                    HashSet<string> resourceIdentifiers = await this.DrainFullyAsync(enumerable);

                    childIdentifiers.UnionWith(resourceIdentifiers);
                }

                Assert.AreEqual(numItems, parentIdentifiers.Count + childIdentifiers.Count);
            }

            [TestMethod]
            public async Task TestBufferPageAsync(bool aggressivePrefetch, bool exercisePrefetch)
            {
                int numItems = 100;
                IDocumentContainer inMemoryCollection = await this.CreateDocumentContainerAsync(numItems);
                BufferedPartitionRangePageAsyncEnumeratorBase<ReadFeedPage, ReadFeedState> enumerator = aggressivePrefetch ?
                    new FullyBufferedPartitionRangeAsyncEnumerator<ReadFeedPage, ReadFeedState>(
                        new ReadFeedPartitionRangeEnumerator(
                            inMemoryCollection,
                            feedRangeState: new FeedRangeState<ReadFeedState>(
                                new FeedRangePartitionKeyRange(partitionKeyRangeId: "0"),
                                ReadFeedState.Beginning()),
                            readFeedPaginationOptions: new ReadFeedExecutionOptions(pageSizeHint: 10))) :
                    new BufferedPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>(
                        new ReadFeedPartitionRangeEnumerator(
                            inMemoryCollection,
                            feedRangeState: new FeedRangeState<ReadFeedState>(
                                new FeedRangePartitionKeyRange(partitionKeyRangeId: "0"),
                                ReadFeedState.Beginning()),
                            readFeedPaginationOptions: new ReadFeedExecutionOptions(pageSizeHint: 10)));

                int count = 0;

                for (int i = 0; i < 10; i++)
                {
                    // This call is idempotent;
                    await enumerator.PrefetchAsync(trace: NoOpTrace.Singleton, default);
                }

                while (await enumerator.MoveNextAsync(NoOpTrace.Singleton, cancellationToken: default))
                {
                    count += enumerator.Current.Result.GetRecords().Count;
                    if (exercisePrefetch)
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            // This call is idempotent;
                            await enumerator.PrefetchAsync(trace: NoOpTrace.Singleton, default);
                        }
                    }
                }

                Assert.AreEqual(numItems, count);
            }

            public async Task TestMoveNextAndBufferPageAsync(bool aggressivePrefetch, bool exercisePrefetch)
            {
                int numItems = 100;
                IDocumentContainer inMemoryCollection = await this.CreateDocumentContainerAsync(numItems);

                for (int iteration = 0; iteration < Iterations; iteration++)
                {
                    BufferedPartitionRangePageAsyncEnumeratorBase<ReadFeedPage, ReadFeedState> enumerator = aggressivePrefetch ?
                       new FullyBufferedPartitionRangeAsyncEnumerator<ReadFeedPage, ReadFeedState>(
                            new ReadFeedPartitionRangeEnumerator(
                                inMemoryCollection,
                                feedRangeState: new FeedRangeState<ReadFeedState>(
                                    new FeedRangePartitionKeyRange(partitionKeyRangeId: "0"),
                                    ReadFeedState.Beginning()),
                                readFeedPaginationOptions: new ReadFeedExecutionOptions(pageSizeHint: 10))) :
                        new BufferedPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>(
                            new ReadFeedPartitionRangeEnumerator(
                                inMemoryCollection,
                                feedRangeState: new FeedRangeState<ReadFeedState>(
                                    new FeedRangePartitionKeyRange(partitionKeyRangeId: "0"),
                                    ReadFeedState.Beginning()),
                                readFeedPaginationOptions: new ReadFeedExecutionOptions(pageSizeHint: 10)));

                    if (exercisePrefetch)
                    {
                        await enumerator.PrefetchAsync(trace: NoOpTrace.Singleton, default);
                    }

                    int count = 0;
                    while (await enumerator.MoveNextAsync(NoOpTrace.Singleton, cancellationToken: default))
                    {
                        count += enumerator.Current.Result.GetRecords().Count;

                        if (exercisePrefetch)
                        {
                            await enumerator.PrefetchAsync(trace: NoOpTrace.Singleton, default);
                        }
                    }

                    Assert.AreEqual(numItems, count);
                }
            }

            public override IReadOnlyList<Record> GetRecordsFromPage(ReadFeedPage page)
            {
                return page.GetRecords();
            }

            protected override IAsyncEnumerable<TryCatch<ReadFeedPage>> CreateEnumerable(
                IDocumentContainer documentContainer,
                bool aggressivePrefetch = false,
                ReadFeedState state = null)
            {
                PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> CreateBufferedEnumerator(FeedRangeState<ReadFeedState> feedRangeState)
                {
                    BufferedPartitionRangePageAsyncEnumeratorBase<ReadFeedPage, ReadFeedState> enumerator = aggressivePrefetch ?
                        new FullyBufferedPartitionRangeAsyncEnumerator<ReadFeedPage, ReadFeedState>(
                            new ReadFeedPartitionRangeEnumerator(
                                documentContainer,
                                feedRangeState: feedRangeState,
                                readFeedPaginationOptions: new ReadFeedExecutionOptions(pageSizeHint: 10))) :
                        new BufferedPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>(
                            new ReadFeedPartitionRangeEnumerator(
                                documentContainer,
                                feedRangeState: feedRangeState,
                                readFeedPaginationOptions: new ReadFeedExecutionOptions(pageSizeHint: 10)));

                    return enumerator;
                };

                return new PartitionRangePageAsyncEnumerable<ReadFeedPage, ReadFeedState>(
                    feedRangeState: new FeedRangeState<ReadFeedState>(
                        new FeedRangePartitionKeyRange(partitionKeyRangeId: "0"),
                        state ?? ReadFeedState.Beginning()),
                    createPartitionRangeEnumerator: CreateBufferedEnumerator,
                    trace: NoOpTrace.Singleton);
            }

            protected async override Task<IAsyncEnumerator<TryCatch<ReadFeedPage>>> CreateEnumeratorAsync(
                IDocumentContainer inMemoryCollection,
                bool aggressivePrefetch = false,
                bool exercisePrefetch = false,
                ReadFeedState state = null,
                CancellationToken cancellationToken = default)
            {
                BufferedPartitionRangePageAsyncEnumeratorBase<ReadFeedPage, ReadFeedState> enumerator = aggressivePrefetch ?
                new FullyBufferedPartitionRangeAsyncEnumerator<ReadFeedPage, ReadFeedState>(
                    new ReadFeedPartitionRangeEnumerator(
                        inMemoryCollection,
                        feedRangeState: new FeedRangeState<ReadFeedState>(
                            new FeedRangePartitionKeyRange(partitionKeyRangeId: "0"),
                            state ?? ReadFeedState.Beginning()),
                        readFeedPaginationOptions: new ReadFeedExecutionOptions(pageSizeHint: 10))) :
                new BufferedPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>(
                    new ReadFeedPartitionRangeEnumerator(
                        inMemoryCollection,
                        feedRangeState: new FeedRangeState<ReadFeedState>(
                            new FeedRangePartitionKeyRange(partitionKeyRangeId: "0"),
                            state ?? ReadFeedState.Beginning()),
                        readFeedPaginationOptions: new ReadFeedExecutionOptions(pageSizeHint: 10)));

                if (exercisePrefetch)
                {
                    await enumerator.PrefetchAsync(NoOpTrace.Singleton, default);
                }

                return new TracingAsyncEnumerator<TryCatch<ReadFeedPage>>(
                    enumerator: enumerator,
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);
            }
        }
    }
}
