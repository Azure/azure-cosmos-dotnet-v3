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
    public sealed class CrossPartitionInMemoryCollectionPartitionRangeEnumeratorTests 
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

        private sealed class Implementation : InMemoryCollectionPartitionRangeEnumeratorTests<CrossPartitionPage<InMemoryCollectionPage, InMemoryCollectionState>, CrossPartitionState<InMemoryCollectionState>>
        {
            [TestMethod]
            public async Task TestSplitWithResumeContinuationAsync()
            {
                int numItems = 1000;
                InMemoryCollection inMemoryCollection = this.CreateInMemoryCollection(numItems);
                IAsyncEnumerator<TryCatch<CrossPartitionPage<InMemoryCollectionPage, InMemoryCollectionState>>> enumerator = this.CreateEnumerator(inMemoryCollection);

                (HashSet<Guid> firstDrainResults, CrossPartitionState<InMemoryCollectionState> state) = await this.PartialDrainAsync(enumerator, numIterations: 3);

                int minPartitionKeyRangeId = inMemoryCollection.PartitionKeyRangeFeedReed().Keys.Min();
                int maxPartitionKeyRangeId = inMemoryCollection.PartitionKeyRangeFeedReed().Keys.Max();
                // Split the partition we were reading from
                inMemoryCollection.Split(minPartitionKeyRangeId);

                // And a partition we have let to read from
                inMemoryCollection.Split((minPartitionKeyRangeId + maxPartitionKeyRangeId) / 2);

                // Resume from state
                IAsyncEnumerable<TryCatch<CrossPartitionPage<InMemoryCollectionPage, InMemoryCollectionState>>> enumerable = this.CreateEnumerable(inMemoryCollection, state);

                HashSet<Guid> secondDrainResults = await this.DrainFullyAsync(enumerable);
                Assert.AreEqual(numItems, firstDrainResults.Count + secondDrainResults.Count);
            }

            [TestMethod]
            public async Task TestSplitWithDuringDrainAsync()
            {
                int numItems = 1000;
                InMemoryCollection inMemoryCollection = this.CreateInMemoryCollection(numItems);
                IAsyncEnumerable<TryCatch<CrossPartitionPage<InMemoryCollectionPage, InMemoryCollectionState>>> enumerable = this.CreateEnumerable(inMemoryCollection);

                HashSet<Guid> identifiers = new HashSet<Guid>();
                Random random = new Random();
                await foreach (TryCatch<CrossPartitionPage<InMemoryCollectionPage, InMemoryCollectionState>> tryGetPage in enumerable)
                {
                    if (random.Next() % 2 == 0)
                    {
                        List<int> partitionKeyRangeIds = inMemoryCollection.PartitionKeyRangeFeedReed().Keys.ToList();
                        int randomIdToSplit = partitionKeyRangeIds[random.Next(0, partitionKeyRangeIds.Count)];
                        inMemoryCollection.Split(randomIdToSplit);
                    }

                    tryGetPage.ThrowIfFailed();

                    List<InMemoryCollection.Record> records = this.GetRecordsFromPage(tryGetPage.Result);
                    foreach (InMemoryCollection.Record record in records)
                    {
                        identifiers.Add(record.Identifier);
                    }
                }

                Assert.AreEqual(numItems, identifiers.Count);
            }

            internal override InMemoryCollection CreateInMemoryCollection(int numItems, InMemoryCollection.FailureConfigs failureConfigs = null)
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

                InMemoryCollection inMemoryCollection = new InMemoryCollection(partitionKeyDefinition, failureConfigs);

                inMemoryCollection.Split(partitionKeyRangeId: 0);

                inMemoryCollection.Split(partitionKeyRangeId: 1);
                inMemoryCollection.Split(partitionKeyRangeId: 2);

                inMemoryCollection.Split(partitionKeyRangeId: 3);
                inMemoryCollection.Split(partitionKeyRangeId: 4);
                inMemoryCollection.Split(partitionKeyRangeId: 5);
                inMemoryCollection.Split(partitionKeyRangeId: 6);

                for (int i = 0; i < numItems; i++)
                {
                    // Insert an item
                    CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                    inMemoryCollection.CreateItem(item);
                }

                return inMemoryCollection;
            }

            internal override IAsyncEnumerable<TryCatch<CrossPartitionPage<InMemoryCollectionPage, InMemoryCollectionState>>> CreateEnumerable(
                InMemoryCollection inMemoryCollection,
                CrossPartitionState<InMemoryCollectionState> state = null)
            {
                IFeedRangeProvider feedRangeProvider = new InMemoryCollectionFeedRangeProvider(inMemoryCollection);
                PartitionRangePageEnumerator<InMemoryCollectionPage, InMemoryCollectionState> createEnumerator(Cosmos.FeedRange range, InMemoryCollectionState state) => new InMemoryCollectionPartitionRangeEnumerator(
                    inMemoryCollection,
                    partitionKeyRangeId: int.Parse(feedRangeProvider.ToPhysicalPartitionKeyRangeAsync((FeedRangeInternal)range, cancellationToken: default).Result.PartitionKeyRangeId),
                    pageSize: 10,
                    state: state);

                return new CrossPartitionRangePageEnumerable<InMemoryCollectionPage, InMemoryCollectionState>(
                    feedRangeProvider: feedRangeProvider,
                    createPartitionRangeEnumerator: createEnumerator,
                    comparer: PartitionRangePageEnumeratorComparer.Singleton,
                    state: state);
            }

            internal override IAsyncEnumerator<TryCatch<CrossPartitionPage<InMemoryCollectionPage, InMemoryCollectionState>>> CreateEnumerator(
                InMemoryCollection inMemoryCollection,
                CrossPartitionState<InMemoryCollectionState> state = null)
            {
                IFeedRangeProvider feedRangeProvider = new InMemoryCollectionFeedRangeProvider(inMemoryCollection);
                PartitionRangePageEnumerator<InMemoryCollectionPage, InMemoryCollectionState> createEnumerator(Cosmos.FeedRange range, InMemoryCollectionState state) => new InMemoryCollectionPartitionRangeEnumerator(
                    inMemoryCollection,
                    partitionKeyRangeId: int.Parse(feedRangeProvider.ToPhysicalPartitionKeyRangeAsync((FeedRangeInternal)range, cancellationToken: default).Result.PartitionKeyRangeId),
                    pageSize: 10,
                    state: state);

                CrossPartitionRangePageEnumerator<InMemoryCollectionPage, InMemoryCollectionState> enumerator = new CrossPartitionRangePageEnumerator<InMemoryCollectionPage, InMemoryCollectionState>(
                    feedRangeProvider: feedRangeProvider,
                    createPartitionRangeEnumerator: createEnumerator,
                    comparer: PartitionRangePageEnumeratorComparer.Singleton,
                    state: state);

                return enumerator;
            }

            internal override List<InMemoryCollection.Record> GetRecordsFromPage(CrossPartitionPage<InMemoryCollectionPage, InMemoryCollectionState> page)
            {
                return page.Page.Records;
            }

            private sealed class PartitionRangePageEnumeratorComparer : IComparer<PartitionRangePageEnumerator<InMemoryCollectionPage, InMemoryCollectionState>>
            {
                public static readonly PartitionRangePageEnumeratorComparer Singleton = new PartitionRangePageEnumeratorComparer();

                public int Compare(
                    PartitionRangePageEnumerator<InMemoryCollectionPage, InMemoryCollectionState> partitionRangePageEnumerator1,
                    PartitionRangePageEnumerator<InMemoryCollectionPage, InMemoryCollectionState> partitionRangePageEnumerator2)
                {
                    if (object.ReferenceEquals(partitionRangePageEnumerator1, partitionRangePageEnumerator2))
                    {
                        return 0;
                    }

                    // Either both don't have results or both do.
                    return string.CompareOrdinal(
                        ((FeedRangePartitionKeyRange)partitionRangePageEnumerator1.Range).PartitionKeyRangeId,
                        ((FeedRangePartitionKeyRange)partitionRangePageEnumerator2.Range).PartitionKeyRangeId);
                }
            }
        }
    }
}
