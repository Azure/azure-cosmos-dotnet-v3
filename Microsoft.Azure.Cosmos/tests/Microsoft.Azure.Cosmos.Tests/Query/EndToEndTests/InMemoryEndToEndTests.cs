namespace Microsoft.Azure.Cosmos.Tests.Query.EndToEndTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class InMemoryEndToEndTests : EndToEndTestsBase
    {
        private static readonly PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition()
        {
            Paths = new Collection<string>()
            {
                "/pk"
            },
            Kind = PartitionKind.Hash,
            Version = PartitionKeyDefinitionVersion.V2,
        };

        internal override async Task<(IQueryableContainer, List<CosmosObject>)> CreateContainerAsync(
            IReadOnlyList<CosmosObject> documentsToInsert,
            FlakyDocumentContainer.FailureConfigs failureConfigs)
        {
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

            List<CosmosObject> insertedDocuments = new List<CosmosObject>();
            foreach (CosmosObject document in documentsToInsert)
            {
                while (true)
                {
                    TryCatch<Record> monadicCreateRecord = await documentContainer.MonadicCreateItemAsync(
                        document,
                        cancellationToken: default);
                    if (monadicCreateRecord.Succeeded)
                    {
                        insertedDocuments.Add(monadicCreateRecord.Result.ToDocument());
                        break;
                    }
                }
            }

            return (new InMemoryQueryableContainer(documentContainer), insertedDocuments);
        }

        private sealed class InMemoryQueryableContainer : IQueryableContainer
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

            private readonly IDocumentContainer documentContainer;

            public InMemoryQueryableContainer(IDocumentContainer documentContainer)
            {
                this.documentContainer = documentContainer ?? throw new ArgumentNullException(nameof(documentContainer));
            }

            public TryCatch<IQueryPipeline> MonadicCreateQueryPipeline(
                string queryText,
                int pageSize,
                int maxConcurrency,
                ExecutionEnvironment executionEnvironment,
                CosmosElement requestContinuationToken)
            {
                return TryCatch<IQueryPipeline>.FromResult(new InMemoryQueryPipeline(
                    this.documentContainer,
                    executionEnvironment,
                    new SqlQuerySpec(queryText),
                    this.documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, cancellationToken: default).Result,
                    partitionKey: null,
                    GetQueryPlan(queryText),
                    pageSize,
                    maxConcurrency,
                    requestContinuationToken));
            }

            public void Dispose()
            {
                // Do nothing since it's all in memory.
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

        private sealed class InMemoryQueryPipeline : IQueryPipeline
        {
            private readonly IDocumentContainer documentContainer;
            private readonly ExecutionEnvironment executionEnvironment;
            private readonly SqlQuerySpec query;
            private readonly List<FeedRangeEpk> feedRanges;
            private readonly Cosmos.PartitionKey? partitionKey;
            private readonly QueryInfo queryInfo;
            private readonly int pageSize;
            private readonly int maxConcurrency;
            private readonly CosmosElement continuationToken;

            public InMemoryQueryPipeline(
                IDocumentContainer documentContainer,
                ExecutionEnvironment executionEnvironment,
                SqlQuerySpec query,
                List<FeedRangeEpk> feedRanges,
                Cosmos.PartitionKey? partitionKey,
                QueryInfo queryInfo,
                int pageSize,
                int maxConcurrency,
                CosmosElement continuationToken)
            {
                this.documentContainer = documentContainer ?? throw new ArgumentNullException(nameof(documentContainer));
                this.executionEnvironment = executionEnvironment;
                this.query = query;
                this.feedRanges = feedRanges;
                this.partitionKey = partitionKey;
                this.queryInfo = queryInfo;
                this.pageSize = pageSize;
                this.maxConcurrency = maxConcurrency;
                this.continuationToken = continuationToken;
            }

            public IAsyncEnumerator<TryCatch<QueryPage>> GetAsyncEnumerator(
                CancellationToken cancellationToken = default)
            {
                TryCatch<IQueryPipelineStage> tryCreatePipeline = PipelineFactory.MonadicCreate(
                    this.executionEnvironment,
                    this.documentContainer,
                    this.query,
                    this.feedRanges,
                    this.partitionKey,
                    this.queryInfo,
                    this.pageSize,
                    this.maxConcurrency,
                    this.continuationToken,
                    requestCancellationToken: cancellationToken);

                tryCreatePipeline.ThrowIfFailed();

                return tryCreatePipeline.Result;
            }
        }
    }
}
