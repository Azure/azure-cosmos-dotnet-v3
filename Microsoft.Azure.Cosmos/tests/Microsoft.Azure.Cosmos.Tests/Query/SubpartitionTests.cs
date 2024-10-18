﻿namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml;
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
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Test.BaselineTest;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class SubpartitionTests : BaselineTests<SubpartitionTestInput, SubpartitionTestOutput>
    {
        private const int DocumentCount = 100;
        private const int SplitPartitionKey = 2;

        [TestMethod]
        public void TestQueriesOnSplitContainer()
        {
            List<SubpartitionTestInput> inputs = new List<SubpartitionTestInput>
                {
                    new SubpartitionTestInput(description: "SELECT", query: @"SELECT c.id, c.value2 FROM c", ode: true),
                    new SubpartitionTestInput(description: "SELECT without ODE", query: @"SELECT c.id, c.value2 FROM c", ode: false),
                    new SubpartitionTestInput(description: "SELECT ORDER BY with ODE", query: @"SELECT c.id, c.value2, c.intVal FROM c ORDER BY c.intVal", ode: true, sortResults: false),
                    new SubpartitionTestInput(description: "SELECT ORDER BY without ODE", query: @"SELECT c.id, c.value2, c.intVal FROM c ORDER BY c.intVal", ode: false, sortResults: false),
                };

            this.ExecuteTestSuite(inputs);
        }

        /// <summary>
        /// The test is a baseline for mock framework which splits the container at the top level of a hierarchical partition key.
        /// After split, it is expected that more than one physical partitions contain data for some value of a top level path of partition key.
        /// Please note that this does NOT occur in a single-partition key scenario where all data for a given value of a partition key
        ///  is contained within single physical partition.
        /// This situation is known to create issues, especially while running queries due to inconsistent handling of FeedRangePartitionKey and FeedRangeEpk in the SDK stack.
        /// Test framework's behavior in being able to replicate this situation is critical to for ensuring that tests provide sufficient protection against regressions.
        /// </summary>
        [TestMethod]
        public async Task VerifyTestFrameworkSupportsPartitionSplit()
        {
            PartitionKeyDefinition partitionKeyDefinition = CreatePartitionKeyDefinition();
            InMemoryContainer inMemoryContainer = await CreateSplitInMemoryDocumentContainerAsync(DocumentCount, partitionKeyDefinition);
            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKeyBuilder().Add(SplitPartitionKey.ToString()).Build();
            FeedRangePartitionKey feedRangePartitionKey = new FeedRangePartitionKey(partitionKey);
            FeedRangeEpk feedRangeEpk = InMemoryContainer.ResolveFeedRangeBasedOnPrefixContainer(feedRangePartitionKey, partitionKeyDefinition) as FeedRangeEpk;
            Assert.IsNotNull(feedRangeEpk);
            TryCatch<int> pkRangeId = inMemoryContainer.MonadicGetPartitionKeyRangeIdFromFeedRange(feedRangeEpk);
            Assert.IsTrue(pkRangeId.Failed, $"Expected to fail for partition key {SplitPartitionKey}");
            Assert.IsTrue(pkRangeId.Exception.InnerException.Message.StartsWith("Epk Range: [B5-D7-B7-26-D6-EA-DB-11-F1-EF-AD-92-12-15-D6-60,B5-D7-B7-26-D6-EA-DB-11-F1-EF-AD-92-12-15-D6-60-FF) is gone."), "Gone exception is expected!");
        }

        public override SubpartitionTestOutput ExecuteTest(SubpartitionTestInput input)
        {
            QueryRequestOptions queryRequestOptions = new QueryRequestOptions()
            {
                PartitionKey = new PartitionKeyBuilder().Add(SplitPartitionKey.ToString()).Build()
            };

            IMonadicDocumentContainer monadicDocumentContainer = CreateSplitDocumentContainerAsync(DocumentCount, queryRequestOptions).Result;
            DocumentContainer documentContainer = new DocumentContainer(monadicDocumentContainer);
            TryCatch _ = monadicDocumentContainer.MonadicRefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default).Result;
            List<FeedRangeEpk> containerRanges = documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, cancellationToken: default).Result;

            List<CosmosElement> documents = new List<CosmosElement>();
            IQueryPipelineStage queryPipelineStage = CreateQueryPipelineStage(documentContainer, input, queryRequestOptions, containerRanges);

            while (queryPipelineStage.MoveNextAsync(NoOpTrace.Singleton, cancellationToken: default).Result)
            {
                TryCatch<QueryPage> tryGetPage = queryPipelineStage.Current;

                if (tryGetPage.Failed)
                {
                    Assert.Fail("Unexpected error. Gone Exception should not reach till here");
                }

                documents.AddRange(tryGetPage.Result.Documents);
            }

            return new SubpartitionTestOutput(documents, input.SortResults);
        }

        private static IQueryPipelineStage CreateQueryPipelineStage(
            DocumentContainer documentContainer,
            SubpartitionTestInput input,
            QueryRequestOptions queryRequestOptions,
            IReadOnlyList<FeedRangeEpk> containerRanges)
        {
            string query = input.Query;
            CosmosElement continuationToken = null;
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition()
            {
                Paths = new System.Collections.ObjectModel.Collection<string>()
                {
                    "/id",
                    "/value1",
                    "/value2"
                },
                Kind = PartitionKind.MultiHash,
                Version = PartitionKeyDefinitionVersion.V2,
            };

            queryRequestOptions.EnableOptimisticDirectExecution = input.ODE;

            CosmosSerializerCore serializerCore = new();
            using StreamReader streamReader = new(serializerCore.ToStreamSqlQuerySpec(new SqlQuerySpec(query), Documents.ResourceType.Document));
            string sqlQuerySpecJsonString = streamReader.ReadToEnd();

            (PartitionedQueryExecutionInfo partitionedQueryExecutionInfo, QueryPartitionProvider queryPartitionProvider) = GetPartitionedQueryExecutionInfoAndPartitionProvider(sqlQuerySpecJsonString, partitionKeyDefinition);
            CosmosQueryExecutionContextFactory.InputParameters inputParameters = CosmosQueryExecutionContextFactory.InputParameters.Create(
                sqlQuerySpec: new SqlQuerySpec(query),
                initialUserContinuationToken: continuationToken,
                initialFeedRange: null,
                maxConcurrency: queryRequestOptions.MaxConcurrency,
                maxItemCount: queryRequestOptions.MaxItemCount,
                maxBufferedItemCount: queryRequestOptions.MaxBufferedItemCount,
                partitionKey: queryRequestOptions.PartitionKey,
                properties: new Dictionary<string, object>() { { "x-ms-query-partitionkey-definition", partitionKeyDefinition } },
                partitionedQueryExecutionInfo: null,
                returnResultsInDeterministicOrder: null,
                enableOptimisticDirectExecution: queryRequestOptions.EnableOptimisticDirectExecution,
                isNonStreamingOrderByQueryFeatureDisabled: queryRequestOptions.IsNonStreamingOrderByQueryFeatureDisabled,
                enableDistributedQueryGatewayMode: queryRequestOptions.EnableDistributedQueryGatewayMode,
                testInjections: queryRequestOptions.TestSettings);

            List<PartitionKeyRange> targetPkRanges = new();
            foreach (FeedRangeEpk feedRangeEpk in containerRanges)
            {
                targetPkRanges.Add(new PartitionKeyRange
                {
                    MinInclusive = feedRangeEpk.Range.Min,
                    MaxExclusive = feedRangeEpk.Range.Max,
                });
            }

            string databaseId = "db1234";
            string resourceLink = $"dbs/{databaseId}/colls";
            CosmosQueryContextCore cosmosQueryContextCore = new CosmosQueryContextCore(
                client: new TestCosmosQueryClient(queryPartitionProvider, targetPkRanges),
                resourceTypeEnum: Documents.ResourceType.Document,
                operationType: Documents.OperationType.Query,
                resourceType: typeof(QueryResponseCore),
                resourceLink: resourceLink,
                isContinuationExpected: true,
                allowNonValueAggregateQuery: true,
                useSystemPrefix: false,
                correlatedActivityId: Guid.NewGuid());

            IQueryPipelineStage queryPipelineStage = CosmosQueryExecutionContextFactory.Create(
                        documentContainer,
                        cosmosQueryContextCore,
                        inputParameters,
                        NoOpTrace.Singleton);

            return queryPipelineStage;
        }

        internal static Tuple<PartitionedQueryExecutionInfo, QueryPartitionProvider> GetPartitionedQueryExecutionInfoAndPartitionProvider(string querySpecJsonString, PartitionKeyDefinition pkDefinition)
        {
            QueryPartitionProvider queryPartitionProvider = CreateCustomQueryPartitionProvider();
            TryCatch<PartitionedQueryExecutionInfo> tryGetQueryPlan = queryPartitionProvider.TryGetPartitionedQueryExecutionInfo(
                querySpecJsonString: querySpecJsonString,
                partitionKeyDefinition: pkDefinition,
                vectorEmbeddingPolicy: null,
                requireFormattableOrderByQuery: true,
                isContinuationExpected: true,
                allowNonValueAggregateQuery: true,
                hasLogicalPartitionKey: false,
                allowDCount: true,
                useSystemPrefix: false,
                geospatialType: Cosmos.GeospatialType.Geography);

            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo = tryGetQueryPlan.Succeeded ? tryGetQueryPlan.Result : throw tryGetQueryPlan.Exception;
            return Tuple.Create(partitionedQueryExecutionInfo, queryPartitionProvider);
        }

        private static QueryPartitionProvider CreateCustomQueryPartitionProvider()
        {
            Dictionary<string, object> queryEngineConfiguration = new Dictionary<string, object>()
            {
                {"maxSqlQueryInputLength", 262144},
                {"maxJoinsPerSqlQuery", 5},
                {"maxLogicalAndPerSqlQuery", 2000},
                {"maxLogicalOrPerSqlQuery", 2000},
                {"maxUdfRefPerSqlQuery", 10},
                {"maxInExpressionItemsCount", 16000},
                {"queryMaxGroupByTableCellCount", 500000 },
                {"queryMaxInMemorySortDocumentCount", 500},
                {"maxQueryRequestTimeoutFraction", 0.90},
                {"sqlAllowNonFiniteNumbers", false},
                {"sqlAllowAggregateFunctions", true},
                {"sqlAllowSubQuery", true},
                {"sqlAllowScalarSubQuery", true},
                {"allowNewKeywords", true},
                {"sqlAllowLike", true},
                {"sqlAllowGroupByClause", true},
                {"maxSpatialQueryCells", 12},
                {"spatialMaxGeometryPointCount", 256},
                {"sqlDisableQueryILOptimization", false},
                {"sqlDisableFilterPlanOptimization", false},
                {"clientDisableOptimisticDirectExecution", false}
            };

            return new QueryPartitionProvider(queryEngineConfiguration);
        }

        internal static PartitionKeyDefinition CreatePartitionKeyDefinition()
        {
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition()
            {
                Paths = new System.Collections.ObjectModel.Collection<string>()
                {
                    "/id",
                    "/value1",
                    "/value2"
                },
                Kind = PartitionKind.MultiHash,
                Version = PartitionKeyDefinitionVersion.V2,
            };

            return partitionKeyDefinition;
        }

        private static async Task<IDocumentContainer> CreateSplitDocumentContainerAsync(int numItems, QueryRequestOptions queryRequestOptions)
        {
            PartitionKeyDefinition partitionKeyDefinition = CreatePartitionKeyDefinition();
            InMemoryContainer inMemoryContainer = await CreateSplitInMemoryDocumentContainerAsync(numItems, partitionKeyDefinition, queryRequestOptions);
            DocumentContainer documentContainer = new DocumentContainer(inMemoryContainer);
            return documentContainer;
        }

        private static async Task<InMemoryContainer> CreateSplitInMemoryDocumentContainerAsync(int numItems, PartitionKeyDefinition partitionKeyDefinition, QueryRequestOptions queryRequestOptions = null)
        {
            InMemoryContainer inMemoryContainer = new InMemoryContainer(partitionKeyDefinition, createSplitForMultiHashAtSecondlevel: true, resolvePartitionsBasedOnPrefix: true, queryRequestOptions: queryRequestOptions);
            for (int i = 0; i < numItems; i++)
            {
                CosmosObject item = CosmosObject.Parse($"{{\"id\" : \"{i % 5}\", \"value1\" : \"{Guid.NewGuid()}\", \"value2\" : \"{i}\", \"intVal\" : {(numItems/2) - i} }}");
                while (true)
                {
                    TryCatch<Record> monadicCreateRecord = await inMemoryContainer.MonadicCreateItemAsync(item, cancellationToken: default);
                    if (monadicCreateRecord.Succeeded)
                    {
                        break;
                    }
                }
            }

            await inMemoryContainer.MonadicSplitAsync(FeedRangeEpk.FullRange, cancellationToken: default);

            return inMemoryContainer;
        }

        internal class TestCosmosQueryClient : CosmosQueryClient
        {
            private readonly QueryPartitionProvider queryPartitionProvider;
            private readonly IReadOnlyList<PartitionKeyRange> targetPartitionKeyRanges;

            public TestCosmosQueryClient(QueryPartitionProvider queryPartitionProvider, IEnumerable<PartitionKeyRange> targetPartitionKeyRanges)
            {
                this.queryPartitionProvider = queryPartitionProvider;
                this.targetPartitionKeyRanges = targetPartitionKeyRanges.ToList();
            }

            public override Action<IQueryable> OnExecuteScalarQueryCallback => throw new NotImplementedException();

            public override bool BypassQueryParsing()
            {
                return false;
            }

            public override void ClearSessionTokenCache(string collectionFullName)
            {
                throw new NotImplementedException();
            }

            public override Task<TryCatch<QueryPage>> ExecuteItemQueryAsync(string resourceUri, ResourceType resourceType, OperationType operationType, Cosmos.FeedRange feedRange, QueryRequestOptions requestOptions, AdditionalRequestHeaders additionalRequestHeaders, SqlQuerySpec sqlQuerySpec, string continuationToken, int pageSize, ITrace trace, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public override Task<PartitionedQueryExecutionInfo> ExecuteQueryPlanRequestAsync(string resourceUri, ResourceType resourceType, OperationType operationType, SqlQuerySpec sqlQuerySpec, Cosmos.PartitionKey? partitionKey, string supportedQueryFeatures, Guid clientQueryCorrelationId, ITrace trace, CancellationToken cancellationToken)
            {
                return Task.FromResult(new PartitionedQueryExecutionInfo());
            }

            public override Task ForceRefreshCollectionCacheAsync(string collectionLink, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public override Task<ContainerQueryProperties> GetCachedContainerQueryPropertiesAsync(string containerLink, Cosmos.PartitionKey? partitionKey, ITrace trace, CancellationToken cancellationToken)
            {
                List<string> hashes = new();
                foreach (Documents.Routing.IPartitionKeyComponent component in partitionKey.Value.InternalKey.Components)
                {
                    PartitionKeyHash partitionKeyHash = component switch
                    {
                        null => PartitionKeyHash.V2.HashUndefined(),
                        Documents.Routing.StringPartitionKeyComponent stringPartitionKey => PartitionKeyHash.V2.Hash((string)stringPartitionKey.ToObject()),
                        Documents.Routing.NumberPartitionKeyComponent numberPartitionKey => PartitionKeyHash.V2.Hash(Number64.ToDouble(numberPartitionKey.Value)),
                        _ => throw new ArgumentOutOfRangeException(),
                    };
                    hashes.Add(partitionKeyHash.Value);
                }

                string min = string.Join(string.Empty, hashes);
                string max = min + "-FF";
                return Task.FromResult(new ContainerQueryProperties(
                    "test",
                    new List<Documents.Routing.Range<string>>
                    {
                        new Documents.Routing.Range<string>(
                            min,
                            max,
                            true,
                            true)
                    },
                    SubpartitionTests.CreatePartitionKeyDefinition(),
                    vectorEmbeddingPolicy: null,
                    Cosmos.GeospatialType.Geometry));
            }

            public override Task<bool> GetClientDisableOptimisticDirectExecutionAsync()
            {
                return Task.FromResult(this.queryPartitionProvider.ClientDisableOptimisticDirectExecution);
            }

            public override Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangeByFeedRangeAsync(string resourceLink, string collectionResourceId, PartitionKeyDefinition partitionKeyDefinition, FeedRangeInternal feedRangeInternal, bool forceRefresh, ITrace trace)
            {
                throw new NotImplementedException();
            }

            public override Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangesAsync(string resourceLink, string collectionResourceId, IReadOnlyList<Documents.Routing.Range<string>> providedRanges, bool forceRefresh, ITrace trace)
            {
                return Task.FromResult(this.targetPartitionKeyRanges.ToList());
            }

            public override Task<IReadOnlyList<PartitionKeyRange>> TryGetOverlappingRangesAsync(string collectionResourceId, Documents.Routing.Range<string> range, bool forceRefresh = false)
            {
                throw new NotImplementedException();
            }

            public override Task<TryCatch<PartitionedQueryExecutionInfo>> TryGetPartitionedQueryExecutionInfoAsync(
                SqlQuerySpec sqlQuerySpec,
                ResourceType resourceType,
                PartitionKeyDefinition partitionKeyDefinition,
                Cosmos.VectorEmbeddingPolicy vectorEmbeddingPolicy,
                bool requireFormattableOrderByQuery,
                bool isContinuationExpected,
                bool allowNonValueAggregateQuery,
                bool hasLogicalPartitionKey,
                bool allowDCount,
                bool useSystemPrefix,
                Cosmos.GeospatialType geospatialType,
                CancellationToken cancellationToken)
            {
                CosmosSerializerCore serializerCore = new();
                using StreamReader streamReader = new(serializerCore.ToStreamSqlQuerySpec(sqlQuerySpec, Documents.ResourceType.Document));
                string sqlQuerySpecJsonString = streamReader.ReadToEnd();

                (PartitionedQueryExecutionInfo partitionedQueryExecutionInfo, QueryPartitionProvider queryPartitionProvider) = OptimisticDirectExecutionQueryBaselineTests.GetPartitionedQueryExecutionInfoAndPartitionProvider(sqlQuerySpecJsonString, partitionKeyDefinition);
                return Task.FromResult(TryCatch<PartitionedQueryExecutionInfo>.FromResult(partitionedQueryExecutionInfo));
            }
        }
    }

    public class SubpartitionTestInput : BaselineTestInput
    {
        public SubpartitionTestInput(string description, string query, bool ode, bool sortResults = true)
            :base(description)
        {
            this.Query = query;
            this.ODE = ode;
            this.SortResults = sortResults;
        }

        internal string Query { get; }

        internal bool ODE { get; }

        internal bool SortResults { get; }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteElementString("Description", this.Description);
            xmlWriter.WriteStartElement("Query");
            xmlWriter.WriteCData(this.Query);
            xmlWriter.WriteEndElement();
            xmlWriter.WriteElementString("ODE", this.ODE.ToString());
        }
    }

    public class SubpartitionTestOutput : BaselineTestOutput
    {
        private readonly List<CosmosElement> documents;
        private readonly bool sortResults;

        internal SubpartitionTestOutput(IReadOnlyList<CosmosElement> documents, bool sortResults)
        {
            this.documents = documents.ToList();
            this.sortResults = sortResults;
        }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteStartElement("Documents");

            IEnumerable<string> lines = this.documents.Select(doc => doc.ToString());
            if(this.sortResults)
            {
                lines = lines.OrderBy(serializedDoc => serializedDoc);
            }

            string content = string.Join($",{Environment.NewLine}", lines);
            xmlWriter.WriteCData(content);
            xmlWriter.WriteEndElement();
        }
    }
}
