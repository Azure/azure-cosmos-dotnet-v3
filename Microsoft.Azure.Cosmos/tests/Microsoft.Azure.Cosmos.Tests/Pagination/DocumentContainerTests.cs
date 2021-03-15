//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Moq;

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

        private static class FeedDrainFunctions
        {
            public static readonly DrainFunctions<ReadFeedState> ReadFeed = new DrainFunctions<ReadFeedState>(
                drainOnePageAsync: async (documentContainer, feedRange) =>
                {
                    ReadFeedPage page = await documentContainer.ReadFeedAsync(
                        feedRangeState: new FeedRangeState<ReadFeedState>(feedRange, ReadFeedState.Beginning()),
                        readFeedPaginationOptions: new ReadFeedPaginationOptions(pageSizeHint: 1),
                        trace: NoOpTrace.Singleton,
                        cancellationToken: default);
                    return (page.GetRecords().Count, page.State);
                },
                drainAllPagesAsync: async (documentContainer, resumeState, feedRange) =>
                {
                    int count = 0;
                    do
                    {
                        ReadFeedPage page = await documentContainer.ReadFeedAsync(
                            feedRangeState: new FeedRangeState<ReadFeedState>(feedRange, resumeState),
                            readFeedPaginationOptions: new ReadFeedPaginationOptions(pageSizeHint: 1),
                            trace: NoOpTrace.Singleton,
                            cancellationToken: default);
                        resumeState = page.State;
                        count += page.GetRecords().Count;
                    }
                    while (resumeState != null);

                    return count;
                });

            public static readonly DrainFunctions<ChangeFeedState> ChangeFeed = new DrainFunctions<ChangeFeedState>(
                drainOnePageAsync: async (documentContainer, feedRange) =>
                {
                    ChangeFeedPage page = await documentContainer.ChangeFeedAsync(
                        feedRangeState: new FeedRangeState<ChangeFeedState>(feedRange, ChangeFeedState.Beginning()),
                        changeFeedPaginationOptions: new ChangeFeedPaginationOptions(
                            mode: ChangeFeedMode.Incremental,
                            pageSizeHint: 1),
                        trace: NoOpTrace.Singleton,
                        cancellationToken: default);

                    if (!(page is ChangeFeedSuccessPage successPage))
                    {
                        throw new InvalidOperationException();
                    }

                    return (successPage.GetRecords().Count, page.State);
                },
                drainAllPagesAsync: async (documentContainer, resumeState, feedRange) =>
                {
                    int count = 0;
                    while (true)
                    {
                        ChangeFeedPage page = await documentContainer.ChangeFeedAsync(
                            feedRangeState: new FeedRangeState<ChangeFeedState>(feedRange, resumeState),
                            changeFeedPaginationOptions: new ChangeFeedPaginationOptions(
                                mode: ChangeFeedMode.Incremental,
                                pageSizeHint: 1),
                            trace: NoOpTrace.Singleton,
                            cancellationToken: default);
                        resumeState = page.State;

                        if (page is ChangeFeedNotModifiedPage notModifiedPage)
                        {
                            break;
                        }

                        if (!(page is ChangeFeedSuccessPage changeFeedSuccessPage))
                        {
                            throw new InvalidOperationException();
                        }

                        count += changeFeedSuccessPage.GetRecords().Count;
                    }

                    return count;
                });

            public static readonly DrainFunctions<QueryState> Query = new DrainFunctions<QueryState>(
                drainOnePageAsync: async (documentContainer, feedRange) =>
                {
                    QueryPage page = await documentContainer.QueryAsync(
                        sqlQuerySpec: new Cosmos.Query.Core.SqlQuerySpec("SELECT * FROM c"),
                        feedRangeState: new FeedRangeState<QueryState>(feedRange, state: null),
                        queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: 1),
                        trace: NoOpTrace.Singleton,
                        cancellationToken: default);

                    return (page.Documents.Count, page.State);
                },
                drainAllPagesAsync: async (documentContainer, resumeState, feedRange) =>
                {
                    int count = 0;
                    do
                    {
                        QueryPage page = await documentContainer.QueryAsync(
                            sqlQuerySpec: new Cosmos.Query.Core.SqlQuerySpec("SELECT * FROM c"),
                            feedRangeState: new FeedRangeState<QueryState>(feedRange, resumeState),
                            queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: 1),
                            trace: NoOpTrace.Singleton,
                            cancellationToken: default);

                        resumeState = page.State;

                        count += page.Documents.Count;
                    }
                    while (resumeState != null);

                    return count;
                });
        }

        internal abstract IDocumentContainer CreateDocumentContainer(
            PartitionKeyDefinition partitionKeyDefinition,
            int numItemToInsert = 0,
            FlakyDocumentContainer.FailureConfigs failureConfigs = default);

        [TestMethod]
        public async Task TestGetFeedRanges()
        {
            IDocumentContainer documentContainer = this.CreateDocumentContainer(PartitionKeyDefinition);

            {
                // Start off with one range.
                List<FeedRangeEpk> ranges = await documentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);
                Assert.AreEqual(expected: 1, ranges.Count);

                await documentContainer.SplitAsync(ranges[0], cancellationToken: default);
            }

            {
                // Still have one range, since we have let to refresh.
                List<FeedRangeEpk> ranges = await documentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);
                Assert.AreEqual(expected: 1, ranges.Count);
            }

            await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);

            {
                // Now we should have two ranges after a refresh.
                List<FeedRangeEpk> ranges = await documentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);
                Assert.AreEqual(expected: 2, ranges.Count);
            }
        }

        [TestMethod]
        public async Task TestGetOverlappingRanges()
        {
            IDocumentContainer documentContainer = this.CreateDocumentContainer(PartitionKeyDefinition);

            // Check range with no children (base case)
            {
                List<FeedRangeEpk> ranges = await documentContainer.GetChildRangeAsync(
                    feedRange: new FeedRangePartitionKeyRange("0"),
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);
                Assert.AreEqual(expected: 1, ranges.Count);
            }

            await documentContainer.SplitAsync(new FeedRangePartitionKeyRange("0"), cancellationToken: default);
            await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);

            // Check the leaves
            foreach (int i in new int[] { 1, 2 })
            {
                List<FeedRangeEpk> ranges = await documentContainer.GetChildRangeAsync(
                    feedRange: new FeedRangePartitionKeyRange(i.ToString()),
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);
                Assert.AreEqual(expected: 1, ranges.Count);
            }

            // Check a range with children
            {
                List<FeedRangeEpk> ranges = await documentContainer.GetChildRangeAsync(
                    feedRange: new FeedRangePartitionKeyRange("0"),
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);
                Assert.AreEqual(expected: 2, ranges.Count);
            }

            // Check a range that isn't an integer
            {
                TryCatch<List<FeedRangeEpk>> monad = await documentContainer.MonadicGetChildRangeAsync(
                    feedRange: new FeedRangePartitionKeyRange("asdf"),
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);
                Assert.IsFalse(monad.Succeeded);
            }

            // Check a range that doesn't exist
            {
                TryCatch<List<FeedRangeEpk>> monad = await documentContainer.MonadicGetChildRangeAsync(
                    feedRange: new FeedRangePartitionKeyRange("42"),
                    trace: NoOpTrace.Singleton,
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
            Record _ = await documentContainer.CreateItemAsync(item1, cancellationToken: default);

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
        public async Task TestSplit_ReadFeedAsync()
        {
            await this.TestSplitAsyncImplementation(FeedDrainFunctions.ReadFeed);
        }

        [TestMethod]
        public async Task TestSplit_ChangeFeedAsync()
        {
            await this.TestSplitAsyncImplementation(FeedDrainFunctions.ChangeFeed);
        }

        [TestMethod]
        public async Task TestSplit_QueryAsync()
        {
            await this.TestSplitAsyncImplementation(FeedDrainFunctions.Query);
        }

        private async Task TestSplitAsyncImplementation<TState>(DrainFunctions<TState> drainFunctions)
            where TState : State
        {
            // Container setup
            int numItemsToInsert = 10;
            IDocumentContainer documentContainer = this.CreateDocumentContainer(PartitionKeyDefinition, numItemsToInsert);

            IReadOnlyList<FeedRangeInternal> ranges = await documentContainer.GetFeedRangesAsync(
                trace: NoOpTrace.Singleton,
                cancellationToken: default);

            Assert.AreEqual(1, ranges.Count);

            (int firstPageCount, TState resumeState) = await drainFunctions.DrainOnePageAsync(documentContainer, ranges[0]);

            await documentContainer.SplitAsync(ranges[0], cancellationToken: default);
            await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
            IReadOnlyList<FeedRangeInternal> childRanges = await documentContainer.GetFeedRangesAsync(
                trace: NoOpTrace.Singleton,
                cancellationToken: default);
            Assert.AreEqual(2, childRanges.Count);

            int childOneCount = await drainFunctions.DrainAllPagesAsync(documentContainer, resumeState, childRanges[0]);
            int childTwoCount = await drainFunctions.DrainAllPagesAsync(documentContainer, resumeState, childRanges[1]);

            Assert.AreEqual(numItemsToInsert, firstPageCount + childOneCount + childTwoCount);
        }

        [TestMethod]
        public async Task TestSplitAfterSplit_ReadFeedAsync()
        {
            await this.TestSplitAfterSplitImplementationAsync(FeedDrainFunctions.ReadFeed);
        }

        [TestMethod]
        public async Task TestSplitAfterSplit_ChangeFeedAsync()
        {
            await this.TestSplitAfterSplitImplementationAsync(FeedDrainFunctions.ChangeFeed);
        }

        [TestMethod]
        public async Task TestSplitAfterSplit_QueryAsync()
        {
            await this.TestSplitAfterSplitImplementationAsync(FeedDrainFunctions.Query);
        }

        private async Task TestSplitAfterSplitImplementationAsync<TState>(DrainFunctions<TState> drainFunctions)
            where TState : State
        {
            int numItemsToInsert = 10;
            IDocumentContainer documentContainer = this.CreateDocumentContainer(PartitionKeyDefinition, numItemsToInsert);

            IReadOnlyList<FeedRangeInternal> ranges = await documentContainer.GetFeedRangesAsync(
                trace: NoOpTrace.Singleton,
                cancellationToken: default);
            Assert.AreEqual(1, ranges.Count);

            await documentContainer.SplitAsync(ranges[0], cancellationToken: default);
            await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);

            IReadOnlyList<FeedRangeInternal> childRanges = await documentContainer.GetChildRangeAsync(
                ranges[0],
                trace: NoOpTrace.Singleton,
                cancellationToken: default);
            Assert.AreEqual(2, childRanges.Count);

            foreach (FeedRangeInternal childRange in childRanges)
            {
                await documentContainer.SplitAsync(childRange, cancellationToken: default);
            }

            await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);

            int count = 0;
            foreach (FeedRangeInternal childRange in childRanges)
            {
                IReadOnlyList<FeedRangeInternal> grandChildrenRanges = await documentContainer.GetChildRangeAsync(
                    childRange,
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);
                Assert.AreEqual(2, grandChildrenRanges.Count);
                foreach (FeedRangeInternal grandChildrenRange in grandChildrenRanges)
                {
                    count += await drainFunctions.DrainAllFromStartAsync(documentContainer, grandChildrenRange);
                }
            }

            Assert.AreEqual(numItemsToInsert, count);
        }

        [TestMethod]
        public async Task TestMerge_ReadFeedAsync()
        {
            await this.TestMergeImplementationAsync(FeedDrainFunctions.ReadFeed);
        }

        [TestMethod]
        public async Task TestMerge_ChangeFeedAsync()
        {
            await this.TestMergeImplementationAsync(FeedDrainFunctions.ChangeFeed);
        }

        [TestMethod]
        public async Task TestMerge_QueryAsync()
        {
            await this.TestMergeImplementationAsync(FeedDrainFunctions.Query);
        }

        private async Task TestMergeImplementationAsync<TState>(
            DrainFunctions<TState> drainFunctions)
            where TState : State
        {
            IDocumentContainer documentContainer = this.CreateDocumentContainer(PartitionKeyDefinition);
            IReadOnlyList<FeedRangeInternal> ranges = await documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, cancellationToken: default);
            Assert.AreEqual(1, ranges.Count);
            await documentContainer.SplitAsync(ranges[0], cancellationToken: default);
            await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
            IReadOnlyList<FeedRangeInternal> childRanges = await documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, cancellationToken: default);
            Assert.AreEqual(2, childRanges.Count);

            int numItemsToInsert = 10;
            for (int i = 0; i < numItemsToInsert; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                await documentContainer.CreateItemAsync(item, cancellationToken: default);
            }

            // Partially drain one partition
            (int _, TState resumeState) = await drainFunctions.DrainOnePageAsync(documentContainer, childRanges[0]);

            // Resume from that continuation after merge and compare the results pre-merge
            int countBeforeMerge = await drainFunctions.DrainAllPagesAsync(documentContainer, resumeState, childRanges[0]);

            await documentContainer.MergeAsync(childRanges[0], childRanges[1], cancellationToken: default);
            await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
            IReadOnlyList<FeedRangeInternal> mergedRanges = await documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, cancellationToken: default);
            Assert.AreEqual(1, mergedRanges.Count);

            int countAfterMerge = await drainFunctions.DrainAllPagesAsync(documentContainer, resumeState, childRanges[0]);

            Assert.AreEqual(countBeforeMerge, countAfterMerge);

            // Check that the merged sums up to the splits 
            int mergedCount = await drainFunctions.DrainAllFromStartAsync(documentContainer, mergedRanges[0]);
            int childCount1 = await drainFunctions.DrainAllFromStartAsync(documentContainer, childRanges[0]);
            int childCount2 = await drainFunctions.DrainAllFromStartAsync(documentContainer, childRanges[1]);
            Assert.AreEqual(mergedCount, childCount1 + childCount2);
        }

        [TestMethod]
        public async Task TestMergeAfterMerge_ReadFeedAsync()
        {
            await this.TestMergeAfterMergeImplementationAsync(FeedDrainFunctions.ReadFeed);
        }

        [TestMethod]
        public async Task TestMergeAfterMerge_ChangeFeedAsync()
        {
            await this.TestMergeAfterMergeImplementationAsync(FeedDrainFunctions.ChangeFeed);
        }

        [TestMethod]
        public async Task TestMergeAfterMerge_QueryAsync()
        {
            await this.TestMergeAfterMergeImplementationAsync(FeedDrainFunctions.Query);
        }

        private async Task TestMergeAfterMergeImplementationAsync<TState>(
            DrainFunctions<TState> drainFunctions)
            where TState : State
        {
            IDocumentContainer documentContainer = this.CreateDocumentContainer(PartitionKeyDefinition);
            IReadOnlyList<FeedRangeInternal> ranges = await documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, cancellationToken: default);
            Assert.AreEqual(1, ranges.Count);
            await documentContainer.SplitAsync(ranges[0], cancellationToken: default);
            await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
            IReadOnlyList<FeedRangeInternal> childRanges = await documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, cancellationToken: default);
            Assert.AreEqual(2, childRanges.Count);
            await documentContainer.SplitAsync(childRanges[0], cancellationToken: default);
            await documentContainer.SplitAsync(childRanges[1], cancellationToken: default);
            await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
            IReadOnlyList<FeedRangeInternal> grandChildRanges = await documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, cancellationToken: default);
            Assert.AreEqual(4, grandChildRanges.Count);

            int numItemsToInsert = 10;
            for (int i = 0; i < numItemsToInsert; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                await documentContainer.CreateItemAsync(item, cancellationToken: default);
            }

            await documentContainer.MergeAsync(grandChildRanges[0], grandChildRanges[1], cancellationToken: default);
            await documentContainer.MergeAsync(grandChildRanges[2], grandChildRanges[3], cancellationToken: default);
            await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
            IReadOnlyList<FeedRangeInternal> mergedGrandChildrenRanges = await documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, cancellationToken: default);
            Assert.AreEqual(2, mergedGrandChildrenRanges.Count);
            await documentContainer.MergeAsync(mergedGrandChildrenRanges[0], mergedGrandChildrenRanges[1], cancellationToken: default);
            await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
            IReadOnlyList<FeedRangeInternal> mergedRanges = await documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, cancellationToken: default);
            Assert.AreEqual(1, mergedRanges.Count);

            // Check that the merged sums up to the splits 
            int mergedCount = await drainFunctions.DrainAllFromStartAsync(documentContainer, mergedRanges[0]);
            int grandChildCount1 = await drainFunctions.DrainAllFromStartAsync(documentContainer, grandChildRanges[0]);
            int grandChildCount2 = await drainFunctions.DrainAllFromStartAsync(documentContainer, grandChildRanges[1]);
            int grandChildCount3 = await drainFunctions.DrainAllFromStartAsync(documentContainer, grandChildRanges[2]);
            int grandChildCount4 = await drainFunctions.DrainAllFromStartAsync(documentContainer, grandChildRanges[3]);
            Assert.AreEqual(mergedCount, grandChildCount1 + grandChildCount2 + grandChildCount3 + grandChildCount4);
        }

        [TestMethod]
        public async Task TestSplitAfterMerge_ReadFeedAsync()
        {
            await this.TestSplitAfterMergeImplementationAsync(FeedDrainFunctions.ReadFeed);
        }

        [TestMethod]
        public async Task TestSplitAfterMerge_ChangeFeedAsync()
        {
            await this.TestSplitAfterMergeImplementationAsync(FeedDrainFunctions.ChangeFeed);
        }

        [TestMethod]
        public async Task TestSplitAfterMerge_QueryAsync()
        {
            await this.TestSplitAfterMergeImplementationAsync(FeedDrainFunctions.Query);
        }

        private async Task TestSplitAfterMergeImplementationAsync<TState>(
            DrainFunctions<TState> drainFunctions)
            where TState : State
        {
            IDocumentContainer documentContainer = this.CreateDocumentContainer(PartitionKeyDefinition);
            IReadOnlyList<FeedRangeInternal> ranges = await documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, cancellationToken: default);
            Assert.AreEqual(1, ranges.Count);
            await documentContainer.SplitAsync(ranges[0], cancellationToken: default);
            await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
            IReadOnlyList<FeedRangeInternal> childRanges = await documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, cancellationToken: default);
            Assert.AreEqual(2, childRanges.Count);

            int numItemsToInsert = 10;
            for (int i = 0; i < numItemsToInsert; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                await documentContainer.CreateItemAsync(item, cancellationToken: default);
            }

            await documentContainer.MergeAsync(childRanges[0], childRanges[1], cancellationToken: default);
            await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
            IReadOnlyList<FeedRangeInternal> mergedRanges = await documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, cancellationToken: default);
            Assert.AreEqual(1, mergedRanges.Count);

            (int firstPageCount, TState resumeState) = await drainFunctions.DrainOnePageAsync(documentContainer, mergedRanges[0]);

            await documentContainer.SplitAsync(mergedRanges[0], cancellationToken: default);
            await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
            childRanges = await documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, cancellationToken: default);
            Assert.AreEqual(2, childRanges.Count);

            int childCount1 = await drainFunctions.DrainAllPagesAsync(documentContainer, resumeState, childRanges[0]);
            int childCount2 = await drainFunctions.DrainAllPagesAsync(documentContainer, resumeState, childRanges[1]);

            Assert.AreEqual(numItemsToInsert, firstPageCount + childCount1 + childCount2);
        }

        [TestMethod]
        public async Task TestReadFeedAsync()
        {
            IDocumentContainer documentContainer = this.CreateDocumentContainer(PartitionKeyDefinition);

            int numItemsToInsert = 10;
            for (int i = 0; i < numItemsToInsert; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                await documentContainer.CreateItemAsync(item, cancellationToken: default);
            }

            List<FeedRangeEpk> ranges = await documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, cancellationToken: default);
            FeedRangeEpk range = ranges[0];

            {
                int count = 0;
                ReadFeedState readFeedState = ReadFeedState.Beginning();
                while (readFeedState != null)
                {
                    ReadFeedPage fullRangePage = await documentContainer.ReadFeedAsync(
                        new FeedRangeState<ReadFeedState>(range, readFeedState),
                        readFeedPaginationOptions: new ReadFeedPaginationOptions(pageSizeHint: 100),
                        NoOpTrace.Singleton,
                        cancellationToken: default);

                    readFeedState = fullRangePage.State;
                    count += fullRangePage.GetRecords().Count;
                }

                Assert.AreEqual(numItemsToInsert, count);
            }

            {
                ReadFeedPage partitionKeyPage = await documentContainer.ReadFeedAsync(
                    new FeedRangeState<ReadFeedState>(new FeedRangePartitionKey(new Cosmos.PartitionKey(0)), ReadFeedState.Beginning()),
                    readFeedPaginationOptions: new ReadFeedPaginationOptions(pageSizeHint: 100),
                    NoOpTrace.Singleton,
                    cancellationToken: default);
                Assert.AreEqual(1, partitionKeyPage.GetRecords().Count);
            }

            {
                PartitionKeyHash? start = range.Range.Min == string.Empty ? (PartitionKeyHash?)null : PartitionKeyHash.Parse(range.Range.Min);
                PartitionKeyHash? end = range.Range.Max == string.Empty ? (PartitionKeyHash?)null : PartitionKeyHash.Parse(range.Range.Max);
                PartitionKeyHashRange hashRange = new PartitionKeyHashRange(start, end);
                PartitionKeyHashRanges hashRanges = PartitionKeyHashRangeSplitterAndMerger.SplitRange(hashRange, rangeCount: 2);

                long sumChildCount = 0;
                foreach (PartitionKeyHashRange value in hashRanges)
                {
                    // Should get back only the document within the epk range.
                    ReadFeedPage partitionKeyRangePage = await documentContainer.ReadFeedAsync(
                        new FeedRangeState<ReadFeedState>(
                            new FeedRangeEpk(
                                new Documents.Routing.Range<string>(
                                    min: value.StartInclusive.HasValue ? value.StartInclusive.Value.ToString() : string.Empty,
                                    max: value.EndExclusive.HasValue ? value.EndExclusive.Value.ToString() : string.Empty,
                                    isMinInclusive: true,
                                    isMaxInclusive: false)),
                            ReadFeedState.Beginning()),
                        readFeedPaginationOptions: new ReadFeedPaginationOptions(pageSizeHint: 100),
                        NoOpTrace.Singleton,
                        cancellationToken: default);
                    sumChildCount += partitionKeyRangePage.GetRecords().Count;
                }

                Assert.AreEqual(numItemsToInsert, sumChildCount);
            }
        }

        [TestMethod]
        public async Task TestQueryAsync()
        {
            IDocumentContainer documentContainer = this.CreateDocumentContainer(PartitionKeyDefinition);

            int numItemsToInsert = 10;
            for (int i = 0; i < numItemsToInsert; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                await documentContainer.CreateItemAsync(item, cancellationToken: default);
            }

            List<FeedRangeEpk> ranges = await documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, cancellationToken: default);
            FeedRangeEpk range = ranges[0];

            {
                QueryPage fullRangePage = await documentContainer.QueryAsync(
                    sqlQuerySpec: new Cosmos.Query.Core.SqlQuerySpec("SELECT * FROM c"),
                    feedRangeState: new FeedRangeState<QueryState>(range, state: null),
                    queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: int.MaxValue),
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);
                Assert.AreEqual(numItemsToInsert, fullRangePage.Documents.Count);
            }

            {
                QueryPage partitionKeyPage = await documentContainer.QueryAsync(
                    sqlQuerySpec: new Cosmos.Query.Core.SqlQuerySpec("SELECT * FROM c"),
                    feedRangeState: new FeedRangeState<QueryState>(new FeedRangePartitionKey(new Cosmos.PartitionKey(0)), state: null),
                    queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: int.MaxValue),
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);
                Assert.AreEqual(1, partitionKeyPage.Documents.Count);
            }

            {
                PartitionKeyHash? start = range.Range.Min == string.Empty ? (PartitionKeyHash?)null : PartitionKeyHash.Parse(range.Range.Min);
                PartitionKeyHash? end = range.Range.Max == string.Empty ? (PartitionKeyHash?)null : PartitionKeyHash.Parse(range.Range.Max);
                PartitionKeyHashRange hashRange = new PartitionKeyHashRange(start, end);
                PartitionKeyHashRanges hashRanges = PartitionKeyHashRangeSplitterAndMerger.SplitRange(hashRange, rangeCount: 2);

                long sumChildCount = 0;
                foreach (PartitionKeyHashRange value in hashRanges)
                {
                    // Should get back only the document within the epk range.
                    QueryPage partitionKeyRangePage = await documentContainer.QueryAsync(
                        sqlQuerySpec: new Cosmos.Query.Core.SqlQuerySpec("SELECT * FROM c"),
                        feedRangeState: new FeedRangeState<QueryState>(
                            new FeedRangeEpk(
                                new Documents.Routing.Range<string>(
                                    min: value.StartInclusive.HasValue ? value.StartInclusive.Value.ToString() : string.Empty,
                                    max: value.EndExclusive.HasValue ? value.EndExclusive.Value.ToString() : string.Empty,
                                    isMinInclusive: true,
                                    isMaxInclusive: false)),
                            state: null),
                        queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: int.MaxValue),
                        NoOpTrace.Singleton,
                        cancellationToken: default);
                    sumChildCount += partitionKeyRangePage.Documents.Count;
                }

                Assert.AreEqual(numItemsToInsert, sumChildCount);
            }
        }

        [TestMethod]
        public async Task ValidateExceptionForMonadicChangeFeedAsync()
        {
            CosmosException dummyException = new CosmosException(
                message: "dummy",
                statusCode: HttpStatusCode.TooManyRequests,
                subStatusCode: 3200,
                activityId: "fakeId",
                requestCharge: 1.0);
            ResponseMessage message = new ResponseMessage(
                statusCode: HttpStatusCode.TooManyRequests,
                requestMessage: null,
                headers: null,
                cosmosException: dummyException,
                trace: NoOpTrace.Singleton);

            Mock<CosmosClientContext> mockCosmosClientContext = new Mock<CosmosClientContext>();
            mockCosmosClientContext.Setup(
                context => context.ProcessResourceOperationStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<OperationType>(),
                    It.IsAny<RequestOptions>(),
                    It.IsAny<ContainerInternal>(),
                    It.IsAny<Microsoft.Azure.Cosmos.FeedRange>(),
                    It.IsAny<Stream>(),
                    It.IsAny<Action<RequestMessage>>(),
                    It.IsAny<ITrace>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(message));
            Mock<ContainerInternal> mockContainerInternal = new Mock<ContainerInternal>();
            mockContainerInternal.SetupGet(container1 => container1.ClientContext).Returns(mockCosmosClientContext.Object);
            Mock<CosmosQueryClient> mockCosmosQueryClient = new Mock<CosmosQueryClient>();
            NetworkAttachedDocumentContainer container = new NetworkAttachedDocumentContainer(
                mockContainerInternal.Object,
                mockCosmosQueryClient.Object);

            FeedRangeState<ChangeFeedState> state = new FeedRangeState<ChangeFeedState>();
            ChangeFeedPaginationOptions options = new ChangeFeedPaginationOptions(ChangeFeedMode.Incremental);
            TryCatch<ChangeFeedPage> result = await container.MonadicChangeFeedAsync(
                state,
                options,
                NoOpTrace.Singleton,
                CancellationToken.None);

            Assert.IsNotNull(result.Exception);
            CosmosException ex = result.Exception.InnerException as CosmosException;
            Assert.IsNotNull(ex);
            Assert.AreSame(dummyException, ex);
        }

        private readonly struct DrainFunctions<TState>
            where TState : State
        {
            public DrainFunctions(
                Func<IDocumentContainer, FeedRangeInternal, Task<(int, TState)>> drainOnePageAsync,
                Func<IDocumentContainer, TState, FeedRangeInternal, Task<int>> drainAllPagesAsync)
            {
                this.DrainOnePageAsync = drainOnePageAsync ?? throw new ArgumentNullException(nameof(drainOnePageAsync));
                this.DrainAllPagesAsync = drainAllPagesAsync ?? throw new ArgumentNullException(nameof(drainAllPagesAsync));
            }

            public Func<IDocumentContainer, FeedRangeInternal, Task<(int, TState)>> DrainOnePageAsync { get; }
            public Func<IDocumentContainer, TState, FeedRangeInternal, Task<int>> DrainAllPagesAsync { get; }

            public async Task<int> DrainAllFromStartAsync(IDocumentContainer documentContainer, FeedRangeInternal feedRange)
            {
                (int firstPageCount, TState resumeState) = await this.DrainOnePageAsync(documentContainer, feedRange);
                int remainingPageCount = await this.DrainAllPagesAsync(documentContainer, resumeState, feedRange);

                return firstPageCount + remainingPageCount;
            }
        }
    }
}
