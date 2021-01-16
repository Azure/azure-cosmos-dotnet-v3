//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class DocumentContainerChangeFeedTests
    {
        [TestMethod]
        public async Task EmptyContainerTestAsync()
        {
            IDocumentContainer documentContainer = await this.CreateDocumentContainerAsync(numItems: 0);
            List<FeedRangeEpk> ranges = await documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, cancellationToken: default);
            TryCatch<ChangeFeedPage> monadicChangeFeedPage = await documentContainer.MonadicChangeFeedAsync(
                ChangeFeedState.Beginning(),
                ranges[0],
                pageSize: 10,
                changeFeedMode: ChangeFeedMode.Incremental,
                contentSerializationFormat: null,
                trace: NoOpTrace.Singleton,
                cancellationToken: default);

            Assert.IsTrue(monadicChangeFeedPage.Succeeded);
            Assert.IsTrue(monadicChangeFeedPage.Result is ChangeFeedNotModifiedPage);
        }

        [TestMethod]
        public async Task StartFromBeginingTestAsync()
        {
            IDocumentContainer documentContainer = await this.CreateDocumentContainerAsync(numItems: 10);
            List<FeedRangeEpk> ranges = await documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, cancellationToken: default);

            // Should get back the all the documents inserted so far
            ChangeFeedState resumeState;
            {
                TryCatch<ChangeFeedPage> monadicChangeFeedPage = await documentContainer.MonadicChangeFeedAsync(
                    ChangeFeedState.Beginning(),
                    ranges[0],
                    pageSize: int.MaxValue,
                    changeFeedMode: ChangeFeedMode.Incremental,
                    contentSerializationFormat: null,
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);

                Assert.IsTrue(monadicChangeFeedPage.Succeeded);

                resumeState = monadicChangeFeedPage.Result.State;
            }

            // No more changes left
            {
                TryCatch<ChangeFeedPage> monadicChangeFeedPage = await documentContainer.MonadicChangeFeedAsync(
                    resumeState,
                    ranges[0],
                    pageSize: 10,
                    changeFeedMode: ChangeFeedMode.Incremental,
                    contentSerializationFormat: null,
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);

                Assert.IsTrue(monadicChangeFeedPage.Succeeded);
                Assert.IsTrue(monadicChangeFeedPage.Result is ChangeFeedNotModifiedPage);
            }
        }

        [TestMethod]
        public async Task StartFromTimeTestAsync()
        {
            IDocumentContainer documentContainer = await this.CreateDocumentContainerAsync(numItems: 10);
            List<FeedRangeEpk> ranges = await documentContainer.GetFeedRangesAsync(trace: NoOpTrace.Singleton, cancellationToken: default);

            DateTime now = DateTime.UtcNow;
            // No changes let
            {
                TryCatch<ChangeFeedPage> monadicChangeFeedPage = await documentContainer.MonadicChangeFeedAsync(
                    ChangeFeedState.Time(now),
                    ranges[0],
                    pageSize: 10,
                    changeFeedMode: ChangeFeedMode.Incremental,
                    contentSerializationFormat: null,
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);

                Assert.IsTrue(monadicChangeFeedPage.Succeeded);
                Assert.IsTrue(monadicChangeFeedPage.Result is ChangeFeedNotModifiedPage);
            }

            // Insert some items
            for (int i = 0; i < 10; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                while (true)
                {
                    TryCatch<Record> monadicCreateRecord = await documentContainer.MonadicCreateItemAsync(item, cancellationToken: default);
                    if (monadicCreateRecord.Succeeded)
                    {
                        break;
                    }
                }
            }

            // Now we should be able to see the changes
            {
                TryCatch<ChangeFeedPage> monadicChangeFeedPage = await documentContainer.MonadicChangeFeedAsync(
                    ChangeFeedState.Time(now),
                    ranges[0],
                    pageSize: int.MaxValue,
                    changeFeedMode: ChangeFeedMode.Incremental,
                    contentSerializationFormat: null,
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);

                Assert.IsTrue(monadicChangeFeedPage.Succeeded);
            }
        }

        [TestMethod]
        public async Task StartFromNowAsync()
        {
            IDocumentContainer documentContainer = await this.CreateDocumentContainerAsync(numItems: 10);
            List<FeedRangeEpk> ranges = await documentContainer.GetFeedRangesAsync(trace: NoOpTrace.Singleton, cancellationToken: default);

            ChangeFeedState resumeState;
            // No changes starting from now
            {
                TryCatch<ChangeFeedPage> monadicChangeFeedPage = await documentContainer.MonadicChangeFeedAsync(
                    ChangeFeedState.Now(),
                    ranges[0],
                    pageSize: 10,
                    changeFeedMode: ChangeFeedMode.Incremental,
                    contentSerializationFormat: null,
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);

                Assert.IsTrue(monadicChangeFeedPage.Succeeded);
                if (!(monadicChangeFeedPage.Result is ChangeFeedNotModifiedPage changeFeedNotModifiedPage))
                {
                    Assert.Fail();
                    throw new Exception();
                }

                resumeState = changeFeedNotModifiedPage.State;
            }

            // Insert some items
            for (int i = 0; i < 10; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                while (true)
                {
                    TryCatch<Record> monadicCreateRecord = await documentContainer.MonadicCreateItemAsync(item, cancellationToken: default);
                    if (monadicCreateRecord.Succeeded)
                    {
                        break;
                    }
                }
            }

            {
                TryCatch<ChangeFeedPage> monadicChangeFeedPage = await documentContainer.MonadicChangeFeedAsync(
                    resumeState,
                    ranges[0],
                    pageSize: 10,
                    changeFeedMode: ChangeFeedMode.Incremental,
                    contentSerializationFormat: null,
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);

                Assert.IsTrue(monadicChangeFeedPage.Succeeded);
            }
        }

        [TestMethod]
        public async Task ReadPartitionKeyTestAsync()
        {
            IDocumentContainer documentContainer = await this.CreateDocumentContainerAsync(numItems: 0);
            for (int i = 0; i < 10; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                while (true)
                {
                    TryCatch<Record> monadicCreateRecord = await documentContainer.MonadicCreateItemAsync(item, cancellationToken: default);
                    if (monadicCreateRecord.Succeeded)
                    {
                        break;
                    }
                }
            }

            // Should get back only the document with the partition key.
            TryCatch<ChangeFeedPage> monadicChangeFeedPage = await documentContainer.MonadicChangeFeedAsync(
                ChangeFeedState.Beginning(),
                new FeedRangePartitionKey(new Cosmos.PartitionKey(0)),
                pageSize: int.MaxValue,
                changeFeedMode: ChangeFeedMode.Incremental,
                contentSerializationFormat: null,
                NoOpTrace.Singleton,
                cancellationToken: default);

            Assert.IsTrue(monadicChangeFeedPage.Succeeded);

            if (!(monadicChangeFeedPage.Result is ChangeFeedSuccessPage changeFeedSuccessPage))
            {
                Assert.Fail();
                throw new Exception();
            }

            MemoryStream memoryStream = new MemoryStream();
            changeFeedSuccessPage.Content.CopyTo(memoryStream);
            CosmosObject response = CosmosObject.CreateFromBuffer(memoryStream.ToArray());
            long count = Number64.ToLong(((CosmosNumber)response["_count"]).Value);

            Assert.AreEqual(1, count);
        }

        [TestMethod]
        public async Task EpkRangeFilteringAsync()
        {
            IDocumentContainer documentContainer = await this.CreateDocumentContainerAsync(numItems: 100);

            List<FeedRangeEpk> ranges = await documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, cancellationToken: default);
            FeedRangeEpk range = ranges[0];

            PartitionKeyHashRange hashRange = new PartitionKeyHashRange(PartitionKeyHash.Parse(range.Range.Min), PartitionKeyHash.Parse(range.Range.Max));
            PartitionKeyHashRanges hashRanges = PartitionKeyHashRangeSplitterAndMerger.SplitRange(hashRange, rangeCount: 2);

            long sumChildCount = 0;
            foreach (PartitionKeyHashRange value in hashRanges)
            {
                // Should get back only the document within the epk range.
                TryCatch<ChangeFeedPage> monadicChangeFeedPage = await documentContainer.MonadicChangeFeedAsync(
                    ChangeFeedState.Beginning(),
                    new FeedRangeEpk(
                        new Documents.Routing.Range<string>(
                            min: value.StartInclusive.Value.ToString(),
                            max: value.EndExclusive.Value.ToString(),
                            isMinInclusive: true,
                            isMaxInclusive: false)),
                    pageSize: int.MaxValue,
                    changeFeedMode: ChangeFeedMode.Incremental,
                    contentSerializationFormat: null,
                    NoOpTrace.Singleton,
                    cancellationToken: default);

                Assert.IsTrue(monadicChangeFeedPage.Succeeded);

                if (!(monadicChangeFeedPage.Result is ChangeFeedSuccessPage changeFeedSuccessPage))
                {
                    Assert.Fail();
                    throw new Exception();
                }

                MemoryStream memoryStream = new MemoryStream();
                changeFeedSuccessPage.Content.CopyTo(memoryStream);
                CosmosObject response = CosmosObject.CreateFromBuffer(memoryStream.ToArray());
                sumChildCount += Number64.ToLong(((CosmosNumber)response["_count"]).Value);
            }

            long numRecords = (await documentContainer.ReadFeedAsync(
                feedRange: range,
                readFeedState: ReadFeedState.Beginning(),
                pageSize: int.MaxValue,
                trace: NoOpTrace.Singleton,
                cancellationToken: default,
                queryRequestOptions: default)).GetRecords().Count;

            Assert.AreEqual(numRecords, sumChildCount);
        }

        [TestMethod]
        public async Task ReadChangesAcrossSplitsAsync()
        {
            IDocumentContainer documentContainer = await this.CreateDocumentContainerAsync(numItems: 100);
            List<FeedRangeEpk> ranges = await documentContainer.GetFeedRangesAsync(trace: NoOpTrace.Singleton, cancellationToken: default);
            long numRecords = (await documentContainer.ReadFeedAsync(
                feedRange: ranges[0],
                readFeedState: ReadFeedState.Beginning(),
                pageSize: int.MaxValue,
                queryRequestOptions: default,
                trace: NoOpTrace.Singleton,
                cancellationToken: default)).GetRecords().Count;

            await documentContainer.SplitAsync(ranges[0], cancellationToken: default);
            await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);

            List<FeedRangeEpk> children = await documentContainer.GetChildRangeAsync(ranges[0], trace: NoOpTrace.Singleton, cancellationToken: default);

            long sumOfChildCounts = 0;
            foreach (FeedRangeInternal child in children)
            {
                TryCatch<ChangeFeedPage> monadicChangeFeedPage = await documentContainer.MonadicChangeFeedAsync(
                    ChangeFeedState.Beginning(),
                    child,
                    pageSize: 1000,
                    changeFeedMode: ChangeFeedMode.Incremental,
                    contentSerializationFormat: null,
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);

                Assert.IsTrue(monadicChangeFeedPage.Succeeded);

                if (!(monadicChangeFeedPage.Result is ChangeFeedSuccessPage changeFeedSuccessPage))
                {
                    Assert.Fail();
                    throw new Exception();
                }

                MemoryStream memoryStream = new MemoryStream();
                changeFeedSuccessPage.Content.CopyTo(memoryStream);
                CosmosObject response = CosmosObject.CreateFromBuffer(memoryStream.ToArray());
                long childCount = Number64.ToLong(((CosmosNumber)response["_count"]).Value);
                sumOfChildCounts += childCount;
            }

            Assert.AreEqual(numRecords, sumOfChildCounts);
        }

        private async Task<IDocumentContainer> CreateDocumentContainerAsync(
            int numItems,
            FlakyDocumentContainer.FailureConfigs failureConfigs = default)
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

            IMonadicDocumentContainer monadicDocumentContainer = new InMemoryContainer(partitionKeyDefinition);
            if (failureConfigs != null)
            {
                monadicDocumentContainer = new FlakyDocumentContainer(monadicDocumentContainer, failureConfigs);
            }

            DocumentContainer documentContainer = new DocumentContainer(monadicDocumentContainer);
            for (int i = 0; i < 3; i++)
            {
                IReadOnlyList<FeedRangeInternal> ranges = await documentContainer.GetFeedRangesAsync(trace: NoOpTrace.Singleton, cancellationToken: default);
                foreach (FeedRangeInternal range in ranges)
                {
                    await documentContainer.SplitAsync(range, cancellationToken: default);
                }

                await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
            }

            for (int i = 0; i < numItems; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                while (true)
                {
                    TryCatch<Record> monadicCreateRecord = await documentContainer.MonadicCreateItemAsync(item, cancellationToken: default);
                    if (monadicCreateRecord.Succeeded)
                    {
                        break;
                    }
                }
            }

            return documentContainer;
        }
    }
}
