//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    /// <summary>
    /// Covers the over-fetch factor applied to the initial per-partition page size of a cross-partition
    /// ORDER BY query, and the AZURE_COSMOS_PAGE_SIZE_FACTOR_FOR_TOP emergency override.
    /// </summary>
    /// <remarks>
    /// The two concerns are tested separately on purpose. PipelineFactory caches the factor in a static
    /// initializer, so the page size arithmetic takes the factor as a parameter and never observes the
    /// environment; only the ConfigurationManager tests manipulate the environment variable.
    /// </remarks>
    [TestClass]
    public class PageSizeFactorForTopTests
    {
        private const string EnvVarName = "AZURE_COSMOS_PAGE_SIZE_FACTOR_FOR_TOP";
        private const int TargetRangeCount = 10;
        private const int DefaultFactor = 2;
        private const int ContinuationFactor = 5;

        private string priorEnvVarValue;

        [TestInitialize]
        public void TestInitialize()
        {
            this.priorEnvVarValue = Environment.GetEnvironmentVariable(EnvVarName);
            Environment.SetEnvironmentVariable(EnvVarName, null);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Environment.SetEnvironmentVariable(EnvVarName, this.priorEnvVarValue);
        }

        [DataTestMethod]
        [DataRow("5", 5, DisplayName = "Legacy value restores the previous behavior")]
        [DataRow("3", 3, DisplayName = "Arbitrary valid value is honored")]
        [DataRow("1", 1, DisplayName = "Minimum valid value is honored")]
        [DataRow("0", 1, DisplayName = "Zero is clamped to the minimum")]
        [DataRow("-3", 1, DisplayName = "Negative value is clamped to the minimum")]
        [DataRow("abc", DefaultFactor, DisplayName = "Non-numeric value falls back to the default")]
        [DataRow("2.5", DefaultFactor, DisplayName = "Decimal value falls back to the default")]
        [DataRow("5x", DefaultFactor, DisplayName = "Trailing garbage falls back to the default")]
        [DataRow("99999999999999999999", DefaultFactor, DisplayName = "Overflowing value falls back to the default")]
        [DataRow("", DefaultFactor, DisplayName = "Empty value falls back to the default")]
        [DataRow(" ", DefaultFactor, DisplayName = "Whitespace falls back to the default")]
        public void EnvironmentVariableOverride(string envVarValue, int expectedFactor)
        {
            Environment.SetEnvironmentVariable(EnvVarName, envVarValue);

            Assert.AreEqual(expectedFactor, Microsoft.Azure.Cosmos.ConfigurationManager.GetPageSizeFactorForTop());
        }

        [TestMethod]
        public void NonOrderByQueryIsNotAdjusted()
        {
            QueryInfo queryInfo = new QueryInfo() { Top = 1000 };

            Assert.AreEqual(
                1000,
                PipelineFactory.ComputeOptimalPageSize(
                    queryInfo: queryInfo,
                    targetRangeCount: TargetRangeCount,
                    maxItemCount: 1000,
                    isContinuationExpected: true,
                    pageSizeFactorForTop: DefaultFactor));
        }

        [DataTestMethod]
        [DataRow(2, 200L, DisplayName = "Default factor")]
        [DataRow(5, 500L, DisplayName = "Legacy factor")]
        [DataRow(1, 100L, DisplayName = "Minimum factor")]
        public void OrderByWithTopScalesWithTheFactor(int factor, long expectedPageSize)
        {
            // ceil(1000 / 10) * factor
            Assert.AreEqual(
                expectedPageSize,
                PipelineFactory.ComputeOptimalPageSize(
                    queryInfo: CreateOrderByQueryInfo(top: 1000),
                    targetRangeCount: TargetRangeCount,
                    maxItemCount: 1000,
                    isContinuationExpected: true,
                    pageSizeFactorForTop: factor));
        }

        [TestMethod]
        public void OrderByWithTopIsCappedByTopAndMaxItemCount()
        {
            // ceil(3 / 10) * 2 = 2, which is below top.
            Assert.AreEqual(
                2,
                PipelineFactory.ComputeOptimalPageSize(
                    queryInfo: CreateOrderByQueryInfo(top: 3),
                    targetRangeCount: TargetRangeCount,
                    maxItemCount: 1000,
                    isContinuationExpected: true,
                    pageSizeFactorForTop: DefaultFactor));

            // ceil(1 / 10) * 2 = 2, capped by top.
            Assert.AreEqual(
                1,
                PipelineFactory.ComputeOptimalPageSize(
                    queryInfo: CreateOrderByQueryInfo(top: 1),
                    targetRangeCount: TargetRangeCount,
                    maxItemCount: 1000,
                    isContinuationExpected: true,
                    pageSizeFactorForTop: DefaultFactor));

            // ceil(1000 / 10) * 2 = 200, capped by maxItemCount.
            Assert.AreEqual(
                50,
                PipelineFactory.ComputeOptimalPageSize(
                    queryInfo: CreateOrderByQueryInfo(top: 1000),
                    targetRangeCount: TargetRangeCount,
                    maxItemCount: 50,
                    isContinuationExpected: true,
                    pageSizeFactorForTop: DefaultFactor));
        }

        [TestMethod]
        public void FactorIsInertWhenRangeCountDoesNotExceedIt()
        {
            // ceil(1000 / 1) * 2 = 2000, capped by top: a single partition container is unaffected by the
            // factor, and stays unaffected for any value the override can produce.
            foreach (int factor in new[] { 1, DefaultFactor, 5, 100 })
            {
                Assert.AreEqual(
                    1000,
                    PipelineFactory.ComputeOptimalPageSize(
                        queryInfo: CreateOrderByQueryInfo(top: 1000),
                        targetRangeCount: 1,
                        maxItemCount: 1000,
                        isContinuationExpected: true,
                        pageSizeFactorForTop: factor));
            }

            // ceil(1000 / 2) * 2 = 1000, capped by top: a two range container sees no reduction at all.
            Assert.AreEqual(
                1000,
                PipelineFactory.ComputeOptimalPageSize(
                    queryInfo: CreateOrderByQueryInfo(top: 1000),
                    targetRangeCount: 2,
                    maxItemCount: 1000,
                    isContinuationExpected: true,
                    pageSizeFactorForTop: DefaultFactor));

            // ceil(1000 / 3) * 2 = 668: three ranges is the smallest count where lowering the factor
            // changes the page size at all.
            Assert.AreEqual(
                668,
                PipelineFactory.ComputeOptimalPageSize(
                    queryInfo: CreateOrderByQueryInfo(top: 1000),
                    targetRangeCount: 3,
                    maxItemCount: 1000,
                    isContinuationExpected: true,
                    pageSizeFactorForTop: DefaultFactor));

            // At the legacy factor of 5 the same was true all the way up to five ranges.
            Assert.AreEqual(
                1000,
                PipelineFactory.ComputeOptimalPageSize(
                    queryInfo: CreateOrderByQueryInfo(top: 1000),
                    targetRangeCount: 5,
                    maxItemCount: 1000,
                    isContinuationExpected: true,
                    pageSizeFactorForTop: 5));

            // Lowering the factor to 2 is what newly reduces the page size for those containers.
            Assert.AreEqual(
                400,
                PipelineFactory.ComputeOptimalPageSize(
                    queryInfo: CreateOrderByQueryInfo(top: 1000),
                    targetRangeCount: 5,
                    maxItemCount: 1000,
                    isContinuationExpected: true,
                    pageSizeFactorForTop: DefaultFactor));
        }

        [TestMethod]
        public void OrderByWithOffsetLimitUsesTheFactor()
        {
            QueryInfo queryInfo = new QueryInfo()
            {
                OrderBy = new List<SortOrder>() { SortOrder.Ascending },
                Offset = 100,
                Limit = 100,
            };

            // ceil((100 + 100) / 10) * 2
            Assert.AreEqual(
                40,
                PipelineFactory.ComputeOptimalPageSize(
                    queryInfo: queryInfo,
                    targetRangeCount: TargetRangeCount,
                    maxItemCount: 1000,
                    isContinuationExpected: true,
                    pageSizeFactorForTop: DefaultFactor));
        }

        [TestMethod]
        public void OrderByWithoutTopIsUnaffectedByTheFactor()
        {
            // ceil(1000 / 10) * 5, the factor reserved for the no-top branch, regardless of what the
            // TOP branch is configured to use.
            foreach (int factor in new[] { 1, DefaultFactor, 5, 100 })
            {
                Assert.AreEqual(
                    ContinuationFactor * 100,
                    PipelineFactory.ComputeOptimalPageSize(
                        queryInfo: CreateOrderByQueryInfo(top: null),
                        targetRangeCount: TargetRangeCount,
                        maxItemCount: 1000,
                        isContinuationExpected: true,
                        pageSizeFactorForTop: factor));
            }
        }

        [TestMethod]
        public void OrderByWithoutTopIsNotAdjustedWhenNoContinuationIsExpected()
        {
            Assert.AreEqual(
                1000,
                PipelineFactory.ComputeOptimalPageSize(
                    queryInfo: CreateOrderByQueryInfo(top: null),
                    targetRangeCount: TargetRangeCount,
                    maxItemCount: 1000,
                    isContinuationExpected: false,
                    pageSizeFactorForTop: DefaultFactor));
        }

        private static QueryInfo CreateOrderByQueryInfo(uint? top)
        {
            return new QueryInfo()
            {
                OrderBy = new List<SortOrder>() { SortOrder.Ascending },
                Top = top,
            };
        }

        /// <summary>
        /// End to end check that the page size a real pipeline asks the backend for is the one derived from
        /// <see cref="Microsoft.Azure.Cosmos.ConfigurationManager"/>, rather than a literal baked into
        /// <see cref="PipelineFactory"/>.
        /// </summary>
        /// <remarks>
        /// The other tests here exercise the parsing and the arithmetic in isolation, which leaves the wiring
        /// between them uncovered: replacing either the cached field initializer or the argument at the
        /// ComputeOptimalPageSize call site with a constant would keep every one of them green. This test
        /// observes the page size that actually reaches the document container, so it fails in both cases.
        /// </remarks>
        [TestMethod]
        public async Task ProductionPipelineUsesTheConfiguredFactorAsync()
        {
            const int NumRanges = 4;
            const int Top = 100;
            const int MaxItemCount = 1000;

            // PipelineFactory caches the factor in a static initializer, so it reflects the environment as it
            // was when the type was first touched, which TestInitialize cannot undo. The expectation below is
            // a live read, so the two only agree when the host process did not set the variable.
            Assert.IsNull(
                this.priorEnvVarValue,
                $"This test cannot run with {EnvVarName} set in the host process. It was '{this.priorEnvVarValue}'.");

            List<CosmosObject> documents = Enumerable
                .Range(0, 200)
                .Select(x => CosmosObject.Parse($"{{\"pk\" : {x} }}"))
                .ToList();

            // numSplits of 2 takes the single starting range to four.
            PageSizeRecordingDocumentContainer recorder = new PageSizeRecordingDocumentContainer(
                FullPipelineTests.CreateMonadicDocumentContainerAsync(failureConfigs: null));
            DocumentContainer documentContainer = await FullPipelineTests.CreateDocumentContainerAsync(
                documents: documents,
                monadicDocumentContainer: recorder,
                numSplits: 2);

            IReadOnlyList<FeedRangeEpk> feedRanges = await documentContainer.GetFeedRangesAsync(
                NoOpTrace.Singleton,
                cancellationToken: default);
            Assert.AreEqual(NumRanges, feedRanges.Count, "Test assumes a four range container.");

            string query = $"SELECT TOP {Top} c.pk FROM c ORDER BY c.pk";
            QueryInfo queryInfo = GetQueryPlan(query);
            Assert.IsTrue(queryInfo.HasOrderBy && queryInfo.HasTop, "Test assumes the TOP branch is taken.");

            recorder.Reset();

            TryCatch<IQueryPipelineStage> tryCreatePipeline = PipelineFactory.MonadicCreate(
                documentContainer,
                new SqlQuerySpec(query),
                feedRanges,
                partitionKey: null,
                queryInfo,
                hybridSearchQueryInfo: null,
                allRanges: feedRanges,
                maxItemCount: MaxItemCount,
                containerQueryProperties: new ContainerQueryProperties(),
                isContinuationExpected: true,
                maxConcurrency: 10,
                fullTextScoreScope: FullTextScoreScope.Global,
                requestContinuationToken: null);
            tryCreatePipeline.ThrowIfFailed();

            await tryCreatePipeline.Result.MoveNextAsync(NoOpTrace.Singleton, cancellationToken: default);

            long expectedPageSize = PipelineFactory.ComputeOptimalPageSize(
                queryInfo: queryInfo,
                targetRangeCount: NumRanges,
                maxItemCount: MaxItemCount,
                isContinuationExpected: true,
                pageSizeFactorForTop: Microsoft.Azure.Cosmos.ConfigurationManager.GetPageSizeFactorForTop());

            Assert.AreNotEqual(
                0,
                recorder.ObservedPageSizes.Count,
                "The pipeline never queried the container, so the page size was never observed.");
            CollectionAssert.AreEqual(
                Enumerable.Repeat((int)expectedPageSize, recorder.ObservedPageSizes.Count).ToList(),
                recorder.ObservedPageSizes,
                "PipelineFactory is not sourcing its page size factor from ConfigurationManager. "
                    + $"Expected every request to use {expectedPageSize}, observed "
                    + $"[{string.Join(", ", recorder.ObservedPageSizes)}].");
        }

        private static QueryInfo GetQueryPlan(string query)
        {
            TryCatch<PartitionedQueryExecutionInfoInternal> info = QueryPartitionProviderTestInstance.Object.TryGetPartitionedQueryExecutionInfoInternal(
                JsonConvert.SerializeObject(new SqlQuerySpec(query)),
                FullPipelineTests.partitionKeyDefinition,
                vectorEmbeddingPolicy: null,
                requireFormattableOrderByQuery: true,
                isContinuationExpected: false,
                allowNonValueAggregateQuery: true,
                allowDCount: true,
                hasLogicalPartitionKey: false,
                hybridSearchSkipOrderByRewrite: false,
                useSystemPrefix: false,
                geospatialType: Cosmos.GeospatialType.Geography);

            info.ThrowIfFailed();
            return info.Result.QueryInfo;
        }

        /// <summary>
        /// Passes every call through to an inner container, recording the page size hint of each query request.
        /// </summary>
        private sealed class PageSizeRecordingDocumentContainer : IMonadicDocumentContainer
        {
            private readonly IMonadicDocumentContainer inner;

            public PageSizeRecordingDocumentContainer(IMonadicDocumentContainer inner)
            {
                this.inner = inner;
            }

            public List<int> ObservedPageSizes { get; } = new List<int>();

            public void Reset()
            {
                this.ObservedPageSizes.Clear();
            }

            public Task<TryCatch<QueryPage>> MonadicQueryAsync(
                SqlQuerySpec sqlQuerySpec,
                FeedRangeState<QueryState> feedRangeState,
                QueryExecutionOptions queryPaginationOptions,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                if (queryPaginationOptions?.PageSizeLimit != null)
                {
                    this.ObservedPageSizes.Add(queryPaginationOptions.PageSizeLimit.Value);
                }

                return this.inner.MonadicQueryAsync(sqlQuerySpec, feedRangeState, queryPaginationOptions, trace, cancellationToken);
            }

            public Task<TryCatch<Record>> MonadicCreateItemAsync(
                CosmosObject payload,
                CancellationToken cancellationToken) => this.inner.MonadicCreateItemAsync(payload, cancellationToken);

            public Task<TryCatch<Record>> MonadicReadItemAsync(
                CosmosElement partitionKey,
                string identifer,
                CancellationToken cancellationToken) => this.inner.MonadicReadItemAsync(partitionKey, identifer, cancellationToken);

            public Task<TryCatch<ReadFeedPage>> MonadicReadFeedAsync(
                FeedRangeState<ReadFeedState> feedRangeState,
                ReadFeedExecutionOptions readFeedPaginationOptions,
                ITrace trace,
                CancellationToken cancellationToken) => this.inner.MonadicReadFeedAsync(feedRangeState, readFeedPaginationOptions, trace, cancellationToken);

            public Task<TryCatch<ChangeFeedPage>> MonadicChangeFeedAsync(
                FeedRangeState<ChangeFeedState> feedRangeState,
                ChangeFeedExecutionOptions changeFeedPaginationOptions,
                ITrace trace,
                CancellationToken cancellationToken) => this.inner.MonadicChangeFeedAsync(feedRangeState, changeFeedPaginationOptions, trace, cancellationToken);

            public Task<TryCatch> MonadicSplitAsync(
                FeedRangeInternal feedRange,
                CancellationToken cancellationToken) => this.inner.MonadicSplitAsync(feedRange, cancellationToken);

            public Task<TryCatch> MonadicMergeAsync(
                FeedRangeInternal feedRange1,
                FeedRangeInternal feedRange2,
                CancellationToken cancellationToken) => this.inner.MonadicMergeAsync(feedRange1, feedRange2, cancellationToken);

            public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetChildRangeAsync(
                FeedRangeInternal feedRange,
                ITrace trace,
                CancellationToken cancellationToken) => this.inner.MonadicGetChildRangeAsync(feedRange, trace, cancellationToken);

            public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetFeedRangesAsync(
                ITrace trace,
                CancellationToken cancellationToken) => this.inner.MonadicGetFeedRangesAsync(trace, cancellationToken);

            public Task<TryCatch> MonadicRefreshProviderAsync(
                ITrace trace,
                CancellationToken cancellationToken) => this.inner.MonadicRefreshProviderAsync(trace, cancellationToken);

            public Task<TryCatch<string>> MonadicGetResourceIdentifierAsync(
                ITrace trace,
                CancellationToken cancellationToken) => this.inner.MonadicGetResourceIdentifierAsync(trace, cancellationToken);
        }
    }
}
