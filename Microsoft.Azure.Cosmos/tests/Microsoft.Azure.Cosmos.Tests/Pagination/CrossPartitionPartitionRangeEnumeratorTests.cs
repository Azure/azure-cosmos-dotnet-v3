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

        private sealed class Implementation : PartitionRangeEnumeratorTests<CrossPartitionPage<DocumentContainerPage, DocumentContainerState>, CrossPartitionState<DocumentContainerState>>
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
                IAsyncEnumerator<TryCatch<CrossPartitionPage<DocumentContainerPage, DocumentContainerState>>> enumerator = this.CreateEnumerator(inMemoryCollection);

                (HashSet<string> firstDrainResults, CrossPartitionState<DocumentContainerState> state) = await this.PartialDrainAsync(enumerator, numIterations: 3);

                int minPartitionKeyRangeId = (await inMemoryCollection.GetFeedRangesAsync(cancellationToken: default))
                    .Select(range => int.Parse(range.Id)).Min();
                int maxPartitionKeyRangeId = (await inMemoryCollection.GetFeedRangesAsync(cancellationToken: default))
                    .Select(range => int.Parse(range.Id)).Max();
                // Split the partition we were reading from
                await inMemoryCollection.SplitAsync(minPartitionKeyRangeId, cancellationToken: default);

                // And a partition we have let to read from
                await inMemoryCollection.SplitAsync((minPartitionKeyRangeId + maxPartitionKeyRangeId) / 2, cancellationToken: default);

                // Resume from state
                IAsyncEnumerable<TryCatch<CrossPartitionPage<DocumentContainerPage, DocumentContainerState>>> enumerable = this.CreateEnumerable(inMemoryCollection, state);

                HashSet<string> secondDrainResults = await this.DrainFullyAsync(enumerable);
                Assert.AreEqual(numItems, firstDrainResults.Count + secondDrainResults.Count);
            }

            [TestMethod]
            public async Task TestSplitWithDuringDrainAsync()
            {
                int numItems = 1000;
                IDocumentContainer inMemoryCollection = await this.CreateDocumentContainerAsync(numItems);
                IAsyncEnumerable<TryCatch<CrossPartitionPage<DocumentContainerPage, DocumentContainerState>>> enumerable = this.CreateEnumerable(inMemoryCollection);

                HashSet<string> identifiers = new HashSet<string>();
                Random random = new Random();
                await foreach (TryCatch<CrossPartitionPage<DocumentContainerPage, DocumentContainerState>> tryGetPage in enumerable)
                {
                    if (random.Next() % 2 == 0)
                    {
                        List<int> partitionKeyRangeIds = (await inMemoryCollection.GetFeedRangesAsync(cancellationToken: default))
                            .Select(range => int.Parse(range.Id))
                            .ToList();
                        int randomIdToSplit = partitionKeyRangeIds[random.Next(0, partitionKeyRangeIds.Count)];
                        await inMemoryCollection.SplitAsync(randomIdToSplit, cancellationToken: default);
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

            public override IAsyncEnumerable<TryCatch<CrossPartitionPage<DocumentContainerPage, DocumentContainerState>>> CreateEnumerable(
                IDocumentContainer inMemoryCollection,
                CrossPartitionState<DocumentContainerState> state = null)
            {
                PartitionRangePageAsyncEnumerator<DocumentContainerPage, DocumentContainerState> createEnumerator(
                    PartitionKeyRange range,
                    DocumentContainerState state) => new DocumentContainerPartitionRangeEnumerator(
                        inMemoryCollection,
                        partitionKeyRangeId: int.Parse(range.Id),
                        pageSize: 10,
                        state: state);

                return new CrossPartitionRangePageAsyncEnumerable<DocumentContainerPage, DocumentContainerState>(
                    feedRangeProvider: inMemoryCollection,
                    createPartitionRangeEnumerator: createEnumerator,
                    comparer: PartitionRangePageAsyncEnumeratorComparer.Singleton,
                    state: state);
            }

            public override IAsyncEnumerator<TryCatch<CrossPartitionPage<DocumentContainerPage, DocumentContainerState>>> CreateEnumerator(
                IDocumentContainer inMemoryCollection,
                CrossPartitionState<DocumentContainerState> state = null)
            {
                PartitionRangePageAsyncEnumerator<DocumentContainerPage, DocumentContainerState> createEnumerator(
                    PartitionKeyRange range,
                    DocumentContainerState state) => new DocumentContainerPartitionRangeEnumerator(
                        inMemoryCollection,
                        partitionKeyRangeId: int.Parse(range.Id),
                        pageSize: 10,
                        state: state);

                CrossPartitionRangePageAsyncEnumerator<DocumentContainerPage, DocumentContainerState> enumerator = new CrossPartitionRangePageAsyncEnumerator<DocumentContainerPage, DocumentContainerState>(
                    feedRangeProvider: inMemoryCollection,
                    createPartitionRangeEnumerator: createEnumerator,
                    comparer: PartitionRangePageAsyncEnumeratorComparer.Singleton,
                    state: state);

                return enumerator;
            }

            public override IReadOnlyList<Record> GetRecordsFromPage(CrossPartitionPage<DocumentContainerPage, DocumentContainerState> page)
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
                        partitionRangePageEnumerator1.Range.MinInclusive,
                        partitionRangePageEnumerator2.Range.MinInclusive);
                }
            }
        }
    }
}
