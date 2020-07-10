//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Remote;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class ParallelCrossPartitionQueryPipelineStageTests
    {
        [TestMethod]
        public void MonadicCreate_NullContinuationToken()
        {
            Mock<IFeedRangeProvider> mockFeedRangeProvider = new Mock<IFeedRangeProvider>();
            Mock<IQueryDataSource> mockQueryDataSource = new Mock<IQueryDataSource>();
            TryCatch<IQueryPipelineStage> monadicCreate = ParallelCrossPartitionQueryPipelineStage.MonadicCreate(
                feedRangeProvider: mockFeedRangeProvider.Object,
                queryDataSource: mockQueryDataSource.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c"),
                pageSize: 10,
                continuationToken: null);
            Assert.IsTrue(monadicCreate.Succeeded);
        }

        [TestMethod]
        public void MonadicCreate_NonCosmosArrayContinuationToken()
        {
            Mock<IFeedRangeProvider> mockFeedRangeProvider = new Mock<IFeedRangeProvider>();
            Mock<IQueryDataSource> mockQueryDataSource = new Mock<IQueryDataSource>();
            TryCatch<IQueryPipelineStage> monadicCreate = ParallelCrossPartitionQueryPipelineStage.MonadicCreate(
                feedRangeProvider: mockFeedRangeProvider.Object,
                queryDataSource: mockQueryDataSource.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c"),
                pageSize: 10,
                continuationToken: CosmosObject.Create(new Dictionary<string, CosmosElement>()));
            Assert.IsTrue(monadicCreate.Failed);
            Assert.IsTrue(monadicCreate.InnerMostException is MalformedContinuationTokenException);
        }

        [TestMethod]
        public void MonadicCreate_EmptyArrayContinuationToken()
        {
            Mock<IFeedRangeProvider> mockFeedRangeProvider = new Mock<IFeedRangeProvider>();
            Mock<IQueryDataSource> mockQueryDataSource = new Mock<IQueryDataSource>();
            TryCatch<IQueryPipelineStage> monadicCreate = ParallelCrossPartitionQueryPipelineStage.MonadicCreate(
                feedRangeProvider: mockFeedRangeProvider.Object,
                queryDataSource: mockQueryDataSource.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c"),
                pageSize: 10,
                continuationToken: CosmosArray.Create(new List<CosmosElement>()));
            Assert.IsTrue(monadicCreate.Failed);
            Assert.IsTrue(monadicCreate.InnerMostException is MalformedContinuationTokenException);
        }

        [TestMethod]
        public void MonadicCreate_NonCompositeContinuationToken()
        {
            Mock<IFeedRangeProvider> mockFeedRangeProvider = new Mock<IFeedRangeProvider>();
            Mock<IQueryDataSource> mockQueryDataSource = new Mock<IQueryDataSource>();
            TryCatch<IQueryPipelineStage> monadicCreate = ParallelCrossPartitionQueryPipelineStage.MonadicCreate(
                feedRangeProvider: mockFeedRangeProvider.Object,
                queryDataSource: mockQueryDataSource.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c"),
                pageSize: 10,
                continuationToken: CosmosArray.Create(new List<CosmosElement>() { CosmosString.Create("asdf") }));
            Assert.IsTrue(monadicCreate.Failed);
            Assert.IsTrue(monadicCreate.InnerMostException is MalformedContinuationTokenException);
        }

        [TestMethod]
        public void MonadicCreate_SingleCompositeContinuationToken()
        {
            Mock<IFeedRangeProvider> mockFeedRangeProvider = new Mock<IFeedRangeProvider>();
            Mock<IQueryDataSource> mockQueryDataSource = new Mock<IQueryDataSource>();

            CompositeContinuationToken token = new CompositeContinuationToken()
            {
                Range = new Documents.Routing.Range<string>("A", "B", true, false),
                Token = "asdf",
            };

            TryCatch<IQueryPipelineStage> monadicCreate = ParallelCrossPartitionQueryPipelineStage.MonadicCreate(
                feedRangeProvider: mockFeedRangeProvider.Object,
                queryDataSource: mockQueryDataSource.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c"),
                pageSize: 10,
                continuationToken: CosmosArray.Create(new List<CosmosElement>() { CompositeContinuationToken.ToCosmosElement(token) }));
            Assert.IsTrue(monadicCreate.Succeeded);
        }

        [TestMethod]
        public void MonadicCreate_MultipleCompositeContinuationToken()
        {
            Mock<IFeedRangeProvider> mockFeedRangeProvider = new Mock<IFeedRangeProvider>();
            Mock<IQueryDataSource> mockQueryDataSource = new Mock<IQueryDataSource>();

            CompositeContinuationToken token = new CompositeContinuationToken()
            {
                Range = new Documents.Routing.Range<string>("A", "B", true, false),
                Token = "asdf",
            };

            TryCatch<IQueryPipelineStage> monadicCreate = ParallelCrossPartitionQueryPipelineStage.MonadicCreate(
                feedRangeProvider: mockFeedRangeProvider.Object,
                queryDataSource: mockQueryDataSource.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c"),
                pageSize: 10,
                continuationToken: CosmosArray.Create(
                    new List<CosmosElement>()
                    {
                        CompositeContinuationToken.ToCosmosElement(token),
                        CompositeContinuationToken.ToCosmosElement(token)
                    }));
            Assert.IsTrue(monadicCreate.Succeeded);
        }

        [TestMethod]
        public async Task TestDrainFully_StartFromBeginingAsync()
        {
            int numItems = 1000;
            InMemoryCollection inMemoryCollection = CreateInMemoryCollection(numItems);
            IFeedRangeProvider feedRangeProvider = new InMemoryCollectionFeedRangeProvider(inMemoryCollection);
            IQueryDataSource queryDataSource = new InMemoryCollectionQueryDataSource(inMemoryCollection);

            TryCatch<IQueryPipelineStage> monadicCreate = ParallelCrossPartitionQueryPipelineStage.MonadicCreate(
                feedRangeProvider: feedRangeProvider,
                queryDataSource: queryDataSource,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c"),
                pageSize: 10,
                continuationToken: default);
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
        }

        [TestMethod]
        public async Task TestDrainFully_WithStateResume()
        {
            int numItems = 1000;
            InMemoryCollection inMemoryCollection = CreateInMemoryCollection(numItems);
            IFeedRangeProvider feedRangeProvider = new InMemoryCollectionFeedRangeProvider(inMemoryCollection);
            IQueryDataSource queryDataSource = new InMemoryCollectionQueryDataSource(inMemoryCollection);

            TryCatch<IQueryPipelineStage> monadicCreate = ParallelCrossPartitionQueryPipelineStage.MonadicCreate(
                feedRangeProvider: feedRangeProvider,
                queryDataSource: queryDataSource,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c"),
                pageSize: 10,
                continuationToken: default);
            Assert.IsTrue(monadicCreate.Succeeded);
            IQueryPipelineStage queryPipelineStage = monadicCreate.Result;

            List<CosmosElement> documents = new List<CosmosElement>();
            while (await queryPipelineStage.MoveNextAsync())
            {
                TryCatch<QueryPage> tryGetQueryPage = queryPipelineStage.Current;
                Assert.IsTrue(tryGetQueryPage.Succeeded);

                QueryPage queryPage = tryGetQueryPage.Result;
                documents.AddRange(queryPage.Documents);

                if (queryPage.State != null)
                {
                    queryPipelineStage = ParallelCrossPartitionQueryPipelineStage.MonadicCreate(
                    feedRangeProvider: feedRangeProvider,
                    queryDataSource: queryDataSource,
                    sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c"),
                    pageSize: 10,
                    continuationToken: queryPage.State.Value).Result;
                }
            }

            Assert.AreEqual(numItems, documents.Count);
        }

        private static InMemoryCollection CreateInMemoryCollection(int numItems, InMemoryCollection.FailureConfigs failureConfigs = null)
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

            InMemoryCollection inMemoryCollection = new InMemoryCollection(partitionKeyDefinition, failureConfigs);

            inMemoryCollection.Split(partitionKeyRangeId: 0);

            inMemoryCollection.Split(partitionKeyRangeId: 1);
            inMemoryCollection.Split(partitionKeyRangeId: 2);

            inMemoryCollection.Split(partitionKeyRangeId: 3);
            inMemoryCollection.Split(partitionKeyRangeId: 4);
            inMemoryCollection.Split(partitionKeyRangeId: 5);
            inMemoryCollection.Split(partitionKeyRangeId: 6);

            for (int i = 0; i < numItems; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                inMemoryCollection.CreateItem(item);
            }

            return inMemoryCollection;
        }
    }
}
