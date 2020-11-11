//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class CrossPartitionPartitionRangeEnumeratorTests 
    {
        [TestMethod]
        public async Task Test429sAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.Test429sAsync();
        }

        [TestMethod]
        public async Task Test429sWithContinuationsAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.Test429sWithContinuationsAsync();
        }

        [TestMethod]
        public async Task TestDrainFullyAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.TestDrainFullyAsync();
        }

        [TestMethod]
        public async Task TestEmptyPages()
        {
            Implementation implementation = new Implementation();
            await implementation.TestEmptyPages();
        }

        [TestMethod]
        public async Task TestResumingFromStateAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.TestResumingFromStateAsync();
        }

        [TestMethod]
        public async Task TestSplitWithDuringDrainAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.TestSplitWithDuringDrainAsync();
        }

        [TestMethod]
        public async Task TestSplitWithResumeContinuationAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.TestSplitWithResumeContinuationAsync();
        }

        private sealed class Implementation : PartitionRangeEnumeratorTests<CrossFeedRangePage<ReadFeedPage, ReadFeedState>, CrossFeedRangeState<ReadFeedState>>
        {
            public Implementation()
                : base(singlePartition: false)
            {
            }

            [TestMethod]
            public async Task TestSplitWithResumeContinuationAsync()
            {
                int numItems = 1000;
                IDocumentContainer inMemoryCollection = await this.CreateDocumentContainerAsync(numItems);
                IAsyncEnumerator<TryCatch<CrossFeedRangePage<ReadFeedPage, ReadFeedState>>> enumerator = this.CreateEnumerator(inMemoryCollection);

                (HashSet<string> firstDrainResults, CrossFeedRangeState<ReadFeedState> state) = await this.PartialDrainAsync(enumerator, numIterations: 3);

                IReadOnlyList<FeedRangeInternal> ranges = await inMemoryCollection.GetFeedRangesAsync(cancellationToken: default);

                // Split the partition we were reading from
                await inMemoryCollection.SplitAsync(ranges.First(), cancellationToken: default);

                // And a partition we have let to read from
                await inMemoryCollection.SplitAsync(ranges[ranges.Count / 2], cancellationToken: default);

                // Resume from state
                IAsyncEnumerable<TryCatch<CrossFeedRangePage<ReadFeedPage, ReadFeedState>>> enumerable = this.CreateEnumerable(inMemoryCollection, state);

                HashSet<string> secondDrainResults = await this.DrainFullyAsync(enumerable);
                Assert.AreEqual(numItems, firstDrainResults.Count + secondDrainResults.Count);
            }

            [TestMethod]
            public async Task TestSplitWithDuringDrainAsync()
            {
                int numItems = 1000;
                IDocumentContainer inMemoryCollection = await this.CreateDocumentContainerAsync(numItems);
                IAsyncEnumerable<TryCatch<CrossFeedRangePage<ReadFeedPage, ReadFeedState>>> enumerable = this.CreateEnumerable(inMemoryCollection);

                HashSet<string> identifiers = new HashSet<string>();
                Random random = new Random();
                await foreach (TryCatch<CrossFeedRangePage<ReadFeedPage, ReadFeedState>> tryGetPage in enumerable)
                {
                    if (random.Next() % 2 == 0)
                    {
                        List<FeedRangeEpk> ranges = await inMemoryCollection.GetFeedRangesAsync(cancellationToken: default);
                        FeedRangeInternal randomRangeToSplit = ranges[random.Next(0, ranges.Count)];
                        await inMemoryCollection.SplitAsync(randomRangeToSplit, cancellationToken: default);
                    }

                    tryGetPage.ThrowIfFailed();

                    IReadOnlyList<Record> records = this.GetRecordsFromPage(tryGetPage.Result);
                    foreach (Record record in records)
                    {
                        identifiers.Add(record.Identifier);
                    }
                }

                Assert.AreEqual(numItems, identifiers.Count);
            }

            public override IAsyncEnumerable<TryCatch<CrossFeedRangePage<ReadFeedPage, ReadFeedState>>> CreateEnumerable(
                IDocumentContainer inMemoryCollection,
                CrossFeedRangeState<ReadFeedState> state = null)
            {
                PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> createEnumerator(
                    FeedRangeInternal range,
                    ReadFeedState state) => new ReadFeedPartitionRangeEnumerator(
                        inMemoryCollection,
                        feedRange: range,
                        pageSize: 10,
                        queryRequestOptions: default,
                        cancellationToken: default,
                        state: state);

                return new CrossPartitionRangePageAsyncEnumerable<ReadFeedPage, ReadFeedState>(
                    feedRangeProvider: inMemoryCollection,
                    createPartitionRangeEnumerator: createEnumerator,
                    comparer: PartitionRangePageAsyncEnumeratorComparer.Singleton,
                    maxConcurrency: 10,
                    state: state ?? new CrossFeedRangeState<ReadFeedState>(
                        new FeedRangeState<ReadFeedState>[]
                        { 
                            new FeedRangeState<ReadFeedState>(FeedRangeEpk.FullRange, new ReadFeedState(CosmosNull.Create()))
                        }));
            }

            public override IAsyncEnumerator<TryCatch<CrossFeedRangePage<ReadFeedPage, ReadFeedState>>> CreateEnumerator(
                IDocumentContainer inMemoryCollection,
                CrossFeedRangeState<ReadFeedState> state = null)
            {
                PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> createEnumerator(
                    FeedRangeInternal range,
                    ReadFeedState state) => new ReadFeedPartitionRangeEnumerator(
                        inMemoryCollection,
                        feedRange: range,
                        pageSize: 10,
                        queryRequestOptions: default,
                        cancellationToken: default,
                        state: state);

                CrossPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState> enumerator = new CrossPartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>(
                    feedRangeProvider: inMemoryCollection,
                    createPartitionRangeEnumerator: createEnumerator,
                    comparer: PartitionRangePageAsyncEnumeratorComparer.Singleton,
                    maxConcurrency: 10,
                    cancellationToken: default,
                    state: state ?? new CrossFeedRangeState<ReadFeedState>(
                        new FeedRangeState<ReadFeedState>[]
                        {
                            new FeedRangeState<ReadFeedState>(FeedRangeEpk.FullRange, new ReadFeedState(CosmosNull.Create()))
                        }));

                return enumerator;
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
                        ((FeedRangeEpk)partitionRangePageEnumerator1.Range).Range.Min,
                        ((FeedRangeEpk)partitionRangePageEnumerator2.Range).Range.Min);
                }
            }
        }
    }
}
