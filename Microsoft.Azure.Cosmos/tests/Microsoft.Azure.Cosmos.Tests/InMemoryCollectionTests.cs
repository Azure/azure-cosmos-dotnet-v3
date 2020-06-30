//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.RecordIO;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class InMemoryCollectionTests
    {
        [TestMethod]
        public void TestCrud()
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
            InMemoryCollection.Record record = inMemoryCollection.CreateItem(item);
            Assert.IsNotNull(record);
            Assert.AreNotEqual(Guid.Empty, record.Identifier);
            Assert.AreEqual(1, record.ResourceIdentifier);

            // Try to read it back
            Assert.IsTrue(
                inMemoryCollection.TryReadItem(
                    partitionKey: CosmosNumber64.Create(42),
                    record.Identifier,
                    out InMemoryCollection.Record readRecord));

            Assert.AreEqual(item.ToString(), readRecord.Payload.ToString());
        }

        [TestMethod]
        public void TestPartitionKey()
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
            InMemoryCollection.Record record1 = inMemoryCollection.CreateItem(item1);

            // Insert into another partition key
            CosmosObject item2 = CosmosObject.Parse("{\"pk\" : 1337 }");
            InMemoryCollection.Record record2 = inMemoryCollection.CreateItem(item2);

            // Try to read back an id with wrong pk
            Assert.IsFalse(
                inMemoryCollection.TryReadItem(
                    partitionKey: item1["pk"],
                    record2.Identifier,
                    out InMemoryCollection.Record _));
        }

        [TestMethod]
        public void UndefinedPartitionKey()
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
            InMemoryCollection.Record record = inMemoryCollection.CreateItem(item);

            // Try to read back an id with wrong pk
            Assert.IsTrue(
                inMemoryCollection.TryReadItem(
                    partitionKey: null,
                    record.Identifier,
                    out InMemoryCollection.Record _));
        }

        [TestMethod]
        public void Split()
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

            Assert.AreEqual(1, inMemoryCollection.PartitionKeyRangeFeedReed().Count);

            int numItemsToInsert = 10;
            for (int i = 0; i < numItemsToInsert; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                inMemoryCollection.CreateItem(item);
            }

            inMemoryCollection.Split(partitionKeyRangeId: 0);

            Assert.AreEqual(2, inMemoryCollection.PartitionKeyRangeFeedReed().Count);
            (int leftChild, int rightChild) = inMemoryCollection.GetChildRanges(partitionKeyRangeId: 0);
            Assert.AreEqual(1, leftChild);
            Assert.AreEqual(2, rightChild);

            int AssertChildPartition(int partitionKeyRangeId)
            {
                TryCatch<(List<InMemoryCollection.Record> records, long? continuation)> tryGetPartitionRecords = inMemoryCollection.ReadFeed(
                    partitionKeyRangeId: partitionKeyRangeId,
                    resourceIndentifer: 0,
                    pageSize: 100);
                tryGetPartitionRecords.ThrowIfFailed();

                List<long> values = new List<long>();
                foreach (InMemoryCollection.Record record in tryGetPartitionRecords.Result.records)
                {
                    values.Add(Number64.ToLong((record.Payload["pk"] as CosmosNumber).Value));
                }

                List<long> sortedValues = values.OrderBy(x => x).ToList();
                Assert.IsTrue(values.SequenceEqual(sortedValues));

                return values.Count;
            }

            int count = AssertChildPartition(partitionKeyRangeId: 1) + AssertChildPartition(partitionKeyRangeId: 2);
            Assert.AreEqual(numItemsToInsert, count);
        }

        [TestMethod]
        public void MultiSplit()
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
                inMemoryCollection.CreateItem(item);
            }

            Assert.AreEqual(1, inMemoryCollection.PartitionKeyRangeFeedReed().Count);

            inMemoryCollection.Split(partitionKeyRangeId: 0);

            Assert.AreEqual(2, inMemoryCollection.PartitionKeyRangeFeedReed().Count);
            (int leftChild, int rightChild) = inMemoryCollection.GetChildRanges(partitionKeyRangeId: 0);
            Assert.AreEqual(1, leftChild);
            Assert.AreEqual(2, rightChild);

            inMemoryCollection.Split(partitionKeyRangeId: 1);
            inMemoryCollection.Split(partitionKeyRangeId: 2);


            Assert.AreEqual(4, inMemoryCollection.PartitionKeyRangeFeedReed().Count);
            (int leftChild1, int rightChild1) = inMemoryCollection.GetChildRanges(partitionKeyRangeId: 1);
            Assert.AreEqual(3, leftChild1);
            Assert.AreEqual(4, rightChild1);

            (int leftChild2, int rightChild2) = inMemoryCollection.GetChildRanges(partitionKeyRangeId: 2);
            Assert.AreEqual(5, leftChild2);
            Assert.AreEqual(6, rightChild2);

            int AssertChildPartition(int partitionKeyRangeId)
            {
                TryCatch<(List<InMemoryCollection.Record> records, long? continuation)> tryGetPartitionRecords = inMemoryCollection.ReadFeed(
                    partitionKeyRangeId: partitionKeyRangeId,
                    resourceIndentifer: 0,
                    pageSize: 100);
                tryGetPartitionRecords.ThrowIfFailed();

                List<long> values = new List<long>();
                foreach (InMemoryCollection.Record record in tryGetPartitionRecords.Result.records)
                {
                    values.Add(Number64.ToLong((record.Payload["pk"] as CosmosNumber).Value));
                }

                List<long> sortedValues = values.OrderBy(x => x).ToList();
                Assert.IsTrue(values.SequenceEqual(sortedValues));

                return values.Count;
            }

            int count = AssertChildPartition(partitionKeyRangeId: 3)
                + AssertChildPartition(partitionKeyRangeId: 4)
                + AssertChildPartition(partitionKeyRangeId: 5)
                + AssertChildPartition(partitionKeyRangeId: 6);
            Assert.AreEqual(numItemsToInsert, count);
        }
    }
}
