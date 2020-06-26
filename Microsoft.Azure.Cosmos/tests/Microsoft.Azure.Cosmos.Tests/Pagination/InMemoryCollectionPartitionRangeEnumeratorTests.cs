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

            HashSet<long> resourceIdentifiers = await GetResourceIdentifiersAsync(enumerable);
            Assert.AreEqual(numItems, resourceIdentifiers.Count);
        }

        [TestMethod]
        public async Task TestResumingFromState()
        {
            int numItems = 100;
            InMemoryCollection inMemoryCollection = CreateInMemoryCollection(numItems);
            InMemoryCollectionPartitionRangeEnumerator enumerator = new InMemoryCollectionPartitionRangeEnumerator(
                inMemoryCollection,
                partitionKeyRangeId: 0,
                pageSize: 10);

            List<InMemoryCollection.Record> records = new List<InMemoryCollection.Record>();
            State state = default;

            // Drain a couple of iterations
            for (int i = 0; i < 3; i++)
            {
                await enumerator.MoveNextAsync();

                TryCatch<Page> tryGetPage = enumerator.Current;
                tryGetPage.ThrowIfFailed();

                if (!(tryGetPage.Result is InMemoryCollectionPartitionRangeEnumerator.InMemoryCollectionPage page))
                {
                    throw new InvalidCastException();
                }

                records.AddRange(page.Records);
                state = enumerator.State;
            }

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
            HashSet<long> resourceIdentifiers = await GetResourceIdentifiersAsync(enumerable);

            Assert.AreEqual(numItems, records.Count + resourceIdentifiers.Count);
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

            List<InMemoryCollection.Record> records = new List<InMemoryCollection.Record>();
            State state = default;

            // Drain a couple of iterations
            for (int i = 0; i < 3; i++)
            {
                await enumerator.MoveNextAsync();

                TryCatch<Page> tryGetPage = enumerator.Current;
                tryGetPage.ThrowIfFailed();

                if (!(tryGetPage.Result is InMemoryCollectionPartitionRangeEnumerator.InMemoryCollectionPage page))
                {
                    throw new InvalidCastException();
                }

                records.AddRange(page.Records);
                state = enumerator.State;
            }

            inMemoryCollection.Split(partitionKeyRangeId: 0);

            // Try To read from the partition that is gone.
            await enumerator.MoveNextAsync();
            Assert.IsTrue(enumerator.Current.Failed);

            // Resume on the children using the parent continuaiton token
            PartitionRangePageEnumerable enumerable1 = new PartitionRangePageEnumerable(
                range: new FeedRangePartitionKeyRange("1"),
                state: state,
                (range, state) => new InMemoryCollectionPartitionRangeEnumerator(
                    inMemoryCollection,
                    partitionKeyRangeId: int.Parse(((FeedRangePartitionKeyRange)range).PartitionKeyRangeId),
                    pageSize: 10,
                    state: state));
            HashSet<long> resourceIdentifiers1 = await GetResourceIdentifiersAsync(enumerable1);

            PartitionRangePageEnumerable enumerable2 = new PartitionRangePageEnumerable(
                range: new FeedRangePartitionKeyRange("2"),
                state: state,
                (range, state) => new InMemoryCollectionPartitionRangeEnumerator(
                    inMemoryCollection,
                    partitionKeyRangeId: int.Parse(((FeedRangePartitionKeyRange)range).PartitionKeyRangeId),
                    pageSize: 10,
                    state: state));
            HashSet<long> resourceIdentifiers2 = await GetResourceIdentifiersAsync(enumerable2);

            List<long> commonAmongChildren = resourceIdentifiers1.Intersect(resourceIdentifiers2).ToList();
            Assert.AreEqual(0, commonAmongChildren.Count);

            Assert.AreEqual(numItems, records.Count + resourceIdentifiers1.Count + resourceIdentifiers2.Count);
        }

        private static InMemoryCollection CreateInMemoryCollection(int numItems)
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

            InMemoryCollection inMemoryCollection = new InMemoryCollection(partitionKeyDefinition);

            for (int i = 0; i < numItems; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                inMemoryCollection.CreateItem(item);
            }

            return inMemoryCollection;
        }

        private static async Task<HashSet<long>> GetResourceIdentifiersAsync(
            PartitionRangePageEnumerable enumerable,
            int? numIterations = default)
        {
            int iterationNumber = 0;
            HashSet<long> resourceIdentifiers = new HashSet<long>();
            await foreach (TryCatch<Page> tryGetPage in enumerable)
            {
                if (numIterations.HasValue && iterationNumber >= numIterations)
                {
                    break;
                }

                tryGetPage.ThrowIfFailed();

                if (!(tryGetPage.Result is InMemoryCollectionPartitionRangeEnumerator.InMemoryCollectionPage page))
                {
                    throw new InvalidCastException();
                }

                foreach (InMemoryCollection.Record record in page.Records)
                {
                    resourceIdentifiers.Add(record.ResourceIdentifier);
                }

                iterationNumber++;
            }

            return resourceIdentifiers;
        }
    }
}
