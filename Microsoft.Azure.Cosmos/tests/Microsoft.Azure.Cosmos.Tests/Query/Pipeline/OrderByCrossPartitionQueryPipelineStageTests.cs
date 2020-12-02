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
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class OrderByCrossPartitionQueryPipelineStageTests
    {
        [TestMethod]
        public void MonadicCreate_NullContinuationToken()
        {
            Mock<IDocumentContainer> mockDocumentContainer = new Mock<IDocumentContainer>();

            TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: mockDocumentContainer.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c ORDER BY c._ts"),
                targetRanges: new List<FeedRangeEpk>() { FeedRangeEpk.FullRange },
                partitionKey: null,
                orderByColumns: new List<OrderByColumn>()
                {
                    new OrderByColumn("_ts", SortOrder.Ascending)
                },
                pageSize: 10,
                maxConcurrency: 10,
                cancellationToken: default,
                continuationToken: null);
            Assert.IsTrue(monadicCreate.Succeeded);
        }

        [TestMethod]
        public void MonadicCreate_NonCosmosArrayContinuationToken()
        {
            Mock<IDocumentContainer> mockDocumentContainer = new Mock<IDocumentContainer>();

            TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: mockDocumentContainer.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c ORDER BY c._ts"),
                targetRanges: new List<FeedRangeEpk>() { FeedRangeEpk.FullRange },
                partitionKey: null,
                orderByColumns: new List<OrderByColumn>()
                {
                    new OrderByColumn("_ts", SortOrder.Ascending)
                },
                pageSize: 10,
                maxConcurrency: 10,
                cancellationToken: default,
                continuationToken: CosmosObject.Create(new Dictionary<string, CosmosElement>()));
            Assert.IsTrue(monadicCreate.Failed);
            Assert.IsTrue(monadicCreate.InnerMostException is MalformedContinuationTokenException);
        }

        [TestMethod]
        public void MonadicCreate_EmptyArrayContinuationToken()
        {
            Mock<IDocumentContainer> mockDocumentContainer = new Mock<IDocumentContainer>();

            TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: mockDocumentContainer.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c ORDER BY c._ts"),
                targetRanges: new List<FeedRangeEpk>() { FeedRangeEpk.FullRange },
                partitionKey: null,
                orderByColumns: new List<OrderByColumn>()
                {
                    new OrderByColumn("_ts", SortOrder.Ascending)
                },
                pageSize: 10,
                maxConcurrency: 10,
                cancellationToken: default,
                continuationToken: CosmosArray.Create(new List<CosmosElement>()));
            Assert.IsTrue(monadicCreate.Failed);
            Assert.IsTrue(monadicCreate.InnerMostException is MalformedContinuationTokenException);
        }

        [TestMethod]
        public void MonadicCreate_NonParallelContinuationToken()
        {
            Mock<IDocumentContainer> mockDocumentContainer = new Mock<IDocumentContainer>();

            TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: mockDocumentContainer.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c ORDER BY c._ts"),
                targetRanges: new List<FeedRangeEpk>() { FeedRangeEpk.FullRange },
                partitionKey: null,
                orderByColumns: new List<OrderByColumn>()
                {
                    new OrderByColumn("_ts", SortOrder.Ascending)
                },
                pageSize: 10,
                maxConcurrency: 10,
                cancellationToken: default,
                continuationToken: CosmosArray.Create(new List<CosmosElement>() { CosmosString.Create("asdf") }));
            Assert.IsTrue(monadicCreate.Failed);
            Assert.IsTrue(monadicCreate.InnerMostException is MalformedContinuationTokenException);
        }

        [TestMethod]
        public void MonadicCreate_SingleOrderByContinuationToken()
        {
            Mock<IDocumentContainer> mockDocumentContainer = new Mock<IDocumentContainer>();

            ParallelContinuationToken parallelContinuationToken = new ParallelContinuationToken(
                token: "asdf",
                range: new Documents.Routing.Range<string>("A", "B", true, false));

            OrderByContinuationToken orderByContinuationToken = new OrderByContinuationToken(
                parallelContinuationToken,
                new List<OrderByItem>() { new OrderByItem(CosmosObject.Create(new Dictionary<string, CosmosElement>() { { "item", CosmosString.Create("asdf") } })) },
                rid: "rid",
                skipCount: 42,
                filter: "filter");

            TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: mockDocumentContainer.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c ORDER BY c._ts"),
                targetRanges: new List<FeedRangeEpk>() { new FeedRangeEpk(new Range<string>(min: "A", max: "B", isMinInclusive: true, isMaxInclusive: false)) },
                partitionKey: null,
                orderByColumns: new List<OrderByColumn>()
                {
                    new OrderByColumn("_ts", SortOrder.Ascending)
                },
                pageSize: 10,
                maxConcurrency: 10,
                cancellationToken: default,
                continuationToken: CosmosArray.Create(
                    new List<CosmosElement>()
                    {
                        OrderByContinuationToken.ToCosmosElement(orderByContinuationToken)
                    }));
            Assert.IsTrue(monadicCreate.Succeeded);
        }

        [TestMethod]
        public void MonadicCreate_MultipleOrderByContinuationToken()
        {
            Mock<IDocumentContainer> mockDocumentContainer = new Mock<IDocumentContainer>();

            ParallelContinuationToken parallelContinuationToken1 = new ParallelContinuationToken(
                token: "asdf",
                range: new Documents.Routing.Range<string>("A", "B", true, false));

            OrderByContinuationToken orderByContinuationToken1 = new OrderByContinuationToken(
                parallelContinuationToken1,
                new List<OrderByItem>() { new OrderByItem(CosmosObject.Create(new Dictionary<string, CosmosElement>() { { "item", CosmosString.Create("asdf") } })) },
                rid: "rid",
                skipCount: 42,
                filter: "filter");

            ParallelContinuationToken parallelContinuationToken2 = new ParallelContinuationToken(
                token: "asdf",
                range: new Documents.Routing.Range<string>("B", "C", true, false));

            OrderByContinuationToken orderByContinuationToken2 = new OrderByContinuationToken(
                parallelContinuationToken2,
                new List<OrderByItem>() { new OrderByItem(CosmosObject.Create(new Dictionary<string, CosmosElement>() { { "item", CosmosString.Create("asdf") } })) },
                rid: "rid",
                skipCount: 42,
                filter: "filter");

            TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: mockDocumentContainer.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c ORDER BY c._ts"),
                targetRanges: new List<FeedRangeEpk>()
                {
                    new FeedRangeEpk(new Range<string>(min: "A", max: "B", isMinInclusive: true, isMaxInclusive: false)),
                    new FeedRangeEpk(new Range<string>(min: "B", max: "C", isMinInclusive: true, isMaxInclusive: false)),
                },
                partitionKey: null,
                orderByColumns: new List<OrderByColumn>()
                {
                    new OrderByColumn("_ts", SortOrder.Ascending)
                },
                pageSize: 10,
                maxConcurrency: 10,
                cancellationToken: default,
                continuationToken: CosmosArray.Create(
                    new List<CosmosElement>()
                    {
                        OrderByContinuationToken.ToCosmosElement(orderByContinuationToken1),
                        OrderByContinuationToken.ToCosmosElement(orderByContinuationToken2)
                    }));
            Assert.IsTrue(monadicCreate.Succeeded);
        }

        [TestMethod]
        public async Task TestDrainFully_StartFromBeginingAsync()
        {
            int numItems = 1000;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);

            TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: documentContainer,
                sqlQuerySpec: new SqlQuerySpec(@"
                    SELECT c._rid AS _rid, [{""item"": c._ts}] AS orderByItems, c AS payload
                    FROM c
                    WHERE {documentdb-formattableorderbyquery-filter}
                    ORDER BY c._ts"),
                targetRanges: await documentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton, 
                    cancellationToken: default),
                partitionKey: null,
                orderByColumns: new List<OrderByColumn>()
                {
                    new OrderByColumn("c._ts", SortOrder.Ascending)
                },
                pageSize: 10,
                maxConcurrency: 10,
                cancellationToken: default,
                continuationToken: null);
            Assert.IsTrue(monadicCreate.Succeeded);
            IQueryPipelineStage queryPipelineStage = monadicCreate.Result;

            List<CosmosElement> documents = new List<CosmosElement>();
            while (await queryPipelineStage.MoveNextAsync())
            {
                TryCatch<QueryPage> tryGetQueryPage = queryPipelineStage.Current;
                if (tryGetQueryPage.Failed)
                {
                    Assert.Fail(tryGetQueryPage.Exception.ToString());
                }

                QueryPage queryPage = tryGetQueryPage.Result;
                documents.AddRange(queryPage.Documents);
            }

            Assert.AreEqual(numItems, documents.Count);
            Assert.IsTrue(documents.OrderBy(document => ((CosmosObject)document)["_ts"]).ToList().SequenceEqual(documents));
        }

        [TestMethod]
        public async Task TestDrainFully_StartFromBeginingAsync_NoDocuments()
        {
            int numItems = 0;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);

            TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: documentContainer,
                sqlQuerySpec: new SqlQuerySpec(@"
                    SELECT c._rid AS _rid, [{""item"": c._ts}] AS orderByItems, c AS payload
                    FROM c
                    WHERE {documentdb-formattableorderbyquery-filter}
                    ORDER BY c._ts"),
                targetRanges: await documentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton, 
                    cancellationToken: default),
                partitionKey: null,
                orderByColumns: new List<OrderByColumn>()
                {
                    new OrderByColumn("c._ts", SortOrder.Ascending)
                },
                pageSize: 10,
                maxConcurrency: 10,
                cancellationToken: default,
                continuationToken: null);
            Assert.IsTrue(monadicCreate.Succeeded);
            IQueryPipelineStage queryPipelineStage = monadicCreate.Result;

            List<CosmosElement> documents = new List<CosmosElement>();
            while (await queryPipelineStage.MoveNextAsync())
            {
                TryCatch<QueryPage> tryGetQueryPage = queryPipelineStage.Current;
                if (tryGetQueryPage.Failed)
                {
                    Assert.Fail(tryGetQueryPage.Exception.ToString());
                }

                QueryPage queryPage = tryGetQueryPage.Result;
                documents.AddRange(queryPage.Documents);
            }

            Assert.AreEqual(numItems, documents.Count);
            Assert.IsTrue(documents.OrderBy(document => ((CosmosObject)document)["_ts"]).ToList().SequenceEqual(documents));
        }

        [TestMethod]
        public async Task TestDrainFully_WithStateResume()
        {
            int numItems = 1000;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);

            List<CosmosElement> documents = new List<CosmosElement>();

            QueryState queryState = null;
            do
            {
                TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                    documentContainer: documentContainer,
                    sqlQuerySpec: new SqlQuerySpec(@"
                        SELECT c._rid AS _rid, [{""item"": c._ts}] AS orderByItems, c AS payload
                        FROM c
                        WHERE {documentdb-formattableorderbyquery-filter}
                        ORDER BY c._ts"),
                    targetRanges: await documentContainer.GetFeedRangesAsync(
                        trace: NoOpTrace.Singleton, 
                        cancellationToken: default),
                    partitionKey: null,
                    orderByColumns: new List<OrderByColumn>()
                    {
                    new OrderByColumn("c._ts", SortOrder.Ascending)
                    },
                    pageSize: 10,
                    maxConcurrency: 10,
                    cancellationToken: default,
                    continuationToken: queryState?.Value);
                Assert.IsTrue(monadicCreate.Succeeded);
                IQueryPipelineStage queryPipelineStage = monadicCreate.Result;

                QueryPage queryPage;
                do
                {
                    // We need to drain out all the initial empty pages,
                    // since they are non resumable state.
                    Assert.IsTrue(await queryPipelineStage.MoveNextAsync());
                    TryCatch<QueryPage> tryGetQueryPage = queryPipelineStage.Current;
                    if (tryGetQueryPage.Failed)
                    {
                        Assert.Fail(tryGetQueryPage.Exception.ToString());
                    }

                    queryPage = tryGetQueryPage.Result;
                    documents.AddRange(queryPage.Documents);
                    queryState = queryPage.State;
                } while ((queryPage.Documents.Count == 0) && (queryState != null));
            } while (queryState != null);

            Assert.AreEqual(numItems, documents.Count);
            Assert.IsTrue(documents.OrderBy(document => ((CosmosObject)document)["_ts"]).ToList().SequenceEqual(documents));
        }

        [TestMethod]
        public async Task TestDrainFully_WithSplits()
        {
            int numItems = 1000;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);

            TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: documentContainer,
                sqlQuerySpec: new SqlQuerySpec(@"
                    SELECT c._rid AS _rid, [{""item"": c._ts}] AS orderByItems, c AS payload
                    FROM c
                    WHERE {documentdb-formattableorderbyquery-filter}
                    ORDER BY c._ts"),
                targetRanges: await documentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton, 
                    cancellationToken: default),
                partitionKey: null,
                orderByColumns: new List<OrderByColumn>()
                {
                    new OrderByColumn("c._ts", SortOrder.Ascending)
                },
                pageSize: 10,
                maxConcurrency: 10,
                cancellationToken: default,
                continuationToken: null);
            Assert.IsTrue(monadicCreate.Succeeded);
            IQueryPipelineStage queryPipelineStage = monadicCreate.Result;

            Random random = new Random();
            List<CosmosElement> documents = new List<CosmosElement>();
            while (await queryPipelineStage.MoveNextAsync())
            {
                TryCatch<QueryPage> tryGetQueryPage = queryPipelineStage.Current;
                if (tryGetQueryPage.Failed)
                {
                    Assert.Fail(tryGetQueryPage.Exception.ToString());
                }

                QueryPage queryPage = tryGetQueryPage.Result;
                documents.AddRange(queryPage.Documents);

                if (random.Next() % 4 == 0)
                {
                    // Can not always split otherwise the split handling code will livelock trying to split proof every partition in a cycle.
                    await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
                    List<FeedRangeEpk> ranges = documentContainer.GetFeedRangesAsync(
                        trace: NoOpTrace.Singleton, 
                        cancellationToken: default).Result;
                    FeedRangeInternal randomRange = ranges[random.Next(ranges.Count)];
                    await documentContainer.SplitAsync(randomRange, cancellationToken: default);
                }
            }

            Assert.AreEqual(numItems, documents.Count);
            Assert.IsTrue(documents.OrderBy(document => ((CosmosObject)document)["_ts"]).ToList().SequenceEqual(documents));
        }

        [TestMethod]
        public async Task TestDrainFully_WithSplit_WithStateResume()
        {
            int numItems = 1000;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);

            int seed = new Random().Next();
            Random random = new Random(seed);
            List<CosmosElement> documents = new List<CosmosElement>();
            QueryState queryState = null;

            do
            {
                TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                    documentContainer: documentContainer,
                    sqlQuerySpec: new SqlQuerySpec(@"
                        SELECT c._rid AS _rid, [{""item"": c._ts}] AS orderByItems, c AS payload
                        FROM c
                        WHERE {documentdb-formattableorderbyquery-filter}
                        ORDER BY c._ts"),
                    targetRanges: await documentContainer.GetFeedRangesAsync(
                        trace: NoOpTrace.Singleton, 
                        cancellationToken: default),
                    partitionKey: null,
                    orderByColumns: new List<OrderByColumn>()
                    {
                        new OrderByColumn("c._ts", SortOrder.Ascending)
                    },
                    pageSize: 10,
                    maxConcurrency: 10,
                    cancellationToken: default,
                    continuationToken: queryState?.Value);
                if (monadicCreate.Failed)
                {
                    Assert.Fail(monadicCreate.Exception.ToString());
                }

                IQueryPipelineStage queryPipelineStage = monadicCreate.Result;

                QueryPage queryPage;
                do
                {
                    // We need to drain out all the initial empty pages,
                    // since they are non resumable state.
                    Assert.IsTrue(await queryPipelineStage.MoveNextAsync());
                    TryCatch<QueryPage> tryGetQueryPage = queryPipelineStage.Current;
                    if (tryGetQueryPage.Failed)
                    {
                        Assert.Fail(tryGetQueryPage.Exception.ToString());
                    }

                    queryPage = tryGetQueryPage.Result;
                    documents.AddRange(queryPage.Documents);
                    queryState = queryPage.State;
                } while ((queryPage.Documents.Count == 0) && (queryState != null));

                // Split
                List<FeedRangeEpk> ranges = documentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton, 
                    cancellationToken: default).Result;
                FeedRangeInternal randomRange = ranges[random.Next(ranges.Count)];
                await documentContainer.SplitAsync(randomRange, cancellationToken: default);
            } while (queryState != null);

            Assert.AreEqual(numItems, documents.Count, $"Failed with seed: {seed}. got {documents.Count} documents when {numItems} was expected.");
            Assert.IsTrue(documents.OrderBy(document => ((CosmosObject)document)["_ts"]).ToList().SequenceEqual(documents), $"Failed with seed: {seed}");
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
                await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
                IReadOnlyList<FeedRangeInternal> ranges = await documentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton, 
                    cancellationToken: default);
                foreach (FeedRangeInternal range in ranges)
                {
                    await documentContainer.SplitAsync(range, cancellationToken: default);
                }
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
