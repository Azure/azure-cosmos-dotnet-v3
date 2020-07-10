namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Remote;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class QueryPartitionRangePageEnumeratorTests
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
        public async Task TestSplitAsync()
        {
            Implementation implementation = new Implementation();
            await implementation.TestSplitAsync();
        }

        [TestClass]
        private sealed class Implementation : InMemoryCollectionPartitionRangeEnumeratorTests<QueryPage, QueryState>
        {
            [TestMethod]
            public async Task TestSplitAsync()
            {
                int numItems = 100;
                InMemoryCollection inMemoryCollection = this.CreateInMemoryCollection(numItems);
                IAsyncEnumerator<TryCatch<QueryPage>> enumerator = this.CreateEnumerator(inMemoryCollection);

                (HashSet<Guid> parentIdentifiers, QueryState state) = await this.PartialDrainAsync(enumerator, numIterations: 3);

                // Split the partition
                inMemoryCollection.Split(partitionKeyRangeId: 0);

                // Try To read from the partition that is gone.
                await enumerator.MoveNextAsync();
                Assert.IsTrue(enumerator.Current.Failed);

                // Resume on the children using the parent continuaiton token
                HashSet<Guid> childIdentifiers = new HashSet<Guid>();
                foreach (int partitionKeyRangeId in new int[] { 1, 2 })
                {
                    IAsyncEnumerable<TryCatch<QueryPage>> enumerable = new PartitionRangePageEnumerable<QueryPage, QueryState>(
                        range: new PartitionKeyRange()
                        {
                            Id = partitionKeyRangeId.ToString(),
                            MinInclusive = partitionKeyRangeId.ToString(),
                            MaxExclusive = partitionKeyRangeId.ToString(),
                        },
                        state: state,
                        (range, state) => new QueryPartitionRangePageEnumerator(
                            queryDataSource: new InMemoryCollectionQueryDataSource(inMemoryCollection),
                            sqlQuerySpec: new Cosmos.Query.Core.SqlQuerySpec("SELECT * FROM c"),
                            feedRange: range,
                            pageSize: 10,
                            state: state));
                    HashSet<Guid> resourceIdentifiers = await this.DrainFullyAsync(enumerable);

                    childIdentifiers.UnionWith(resourceIdentifiers);
                }

                Assert.AreEqual(numItems, parentIdentifiers.Count + childIdentifiers.Count);
            }

            internal override List<InMemoryCollection.Record> GetRecordsFromPage(QueryPage page)
            {
                List<InMemoryCollection.Record> records = new List<InMemoryCollection.Record>(page.Documents.Count);
                foreach (CosmosElement element in page.Documents)
                {
                    CosmosObject document = (CosmosObject)element;
                    long resourceIdentifier = Number64.ToLong(((CosmosNumber)document["_rid"]).Value);
                    long timestamp = Number64.ToLong(((CosmosNumber)document["_ts"]).Value);
                    Guid identifer = Guid.Parse(((CosmosString)document["id"]).Value);

                    records.Add(new InMemoryCollection.Record(resourceIdentifier, timestamp, identifer, document));
                }

                return records;
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

            internal override IAsyncEnumerable<TryCatch<QueryPage>> CreateEnumerable(
                InMemoryCollection inMemoryCollection,
                QueryState state = null)
            {
                return new PartitionRangePageEnumerable<QueryPage, QueryState>(
                    range: new PartitionKeyRange()
                    {
                        Id = "0",
                        MinInclusive = "0",
                        MaxExclusive = "0",
                    },
                    state: state,
                    (range, state) => new QueryPartitionRangePageEnumerator(
                        queryDataSource: new InMemoryCollectionQueryDataSource(inMemoryCollection),
                        sqlQuerySpec: new Cosmos.Query.Core.SqlQuerySpec("SELECT * FROM c"),
                        feedRange: range,
                        pageSize: 10,
                        state: state));
            }

            internal override IAsyncEnumerator<TryCatch<QueryPage>> CreateEnumerator(
                InMemoryCollection inMemoryCollection,
                QueryState state = default)
            {
                return new QueryPartitionRangePageEnumerator(
                    queryDataSource: new InMemoryCollectionQueryDataSource(inMemoryCollection),
                    sqlQuerySpec: new Cosmos.Query.Core.SqlQuerySpec("SELECT * FROM c"),
                    feedRange: new PartitionKeyRange()
                    {
                        Id = "0",
                        MinInclusive = "0",
                        MaxExclusive = "0",
                    },
                    pageSize: 10,
                    state: state);
            }
        }
    }
}
