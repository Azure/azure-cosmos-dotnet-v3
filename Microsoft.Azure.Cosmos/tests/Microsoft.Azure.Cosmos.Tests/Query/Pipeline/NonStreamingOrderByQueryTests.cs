//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using System.Threading;
    using System;
    using System.Linq;

    [TestClass]
    public class NonStreamingOrderByQueryTests
    {
        private const int MaxConcurrency = 10;

        private const int DocumentCount = 420;

        private static readonly int[] PageSizes = new [] { 1, 10, 100, DocumentCount };

        private static readonly IReadOnlyList<OrderByColumn> OrderByColumnPkAsc = new List<OrderByColumn>
        {
            new OrderByColumn("c.pk", SortOrder.Ascending)
        };

        private static readonly IReadOnlyList<OrderByColumn> OrderByColumnPkDesc = new List<OrderByColumn>
        {
            new OrderByColumn("c.pk", SortOrder.Descending)
        };

        [TestMethod]
        public async Task ParityTests()
        {
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(DocumentCount);

            IReadOnlyList<TestCase> testCases = new List<TestCase>
            {
                MakeTest(
                    queryText: @"
                        SELECT c._rid AS _rid, [{""item"": c.pk}] AS orderByItems, c AS payload
                        FROM c
                        WHERE {documentdb-formattableorderbyquery-filter}
                        ORDER BY c.pk",
                    orderByColumns: OrderByColumnPkAsc),
                MakeTest(
                    queryText: @"
                        SELECT c._rid AS _rid, [{""item"": c.pk}] AS orderByItems, c AS payload
                        FROM c
                        WHERE {documentdb-formattableorderbyquery-filter}
                        ORDER BY c.pk DESC",
                    orderByColumns: OrderByColumnPkDesc),
            };

            await RunParityTests(
                documentContainer,
                await documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, default),
                testCases);
        }

        private static async Task RunParityTests(
            IDocumentContainer documentContainer,
            IReadOnlyList<FeedRangeEpk> ranges,
            IReadOnlyList<TestCase> testCases)
        {
            IDocumentContainer nonStreamingContainer = new NonStreamingDocumentContainer(documentContainer);

            foreach (TestCase testCase in testCases)
            {
                foreach (int pageSize in testCase.PageSizes)
                {
                    IReadOnlyList<CosmosElement> nonStreamingResult = await CreateAndRunPipelineStage(
                        documentContainer: nonStreamingContainer,
                        ranges: ranges,
                        queryText: testCase.QueryText,
                        orderByColumns: testCase.OrderByColumns,
                        pageSize: pageSize);

                    IReadOnlyList<CosmosElement> streamingResult = await CreateAndRunPipelineStage(
                        documentContainer: documentContainer,
                        ranges: ranges,
                        queryText: testCase.QueryText,
                        orderByColumns: testCase.OrderByColumns,
                        pageSize: pageSize);

                    if (!streamingResult.SequenceEqual(nonStreamingResult))
                    {
                        Assert.Fail($"Results mismatch for query:\n{testCase.QueryText}\npageSize: {pageSize}");
                    }
                }
            }
        }

        private static async Task<IReadOnlyList<CosmosElement>> CreateAndRunPipelineStage(
            IDocumentContainer documentContainer,
            IReadOnlyList<FeedRangeEpk> ranges,
            string queryText,
            IReadOnlyList<OrderByColumn> orderByColumns,
            int pageSize)
        {
            TryCatch<IQueryPipelineStage> pipelineStage = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                    documentContainer: documentContainer,
                    sqlQuerySpec: new SqlQuerySpec(queryText),
                    targetRanges: ranges,
                    partitionKey: null,
                    orderByColumns: orderByColumns,
                    queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: pageSize),
                    maxConcurrency: MaxConcurrency,
                    continuationToken: null);

            Assert.IsTrue(pipelineStage.Succeeded);

            IQueryPipelineStage stage = pipelineStage.Result;
            List<CosmosElement> documents = new List<CosmosElement>();
            while (await stage.MoveNextAsync(NoOpTrace.Singleton, default))
            {
                Assert.IsTrue(stage.Current.Succeeded);
                Assert.IsTrue(stage.Current.Result.Documents.Count <= pageSize);
                DebugTraceHelpers.TracePage(stage.Current.Result);
                documents.AddRange(stage.Current.Result.Documents);
            }

            return documents;
        }

        private static TestCase MakeTest(string queryText, IReadOnlyList<OrderByColumn> orderByColumns)
        {
            return MakeTest(queryText, orderByColumns, PageSizes);
        }

        private static TestCase MakeTest(
            string queryText,
            IReadOnlyList<OrderByColumn> orderByColumns,
            int[] pageSizes)
        {
            return new TestCase(queryText, orderByColumns, pageSizes);
        }

        private sealed class TestCase
        {
            public string QueryText { get; }

            public IReadOnlyList<OrderByColumn> OrderByColumns { get; }

            public int[] PageSizes { get; }

            public TestCase(
                string queryText,
                IReadOnlyList<OrderByColumn> orderByColumns,
                int[] pageSizes)
            {
                this.QueryText = queryText;
                this.OrderByColumns = orderByColumns;
                this.PageSizes = pageSizes;
            }
        }

        private sealed class NonStreamingDocumentContainer : IDocumentContainer
        {
            private readonly IDocumentContainer inner;

            public NonStreamingDocumentContainer(IDocumentContainer inner)
            {
                this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            }

            public Task<ChangeFeedPage> ChangeFeedAsync(
                FeedRangeState<ChangeFeedState> feedRangeState,
                ChangeFeedPaginationOptions changeFeedPaginationOptions,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                return this.inner.ChangeFeedAsync(feedRangeState, changeFeedPaginationOptions, trace, cancellationToken);
            }

            public Task<Record> CreateItemAsync(CosmosObject payload, CancellationToken cancellationToken)
            {
                return this.inner.CreateItemAsync(payload, cancellationToken);
            }

            public Task<List<FeedRangeEpk>> GetChildRangeAsync(
                FeedRangeInternal feedRange,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                return this.inner.GetChildRangeAsync(feedRange, trace, cancellationToken);
            }

            public Task<List<FeedRangeEpk>> GetFeedRangesAsync(ITrace trace, CancellationToken cancellationToken)
            {
                return this.inner.GetFeedRangesAsync(trace, cancellationToken);
            }

            public Task<string> GetResourceIdentifierAsync(ITrace trace, CancellationToken cancellationToken)
            {
                return this.inner.GetResourceIdentifierAsync(trace, cancellationToken);
            }

            public Task MergeAsync(FeedRangeInternal feedRange1, FeedRangeInternal feedRange2, CancellationToken cancellationToken)
            {
                return this.inner.MergeAsync(feedRange1, feedRange2, cancellationToken);
            }

            public Task<TryCatch<ChangeFeedPage>> MonadicChangeFeedAsync(
                FeedRangeState<ChangeFeedState> feedRangeState,
                ChangeFeedPaginationOptions changeFeedPaginationOptions,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                return this.inner.MonadicChangeFeedAsync(feedRangeState, changeFeedPaginationOptions, trace, cancellationToken);
            }

            public Task<TryCatch<Record>> MonadicCreateItemAsync(CosmosObject payload, CancellationToken cancellationToken)
            {
                return this.inner.MonadicCreateItemAsync(payload, cancellationToken);
            }

            public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetChildRangeAsync(
                FeedRangeInternal feedRange,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                return this.inner.MonadicGetChildRangeAsync(feedRange, trace, cancellationToken);
            }

            public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetFeedRangesAsync(ITrace trace, CancellationToken cancellationToken)
            {
                return this.inner.MonadicGetFeedRangesAsync(trace, cancellationToken);
            }

            public Task<TryCatch<string>> MonadicGetResourceIdentifierAsync(ITrace trace, CancellationToken cancellationToken)
            {
                return this.inner.MonadicGetResourceIdentifierAsync(trace, cancellationToken);
            }

            public Task<TryCatch> MonadicMergeAsync(
                FeedRangeInternal feedRange1,
                FeedRangeInternal feedRange2,
                CancellationToken cancellationToken)
            {
                return this.inner.MonadicMergeAsync(feedRange1, feedRange2, cancellationToken);
            }

            public async Task<TryCatch<QueryPage>> MonadicQueryAsync(
                SqlQuerySpec sqlQuerySpec,
                FeedRangeState<QueryState> feedRangeState,
                QueryPaginationOptions queryPaginationOptions,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                TryCatch<QueryPage> queryPage = await this.inner.MonadicQueryAsync(sqlQuerySpec, feedRangeState, queryPaginationOptions, trace, cancellationToken);

                if (queryPage.Failed)
                {
                    return queryPage;
                }

                QueryPage page = queryPage.Result;
                DebugTraceHelpers.TraceBackendResponse(page);

                return TryCatch<QueryPage>.FromResult(new QueryPage(
                    page.Documents,
                    page.RequestCharge,
                    page.ActivityId,
                    page.CosmosQueryExecutionInfo,
                    page.DistributionPlanSpec,
                    page.DisallowContinuationTokenMessage,
                    page.AdditionalHeaders,
                    page.State,
                    streaming: false));
            }

            public Task<TryCatch<ReadFeedPage>> MonadicReadFeedAsync(
                FeedRangeState<ReadFeedState> feedRangeState,
                ReadFeedPaginationOptions readFeedPaginationOptions,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                return this.inner.MonadicReadFeedAsync(feedRangeState, readFeedPaginationOptions, trace, cancellationToken);
            }

            public Task<TryCatch<Record>> MonadicReadItemAsync(
                CosmosElement partitionKey,
                string identifer,
                CancellationToken cancellationToken)
            {
                return this.inner.MonadicReadItemAsync(partitionKey, identifer, cancellationToken);
            }

            public Task<TryCatch> MonadicRefreshProviderAsync(ITrace trace, CancellationToken cancellationToken)
            {
                return this.inner.MonadicRefreshProviderAsync(trace, cancellationToken);
            }

            public Task<TryCatch> MonadicSplitAsync(FeedRangeInternal feedRange, CancellationToken cancellationToken)
            {
                return this.inner.MonadicSplitAsync(feedRange, cancellationToken);
            }

            public async Task<QueryPage> QueryAsync(
                SqlQuerySpec sqlQuerySpec,
                FeedRangeState<QueryState> feedRangeState,
                QueryPaginationOptions queryPaginationOptions,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                TryCatch<QueryPage> queryPage = await this.MonadicQueryAsync(
                    sqlQuerySpec,
                    feedRangeState,
                    queryPaginationOptions,
                    trace,
                    cancellationToken);
                queryPage.ThrowIfFailed();
                return queryPage.Result;
            }

            public Task<ReadFeedPage> ReadFeedAsync(
                FeedRangeState<ReadFeedState> feedRangeState,
                ReadFeedPaginationOptions readFeedPaginationOptions,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                return this.inner.ReadFeedAsync(feedRangeState, readFeedPaginationOptions, trace, cancellationToken);
            }

            public Task<Record> ReadItemAsync(CosmosElement partitionKey, string identifier, CancellationToken cancellationToken)
            {
                return this.inner.ReadItemAsync(partitionKey, identifier, cancellationToken);
            }

            public Task RefreshProviderAsync(ITrace trace, CancellationToken cancellationToken)
            {
                return this.inner.RefreshProviderAsync(trace, cancellationToken);
            }

            public Task SplitAsync(FeedRangeInternal feedRange, CancellationToken cancellationToken)
            {
                return this.inner.SplitAsync(feedRange, cancellationToken);
            }
        }

        private static class DebugTraceHelpers
        {
            private const bool enabled = false;

            [Conditional("DEBUG")]
            public static void TracePipelineStagePage(QueryPage page)
            {
                if (enabled)
                {
                    System.Diagnostics.Trace.WriteLine("\nReceived next page from pipeline: ");
                    TracePage(page);
                }
            }

            [Conditional("DEBUG")]
            public static void TraceBackendResponse(QueryPage page)
            {
                if (enabled)
                {
                    System.Diagnostics.Trace.WriteLine("Serving query from backend: ");
                    TracePage(page);
                }
            }

            [Conditional("DEBUG")]
            public static void TracePage(QueryPage page)
            {
                if (enabled)
                {
                    System.Diagnostics.Trace.WriteLine("Page:");
                    System.Diagnostics.Trace.WriteLine($"    ActivityId: {page.ActivityId}");
                    System.Diagnostics.Trace.WriteLine($"    RequestCharge: {page.RequestCharge}");
                    System.Diagnostics.Trace.WriteLine($"    ActivityId: {page.ActivityId}");

                    System.Diagnostics.Trace.WriteLine($"    AdditionalHeaders: ");
                    foreach (KeyValuePair<string, string> header in page.AdditionalHeaders)
                    {
                        System.Diagnostics.Trace.WriteLine($"        [{header.Key}] = {header.Value}");
                    }

                    System.Diagnostics.Trace.WriteLine($"    Results:");
                    foreach (CosmosElement result in page.Documents)
                    {
                        System.Diagnostics.Trace.WriteLine($"        {result}");
                    }
                }
            }
        }


        private static async Task<IDocumentContainer> CreateDocumentContainerAsync(int documentCount)
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

            for (int i = 0; i < documentCount; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"pk\" : {i} }}");
                TryCatch<Record> monadicCreateRecord = await documentContainer.MonadicCreateItemAsync(item, cancellationToken: default);
                Assert.IsTrue(monadicCreateRecord.Succeeded);
            }

            return documentContainer;
        }
    }
}