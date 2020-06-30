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

    [TestClass]
    public class SinglePartitionInMemoryCollectionPartitionRangeEnumeratorTests : InMemoryCollectionPartitionRangeEnumeratorTests
    {
        [TestMethod]
        public async Task TestSplitAsync()
        {
            int numItems = 100;
            InMemoryCollection inMemoryCollection = this.CreateInMemoryCollection(numItems);
            InMemoryCollectionPartitionRangeEnumerator enumerator = new InMemoryCollectionPartitionRangeEnumerator(
                inMemoryCollection,
                partitionKeyRangeId: 0,
                pageSize: 10);

            (HashSet<Guid> parentIdentifiers, State state) = await this.PartialDrainAsync(enumerator, numIterations: 3);

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
                HashSet<Guid> resourceIdentifiers = await this.DrainFullyAsync(enumerable1);

                childIdentifiers.UnionWith(resourceIdentifiers);
            }

            Assert.AreEqual(numItems, parentIdentifiers.Count + childIdentifiers.Count);
        }

        internal override List<InMemoryCollection.Record> GetRecordsFromPage(Page page)
        {
            if (!(page is InMemoryCollectionPartitionRangeEnumerator.InMemoryCollectionPage inMemoryCollectionPage))
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
            return new PartitionRangePageEnumerable(
                range: new FeedRangePartitionKeyRange("0"),
                state: state,
                (range, state) => new InMemoryCollectionPartitionRangeEnumerator(
                    inMemoryCollection,
                    partitionKeyRangeId: int.Parse(((FeedRangePartitionKeyRange)range).PartitionKeyRangeId),
                    pageSize: 10,
                    state: state));
        }

        internal override IAsyncEnumerator<TryCatch<Page>> CreateEnumerator(InMemoryCollection inMemoryCollection, State state = null)
        {
            return new InMemoryCollectionPartitionRangeEnumerator(
                inMemoryCollection,
                partitionKeyRangeId: 0,
                pageSize: 10,
                state: state);
        }
    }
}
