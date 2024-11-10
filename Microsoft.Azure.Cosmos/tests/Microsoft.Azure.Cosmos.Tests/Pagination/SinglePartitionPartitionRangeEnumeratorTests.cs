//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tests.Query.Pipeline;

    [TestClass]
    public sealed class SinglePartitionPartitionRangeEnumeratorTests
    {
        [TestMethod]
        public async Task Test429sAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.Test429sAsync(false);
        }

        [TestMethod]
        public async Task Test429sWithContinuationsAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.Test429sWithContinuationsAsync(false, false);
        }

        [TestMethod]
        public async Task TestDrainFullyAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.TestDrainFullyAsync(false);
        }

        [TestMethod]
        public async Task TestEmptyPages()
        {
            Implementation implementation = new Implementation();
            await implementation.TestEmptyPages(false);
        }

        [TestMethod]
        public async Task TestResumingFromStateAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.TestResumingFromStateAsync(false, false);
        }

        [TestMethod]
        public async Task TestSplitAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.TestSplitAsync();
        }

        [TestClass]
        private sealed class Implementation : PartitionRangeEnumeratorTests<ReadFeedPage, ReadFeedState>
        {
            public Implementation()
                : base(singlePartition: true)
            {
            }

            [TestMethod]
            public async Task TestSplitAsync()
            {
                int numItems = 100;
                IDocumentContainer inMemoryCollection = await this.CreateDocumentContainerAsync(numItems);
                IReadOnlyList<FeedRangeInternal> ranges = await inMemoryCollection.GetFeedRangesAsync(trace: NoOpTrace.Singleton, cancellationToken: default);
                Assert.AreEqual(1, ranges.Count);

                TracingAsyncEnumerator<TryCatch<ReadFeedPage>> enumerator = new(
                    new ReadFeedPartitionRangeEnumerator(
                        inMemoryCollection,
                        feedRangeState: new FeedRangeState<ReadFeedState>(ranges[0], ReadFeedState.Beginning()),
                        readFeedPaginationOptions: new ReadFeedExecutionOptions(pageSizeHint: 10)),
                    NoOpTrace.Singleton,
                    cancellationToken: default);


                (HashSet<string> parentIdentifiers, ReadFeedState state) = await this.PartialDrainAsync(enumerator, numIterations: 3);

                // Split the partition
                await inMemoryCollection.SplitAsync(ranges[0], cancellationToken: default);

                // Try To read from the partition that is gone.
                await enumerator.MoveNextAsync();
                Assert.IsTrue(enumerator.Current.Failed);

                // Resume on the children using the parent continuaiton token
                HashSet<string> childIdentifiers = new HashSet<string>();

                await inMemoryCollection.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
                IReadOnlyList<FeedRangeInternal> childRanges = await inMemoryCollection.GetFeedRangesAsync(trace: NoOpTrace.Singleton, cancellationToken: default);
                foreach (FeedRangeInternal childRange in childRanges)
                {
                    PartitionRangePageAsyncEnumerable<ReadFeedPage, ReadFeedState> enumerable = new PartitionRangePageAsyncEnumerable<ReadFeedPage, ReadFeedState>(
                        feedRangeState: new FeedRangeState<ReadFeedState>(childRange, state),
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

            public override IReadOnlyList<Record> GetRecordsFromPage(ReadFeedPage page)
            {
                return page.GetRecords();
            }

            protected override IAsyncEnumerable<TryCatch<ReadFeedPage>> CreateEnumerable(
                IDocumentContainer documentContainer,
                bool aggressivePrefetch = false,
                ReadFeedState state = null)
            {
                return new PartitionRangePageAsyncEnumerable<ReadFeedPage, ReadFeedState>(
                    feedRangeState: new FeedRangeState<ReadFeedState>(
                    new FeedRangePartitionKeyRange(partitionKeyRangeId: "0"),
                        state ?? ReadFeedState.Beginning()),
                        (feedRangeState) => new ReadFeedPartitionRangeEnumerator(
                            documentContainer,
                            feedRangeState: feedRangeState,
                            readFeedPaginationOptions: new ReadFeedExecutionOptions(pageSizeHint: 10)),
                    trace: NoOpTrace.Singleton);
            }

            protected override Task<IAsyncEnumerator<TryCatch<ReadFeedPage>>> CreateEnumeratorAsync(
                IDocumentContainer inMemoryCollection,
                bool aggressivePrefetch = false,
                bool exercisePrefetch = false,
                ReadFeedState state = null,
                CancellationToken cancellationToken = default)
            {
                IAsyncEnumerator<TryCatch<ReadFeedPage>> enumerator = new TracingAsyncEnumerator<TryCatch<ReadFeedPage>>(
                    new ReadFeedPartitionRangeEnumerator(
                        inMemoryCollection,
                        feedRangeState: new FeedRangeState<ReadFeedState>(
                            new FeedRangePartitionKeyRange(partitionKeyRangeId: "0"),
                            state ?? ReadFeedState.Beginning()),
                        readFeedPaginationOptions: new ReadFeedExecutionOptions(pageSizeHint: 10)),
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);

                return Task.FromResult(enumerator);
            }
        }
    }
}
