//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Documents;
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

        private sealed class Implementation : PartitionRangeEnumeratorTests<CrossFeedRangePage<DocumentContainerPage, DocumentContainerState>, CrossFeedRangeState<DocumentContainerState>>
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
                IAsyncEnumerator<TryCatch<CrossFeedRangePage<DocumentContainerPage, DocumentContainerState>>> enumerator = this.CreateEnumerator(inMemoryCollection);

                (HashSet<string> firstDrainResults, CrossFeedRangeState<DocumentContainerState> state) = await this.PartialDrainAsync(enumerator, numIterations: 3);

                IReadOnlyList<FeedRangeInternal> ranges = await inMemoryCollection.GetFeedRangesAsync(cancellationToken: default);

                // Split the partition we were reading from
                await inMemoryCollection.SplitAsync(ranges.First(), cancellationToken: default);

                // And a partition we have let to read from
                await inMemoryCollection.SplitAsync(ranges[ranges.Count / 2], cancellationToken: default);

                // Resume from state
                IAsyncEnumerable<TryCatch<CrossFeedRangePage<DocumentContainerPage, DocumentContainerState>>> enumerable = this.CreateEnumerable(inMemoryCollection, state);

                HashSet<string> secondDrainResults = await this.DrainFullyAsync(enumerable);
                Assert.AreEqual(numItems, firstDrainResults.Count + secondDrainResults.Count);
            }

            [TestMethod]
            public async Task TestSplitWithDuringDrainAsync()
            {
                int numItems = 1000;
                IDocumentContainer inMemoryCollection = await this.CreateDocumentContainerAsync(numItems);
                IAsyncEnumerable<TryCatch<CrossFeedRangePage<DocumentContainerPage, DocumentContainerState>>> enumerable = this.CreateEnumerable(inMemoryCollection);

                HashSet<string> identifiers = new HashSet<string>();
                Random random = new Random();
                await foreach (TryCatch<CrossFeedRangePage<DocumentContainerPage, DocumentContainerState>> tryGetPage in enumerable)
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

            public override IAsyncEnumerable<TryCatch<CrossFeedRangePage<DocumentContainerPage, DocumentContainerState>>> CreateEnumerable(
                IDocumentContainer inMemoryCollection,
                CrossFeedRangeState<DocumentContainerState> state = null)
            {
                PartitionRangePageAsyncEnumerator<DocumentContainerPage, DocumentContainerState> createEnumerator(
                    FeedRangeInternal range,
                    DocumentContainerState state) => new DocumentContainerPartitionRangeEnumerator(
                        inMemoryCollection,
                        feedRange: range,
                        pageSize: 10,
                        cancellationToken: default,
                        state: state);

                return new CrossPartitionRangePageAsyncEnumerable<DocumentContainerPage, DocumentContainerState>(
                    feedRangeProvider: inMemoryCollection,
                    createPartitionRangeEnumerator: createEnumerator,
                    comparer: PartitionRangePageAsyncEnumeratorComparer.Singleton,
                    maxConcurrency: 10,
                    state: state);
            }

            public override IAsyncEnumerator<TryCatch<CrossFeedRangePage<DocumentContainerPage, DocumentContainerState>>> CreateEnumerator(
                IDocumentContainer inMemoryCollection,
                CrossFeedRangeState<DocumentContainerState> state = null)
            {
                PartitionRangePageAsyncEnumerator<DocumentContainerPage, DocumentContainerState> createEnumerator(
                    FeedRangeInternal range,
                    DocumentContainerState state) => new DocumentContainerPartitionRangeEnumerator(
                        inMemoryCollection,
                        feedRange: range,
                        pageSize: 10,
                        cancellationToken: default,
                        state: state);

                CrossPartitionRangePageAsyncEnumerator<DocumentContainerPage, DocumentContainerState> enumerator = new CrossPartitionRangePageAsyncEnumerator<DocumentContainerPage, DocumentContainerState>(
                    feedRangeProvider: inMemoryCollection,
                    createPartitionRangeEnumerator: createEnumerator,
                    comparer: PartitionRangePageAsyncEnumeratorComparer.Singleton,
                    maxConcurrency: 10,
                    cancellationToken: default,
                    state: state);

                return enumerator;
            }

            public override IReadOnlyList<Record> GetRecordsFromPage(CrossFeedRangePage<DocumentContainerPage, DocumentContainerState> page)
            {
                return page.Page.Records;
            }

            private sealed class PartitionRangePageAsyncEnumeratorComparer : IComparer<PartitionRangePageAsyncEnumerator<DocumentContainerPage, DocumentContainerState>>
            {
                public static readonly PartitionRangePageAsyncEnumeratorComparer Singleton = new PartitionRangePageAsyncEnumeratorComparer();

                public int Compare(
                    PartitionRangePageAsyncEnumerator<DocumentContainerPage, DocumentContainerState> partitionRangePageEnumerator1,
                    PartitionRangePageAsyncEnumerator<DocumentContainerPage, DocumentContainerState> partitionRangePageEnumerator2)
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
