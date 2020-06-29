//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using System.Linq;
    using System.Security.Policy;

    [TestClass]
    public class InMemoryCollectionPartitionRangeEnumeratorTests
    {
        [TestMethod]
        public async Task TestDrainFullyAsync()
        {
            int numItems = 100;
            InMemoryCollection inMemoryCollection = CreateInMemoryCollection(numItems);
            PartitionRangePageEnumerable enumerable = new PartitionRangePageEnumerable(
                range: new FeedRangePartitionKeyRange("0"),
                state: default,
                (range, state) => new InMemoryCollectionPartitionRangeEnumerator(
                    inMemoryCollection,
                    partitionKeyRangeId: int.Parse(((FeedRangePartitionKeyRange)range).PartitionKeyRangeId),
                    pageSize: 10,
                    state: state));

            HashSet<Guid> identifiers = await DrainFullyAsync(enumerable);
            Assert.AreEqual(numItems, identifiers.Count);
        }

        [TestMethod]
        public async Task TestResumingFromStateAsync()
        {
            int numItems = 100;
            InMemoryCollection inMemoryCollection = CreateInMemoryCollection(numItems);
            InMemoryCollectionPartitionRangeEnumerator enumerator = new InMemoryCollectionPartitionRangeEnumerator(
                inMemoryCollection,
                partitionKeyRangeId: 0,
                pageSize: 10);

            (HashSet<Guid> firstDrainResults, State state) = await PartialDrainAsync(enumerator, numIterations: 3);

            // Resume from state
            enumerator = new InMemoryCollectionPartitionRangeEnumerator(
                inMemoryCollection,
                partitionKeyRangeId: 0,
                pageSize: 10,
                state: state);

            PartitionRangePageEnumerable enumerable = new PartitionRangePageEnumerable(
                range: new FeedRangePartitionKeyRange("0"),
                state: state,
                (range, state) => new InMemoryCollectionPartitionRangeEnumerator(
                    inMemoryCollection,
                    partitionKeyRangeId: int.Parse(((FeedRangePartitionKeyRange)range).PartitionKeyRangeId),
                    pageSize: 10,
                    state: state));
            HashSet<Guid> secondDrainResults = await DrainFullyAsync(enumerable);

            Assert.AreEqual(numItems, firstDrainResults.Count + secondDrainResults.Count);
        }

        [TestMethod]
        public async Task TestSplitAsync()
        {
            int numItems = 100;
            InMemoryCollection inMemoryCollection = CreateInMemoryCollection(numItems);
            InMemoryCollectionPartitionRangeEnumerator enumerator = new InMemoryCollectionPartitionRangeEnumerator(
                inMemoryCollection,
                partitionKeyRangeId: 0,
                pageSize: 10);

            (HashSet<Guid> parentIdentifiers, State state) = await PartialDrainAsync(enumerator, numIterations: 3);

            // Split the partition
            inMemoryCollection.Split(partitionKeyRangeId: 0);

            // Try To read from the partition that is gone.
            await enumerator.MoveNextAsync();
            Assert.IsTrue(enumerator.Current.Failed);

            // Resume on the children using the parent continuaiton token
            HashSet<Guid> childIdentifiers = new HashSet<Guid>();
            foreach (int partitionKeyRangeId in new int[] { 1, 2 })
            {
                PartitionRangePageEnumerable enumerable1 = new PartitionRangePageEnumerable(
                range: new FeedRangePartitionKeyRange(partitionKeyRangeId.ToString()),
                state: state,
                (range, state) => new InMemoryCollectionPartitionRangeEnumerator(
                    inMemoryCollection,
                    partitionKeyRangeId: int.Parse(((FeedRangePartitionKeyRange)range).PartitionKeyRangeId),
                    pageSize: 10,
                    state: state));
                HashSet<Guid> resourceIdentifiers = await DrainFullyAsync(enumerable1);

                childIdentifiers.UnionWith(resourceIdentifiers);
            }

            Assert.AreEqual(numItems, parentIdentifiers.Count + childIdentifiers.Count);
        }

        [TestMethod]
        public async Task Test429sAsync()
        {
            int numItems = 100;
            InMemoryCollection inMemoryCollection = CreateInMemoryCollection(numItems, new InMemoryCollection.FailureConfigs(inject429s: true));
            PartitionRangePageEnumerable enumerable = new PartitionRangePageEnumerable(
                range: new FeedRangePartitionKeyRange("0"),
                state: default,
                (range, state) => new InMemoryCollectionPartitionRangeEnumerator(
                    inMemoryCollection,
                    partitionKeyRangeId: int.Parse(((FeedRangePartitionKeyRange)range).PartitionKeyRangeId),
                    pageSize: 10,
                    state: state));

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
                    if (!(tryGetPage.Result is InMemoryCollectionPartitionRangeEnumerator.InMemoryCollectionPage page))
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

            HashSet<Guid> identifiers = new HashSet<Guid>();
            State state = default;

            InMemoryCollectionPartitionRangeEnumerator enumerator = new InMemoryCollectionPartitionRangeEnumerator(
                inMemoryCollection,
                partitionKeyRangeId: 0,
                pageSize: 10);

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
                    enumerator = new InMemoryCollectionPartitionRangeEnumerator(
                        inMemoryCollection,
                        partitionKeyRangeId: 0,
                        pageSize: 10,
                        state: state);
                }
                else
                {
                    if (!(tryGetPage.Result is InMemoryCollectionPartitionRangeEnumerator.InMemoryCollectionPage page))
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

            for (int i = 0; i < numItems; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                inMemoryCollection.CreateItem(item);
            }

            return inMemoryCollection;
        }

        private static async Task<(HashSet<Guid>, State)> PartialDrainAsync(
            PartitionRangePageEnumerator enumerator,
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

                if (!(tryGetPage.Result is InMemoryCollectionPartitionRangeEnumerator.InMemoryCollectionPage page))
                {
                    throw new InvalidCastException();
                }

                foreach (InMemoryCollection.Record record in page.Records)
                {
                    identifiers.Add(record.Identifier);
                }

                state = enumerator.State;
            }

            return (identifiers, state);
        }

        private static async Task<HashSet<Guid>> DrainFullyAsync(PartitionRangePageEnumerable enumerable)
        {
            HashSet<Guid> identifiers = new HashSet<Guid>();
            await foreach (TryCatch<Page> tryGetPage in enumerable)
            {
                tryGetPage.ThrowIfFailed();

                if (!(tryGetPage.Result is InMemoryCollectionPartitionRangeEnumerator.InMemoryCollectionPage page))
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
    }
}
