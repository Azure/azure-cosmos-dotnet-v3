//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Remote;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Documents;
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
                targetRanges: new List<PartitionKeyRange>() { new PartitionKeyRange() },
                orderByColumns: new List<OrderByCrossPartitionQueryPipelineStage.OrderByColumn>()
                {
                    new OrderByCrossPartitionQueryPipelineStage.OrderByColumn("_ts", Cosmos.Query.Core.ExecutionContext.OrderBy.SortOrder.Ascending)
                },
                pageSize: 10,
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
                targetRanges: new List<PartitionKeyRange>() { new PartitionKeyRange() },
                orderByColumns: new List<OrderByCrossPartitionQueryPipelineStage.OrderByColumn>()
                {
                    new OrderByCrossPartitionQueryPipelineStage.OrderByColumn("_ts", Cosmos.Query.Core.ExecutionContext.OrderBy.SortOrder.Ascending)
                },
                pageSize: 10,
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
                targetRanges: new List<PartitionKeyRange>() { new PartitionKeyRange() },
                orderByColumns: new List<OrderByCrossPartitionQueryPipelineStage.OrderByColumn>()
                {
                    new OrderByCrossPartitionQueryPipelineStage.OrderByColumn("_ts", Cosmos.Query.Core.ExecutionContext.OrderBy.SortOrder.Ascending)
                },
                pageSize: 10,
                continuationToken: CosmosArray.Create(new List<CosmosElement>()));
            Assert.IsTrue(monadicCreate.Failed);
            Assert.IsTrue(monadicCreate.InnerMostException is MalformedContinuationTokenException);
        }

        [TestMethod]
        public void MonadicCreate_NonCompositeContinuationToken()
        {
            Mock<IDocumentContainer> mockDocumentContainer = new Mock<IDocumentContainer>();

            TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: mockDocumentContainer.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c ORDER BY c._ts"),
                targetRanges: new List<PartitionKeyRange>() { new PartitionKeyRange() },
                orderByColumns: new List<OrderByCrossPartitionQueryPipelineStage.OrderByColumn>()
                {
                    new OrderByCrossPartitionQueryPipelineStage.OrderByColumn("_ts", Cosmos.Query.Core.ExecutionContext.OrderBy.SortOrder.Ascending)
                },
                pageSize: 10,
                continuationToken: CosmosArray.Create(new List<CosmosElement>() { CosmosString.Create("asdf") }));
            Assert.IsTrue(monadicCreate.Failed);
            Assert.IsTrue(monadicCreate.InnerMostException is MalformedContinuationTokenException);
        }

        [TestMethod]
        public void MonadicCreate_SingleOrderByContinuationToken()
        {
            Mock<IDocumentContainer> mockDocumentContainer = new Mock<IDocumentContainer>();

            CompositeContinuationToken compositeContinuationToken = new CompositeContinuationToken()
            {
                Range = new Documents.Routing.Range<string>("A", "B", true, false),
                Token = "asdf",
            };

            OrderByContinuationToken orderByContinuationToken = new OrderByContinuationToken(
                compositeContinuationToken,
                new List<OrderByItem>() { new OrderByItem(CosmosObject.Create(new Dictionary<string, CosmosElement>() { { "item", CosmosString.Create("asdf") } })) },
                rid: "rid",
                skipCount: 42,
                filter: "filter");

            TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: mockDocumentContainer.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c ORDER BY c._ts"),
                targetRanges: new List<PartitionKeyRange>() { new PartitionKeyRange() { Id = "0", MinInclusive = "A", MaxExclusive = "B" } },
                orderByColumns: new List<OrderByCrossPartitionQueryPipelineStage.OrderByColumn>()
                {
                    new OrderByCrossPartitionQueryPipelineStage.OrderByColumn("_ts", Cosmos.Query.Core.ExecutionContext.OrderBy.SortOrder.Ascending)
                },
                pageSize: 10,
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

            CompositeContinuationToken token = new CompositeContinuationToken()
            {
                Range = new Documents.Routing.Range<string>("A", "B", true, false),
                Token = "asdf",
            };

            CompositeContinuationToken compositeContinuationToken1 = new CompositeContinuationToken()
            {
                Range = new Documents.Routing.Range<string>("A", "B", true, false),
                Token = "asdf",
            };

            OrderByContinuationToken orderByContinuationToken1 = new OrderByContinuationToken(
                compositeContinuationToken1,
                new List<OrderByItem>() { new OrderByItem(CosmosObject.Create(new Dictionary<string, CosmosElement>() { { "item", CosmosString.Create("asdf") } })) },
                rid: "rid",
                skipCount: 42,
                filter: "filter");

            CompositeContinuationToken compositeContinuationToken2 = new CompositeContinuationToken()
            {
                Range = new Documents.Routing.Range<string>("B", "C", true, false),
                Token = "asdf",
            };

            OrderByContinuationToken orderByContinuationToken2 = new OrderByContinuationToken(
                compositeContinuationToken2,
                new List<OrderByItem>() { new OrderByItem(CosmosObject.Create(new Dictionary<string, CosmosElement>() { { "item", CosmosString.Create("asdf") } })) },
                rid: "rid",
                skipCount: 42,
                filter: "filter");

            TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: mockDocumentContainer.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c ORDER BY c._ts"),
                targetRanges: new List<PartitionKeyRange>()
                {
                    new PartitionKeyRange() { Id = "0", MinInclusive = "A", MaxExclusive = "B" },
                    new PartitionKeyRange() { Id = "1", MinInclusive = "B", MaxExclusive = "C" }
                },
                orderByColumns: new List<OrderByCrossPartitionQueryPipelineStage.OrderByColumn>()
                {
                    new OrderByCrossPartitionQueryPipelineStage.OrderByColumn("_ts", Cosmos.Query.Core.ExecutionContext.OrderBy.SortOrder.Ascending)
                },
                pageSize: 10,
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
            int numItems = 100;
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(numItems);

            TryCatch<IQueryPipelineStage> monadicCreate = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                documentContainer: documentContainer,
                sqlQuerySpec: new SqlQuerySpec("SELECT r._rid, [{\"item\": c._ts}] AS orderByItems, c AS payload FROM Root AS c ORDER BY c._ts"),
                targetRanges: await documentContainer.GetFeedRangesAsync(cancellationToken: default),
                orderByColumns: new List<OrderByCrossPartitionQueryPipelineStage.OrderByColumn>()
                {
                    new OrderByCrossPartitionQueryPipelineStage.OrderByColumn("_ts", SortOrder.Ascending)
                },
                pageSize: 10,
                continuationToken: null);
            Assert.IsTrue(monadicCreate.Succeeded);
            IQueryPipelineStage queryPipelineStage = monadicCreate.Result;

            List<CosmosElement> documents = new List<CosmosElement>();
            while (await queryPipelineStage.MoveNextAsync())
            {
                TryCatch<QueryPage> tryGetQueryPage = queryPipelineStage.Current;
                Assert.IsTrue(tryGetQueryPage.Succeeded);

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
                TryCatch<IQueryPipelineStage> monadicCreate = ParallelCrossPartitionQueryPipelineStage.MonadicCreate(
                    documentContainer: documentContainer,
                    sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c"),
                    pageSize: 10,
                    continuationToken: queryState?.Value);
                Assert.IsTrue(monadicCreate.Succeeded);
                IQueryPipelineStage queryPipelineStage = monadicCreate.Result;

                Assert.IsTrue(await queryPipelineStage.MoveNextAsync());
                TryCatch<QueryPage> tryGetQueryPage = queryPipelineStage.Current;
                Assert.IsTrue(tryGetQueryPage.Succeeded);

                QueryPage queryPage = tryGetQueryPage.Result;
                documents.AddRange(queryPage.Documents);

                queryState = queryPage.State;
            } while (queryState != null);

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

            await documentContainer.SplitAsync(partitionKeyRangeId: 0, cancellationToken: default);

            await documentContainer.SplitAsync(partitionKeyRangeId: 1, cancellationToken: default);
            await documentContainer.SplitAsync(partitionKeyRangeId: 2, cancellationToken: default);

            await documentContainer.SplitAsync(partitionKeyRangeId: 3, cancellationToken: default);
            await documentContainer.SplitAsync(partitionKeyRangeId: 4, cancellationToken: default);
            await documentContainer.SplitAsync(partitionKeyRangeId: 5, cancellationToken: default);
            await documentContainer.SplitAsync(partitionKeyRangeId: 6, cancellationToken: default);

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
