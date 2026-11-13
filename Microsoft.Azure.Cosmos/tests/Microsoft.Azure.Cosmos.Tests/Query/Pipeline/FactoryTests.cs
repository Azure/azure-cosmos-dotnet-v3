//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.SecondaryIndexRouting;
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
        public TestContext TestContext { get; set; }

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
                requestContinuationToken: default,
                statisticsCacheFactory: HybridSearchStatisticsCacheFactory.Create(null, 0));
            Assert.IsTrue(monadicCreatePipeline.Succeeded);
        }

        [TestMethod]
        public async Task ExecutionFactoryUsesFullTextScoreScopeForStatistics()
        {
            List<ScopeTestCase> testCases = new List<ScopeTestCase>
            {
                new ScopeTestCase(FullTextScoreScope.Global),
                new ScopeTestCase(FullTextScoreScope.Local),
            };

            for (int index = 0; index < testCases.Count; index++)
            {
                ScopeTestCase testCase = testCases[index];
                this.TestContext.WriteLine($"Case {index}: {testCase}");
                await VerifyStatisticsRangesAsync(testCase.Scope);
            }
        }

        private static async Task VerifyStatisticsRangesAsync(FullTextScoreScope scope)
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
            queryClient.SetupGet(client => client.HybridSearchStatisticsCacheFactory).Returns(HybridSearchStatisticsCacheFactory.Create(null, 0));
            queryClient.Setup(client => client.GetClientDisableOptimisticDirectExecutionAsync()).ReturnsAsync(false);
            queryClient.Setup(client => client.GetCachedContainerQueryPropertiesAsync(
                It.IsAny<string>(), It.IsAny<Cosmos.PartitionKey?>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ContainerQueryProperties(
                    "rid", null, null, partitionKeyDefinition, null, Cosmos.GeospatialType.Geography, useLengthAwareRangeComparer: false, Array.Empty<ISecondaryIndexMetadata>()));
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
            queryClient.VerifyGet(client => client.HybridSearchStatisticsCacheFactory, Times.Once);
            queryClient.Verify(client => client.GetTargetPartitionKeyRangesAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<Range<string>>>(), false, It.IsAny<ITrace>()),
                Times.Exactly(2));
        }

        [TestMethod]
        public async Task QueryClientCarriesETagFromTheExistingMetadataRead()
        {
            List<ContainerMetadataTestCase> testCases = new List<ContainerMetadataTestCase>
            {
                new ContainerMetadataTestCase("Missing ETag", null),
                new ContainerMetadataTestCase("Present ETag", "etag"),
            };

            for (int index = 0; index < testCases.Count; index++)
            {
                ContainerMetadataTestCase testCase = testCases[index];
                this.TestContext.WriteLine($"Case {index}: {testCase}");
                await VerifyContainerMetadataAsync(testCase.ETag);
            }
        }

        private static async Task VerifyContainerMetadataAsync(string etag)
        {
            ContainerProperties properties = ContainerProperties.CreateWithResourceId("rid");
            properties.Id = "container";
            properties.PartitionKeyPath = "/pk";
            typeof(ContainerProperties).GetProperty(nameof(ContainerProperties.ETag)).SetValue(properties, etag);
            using MockDocumentClient documentClient = new MockDocumentClient();
            Mock<CosmosClientContext> context = new Mock<CosmosClientContext>(MockBehavior.Strict);
            context.SetupGet(client => client.DocumentClient).Returns(documentClient);
            context.SetupGet(client => client.ClientOptions).Returns(new CosmosClientOptions());
            context.Setup(client => client.GetCachedContainerPropertiesAsync(
                "dbs/db/colls/container", NoOpTrace.Singleton, CancellationToken.None)).ReturnsAsync(properties);

            CosmosQueryClient queryClient = new CosmosQueryClientCore(context.Object, null);
            ContainerQueryProperties first = await queryClient.GetCachedContainerQueryPropertiesAsync(
                "dbs/db/colls/container", null, NoOpTrace.Singleton, CancellationToken.None);
            Assert.AreEqual("rid", first.ResourceId);
            Assert.AreEqual(etag, first.ETag);
            Assert.AreSame(properties.PartitionKey, first.PartitionKeyDefinition);
            context.Verify(client => client.GetCachedContainerPropertiesAsync(
                "dbs/db/colls/container", NoOpTrace.Singleton, CancellationToken.None), Times.Once);

            typeof(ContainerProperties).GetProperty(nameof(ContainerProperties.ETag)).SetValue(properties, "updated-etag");
            ContainerQueryProperties second = await queryClient.GetCachedContainerQueryPropertiesAsync(
                "dbs/db/colls/container", null, NoOpTrace.Singleton, CancellationToken.None);
            Assert.AreEqual(etag, first.ETag);
            Assert.AreEqual("updated-etag", second.ETag);
            context.Verify(client => client.GetCachedContainerPropertiesAsync(
                "dbs/db/colls/container", NoOpTrace.Singleton, CancellationToken.None), Times.Exactly(2));
        }

        [TestMethod]
        public async Task PipelineFactoryBindsOnlyEligibleGlobalStatistics()
        {
            List<CacheBindingTestCase> testCases = new List<CacheBindingTestCase>
            {
                new CacheBindingTestCase { Name = "Global", ExpectedCache = true },
                new CacheBindingTestCase { Name = "Global with a narrow target", NarrowTargetRange = true, ExpectedCache = true },
                new CacheBindingTestCase { Name = "Local", Scope = FullTextScoreScope.Local },
                new CacheBindingTestCase { Name = "Disabled", CacheEnabled = false },
                new CacheBindingTestCase { Name = "Statistics not required", RequiresStatistics = false },
                new CacheBindingTestCase { Name = "Missing metadata", MetadataPresent = false },
                new CacheBindingTestCase { Name = "Missing RID", ResourceId = null, ExpectedCache = true },
                new CacheBindingTestCase { Name = "Missing ETag", ETag = null, ExpectedCache = true },
            };

            for (int index = 0; index < testCases.Count; index++)
            {
                CacheBindingTestCase testCase = testCases[index];
                this.TestContext.WriteLine($"Case {index}: {testCase}");
                await VerifyCacheBindingAsync(testCase);
            }
        }

        private static async Task VerifyCacheBindingAsync(CacheBindingTestCase testCase)
        {
            HybridSearchStatisticsCacheFactory cacheFactory = HybridSearchStatisticsCacheFactory.Create(TimeSpan.FromMinutes(1), 4);
            HybridSearchStatisticsCache cache = cacheFactory.GetCache(FullTextScoreScope.Global, CreateHybridPlan());
            HybridSearchQueryInfo plan = CreateHybridPlan();
            plan.RequiresGlobalStatistics = testCase.RequiresStatistics;

            if (!testCase.MetadataPresent)
            {
                plan.FullTextSearchTerms = null;
            }

            ContainerQueryProperties properties = CreateContainerProperties(testCase.ResourceId, testCase.ETag);
            IReadOnlyList<FeedRangeEpk> targetRanges = testCase.NarrowTargetRange
                ? new[] { new FeedRangeEpk(new Range<string>("10", "20", isMinInclusive: true, isMaxInclusive: false)) }
                : new[] { FeedRangeEpk.FullRange };
            Mock<IDocumentContainer> container = new Mock<IDocumentContainer>(MockBehavior.Strict);
            TryCatch<IQueryPipelineStage> created = PipelineFactory.MonadicCreate(
                documentContainer: container.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c"),
                targetRanges: targetRanges,
                partitionKey: null,
                queryInfo: null,
                hybridSearchQueryInfo: plan,
                maxItemCount: 10,
                containerQueryProperties: properties,
                allRanges: new[] { FeedRangeEpk.FullRange },
                isContinuationExpected: false,
                maxConcurrency: 1,
                fullTextScoreScope: testCase.Scope,
                requestContinuationToken: null,
                statisticsCacheFactory: testCase.CacheEnabled ? cacheFactory : HybridSearchStatisticsCacheFactory.Create(null, 0));
            created.ThrowIfFailed();
            await using IQueryPipelineStage pipeline = created.Result;
            HybridSearchCrossPartitionQueryPipelineStage stage = (HybridSearchCrossPartitionQueryPipelineStage)pipeline;
            Assert.AreSame(testCase.ExpectedCache ? cache : HybridSearchStatisticsCache.GetNoOpImpl(), GetPrivateField<HybridSearchStatisticsCache>(stage, "statisticsCache"));
            Assert.AreEqual(properties.ResourceId, GetPrivateField<string>(stage, "containerResourceId"));
            Assert.AreEqual(properties.ETag, GetPrivateField<string>(stage, "containerETag"));
            Assert.AreSame(plan, GetPrivateField<HybridSearchQueryInfo>(stage, "hybridSearchQueryInfo"));
            Assert.AreEqual(0, StoredCount(cache));
            container.VerifyNoOtherCalls();
        }

        private readonly struct ScopeTestCase
        {
            public ScopeTestCase(FullTextScoreScope scope)
            {
                this.Scope = scope;
            }

            public FullTextScoreScope Scope { get; }

            public override string ToString() => $"Scope={this.Scope}";
        }

        private readonly struct ContainerMetadataTestCase
        {
            public ContainerMetadataTestCase(string name, string etag)
            {
                this.Name = name;
                this.ETag = etag;
            }

            public string Name { get; }

            public string ETag { get; }

            public override string ToString() => $"{this.Name}: ETag={this.ETag ?? "<null>"}";
        }

        private sealed class CacheBindingTestCase
        {
            public string Name { get; set; }

            public FullTextScoreScope Scope { get; set; } = FullTextScoreScope.Global;

            public string ResourceId { get; set; } = "rid";

            public string ETag { get; set; } = "etag";

            public bool CacheEnabled { get; set; } = true;

            public bool RequiresStatistics { get; set; } = true;

            public bool MetadataPresent { get; set; } = true;

            public bool NarrowTargetRange { get; set; }

            public bool ExpectedCache { get; set; }

            public override string ToString() =>
                $"{this.Name}: Scope={this.Scope}, RID={this.ResourceId ?? "<null>"}, ETag={this.ETag ?? "<null>"}, " +
                $"Enabled={this.CacheEnabled}, Required={this.RequiresStatistics}, Metadata={this.MetadataPresent}, " +
                $"NarrowRange={this.NarrowTargetRange}, ExpectedCache={this.ExpectedCache}";
        }

        private static HybridSearchQueryInfo CreateHybridPlan()
        {
            return new HybridSearchQueryInfo
            {
                RequiresGlobalStatistics = true,
                GlobalStatisticsQuery = "SELECT COUNT(1) AS documentCount FROM c",
                ComponentQueryInfos = new List<QueryInfo>(),
                FullTextSearchTerms = new List<FullTextSearchTerms>
                {
                    new FullTextSearchTerms
                    {
                        Path = new List<string> { "text" },
                        Terms = new List<FullTextSearchTerm> { new FullTextSearchTerm { Term = "run" } },
                    },
                },
            };
        }

        private static ContainerQueryProperties CreateContainerProperties(string resourceId, string etag)
        {
            return new ContainerQueryProperties(
                resourceId, etag, null, new PartitionKeyDefinition { Paths = new Collection<string> { "/pk" }, Kind = PartitionKind.Hash },
                null, Cosmos.GeospatialType.Geography, useLengthAwareRangeComparer: false, Array.Empty<ISecondaryIndexMetadata>());
        }

        private static T GetPrivateField<T>(object instance, string name)
        {
            FieldInfo field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            return (T)field.GetValue(instance);
        }

        private static int StoredCount(HybridSearchStatisticsCache cache)
        {
            return GetPrivateField<ICollection>(cache, "counts").Count;
        }
    }
}
