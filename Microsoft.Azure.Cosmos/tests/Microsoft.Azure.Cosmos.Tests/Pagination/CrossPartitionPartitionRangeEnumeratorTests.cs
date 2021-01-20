//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
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
        public async Task TestEmptyPages()
        {
            Implementation implementation = new Implementation();
            await implementation.TestEmptyPages();
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
            Implementation implementation = new Implementation();
            await implementation.TestSplitAndMergeImplementationAsync(useState, allowSplits, allowMerges);
        }

        private sealed class Implementation : PartitionRangeEnumeratorTests<CrossFeedRangePage<ReadFeedPage, ReadFeedState>, CrossFeedRangeState<ReadFeedState>>
        {
            public Implementation()
                : base(singlePartition: false)
            {
            }

            [TestMethod]
            public async Task TestSplitAndMergeImplementationAsync(bool useState, bool allowSplits, bool allowMerges)
            {
                int numItems = 1000;
                IDocumentContainer inMemoryCollection = await this.CreateDocumentContainerAsync(numItems);
                IAsyncEnumerator<TryCatch<CrossFeedRangePage<ReadFeedPage, ReadFeedState>>> enumerator = this.CreateEnumerator(inMemoryCollection);
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

                        enumerator = this.CreateEnumerator(inMemoryCollection, tryGetPage.Result.State);
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
                    isStreamingOperation: true,
                    state: state ?? new CrossFeedRangeState<ReadFeedState>(
                        new FeedRangeState<ReadFeedState>[]
                        {
                            new FeedRangeState<ReadFeedState>(FeedRangeEpk.FullRange, ReadFeedState.Beginning())
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
                    isStreamingOperation: true,
                    cancellationToken: default,
                    state: state ?? new CrossFeedRangeState<ReadFeedState>(
                        new FeedRangeState<ReadFeedState>[]
                        {
                            new FeedRangeState<ReadFeedState>(FeedRangeEpk.FullRange, ReadFeedState.Beginning())
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
