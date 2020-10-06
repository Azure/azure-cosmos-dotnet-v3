//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Antlr4.Runtime.Tree;
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
        public async Task TestGetOverlappingRanges()
        {
            IDocumentContainer documentContainer = this.CreateDocumentContainer(PartitionKeyDefinition);

            // Check range with no children (base case)
            {
                List<PartitionKeyRange> ranges = await documentContainer.GetChildRangeAsync(
                    partitionKeyRange: new PartitionKeyRange()
                    {
                        Id = "0"
                    },
                    cancellationToken: default);
                Assert.AreEqual(expected: 1, ranges.Count);
            }

            await documentContainer.SplitAsync(partitionKeyRangeId: 0, cancellationToken: default);

            // Check the leaves
            foreach (int i in new int[] { 1, 2 })
            {
                List<PartitionKeyRange> ranges = await documentContainer.GetChildRangeAsync(
                    partitionKeyRange: new PartitionKeyRange()
                    {
                        Id = i.ToString(),
                    },
                    cancellationToken: default);
                Assert.AreEqual(expected: 1, ranges.Count);
            }

            // Check a range with children
            {
                List<PartitionKeyRange> ranges = await documentContainer.GetChildRangeAsync(
                    partitionKeyRange: new PartitionKeyRange()
                    {
                        Id = "0"
                    },
                    cancellationToken: default);
                Assert.AreEqual(expected: 2, ranges.Count);
            }

            // Check a range that isn't an integer
            {
                TryCatch<List<PartitionKeyRange>> monad = await documentContainer.MonadicGetChildRangeAsync(
                    partitionKeyRange: new PartitionKeyRange()
                    {
                        Id = "asdf"
                    },
                    cancellationToken: default);
                Assert.IsFalse(monad.Succeeded);
            }

            // Check a range that doesn't exist
            {
                TryCatch<List<PartitionKeyRange>> monad = await documentContainer.MonadicGetChildRangeAsync(
                    partitionKeyRange: new PartitionKeyRange()
                    {
                        Id = "42"
                    },
                    cancellationToken: default);
                Assert.IsFalse(monad.Succeeded);
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

            IReadOnlyList<FeedRangeInternal> ranges = await documentContainer.GetFeedRangesAsync(cancellationToken: default);

            Assert.AreEqual(1, ranges.Count);

            int numItemsToInsert = 10;
            for (int i = 0; i < numItemsToInsert; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                await documentContainer.CreateItemAsync(item, cancellationToken: default);
            }

            await documentContainer.SplitAsync(ranges[0], cancellationToken: default);

            IReadOnlyList<FeedRangeInternal> childRanges = await documentContainer.GetFeedRangesAsync(cancellationToken: default);
            Assert.AreEqual(2, childRanges.Count);

            async Task<int> AssertChildPartitionAsync(FeedRangeInternal childRange)
            {
                DocumentContainerPage readFeedPage = await documentContainer.ReadFeedAsync(
                    feedRange: childRange,
                    resourceIdentifier: ResourceId.Empty,
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

            int count = await AssertChildPartitionAsync(childRanges[0]) + await AssertChildPartitionAsync(childRanges[1]);
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

            IReadOnlyList<FeedRangeInternal> ranges = await documentContainer.GetFeedRangesAsync(cancellationToken: default);
            Assert.AreEqual(1, ranges.Count);

            await documentContainer.SplitAsync(ranges[0], cancellationToken: default);

            IReadOnlyList<FeedRangeInternal> childRanges = await documentContainer.GetChildRangeAsync(
                ranges[0], 
                cancellationToken: default);
            Assert.AreEqual(2, childRanges.Count);

            foreach (FeedRangeInternal childRange in childRanges)
            {
                await documentContainer.SplitAsync(childRange, cancellationToken: default);
            }

            int count = 0;
            foreach (FeedRangeInternal childRange in childRanges)
            {
                IReadOnlyList<FeedRangeInternal> grandChildrenRange = await documentContainer.GetChildRangeAsync(
                    childRange,
                    cancellationToken: default);
                Assert.AreEqual(2, grandChildrenRange.Count);
                count += await AssertChildPartitionAsync(childRange);
            }

            async Task<int> AssertChildPartitionAsync(FeedRangeInternal feedRange)
            {
                DocumentContainerPage page = await documentContainer.ReadFeedAsync(
                    feedRange: feedRange,
                    resourceIdentifier: ResourceId.Empty,
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

            Assert.AreEqual(numItemsToInsert, count);
        }
    }
}
