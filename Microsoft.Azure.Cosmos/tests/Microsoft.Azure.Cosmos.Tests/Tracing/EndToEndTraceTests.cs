namespace Microsoft.Azure.Cosmos.Tests.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.ReadFeed;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public abstract class EndToEndTraceTests
    {
        private static readonly Dictionary<string, object> DefaultQueryEngineConfiguration = new Dictionary<string, object>()
        {
            {"maxSqlQueryInputLength", 30720},
            {"maxJoinsPerSqlQuery", 5},
            {"maxLogicalAndPerSqlQuery", 200},
            {"maxLogicalOrPerSqlQuery", 200},
            {"maxUdfRefPerSqlQuery", 2},
            {"maxInExpressionItemsCount", 8000},
            {"queryMaxInMemorySortDocumentCount", 500},
            {"maxQueryRequestTimeoutFraction", 0.90},
            {"sqlAllowNonFiniteNumbers", false},
            {"sqlAllowAggregateFunctions", true},
            {"sqlAllowSubQuery", true},
            {"sqlAllowScalarSubQuery", false},
            {"allowNewKeywords", true},
            {"sqlAllowLike", false},
            {"sqlAllowGroupByClause", false},
            {"maxSpatialQueryCells", 12},
            {"spatialMaxGeometryPointCount", 256},
            {"sqlDisableQueryILOptimization", false},
            {"sqlDisableFilterPlanOptimization", false}
        };

        private static readonly QueryPartitionProvider queryPartitionProvider = new QueryPartitionProvider(DefaultQueryEngineConfiguration);
        private static readonly Documents.PartitionKeyDefinition partitionKeyDefinition = new Documents.PartitionKeyDefinition()
        {
            Paths = new Collection<string>()
            {
                "/pk"
            },
            Kind = Documents.PartitionKind.Hash,
            Version = Documents.PartitionKeyDefinitionVersion.V2,
        };

        [TestMethod]
        public async Task ReadFeedAsync()
        {
            int numItems = 100;
            IDocumentContainer documentContainer = await this.CreateDocumentContainerAsync(numItems);
            CrossPartitionReadFeedAsyncEnumerator enumerator = CrossPartitionReadFeedAsyncEnumerator.Create(
                documentContainer,
                new QueryRequestOptions(),
                new CrossFeedRangeState<ReadFeedState>(ReadFeedCrossFeedRangeState.CreateFromBeginning().FeedRangeStates),
                pageSize: 10,
                cancellationToken: default);

            int numChildren = 1; // One extra since we need to read one past the last user page to get the null continuation.
            Trace rootTrace;
            using (rootTrace = Trace.GetRootTrace("Cross Partition Read Feed"))
            {
                while (await enumerator.MoveNextAsync(rootTrace))
                {
                    numChildren++;
                }
            }

            string traceString = TraceWriter.TraceToText(rootTrace);

            Console.WriteLine(traceString);

            Assert.AreEqual(numChildren, rootTrace.Children.Count);
        }

        [TestMethod]
        public async Task QueryAsync()
        {
            int numItems = 100;
            IDocumentContainer documentContainer = await this.CreateDocumentContainerAsync(numItems);
            IQueryPipelineStage pipelineStage = CreatePipeline(documentContainer, "SELECT * FROM c", pageSize: 10);

            Trace rootTrace;
            int numChildren = 1; // One extra since we need to read one past the last user page to get the null continuation.
            using (rootTrace = Trace.GetRootTrace("Cross Partition Query"))
            {
                while (await pipelineStage.MoveNextAsync(rootTrace))
                {
                    numChildren++;
                }
            }

            string traceString = TraceWriter.TraceToText(rootTrace);

            Console.WriteLine(traceString);

            Assert.AreEqual(numChildren, rootTrace.Children.Count);
        }

        [TestMethod]
        public async Task ChangeFeedAsync()
        {
            int numItems = 100;
            IDocumentContainer documentContainer = await this.CreateDocumentContainerAsync(numItems);
            CrossPartitionChangeFeedAsyncEnumerator enumerator = CrossPartitionChangeFeedAsyncEnumerator.Create(
                documentContainer,
                ChangeFeedMode.Incremental,
                new ChangeFeedRequestOptions()
                { 
                    PageSizeHint = int.MaxValue
                },
                new CrossFeedRangeState<ChangeFeedState>(
                    ChangeFeedCrossFeedRangeState.CreateFromBeginning().FeedRangeStates),
                cancellationToken: default);

            int numChildren = 0;
            Trace rootTrace;
            using (rootTrace = Trace.GetRootTrace("Cross Partition Change Feed"))
            {
                while (await enumerator.MoveNextAsync(rootTrace))
                {
                    numChildren++;

                    if (enumerator.Current.Result.Page is ChangeFeedNotModifiedPage)
                    {
                        break;
                    }
                }
            }

            string traceString = TraceWriter.TraceToText(rootTrace);

            Console.WriteLine(traceString);

            Assert.AreEqual(numChildren, rootTrace.Children.Count);
        }

        internal abstract Task<IDocumentContainer> CreateDocumentContainerAsync(
            int numItems,
            FlakyDocumentContainer.FailureConfigs failureConfigs = default);

        private static IQueryPipelineStage CreatePipeline(IDocumentContainer documentContainer, string query, int pageSize = 10, CosmosElement state = null)
        {
            TryCatch<IQueryPipelineStage> tryCreatePipeline = PipelineFactory.MonadicCreate(
                ExecutionEnvironment.Compute,
                documentContainer,
                new SqlQuerySpec(query),
                new List<FeedRangeEpk>() { FeedRangeEpk.FullRange },
                partitionKey: null,
                GetQueryPlan(query),
                pageSize: pageSize,
                maxConcurrency: 10,
                requestCancellationToken: default,
                requestContinuationToken: state);

            tryCreatePipeline.ThrowIfFailed();

            return tryCreatePipeline.Result;
        }

        private static QueryInfo GetQueryPlan(string query)
        {
            TryCatch<PartitionedQueryExecutionInfoInternal> info = queryPartitionProvider.TryGetPartitionedQueryExecutionInfoInternal(
                new SqlQuerySpec(query),
                partitionKeyDefinition,
                requireFormattableOrderByQuery: true,
                isContinuationExpected: false,
                allowNonValueAggregateQuery: true,
                hasLogicalPartitionKey: false);

            info.ThrowIfFailed();
            return info.Result.QueryInfo;
        }
    }
}
