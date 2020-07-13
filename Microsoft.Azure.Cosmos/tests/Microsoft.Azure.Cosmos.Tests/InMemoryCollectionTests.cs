//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class InMemoryCollectionTests
    {
        [TestMethod]
        public async Task TestCrudAsync()
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

            // Insert an item
            CosmosObject item = CosmosObject.Parse("{\"pk\" : 42 }");
            Record record = await inMemoryCollection.CreateItemAsync(item, cancellationToken: default);
            Assert.IsNotNull(record);
            Assert.AreNotEqual(Guid.Empty, record.Identifier);
            Assert.AreEqual(1, record.ResourceIdentifier);

            // Try to read it back
            Record readRecord = await inMemoryCollection.ReadItemAsync(
                partitionKey: CosmosNumber64.Create(42),
                record.Identifier,
                cancellationToken: default);

            Assert.AreEqual(item.ToString(), readRecord.Payload.ToString());
        }

        [TestMethod]
        public async Task TestPartitionKeyAsync()
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

            // Insert an item
            CosmosObject item1 = CosmosObject.Parse("{\"pk\" : 42 }");
            Record record1 = await inMemoryCollection.CreateItemAsync(item1, cancellationToken: default);

            // Insert into another partition key
            CosmosObject item2 = CosmosObject.Parse("{\"pk\" : 1337 }");
            Record record2 = await inMemoryCollection.CreateItemAsync(item2, cancellationToken: default);

            // Try to read back an id with wrong pk
            TryCatch<Record> monadicReadItem = await inMemoryCollection.MonadicReadItemAsync(
                partitionKey: item1["pk"],
                record2.Identifier,
                cancellationToken: default);
            Assert.IsFalse(monadicReadItem.Succeeded);
        }

        [TestMethod]
        public async Task TestUndefinedPartitionKeyAsync()
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

            // Insert an item
            CosmosObject item = CosmosObject.Parse("{}");
            Record record = await inMemoryCollection.CreateItemAsync(item, cancellationToken: default);

            // Try to read back an id with wrong pk
            TryCatch<Record> monadicReadItem = await inMemoryCollection.MonadicReadItemAsync(
                partitionKey: null,
                record.Identifier,
                cancellationToken: default);
            Assert.IsFalse(monadicReadItem.Succeeded);
        }

        [TestMethod]
        public async Task TestSplitAsync()
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

            Assert.AreEqual(1, (await inMemoryCollection.GetFeedRangesAsync(cancellationToken: default)).Count);

            int numItemsToInsert = 10;
            for (int i = 0; i < numItemsToInsert; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                await inMemoryCollection.CreateItemAsync(item, cancellationToken: default);
            }

            await inMemoryCollection.SplitAsync(partitionKeyRangeId: 0, cancellationToken: default);

            Assert.AreEqual(2, (await inMemoryCollection.GetFeedRangesAsync(cancellationToken: default)).Count);
            List<PartitionKeyRange> ranges = await inMemoryCollection.GetChildRangeAsync(
                new PartitionKeyRange()
                {
                    Id = "0"
                },
                cancellationToken: default);
            Assert.AreEqual(2, ranges.Count);
            Assert.AreEqual(1, int.Parse(ranges[0].Id));
            Assert.AreEqual(2, int.Parse(ranges[1].Id));

            async Task<int> AssertChildPartitionAsync(int partitionKeyRangeId)
            {
                DocumentContainerPage readFeedPage = await inMemoryCollection.ReadFeedAsync(
                    partitionKeyRangeId: partitionKeyRangeId,
                    resourceIdentifier: 0,
                    pageSize: 100,
                    cancellationToken: default);

                List<long> values = new List<long>();
                foreach (Record record in readFeedPage.Records)
                {
                    values.Add(Number64.ToLong((record.Payload["pk"] as CosmosNumber).Value));
                }

                List<long> sortedValues = values.OrderBy(x => x).ToList();
                Assert.IsTrue(values.SequenceEqual(sortedValues));

                return values.Count;
            }

            int count = await AssertChildPartitionAsync(partitionKeyRangeId: 1) + await AssertChildPartitionAsync(partitionKeyRangeId: 2);
            Assert.AreEqual(numItemsToInsert, count);
        }

        [TestMethod]
        public async Task TestMultiSplitAsync()
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

            int numItemsToInsert = 10;
            for (int i = 0; i < numItemsToInsert; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                await inMemoryCollection.CreateItemAsync(item, cancellationToken: default);
            }

            Assert.AreEqual(1, (await inMemoryCollection.GetFeedRangesAsync(cancellationToken: default)).Count);

            await inMemoryCollection.SplitAsync(partitionKeyRangeId: 0, cancellationToken: default);

            Assert.AreEqual(2, (await inMemoryCollection.GetFeedRangesAsync(cancellationToken: default)).Count);
            List<PartitionKeyRange> ranges = await inMemoryCollection.GetChildRangeAsync(
                 new PartitionKeyRange()
                 {
                     Id = "0"
                 },
                 cancellationToken: default);
            Assert.AreEqual(2, ranges.Count);
            Assert.AreEqual(1, int.Parse(ranges[0].Id));
            Assert.AreEqual(2, int.Parse(ranges[1].Id));

            await inMemoryCollection.SplitAsync(partitionKeyRangeId: 1, cancellationToken: default);
            await inMemoryCollection.SplitAsync(partitionKeyRangeId: 2, cancellationToken: default);


            Assert.AreEqual(4, (await inMemoryCollection.GetFeedRangesAsync(cancellationToken: default)).Count);
            List<PartitionKeyRange> ranges1 = await inMemoryCollection.GetChildRangeAsync(
                 new PartitionKeyRange()
                 {
                     Id = "1"
                 },
                 cancellationToken: default);
            Assert.AreEqual(2, ranges.Count);
            Assert.AreEqual(3, int.Parse(ranges[0].Id));
            Assert.AreEqual(4, int.Parse(ranges[1].Id));

            List<PartitionKeyRange> ranges2 = await inMemoryCollection.GetChildRangeAsync(
                 new PartitionKeyRange()
                 {
                     Id = "2"
                 },
                 cancellationToken: default);
            Assert.AreEqual(2, ranges.Count);
            Assert.AreEqual(5, int.Parse(ranges[0].Id));
            Assert.AreEqual(6, int.Parse(ranges[1].Id));

            async Task<int> AssertChildPartitionAsync(int partitionKeyRangeId)
            {
                DocumentContainerPage page = await inMemoryCollection.ReadFeedAsync(
                    partitionKeyRangeId: partitionKeyRangeId,
                    resourceIdentifier: 0,
                    pageSize: 100,
                    cancellationToken: default);

                List<long> values = new List<long>();
                foreach (Record record in page.Records)
                {
                    values.Add(Number64.ToLong((record.Payload["pk"] as CosmosNumber).Value));
                }

                List<long> sortedValues = values.OrderBy(x => x).ToList();
                Assert.IsTrue(values.SequenceEqual(sortedValues));

                return values.Count;
            }

            int count = await AssertChildPartitionAsync(partitionKeyRangeId: 3)
                + await AssertChildPartitionAsync(partitionKeyRangeId: 4)
                + await AssertChildPartitionAsync(partitionKeyRangeId: 5)
                + await AssertChildPartitionAsync(partitionKeyRangeId: 6);
            Assert.AreEqual(numItemsToInsert, count);
        }
    }
}
