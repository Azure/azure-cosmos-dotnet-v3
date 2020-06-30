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
    public class CrossPartitionInMemoryCollectionPartitionRangeEnumeratorTests : InMemoryCollectionPartitionRangeEnumeratorTests
    {
        [TestMethod]
        public async Task TestSplitWithResumeContinuationAsync()
        {
            int numItems = 1000;
            InMemoryCollection inMemoryCollection = this.CreateInMemoryCollection(numItems);

            IFeedRangeProvider feedRangeProvider = new InMemoryCollectionFeedRangeProvider(inMemoryCollection);
            CreatePartitionRangePageEnumerator createEnumerator = (Cosmos.FeedRange range, State state) => new InMemoryCollectionPartitionRangeEnumerator(
                inMemoryCollection,
                partitionKeyRangeId: int.Parse(((FeedRangePartitionKeyRange)range).PartitionKeyRangeId),
                pageSize: 10,
                state: state);

            CrossPartitionRangePageEnumerator enumerator = new CrossPartitionRangePageEnumerator(
                feedRangeProvider: feedRangeProvider,
                createPartitionRangeEnumerator: createEnumerator,
                comparer: PartitionRangePageEnumeratorComparer.Singleton);

            (HashSet<Guid> firstDrainResults, State state) = await this.PartialDrainAsync(enumerator, numIterations: 3);

            int minPartitionKeyRangeId = inMemoryCollection.PartitionKeyRangeFeedReed().Keys.Min();
            int maxPartitionKeyRangeId = inMemoryCollection.PartitionKeyRangeFeedReed().Keys.Max();
            // Split the partition we were reading from
            inMemoryCollection.Split(minPartitionKeyRangeId);

            // And a partition we have let to read from
            inMemoryCollection.Split((minPartitionKeyRangeId + maxPartitionKeyRangeId) / 2);

            // Resume from state
            CrossPartitionRangePageEnumerable enumerable = new CrossPartitionRangePageEnumerable(
                feedRangeProvider: feedRangeProvider,
                createPartitionRangeEnumerator: createEnumerator,
                comparer: PartitionRangePageEnumeratorComparer.Singleton,
                state: state);

            HashSet<Guid> secondDrainResults = await this.DrainFullyAsync(enumerable);
            Assert.AreEqual(numItems, firstDrainResults.Count + secondDrainResults.Count);
        }

        [TestMethod]
        public async Task TestSplitWithDuringDrainAsync()
        {
            int numItems = 1000;
            InMemoryCollection inMemoryCollection = this.CreateInMemoryCollection(numItems);

            IFeedRangeProvider feedRangeProvider = new InMemoryCollectionFeedRangeProvider(inMemoryCollection);
            CreatePartitionRangePageEnumerator createEnumerator = (Cosmos.FeedRange range, State state) => new InMemoryCollectionPartitionRangeEnumerator(
                inMemoryCollection,
                partitionKeyRangeId: int.Parse(((FeedRangePartitionKeyRange)range).PartitionKeyRangeId),
                pageSize: 10,
                state: state);

            CrossPartitionRangePageEnumerable enumerable = new CrossPartitionRangePageEnumerable(
                feedRangeProvider: feedRangeProvider,
                createPartitionRangeEnumerator: createEnumerator,
                comparer: PartitionRangePageEnumeratorComparer.Singleton);

            HashSet<Guid> identifiers = new HashSet<Guid>();
            Random random = new Random();
            await foreach (TryCatch<Page> tryGetPage in enumerable)
            {
                if (random.Next() % 2 == 0)
                {
                    List<int> partitionKeyRangeIds = inMemoryCollection.PartitionKeyRangeFeedReed().Keys.ToList();
                    int randomIdToSplit = partitionKeyRangeIds[random.Next(0, partitionKeyRangeIds.Count)];
                    inMemoryCollection.Split(randomIdToSplit);
                }

                tryGetPage.ThrowIfFailed();

                if (!(tryGetPage.Result is CrossPartitionRangePageEnumerator.CrossPartitionPage crossPartitionPage))
                {
                    throw new InvalidCastException();
                }

                if (!(crossPartitionPage.Page is InMemoryCollectionPartitionRangeEnumerator.InMemoryCollectionPage page))
                {
                    throw new InvalidCastException();
                }

                foreach (InMemoryCollection.Record record in page.Records)
                {
                    identifiers.Add(record.Identifier);
                }
            }

            Assert.AreEqual(numItems, identifiers.Count);
        }

        internal override List<InMemoryCollection.Record> GetRecordsFromPage(Page page)
        {
            if (!(page is CrossPartitionRangePageEnumerator.CrossPartitionPage crossPartitionPage))
            {
                throw new InvalidCastException();
            }

            if (!(crossPartitionPage.Page is InMemoryCollectionPartitionRangeEnumerator.InMemoryCollectionPage inMemoryCollectionPage))
            {
                throw new InvalidCastException();
            }

            return inMemoryCollectionPage.Records;
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

        internal override IAsyncEnumerable<TryCatch<Page>> CreateEnumerable(InMemoryCollection inMemoryCollection, State state = null)
        {
            IFeedRangeProvider feedRangeProvider = new InMemoryCollectionFeedRangeProvider(inMemoryCollection);
            CreatePartitionRangePageEnumerator createEnumerator = (Cosmos.FeedRange range, State state) => new InMemoryCollectionPartitionRangeEnumerator(
                inMemoryCollection,
                partitionKeyRangeId: int.Parse(((FeedRangePartitionKeyRange)range).PartitionKeyRangeId),
                pageSize: 10,
                state: state);

            return new CrossPartitionRangePageEnumerable(
                feedRangeProvider: feedRangeProvider,
                createPartitionRangeEnumerator: createEnumerator,
                comparer: PartitionRangePageEnumeratorComparer.Singleton,
                state: state);
        }

        internal override IAsyncEnumerator<TryCatch<Page>> CreateEnumerator(InMemoryCollection inMemoryCollection, State state = null)
        {
            IFeedRangeProvider feedRangeProvider = new InMemoryCollectionFeedRangeProvider(inMemoryCollection);
            CreatePartitionRangePageEnumerator createEnumerator = (Cosmos.FeedRange range, State state) => new InMemoryCollectionPartitionRangeEnumerator(
                inMemoryCollection,
                partitionKeyRangeId: int.Parse(((FeedRangePartitionKeyRange)range).PartitionKeyRangeId),
                pageSize: 10,
                state: state);

            CrossPartitionRangePageEnumerator enumerator = new CrossPartitionRangePageEnumerator(
                feedRangeProvider: feedRangeProvider,
                createPartitionRangeEnumerator: createEnumerator,
                comparer: PartitionRangePageEnumeratorComparer.Singleton,
                state: state);

            return enumerator;
        }

        private sealed class PartitionRangePageEnumeratorComparer : IComparer<PartitionRangePageEnumerator>
        {
            public static readonly PartitionRangePageEnumeratorComparer Singleton = new PartitionRangePageEnumeratorComparer();

            public int Compare(PartitionRangePageEnumerator partitionRangePageEnumerator1, PartitionRangePageEnumerator partitionRangePageEnumerator2)
            {
                if (object.ReferenceEquals(partitionRangePageEnumerator1, partitionRangePageEnumerator2))
                {
                    return 0;
                }

                if (partitionRangePageEnumerator1.HasMoreResults && !partitionRangePageEnumerator2.HasMoreResults)
                {
                    return -1;
                }

                if (!partitionRangePageEnumerator1.HasMoreResults && partitionRangePageEnumerator2.HasMoreResults)
                {
                    return 1;
                }

                // Either both don't have results or both do.
                return string.CompareOrdinal(
                    ((FeedRangePartitionKeyRange)partitionRangePageEnumerator1.Range).PartitionKeyRangeId,
                    ((FeedRangePartitionKeyRange)partitionRangePageEnumerator2.Range).PartitionKeyRangeId);
            }
        }
    }
}
