//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
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
    }
}
