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
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public abstract class DocumentContainerTests
    {
        private static readonly PartitionKeyDefinition PartitionKeyDefinition = new PartitionKeyDefinition()
        {
            Paths = new System.Collections.ObjectModel.Collection<string>()
            {
                "/pk"
            },
            Kind = PartitionKind.Hash,
            Version = PartitionKeyDefinitionVersion.V2,
        };

        internal abstract IDocumentContainer CreateDocumentContainer(
            PartitionKeyDefinition partitionKeyDefinition,
            FlakyDocumentContainer.FailureConfigs failureConfigs = default);

        [TestMethod]
        public async Task TestGetFeedRanges()
        {
            IDocumentContainer documentContainer = this.CreateDocumentContainer(PartitionKeyDefinition);

            {
                List<PartitionKeyRange> ranges = await documentContainer.GetFeedRangesAsync(cancellationToken: default);
                Assert.AreEqual(expected: 1, ranges.Count);
            }

            await documentContainer.SplitAsync(partitionKeyRangeId: 0, cancellationToken: default);

            {
                List<PartitionKeyRange> ranges = await documentContainer.GetFeedRangesAsync(cancellationToken: default);
                Assert.AreEqual(expected: 2, ranges.Count);
            }
        }

        [TestMethod]
        public async Task TestCrudAsync()
        {
            IDocumentContainer documentContainer = this.CreateDocumentContainer(PartitionKeyDefinition);

            // Insert an item
            CosmosObject item = CosmosObject.Parse("{\"pk\" : 42 }");
            Record record = await documentContainer.CreateItemAsync(item, cancellationToken: default);
            Assert.IsNotNull(record);
            Assert.AreNotEqual(Guid.Empty, record.Identifier);
            Assert.AreEqual(1, record.ResourceIdentifier);

            // Try to read it back
            Record readRecord = await documentContainer.ReadItemAsync(
                partitionKey: CosmosNumber64.Create(42),
                record.Identifier,
                cancellationToken: default);

            Assert.AreEqual(item.ToString(), readRecord.Payload.ToString());
        }

        [TestMethod]
        public async Task TestPartitionKeyAsync()
        {
            IDocumentContainer documentContainer = this.CreateDocumentContainer(PartitionKeyDefinition);

            // Insert an item
            CosmosObject item1 = CosmosObject.Parse("{\"pk\" : 42 }");
            Record record1 = await documentContainer.CreateItemAsync(item1, cancellationToken: default);

            // Insert into another partition key
            CosmosObject item2 = CosmosObject.Parse("{\"pk\" : 1337 }");
            Record record2 = await documentContainer.CreateItemAsync(item2, cancellationToken: default);

            // Try to read back an id with wrong pk
            TryCatch<Record> monadicReadItem = await documentContainer.MonadicReadItemAsync(
                partitionKey: item1["pk"],
                record2.Identifier,
                cancellationToken: default);
            Assert.IsFalse(monadicReadItem.Succeeded);
        }

        [TestMethod]
        public async Task TestUndefinedPartitionKeyAsync()
        {
            IDocumentContainer documentContainer = this.CreateDocumentContainer(PartitionKeyDefinition);

            // Insert an item
            CosmosObject item = CosmosObject.Parse("{}");
            Record record = await documentContainer.CreateItemAsync(item, cancellationToken: default);

            // Try to read back an id with null undefined partition key 
            TryCatch<Record> monadicReadItem = await documentContainer.MonadicReadItemAsync(
                partitionKey: null,
                record.Identifier,
                cancellationToken: default);
            Assert.IsTrue(monadicReadItem.Succeeded);
        }

        [TestMethod]
        public async Task TestSplitAsync()
        {
            IDocumentContainer documentContainer = this.CreateDocumentContainer(PartitionKeyDefinition);

            Assert.AreEqual(1, (await documentContainer.GetFeedRangesAsync(cancellationToken: default)).Count);

            int numItemsToInsert = 10;
            for (int i = 0; i < numItemsToInsert; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                await documentContainer.CreateItemAsync(item, cancellationToken: default);
            }

            await documentContainer.SplitAsync(partitionKeyRangeId: 0, cancellationToken: default);

            Assert.AreEqual(2, (await documentContainer.GetFeedRangesAsync(cancellationToken: default)).Count);
            List<PartitionKeyRange> ranges = await documentContainer.GetChildRangeAsync(
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
                DocumentContainerPage readFeedPage = await documentContainer.ReadFeedAsync(
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

            IDocumentContainer documentContainer = this.CreateDocumentContainer(partitionKeyDefinition);

            int numItemsToInsert = 10;
            for (int i = 0; i < numItemsToInsert; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                await documentContainer.CreateItemAsync(item, cancellationToken: default);
            }

            Assert.AreEqual(1, (await documentContainer.GetFeedRangesAsync(cancellationToken: default)).Count);

            await documentContainer.SplitAsync(partitionKeyRangeId: 0, cancellationToken: default);

            {
                Assert.AreEqual(2, (await documentContainer.GetFeedRangesAsync(cancellationToken: default)).Count);
                List<PartitionKeyRange> ranges = await documentContainer.GetChildRangeAsync(
                     new PartitionKeyRange()
                     {
                         Id = "0"
                     },
                     cancellationToken: default);
                Assert.AreEqual(2, ranges.Count);
                Assert.AreEqual(1, int.Parse(ranges[0].Id));
                Assert.AreEqual(2, int.Parse(ranges[1].Id));
            }

            await documentContainer.SplitAsync(partitionKeyRangeId: 1, cancellationToken: default);
            await documentContainer.SplitAsync(partitionKeyRangeId: 2, cancellationToken: default);

            {
                Assert.AreEqual(4, (await documentContainer.GetFeedRangesAsync(cancellationToken: default)).Count);
                List<PartitionKeyRange> ranges = await documentContainer.GetChildRangeAsync(
                     new PartitionKeyRange()
                     {
                         Id = "1"
                     },
                     cancellationToken: default);
                Assert.AreEqual(2, ranges.Count);
                Assert.AreEqual(3, int.Parse(ranges[0].Id));
                Assert.AreEqual(4, int.Parse(ranges[1].Id));
            }

            {
                List<PartitionKeyRange> ranges = await documentContainer.GetChildRangeAsync(
                 new PartitionKeyRange()
                 {
                     Id = "2"
                 },
                 cancellationToken: default);
                Assert.AreEqual(2, ranges.Count);
                Assert.AreEqual(5, int.Parse(ranges[0].Id));
                Assert.AreEqual(6, int.Parse(ranges[1].Id));
            }

            async Task<int> AssertChildPartitionAsync(int partitionKeyRangeId)
            {
                DocumentContainerPage page = await documentContainer.ReadFeedAsync(
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
