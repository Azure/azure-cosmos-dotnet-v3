//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class ParallelCrossPartitionQueryPipelineStageTests
    {
        [TestMethod]
        public void MonadicCreate_NullContinuationToken()
        {
            Mock<IDocumentContainer> mockDocumentContainer = new Mock<IDocumentContainer>();

            TryCatch<IQueryPipelineStage> monadicCreate = ParallelCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: mockDocumentContainer.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c"),
                targetRanges: new List<FeedRangeEpk>() { FeedRangeEpk.FullRange },
                queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: 10),
                partitionKey: null,
                containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                maxConcurrency: 10,
                prefetchPolicy: PrefetchPolicy.PrefetchSinglePage,
                cancellationToken: default,
                continuationToken: null);
            Assert.IsTrue(monadicCreate.Succeeded);
        }

        [TestMethod]
        public void MonadicCreate_NonCosmosArrayContinuationToken()
        {
            Mock<IDocumentContainer> mockDocumentContainer = new Mock<IDocumentContainer>();

            TryCatch<IQueryPipelineStage> monadicCreate = ParallelCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: mockDocumentContainer.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c"),
                targetRanges: new List<FeedRangeEpk>() { FeedRangeEpk.FullRange },
                queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: 10),
                partitionKey: null,
                containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                maxConcurrency: 10,
                prefetchPolicy: PrefetchPolicy.PrefetchSinglePage,
                cancellationToken: default,
                continuationToken: CosmosObject.Create(new Dictionary<string, CosmosElement>()));
            Assert.IsTrue(monadicCreate.Failed);
            Assert.IsTrue(monadicCreate.InnerMostException is MalformedContinuationTokenException);
        }

        [TestMethod]
        public void MonadicCreate_EmptyArrayContinuationToken()
        {
            Mock<IDocumentContainer> mockDocumentContainer = new Mock<IDocumentContainer>();

            TryCatch<IQueryPipelineStage> monadicCreate = ParallelCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: mockDocumentContainer.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c"),
                targetRanges: new List<FeedRangeEpk>() { FeedRangeEpk.FullRange },
                queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: 10),
                partitionKey: null,
                containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                maxConcurrency: 10,
                prefetchPolicy: PrefetchPolicy.PrefetchSinglePage,
                cancellationToken: default,
                continuationToken: CosmosArray.Create(new List<CosmosElement>()));
            Assert.IsTrue(monadicCreate.Failed);
            Assert.IsTrue(monadicCreate.InnerMostException is MalformedContinuationTokenException);
        }

        [TestMethod]
        public void MonadicCreate_NonParallelContinuationToken()
        {
            Mock<IDocumentContainer> mockDocumentContainer = new Mock<IDocumentContainer>();

            TryCatch<IQueryPipelineStage> monadicCreate = ParallelCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: mockDocumentContainer.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c"),
                targetRanges: new List<FeedRangeEpk>() { FeedRangeEpk.FullRange },
                queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: 10),
                partitionKey: null,
                containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                maxConcurrency: 10,
                prefetchPolicy: PrefetchPolicy.PrefetchSinglePage,
                cancellationToken: default,
                continuationToken: CosmosArray.Create(new List<CosmosElement>() { CosmosString.Create("asdf") }));
            Assert.IsTrue(monadicCreate.Failed);
            Assert.IsTrue(monadicCreate.InnerMostException is MalformedContinuationTokenException);
        }

        [TestMethod]
        public void MonadicCreate_SingleParallelContinuationToken()
        {
            Mock<IDocumentContainer> mockDocumentContainer = new Mock<IDocumentContainer>();

            ParallelContinuationToken token = new ParallelContinuationToken(
                token: "asdf",
                range: new Documents.Routing.Range<string>("A", "B", true, false));

            TryCatch<IQueryPipelineStage> monadicCreate = ParallelCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: mockDocumentContainer.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c"),
                targetRanges: new List<FeedRangeEpk>() { new FeedRangeEpk(new Documents.Routing.Range<string>(min: "A", max: "B", isMinInclusive: true, isMaxInclusive: false)) },
                queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: 10),
                partitionKey: null,
                containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                maxConcurrency: 10,
                prefetchPolicy: PrefetchPolicy.PrefetchSinglePage,
                cancellationToken: default,
                continuationToken: CosmosArray.Create(new List<CosmosElement>() { ParallelContinuationToken.ToCosmosElement(token) }));
            Assert.IsTrue(monadicCreate.Succeeded);
        }

        [TestMethod]
        public void MonadicCreate_MultipleParallelContinuationToken()
        {
            Mock<IDocumentContainer> mockDocumentContainer = new Mock<IDocumentContainer>();

            ParallelContinuationToken token1 = new ParallelContinuationToken(
                token: "asdf",
                range: new Documents.Routing.Range<string>("A", "B", true, false)); 

            ParallelContinuationToken token2 = new ParallelContinuationToken(
                token: "asdf",
                range: new Documents.Routing.Range<string>("B", "C", true, false));

            TryCatch<IQueryPipelineStage> monadicCreate = ParallelCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: mockDocumentContainer.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c"),
                targetRanges: new List<FeedRangeEpk>() 
                {
                    new FeedRangeEpk(new Documents.Routing.Range<string>(min: "A", max: "B", isMinInclusive: true, isMaxInclusive: false)),
                    new FeedRangeEpk(new Documents.Routing.Range<string>(min: "B", max: "C", isMinInclusive: true, isMaxInclusive: false)),
                },
                queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: 10),
                partitionKey: null,
                containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                maxConcurrency: 10,
                prefetchPolicy: PrefetchPolicy.PrefetchSinglePage,
                cancellationToken: default,
                continuationToken: CosmosArray.Create(
                    new List<CosmosElement>()
                    {
                        ParallelContinuationToken.ToCosmosElement(token1),
                        ParallelContinuationToken.ToCosmosElement(token2)
                    }));
            Assert.IsTrue(monadicCreate.Succeeded);
        }

        [TestMethod]
        [DataRow(false, false, false, false, DisplayName = "Use State: false, Allow Splits: false, Allow Merges: false")]
        [DataRow(false, false, true, false, DisplayName = "Use State: false, Allow Splits: false, Allow Merges: true")]
        [DataRow(false, true, false, false, DisplayName = "Use State: false, Allow Splits: true, Allow Merges: false")]
        [DataRow(false, true, true, false, DisplayName = "Use State: false, Allow Splits: true, Allow Merges: true")]
        [DataRow(true, false, false, false, DisplayName = "Use State: true, Allow Splits: false, Allow Merges: false")]
        [DataRow(true, false, true, false, DisplayName = "Use State: true, Allow Splits: false, Allow Merges: true")]
        [DataRow(true, true, false, false, DisplayName = "Use State: true, Allow Splits: true, Allow Merges: false")]
        [DataRow(true, true, true, false, DisplayName = "Use State: true, Allow Splits: true, Allow Merges: true")]
        [DataRow(false, false, false, true, DisplayName = "Use State: false, Allow Splits: false, Allow Merges: false")]
        [DataRow(false, false, true, true, DisplayName = "Use State: false, Allow Splits: false, Allow Merges: true")]
        [DataRow(false, true, false, true, DisplayName = "Use State: false, Allow Splits: true, Allow Merges: false")]
        [DataRow(false, true, true, true, DisplayName = "Use State: false, Allow Splits: true, Allow Merges: true")]
        [DataRow(true, false, false, true, DisplayName = "Use State: true, Allow Splits: false, Allow Merges: false")]
        [DataRow(true, false, true, true, DisplayName = "Use State: true, Allow Splits: false, Allow Merges: true")]
        [DataRow(true, true, false, true, DisplayName = "Use State: true, Allow Splits: true, Allow Merges: false")]
        [DataRow(true, true, true, true, DisplayName = "Use State: true, Allow Splits: true, Allow Merges: true")]
        public async Task TestDrainWithStateSplitsAndMergeAsync(bool useState, bool allowSplits, bool allowMerges, bool aggressivePrefetch)
        {
            async Task<IQueryPipelineStage> CreatePipelineStateAsync(IDocumentContainer documentContainer, CosmosElement continuationToken)
            {
                TryCatch<IQueryPipelineStage> monadicQueryPipelineStage = ParallelCrossPartitionQueryPipelineStage.MonadicCreate(
                    documentContainer: documentContainer,
                    sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c"),
                    targetRanges: await documentContainer.GetFeedRangesAsync(
                        trace: NoOpTrace.Singleton,
                        cancellationToken: default),
                    queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: 10),
                    partitionKey: null,
                    containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                    maxConcurrency: 10,
                    prefetchPolicy: aggressivePrefetch ? PrefetchPolicy.PrefetchAll : PrefetchPolicy.PrefetchSinglePage,
                    cancellationToken: default,
                    continuationToken: continuationToken);
                Assert.IsTrue(monadicQueryPipelineStage.Succeeded);
                IQueryPipelineStage queryPipelineStage = monadicQueryPipelineStage.Result;

                return queryPipelineStage;
            }

            int numItems = 1000;
            IDocumentContainer inMemoryCollection = await CreateDocumentContainerAsync(numItems);
            IQueryPipelineStage queryPipelineStage = await CreatePipelineStateAsync(inMemoryCollection, continuationToken: null);
            List<CosmosElement> documents = new List<CosmosElement>();
            Random random = new Random();
            while (await queryPipelineStage.MoveNextAsync(NoOpTrace.Singleton))
            {
                TryCatch<QueryPage> tryGetPage = queryPipelineStage.Current;
                tryGetPage.ThrowIfFailed();

                documents.AddRange(tryGetPage.Result.Documents);

                if (useState)
                {
                    if (tryGetPage.Result.State == null)
                    {
                        break;
                    }

                    queryPipelineStage = await CreatePipelineStateAsync(inMemoryCollection, continuationToken: tryGetPage.Result.State.Value);
                }

                if (random.Next() % 2 == 0)
                {
                    if (allowSplits && (random.Next() % 2 == 0))
                    {
                        // Split
                        await inMemoryCollection.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
                        List<FeedRangeEpk> ranges = await inMemoryCollection.GetFeedRangesAsync(
                            trace: NoOpTrace.Singleton,
                            cancellationToken: default);
                        FeedRangeInternal randomRangeToSplit = ranges[random.Next(0, ranges.Count)];
                        await inMemoryCollection.SplitAsync(randomRangeToSplit, cancellationToken: default);
                    }

                    if (allowMerges && (random.Next() % 2 == 0))
                    {
                        // Merge
                        await inMemoryCollection.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
                        List<FeedRangeEpk> ranges = await inMemoryCollection.GetFeedRangesAsync(
                            trace: NoOpTrace.Singleton,
                            cancellationToken: default);
                        if (ranges.Count > 1)
                        {
                            ranges = ranges.OrderBy(range => range.Range.Min).ToList();
                            int indexToMerge = random.Next(0, ranges.Count);
                            int adjacentIndex = indexToMerge == (ranges.Count - 1) ? indexToMerge - 1 : indexToMerge + 1;
                            await inMemoryCollection.MergeAsync(ranges[indexToMerge], ranges[adjacentIndex], cancellationToken: default);
                        }
                    }
                }
            }

            Assert.AreEqual(numItems, documents.Count);
        }

        private static async Task<IDocumentContainer> CreateDocumentContainerAsync(
            int numItems,
            FlakyDocumentContainer.FailureConfigs failureConfigs = null)
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
                IReadOnlyList<FeedRangeInternal> ranges = await documentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton, 
                    cancellationToken: default);
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
