//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class FactoryTests
    {
        [TestMethod]
        public void TestCreate()
        {
            Mock<IDocumentContainer> mockDocumentContainer = new Mock<IDocumentContainer>();

            TryCatch<IQueryPipelineStage> monadicCreatePipeline = PipelineFactory.MonadicCreate(
                documentContainer: mockDocumentContainer.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c"),
                targetRanges: new List<FeedRangeEpk>() { FeedRangeEpk.FullRange },
                partitionKey: null,
                containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                allRanges: new List<FeedRangeEpk>() { FeedRangeEpk.FullRange },
                queryInfo: new QueryInfo() { },
                hybridSearchQueryInfo: null,
                maxItemCount: 10,
                isContinuationExpected: true,
                maxConcurrency: 10,
                fullTextScoreScope: FullTextScoreScope.Global,
                requestContinuationToken: default); ;
            Assert.IsTrue(monadicCreatePipeline.Succeeded);
        }

        [DataTestMethod]
        [DataRow(FullTextScoreScope.Global)]
        [DataRow(FullTextScoreScope.Local)]
        public async Task ExecutionFactoryUsesFullTextScoreScopeForStatistics(FullTextScoreScope scope)
        {
            const string statisticsQuery = "SELECT COUNT(1) AS documentCount FROM c";
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition
            {
                Paths = new Collection<string> { "/pk" },
                Kind = PartitionKind.Hash,
            };
            List<PartitionKeyRange> allRanges = new List<PartitionKeyRange>
            {
                new PartitionKeyRange { Id = "0", MinInclusive = "", MaxExclusive = "40" },
                new PartitionKeyRange { Id = "1", MinInclusive = "40", MaxExclusive = "80" },
                new PartitionKeyRange { Id = "2", MinInclusive = "80", MaxExclusive = "FF" },
            };
            List<PartitionKeyRange> targetRanges = new List<PartitionKeyRange>
            {
                new PartitionKeyRange { Id = "0", MinInclusive = "10", MaxExclusive = "20" },
                new PartitionKeyRange { Id = "2", MinInclusive = "90", MaxExclusive = "A0" },
            };
            PartitionedQueryExecutionInfo queryPlan = new PartitionedQueryExecutionInfo
            {
                QueryRanges = targetRanges.Select(range => range.ToRange()).ToList(),
                HybridSearchQueryInfo = new HybridSearchQueryInfo
                {
                    RequiresGlobalStatistics = true,
                    GlobalStatisticsQuery = statisticsQuery,
                    ComponentQueryInfos = new List<QueryInfo>(),
                },
            };
            Mock<CosmosQueryClient> queryClient = new Mock<CosmosQueryClient>(MockBehavior.Strict);
            queryClient.Setup(client => client.GetClientDisableOptimisticDirectExecutionAsync()).ReturnsAsync(false);
            queryClient.Setup(client => client.GetCachedContainerQueryPropertiesAsync(
                It.IsAny<string>(), It.IsAny<Cosmos.PartitionKey?>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContainerQueryProperties(
                    "rid", null, partitionKeyDefinition, null, Cosmos.GeospatialType.Geography, useLengthAwareRangeComparer: false));
            queryClient.SetupSequence(client => client.GetTargetPartitionKeyRangesAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<Range<string>>>(), false, It.IsAny<ITrace>()))
                .ReturnsAsync(targetRanges)
                .ReturnsAsync(allRanges);

            List<FeedRangeEpk> requestedRanges = new List<FeedRangeEpk>();
            Mock<IMonadicDocumentContainer> source = new Mock<IMonadicDocumentContainer>(MockBehavior.Strict);
            source.Setup(container => container.MonadicQueryAsync(
                It.IsAny<SqlQuerySpec>(), It.IsAny<FeedRangeState<QueryState>>(), It.IsAny<QueryExecutionOptions>(),
                It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .Returns<SqlQuerySpec, FeedRangeState<QueryState>, QueryExecutionOptions, ITrace, CancellationToken>(
                    (query, state, options, trace, cancellationToken) =>
                    {
                        Assert.AreEqual(statisticsQuery, query.QueryText);
                        Assert.IsInstanceOfType(state.FeedRange, typeof(FeedRangeEpk));
                        requestedRanges.Add((FeedRangeEpk)state.FeedRange);
                        return Task.FromResult(TryCatch<QueryPage>.FromResult(new QueryPage(
                            new[] { CosmosElement.Parse(@"{""documentCount"":1,""fullTextStatistics"":[]}") },
                            requestCharge: 1,
                            activityId: "statistics",
                            cosmosQueryExecutionInfo: null,
                            distributionPlanSpec: null,
                            disallowContinuationTokenMessage: null,
                            additionalHeaders: null,
                            state: null,
                            streaming: false)));
                    });

            CosmosQueryExecutionContextFactory.InputParameters input = CosmosQueryExecutionContextFactory.InputParameters.Create(
                sqlQuerySpec: new SqlQuerySpec("SELECT TOP 10 * FROM c ORDER BY RANK FullTextScore(c.text, 'term')"),
                initialUserContinuationToken: null,
                initialFeedRange: null,
                maxConcurrency: 1,
                maxItemCount: 10,
                maxBufferedItemCount: 10,
                partitionKey: null,
                properties: null,
                partitionedQueryExecutionInfo: queryPlan,
                returnResultsInDeterministicOrder: true,
                enableOptimisticDirectExecution: false,
                isHybridSearchQueryPlanOptimizationDisabled: false,
                enableDistributedQueryGatewayMode: false,
                fullTextScoreScope: scope,
                testInjections: null);
            CosmosQueryContextCore context = new CosmosQueryContextCore(
                queryClient.Object, ResourceType.Document, OperationType.Query, typeof(object),
                "dbs/db/colls/coll", Guid.NewGuid(), isContinuationExpected: false,
                allowNonValueAggregateQuery: true, useSystemPrefix: false);

            await using IQueryPipelineStage pipeline = CosmosQueryExecutionContextFactory.Create(
                new DocumentContainer(source.Object), context, input, NoOpTrace.Singleton);
            Assert.IsTrue(await pipeline.MoveNextAsync(NoOpTrace.Singleton, CancellationToken.None));
            pipeline.Current.ThrowIfFailed();

            IReadOnlyList<PartitionKeyRange> expected = scope == FullTextScoreScope.Global ? allRanges : targetRanges;
            CollectionAssert.AreEquivalent(
                expected.Select(range => range.ToRange()).ToArray(),
                requestedRanges.Select(range => range.Range).ToArray());
            Assert.AreEqual((double)expected.Count, pipeline.Current.Result.RequestCharge);
            queryClient.Verify(client => client.GetTargetPartitionKeyRangesAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<Range<string>>>(), false, It.IsAny<ITrace>()),
                Times.Exactly(2));
        }
    }
}
