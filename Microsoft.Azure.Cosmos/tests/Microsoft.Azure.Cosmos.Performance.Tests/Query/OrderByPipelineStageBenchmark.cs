namespace Microsoft.Azure.Cosmos.Performance.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;

    //    [Config(typeof(ServerGcConfig))]


    [MemoryDiagnoser]
    public class OrderByPipelineStageBenchmark
    {
        private const int EndUserPageSize = 100;

        private const int MaxConcurrency = 10;

        private static readonly SqlQuerySpec SqlQuerySpec = new SqlQuerySpec(@"
            SELECT c._rid AS _rid, [{""item"": c.index}] AS orderByItems, {""index"": c.index} AS payload
            FROM c
            WHERE {documentdb-formattableorderbyquery-filter}
            ORDER BY c.index");

        private static readonly IReadOnlyList<OrderByColumn> OrderByColumns = new List<OrderByColumn>
        {
            new OrderByColumn("c.index", SortOrder.Ascending)
        };

        private static readonly IDocumentContainer StreamingContainer = MockDocumentContainer.Create(streaming: true);

        private static readonly IDocumentContainer NonStreamingContainer = MockDocumentContainer.Create(streaming: false);

        [Benchmark(Baseline = true)]
        public Task StreamingOrderByPipelineStage()
        {
            return CreateAndRunPipeline(StreamingContainer);
        }

        [Benchmark]
        public Task NonStreamingOrderByPipelineStage()
        {
            return CreateAndRunPipeline(NonStreamingContainer);
        }

        private static async Task CreateAndRunPipeline(IDocumentContainer documentContainer)
        {
            IReadOnlyList<FeedRangeEpk> ranges = await documentContainer.GetFeedRangesAsync(
                trace: NoOpTrace.Singleton,
                cancellationToken: default);

            TryCatch<IQueryPipelineStage> pipelineStage = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                    documentContainer: documentContainer,
                    sqlQuerySpec: SqlQuerySpec,
                    targetRanges: ranges,
                    partitionKey: null,
                    orderByColumns: OrderByColumns,
                    queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: EndUserPageSize),
                    maxConcurrency: MaxConcurrency,
                    continuationToken: null);

            IQueryPipelineStage pipeline = pipelineStage.Result;

            int documentCount = 0;
            while (await pipeline.MoveNextAsync(NoOpTrace.Singleton, CancellationToken.None))
            {
                TryCatch<QueryPage> tryGetQueryPage = pipeline.Current;
                QueryPage queryPage = tryGetQueryPage.Result;

                List<string> documents = new List<string>(queryPage.Documents.Count);
                foreach (CosmosElement document in queryPage.Documents)
                {
                    documents.Add(document.ToString());
                }

                documentCount += documents.Count;
            }
        }

        private sealed class MockDocumentContainer : IDocumentContainer
        {
            private const int LeafPageCount = 100;

            private const int PageSize = 1000;

            private const string ActivityId = "ActivityId";

            private const int QueryCharge = 42;

            private static readonly string CollectionRid = "1HNeAM-TiQY=";

            private static readonly IReadOnlyDictionary<string, string> AdditionalHeaders = new Dictionary<string, string>
            {
                ["x-ms-query-test-header"] = "This is a test",
            };

            private readonly IReadOnlyDictionary<FeedRange, IReadOnlyDictionary<CosmosElement, QueryPage>> pages;

            public MockDocumentContainer(IReadOnlyDictionary<FeedRange, IReadOnlyDictionary<CosmosElement, QueryPage>> pages)
            {
                this.pages = pages ?? throw new ArgumentNullException(nameof(pages));
            }

            public static IDocumentContainer Create(bool streaming)
            {
                IReadOnlyList<FeedRange> feedRanges = new List<FeedRange>
                {
                    new FeedRangeEpk(new Documents.Routing.Range<string>(string.Empty, "AA", true, false)),
                    new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "BB", true, false)),
                    new FeedRangeEpk(new Documents.Routing.Range<string>("BB", "CC", true, false)),
                    new FeedRangeEpk(new Documents.Routing.Range<string>("CC", "DD", true, false)),
                    new FeedRangeEpk(new Documents.Routing.Range<string>("DD", "EE", true, false)),
                    new FeedRangeEpk(new Documents.Routing.Range<string>("EE", "FF", true, false)),
                };

                int feedRangeIndex = 0;
                Dictionary<FeedRange, IReadOnlyDictionary<CosmosElement, QueryPage>> pages = new Dictionary<FeedRange, IReadOnlyDictionary<CosmosElement, QueryPage>>();
                foreach (FeedRangeEpk feedRange in feedRanges)
                {
                    int index = feedRangeIndex;
                    Dictionary<CosmosElement, QueryPage> leafPages = new Dictionary<CosmosElement, QueryPage>();
                    for (int pageIndex = 0; pageIndex < LeafPageCount; ++pageIndex)
                    {
                        CosmosElement state = pageIndex == 0 ? CosmosNull.Create() : CosmosString.Create(pageIndex.ToString());
                        CosmosElement continuationToken = pageIndex == LeafPageCount - 1 ? null : CosmosString.Create((pageIndex + 1).ToString());

                        List<CosmosElement> documents = new List<CosmosElement>(PageSize);
                        for (int documentCount = 0; documentCount < PageSize; ++documentCount)
                        {
                            documents.Add(CreateDocument(index));
                            index += feedRanges.Count;
                        }

                        QueryPage queryPage = new QueryPage(
                            documents: documents,
                            requestCharge: QueryCharge,
                            activityId: ActivityId,
                            cosmosQueryExecutionInfo: null,
                            distributionPlanSpec: null,
                            disallowContinuationTokenMessage: null,
                            state: continuationToken != null ? new QueryState(continuationToken) : null,
                            additionalHeaders: AdditionalHeaders,
                            streaming: streaming);

                        leafPages.Add(state, queryPage);
                    }

                    pages.Add(feedRange, leafPages);
                    ++feedRangeIndex;
                }

                return new MockDocumentContainer(pages);
            }

            private static CosmosElement CreateDocument(int index)
            {
                Documents.ResourceId resourceId = Documents.ResourceId.NewCollectionChildResourceId(
                    CollectionRid,
                    (ulong)index,
                    Documents.ResourceType.Document);

                string documentText = "{" +
                    "\"_rid\":\"" + resourceId.ToString() + "\"," +
                    "\"orderByItems\":" + "[{\"item\": "+ index + "}]," +
                    "\"payload\": {\"index\": " + index + "}" +
                    "}";

                CosmosElement document = CosmosObject.Parse(documentText);
                return document;
            }

            public Task<ChangeFeedPage> ChangeFeedAsync(FeedRangeState<ChangeFeedState> feedRangeState, ChangeFeedPaginationOptions changeFeedPaginationOptions, ITrace trace, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<Record> CreateItemAsync(CosmosObject payload, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<List<FeedRangeEpk>> GetChildRangeAsync(FeedRangeInternal feedRange, ITrace trace, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<List<FeedRangeEpk>> GetFeedRangesAsync(ITrace trace, CancellationToken cancellationToken)
            {
                return Task.FromResult(
                    this.pages.Keys
                        .Cast<FeedRangeEpk>()
                        .ToList());
            }

            public Task<string> GetResourceIdentifierAsync(ITrace trace, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task MergeAsync(FeedRangeInternal feedRange1, FeedRangeInternal feedRange2, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<TryCatch<ChangeFeedPage>> MonadicChangeFeedAsync(FeedRangeState<ChangeFeedState> feedRangeState, ChangeFeedPaginationOptions changeFeedPaginationOptions, ITrace trace, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<TryCatch<Record>> MonadicCreateItemAsync(CosmosObject payload, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetChildRangeAsync(FeedRangeInternal feedRange, ITrace trace, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetFeedRangesAsync(ITrace trace, CancellationToken cancellationToken)
            {
                return Task.FromResult(
                    TryCatch<List<FeedRangeEpk>>.FromResult(
                        this.pages.Keys
                        .Cast<FeedRangeEpk>()
                        .ToList()));
            }

            public Task<TryCatch<string>> MonadicGetResourceIdentifierAsync(ITrace trace, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<TryCatch> MonadicMergeAsync(FeedRangeInternal feedRange1, FeedRangeInternal feedRange2, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public async Task<TryCatch<QueryPage>> MonadicQueryAsync(SqlQuerySpec sqlQuerySpec, FeedRangeState<QueryState> feedRangeState, QueryPaginationOptions queryPaginationOptions, ITrace trace, CancellationToken cancellationToken)
            {
                CosmosElement state = feedRangeState.State?.Value ?? CosmosNull.Create();
                QueryPage queryPage = this.pages[feedRangeState.FeedRange][state];

                await Task.Delay(TimeSpan.FromMilliseconds(20));
                return TryCatch<QueryPage>.FromResult(queryPage);
            }

            public Task<TryCatch<ReadFeedPage>> MonadicReadFeedAsync(FeedRangeState<ReadFeedState> feedRangeState, ReadFeedPaginationOptions readFeedPaginationOptions, ITrace trace, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<TryCatch<Record>> MonadicReadItemAsync(CosmosElement partitionKey, string identifer, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<TryCatch> MonadicRefreshProviderAsync(ITrace trace, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<TryCatch> MonadicSplitAsync(FeedRangeInternal feedRange, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<QueryPage> QueryAsync(SqlQuerySpec sqlQuerySpec, FeedRangeState<QueryState> feedRangeState, QueryPaginationOptions queryPaginationOptions, ITrace trace, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<ReadFeedPage> ReadFeedAsync(FeedRangeState<ReadFeedState> feedRangeState, ReadFeedPaginationOptions readFeedPaginationOptions, ITrace trace, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<Record> ReadItemAsync(CosmosElement partitionKey, string identifier, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task RefreshProviderAsync(ITrace trace, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task SplitAsync(FeedRangeInternal feedRange, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }

         
    }
}
