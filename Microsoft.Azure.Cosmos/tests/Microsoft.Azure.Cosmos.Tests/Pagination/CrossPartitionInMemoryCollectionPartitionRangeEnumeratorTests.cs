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
    public class CrossPartitionInMemoryCollectionPartitionRangeEnumeratorTests
    {
        [TestMethod]
        public async Task TestDrainFullyAsync()
        {
            int numItems = 1000;
            InMemoryCollection inMemoryCollection = CreateInMemoryCollection(numItems);

            CrossPartitionRangePageEnumerable enumerable = new CrossPartitionRangePageEnumerable(
                feedRangeProvider: new InMemoryCollectionFeedRangeProvider(inMemoryCollection),
                createPartitionRangeEnumerator: (range, state) => new InMemoryCollectionPartitionRangeEnumerator(
                    inMemoryCollection,
                    partitionKeyRangeId: int.Parse(((FeedRangePartitionKeyRange)range).PartitionKeyRangeId),
                    pageSize: 10,
                    state: state),
                comparer: PartitionRangePageEnumeratorComparer.Singleton);

            HashSet<Guid> identifiers = await DrainFullyAsync(enumerable);
            Assert.AreEqual(numItems, identifiers.Count);
        }

        [TestMethod]
        public async Task TestResumingFromStateAsync()
        {
            int numItems = 1000;
            InMemoryCollection inMemoryCollection = CreateInMemoryCollection(numItems);

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

            (HashSet<Guid> firstDrainResults, State state) = await PartialDrainAsync(enumerator, numIterations: 3);

            // Resume from state
            CrossPartitionRangePageEnumerable enumerable = new CrossPartitionRangePageEnumerable(
                feedRangeProvider: feedRangeProvider,
                createPartitionRangeEnumerator: createEnumerator,
                comparer: PartitionRangePageEnumeratorComparer.Singleton,
                state: state);

            HashSet<Guid> secondDrainResults = await DrainFullyAsync(enumerable);
            Assert.AreEqual(numItems, firstDrainResults.Count + secondDrainResults.Count);
        }

        [TestMethod]
        public async Task TestSplitWithResumeContinuationAsync()
        {
            int numItems = 1000;
            InMemoryCollection inMemoryCollection = CreateInMemoryCollection(numItems);

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

            (HashSet<Guid> firstDrainResults, State state) = await PartialDrainAsync(enumerator, numIterations: 3);

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

            HashSet<Guid> secondDrainResults = await DrainFullyAsync(enumerable);
            Assert.AreEqual(numItems, firstDrainResults.Count + secondDrainResults.Count);
        }

        [TestMethod]
        public async Task TestSplitWithDuringDrainAsync()
        {
            int numItems = 1000;
            InMemoryCollection inMemoryCollection = CreateInMemoryCollection(numItems);

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

        [TestMethod]
        public async Task Test429sAsync()
        {
            int numItems = 100;
            InMemoryCollection inMemoryCollection = CreateInMemoryCollection(numItems, new InMemoryCollection.FailureConfigs(inject429s: true));
            CrossPartitionRangePageEnumerable enumerable = new CrossPartitionRangePageEnumerable(
                feedRangeProvider: new InMemoryCollectionFeedRangeProvider(inMemoryCollection),
                createPartitionRangeEnumerator: (range, state) => new InMemoryCollectionPartitionRangeEnumerator(
                    inMemoryCollection,
                    partitionKeyRangeId: int.Parse(((FeedRangePartitionKeyRange)range).PartitionKeyRangeId),
                    pageSize: 10,
                    state: state),
                comparer: PartitionRangePageEnumeratorComparer.Singleton);

            HashSet<Guid> identifiers = new HashSet<Guid>();
            await foreach (TryCatch<Page> tryGetPage in enumerable)
            {
                if (tryGetPage.Failed)
                {
                    Exception exception = tryGetPage.Exception;
                    while (exception.InnerException != null)
                    {
                        exception = exception.InnerException;
                    }

                    if (!((exception is CosmosException cosmosException) && (cosmosException.StatusCode == (System.Net.HttpStatusCode)429)))
                    {
                        throw tryGetPage.Exception;
                    }
                }
                else
                {
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
            }

            Assert.AreEqual(numItems, identifiers.Count);
        }

        [TestMethod]
        public async Task Test429sWithContinuationsAsync()
        {
            int numItems = 100;
            InMemoryCollection inMemoryCollection = CreateInMemoryCollection(numItems, new InMemoryCollection.FailureConfigs(inject429s: true));

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

            HashSet<Guid> identifiers = new HashSet<Guid>();
            State state = default;

            while (await enumerator.MoveNextAsync())
            {
                TryCatch<Page> tryGetPage = enumerator.Current;
                if (tryGetPage.Failed)
                {
                    Exception exception = tryGetPage.Exception;
                    while (exception.InnerException != null)
                    {
                        exception = exception.InnerException;
                    }

                    if (!((exception is CosmosException cosmosException) && (cosmosException.StatusCode == (System.Net.HttpStatusCode)429)))
                    {
                        throw tryGetPage.Exception;
                    }

                    // Create a new enumerator from that state to simulate when the user want's to start resume later from a continuation token.
                    enumerator = new CrossPartitionRangePageEnumerator(
                        feedRangeProvider: feedRangeProvider,
                        createPartitionRangeEnumerator: createEnumerator,
                        comparer: PartitionRangePageEnumeratorComparer.Singleton,
                        state: state);
                }
                else
                {
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

                    state = tryGetPage.Result.State;
                }
            }

            Assert.AreEqual(numItems, identifiers.Count);
        }

        private static InMemoryCollection CreateInMemoryCollection(int numItems, InMemoryCollection.FailureConfigs failureConfigs = default)
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

        private static async Task<HashSet<Guid>> DrainFullyAsync(CrossPartitionRangePageEnumerable enumerable)
        {
            HashSet<Guid> identifiers = new HashSet<Guid>();
            await foreach (TryCatch<Page> tryGetPage in enumerable)
            {
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

            return identifiers;
        }

        private static async Task<(HashSet<Guid>, State)> PartialDrainAsync(
            CrossPartitionRangePageEnumerator enumerator,
            int numIterations)
        {
            HashSet<Guid> identifiers = new HashSet<Guid>();
            State state = default;

            // Drain a couple of iterations
            for (int i = 0; i < numIterations; i++)
            {
                await enumerator.MoveNextAsync();

                TryCatch<Page> tryGetPage = enumerator.Current;
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

                state = tryGetPage.Result.State;
            }

            return (identifiers, state);
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
