namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Xml;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Test.BaselineTest;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Moq;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Routing;
    using System.Threading;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using System.IO;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.OptimisticDirectExecutionQuery;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel;

    [TestClass]
    public class OptimisticDirectExecutionQueryBaselineTests : BaselineTests<OptimisticDirectExecutionTestInput, OptimisticDirectExecutionTestOutput>
    {
        [TestMethod]
        [Owner("akotalwar")]
        public void TestPipelineNullContinuationToken()
        {
            Mock<IDocumentContainer> mockDocumentContainer = new Mock<IDocumentContainer>();

            TryCatch<IQueryPipelineStage> monadicCreate = OptimisticDirectExecutionQueryPipelineStage.MonadicCreate(
                documentContainer: mockDocumentContainer.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT VALUE COUNT(1) FROM c"),
                targetRange: FeedRangeEpk.FullRange,
                queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: 10),
                partitionKey: null,
                cancellationToken: default,
                continuationToken: null);
            Assert.IsTrue(monadicCreate.Succeeded);
        }

        [TestMethod]
        [Owner("akotalwar")]
        public void TestPipelineSingleContinuationToken()
        {
            Mock<IDocumentContainer> mockDocumentContainer = new Mock<IDocumentContainer>();

            ParallelContinuationToken parallelContinuationToken = new ParallelContinuationToken(
                token: "asdf",
                range: new Documents.Routing.Range<string>("A", "B", true, false));

            OptimisticDirectExecutionContinuationToken token = new OptimisticDirectExecutionContinuationToken(parallelContinuationToken);
            CosmosElement cosmosElementContinuationToken = OptimisticDirectExecutionContinuationToken.ToCosmosElement(token);

            TryCatch<IQueryPipelineStage> monadicCreate = OptimisticDirectExecutionQueryPipelineStage.MonadicCreate(
                documentContainer: mockDocumentContainer.Object,
                sqlQuerySpec: new SqlQuerySpec("SELECT * FROM c"),
                targetRange: new FeedRangeEpk(new Documents.Routing.Range<string>(min: "A", max: "B", isMinInclusive: true, isMaxInclusive: false)),
                queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: 10),
                partitionKey: null,
                cancellationToken: default,
                continuationToken: cosmosElementContinuationToken);
            Assert.IsTrue(monadicCreate.Succeeded);
        }

        // test checks that the pipeline can take a query to the backend and returns its associated document(s). 
        [TestMethod]
        public async Task TestPipelineForBackendDocumentsAsync()
        {
            int numItems = 10;
            string query = "SELECT VALUE COUNT(1) FROM c";
            IDocumentContainer inMemoryCollection = await CreateDocumentContainerAsync(numItems);
            IQueryPipelineStage queryPipelineStage = await CreateOptimisticDirectExecutionPipelineStateAsync(inMemoryCollection, query, continuationToken: null);
            int documentCountInSinglePartition = 0;

            while (await queryPipelineStage.MoveNextAsync(NoOpTrace.Singleton))
            {
                TryCatch<QueryPage> tryGetPage = queryPipelineStage.Current;
                tryGetPage.ThrowIfFailed();

                documentCountInSinglePartition += Int32.Parse(tryGetPage.Result.Documents[0].ToString());
            }

            Assert.AreEqual(documentCountInSinglePartition, 4);
        }

        // test checks that the pipeline can take a query to the backend and returns its associated document(s) + continuation token.
        [TestMethod]
        public async Task TestPipelineForContinuationTokenAsync()
        {
            int numItems = 100;
            string query = "SELECT * FROM c";
            IDocumentContainer inMemoryCollection = await CreateDocumentContainerAsync(numItems);
            IQueryPipelineStage queryPipelineStage = await CreateOptimisticDirectExecutionPipelineStateAsync(inMemoryCollection, query, continuationToken: null);
            List<CosmosElement> documents = new List<CosmosElement>();
            int continuationTokenCount = 0;

            while (await queryPipelineStage.MoveNextAsync(NoOpTrace.Singleton))
            {
                TryCatch<QueryPage> tryGetPage = queryPipelineStage.Current;
                tryGetPage.ThrowIfFailed();

                documents.AddRange(tryGetPage.Result.Documents);

                if (tryGetPage.Result.State == null)
                {
                    break;
                }
                else
                {
                    queryPipelineStage = await CreateOptimisticDirectExecutionPipelineStateAsync(inMemoryCollection, query, continuationToken: tryGetPage.Result.State.Value);
                }

                continuationTokenCount++;
            }

            Assert.AreEqual(continuationTokenCount, 2);
            Assert.AreEqual(documents.Count, 17);
        }

        // This test confirms that TestInjection.EnableOptimisticDirectExection is set to false from default. 
        // Check test "TestPipelineForDistributedQueryAsync" to understand why this is done
        [TestMethod]
        public async Task TestDefaultTestInjectionSettings()
        {
            TestInjections testInjection = new TestInjections(simulate429s: false, simulateEmptyPages: false);

            Assert.AreEqual(testInjection.EnableOptimisticDirectExecution, false);
        }

        // The reason we have the below test is to show the missing capabilities of the OptimisticDirectExecution pipeline.
        // Currently this pipeline cannot handle distributed queries as it does not have the logic to sum up the values it gets from the backend in partial results.
        // This functionality is available for other pipelines such as the ParallelCrossPartitionQueryPipelineStage as evident below
        [TestMethod]
        public async Task TestPipelineForDistributedQueryAsync()
        {
            int numItems = 100;
            string query = "SELECT VALUE COUNT(1) FROM c";
            IDocumentContainer inMemoryCollection = await CreateDocumentContainerAsync(numItems);
            IQueryPipelineStage optimisticDirectExecutionQueryPipelineStage = await CreateOptimisticDirectExecutionPipelineStateAsync(inMemoryCollection, query, continuationToken: null);
            IQueryPipelineStage parallelQueryPipelineStage = await CreateParallelCrossPartitionPipelineStateAsync(inMemoryCollection, query, continuationToken: null);
            int documentCountOptimisticPipeline = 0;
            int documentCountParallelPipeline = 0;

            List<IQueryPipelineStage> queryPipelineStages = new List<IQueryPipelineStage>
            { 
                optimisticDirectExecutionQueryPipelineStage,
                parallelQueryPipelineStage
            };

            List<int> documentPipelinesCount  = new List<int>
            { 
                documentCountOptimisticPipeline,
                documentCountParallelPipeline
            };

            for (int i = 0; i < queryPipelineStages.Count(); i++)
            {
                while (await queryPipelineStages[i].MoveNextAsync(NoOpTrace.Singleton))
                {
                    TryCatch<QueryPage> tryGetPage = queryPipelineStages[i].Current;
                    tryGetPage.ThrowIfFailed();

                    documentPipelinesCount[i] += Int32.Parse(tryGetPage.Result.Documents[0].ToString());

                    if (tryGetPage.Result.State == null)
                    {
                        break;
                    }
                    else
                    {
                        queryPipelineStages[i] = queryPipelineStages[i].Equals(optimisticDirectExecutionQueryPipelineStage)
                            ? await CreateOptimisticDirectExecutionPipelineStateAsync(inMemoryCollection, query, continuationToken: tryGetPage.Result.State.Value)
                            : await CreateParallelCrossPartitionPipelineStateAsync(inMemoryCollection, query, continuationToken: tryGetPage.Result.State.Value);
                    }
                }
            }

            documentCountOptimisticPipeline = documentPipelinesCount[0];
            documentCountParallelPipeline = documentPipelinesCount[1];
            int countDifference = documentCountParallelPipeline - documentCountOptimisticPipeline;

            Assert.AreNotEqual(documentCountOptimisticPipeline, documentCountParallelPipeline, countDifference.ToString());
            Assert.AreEqual(documentCountOptimisticPipeline, 17);
            Assert.AreEqual(documentCountParallelPipeline, 100);
            Assert.AreEqual(countDifference, 83);
            Assert.AreEqual(documentCountParallelPipeline, numItems);
        }

        private static async Task<IQueryPipelineStage> CreateOptimisticDirectExecutionPipelineStateAsync(IDocumentContainer documentContainer, string query, CosmosElement continuationToken)
        {
            List<FeedRangeEpk> targetRanges = await documentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);
            FeedRangeEpk firstRange = targetRanges[0];

            TryCatch<IQueryPipelineStage> monadicQueryPipelineStage = OptimisticDirectExecutionQueryPipelineStage.MonadicCreate(
            documentContainer: documentContainer,
            sqlQuerySpec: new SqlQuerySpec(query),
            targetRange: firstRange,
            queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: 10),
            partitionKey: null,
            cancellationToken: default,
            continuationToken: continuationToken);

            Assert.IsTrue(monadicQueryPipelineStage.Succeeded);
            IQueryPipelineStage queryPipelineStage = monadicQueryPipelineStage.Result;

            return queryPipelineStage;
        }

        private static async Task<IQueryPipelineStage> CreateParallelCrossPartitionPipelineStateAsync(IDocumentContainer documentContainer, string query, CosmosElement continuationToken)
        {
            List<FeedRangeEpk> targetRanges = await documentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);

            TryCatch<IQueryPipelineStage> monadicQueryPipelineStage = ParallelCrossPartitionQueryPipelineStage.MonadicCreate(
            documentContainer: documentContainer,
            sqlQuerySpec: new SqlQuerySpec(query),
            targetRanges: targetRanges,
            queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: 10),
            partitionKey: null,
            maxConcurrency: 10,
            prefetchPolicy: PrefetchPolicy.PrefetchSinglePage,
            cancellationToken: default,
            continuationToken: continuationToken);

            Assert.IsTrue(monadicQueryPipelineStage.Succeeded);
            IQueryPipelineStage queryPipelineStage = monadicQueryPipelineStage.Result;

            return queryPipelineStage;
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

        [TestMethod]
        [Owner("akotalwar")]
        public void PositiveOptimisticDirectExecutionOutput()
        { 
            List<OptimisticDirectExecutionTestInput> testVariations = new List<OptimisticDirectExecutionTestInput>
            {
                CreateInput(
                @"Partition Key + Value and Distinct",
                "SELECT DISTINCT c.key FROM c",
                true,
                @"/pk",
                @"/value"),
                
                CreateInput(
                @"Partition Key + Value and Min Aggregate",
                "SELECT VALUE MIN(c.key) FROM c",
                true,
                @"/pk",
                @"/value"),
               
                CreateInput(
                @"Partition Key + Value Fields",
                "SELECT c.key FROM c",
                true,
                @"/pk",
                @"/value"),
            };
            this.ExecuteTestSuite(testVariations);
        }
        
        [TestMethod]
        [Owner("akotalwar")]
        public void NegativeOptimisticDirectExecutionOutput()
        {
            List<OptimisticDirectExecutionTestInput> testVariations = new List<OptimisticDirectExecutionTestInput>
            {
                CreateInput(
                @"Null Partition Key Value",
                "SELECT * FROM c",
                false,
                @"/pk",
                Cosmos.PartitionKey.Null),
                
                CreateInput(
                @"None Partition Key Value",
                "SELECT * FROM c",
                false,
                @"/pk",
                Cosmos.PartitionKey.None), 
                
                CreateInput(
                @"C# Null Partition Key Value",
                "SELECT * FROM c",
                false,
                @"/pk",
                null),
            };
            this.ExecuteTestSuite(testVariations);
        }

        private static OptimisticDirectExecutionTestInput CreateInput(
            string description,
            string query,
            bool expectedOptimisticDirectExecution,
            string partitionKeyPath,
            string partitionKeyValue)
        {
            PartitionKeyBuilder pkBuilder = new PartitionKeyBuilder();
            pkBuilder.Add(partitionKeyValue);

            return CreateInput(description, query, expectedOptimisticDirectExecution, partitionKeyPath, pkBuilder.Build());
        }

        private static OptimisticDirectExecutionTestInput CreateInput(
            string description,
            string query,
            bool expectedOptimisticDirectExecution,
            string partitionKeyPath,
            Cosmos.PartitionKey partitionKeyValue)
        {
            return new OptimisticDirectExecutionTestInput(description, query, new SqlQuerySpec(query), expectedOptimisticDirectExecution, partitionKeyPath, partitionKeyValue);
        }
        
        private static PartitionedQueryExecutionInfo GetPartitionedQueryExecutionInfo(string querySpecJsonString, PartitionKeyDefinition pkDefinition)
        {
            TryCatch<PartitionedQueryExecutionInfo> tryGetQueryPlan = QueryPartitionProviderTestInstance.Object.TryGetPartitionedQueryExecutionInfo(
                querySpecJsonString: querySpecJsonString,
                partitionKeyDefinition: pkDefinition,
                requireFormattableOrderByQuery: true,
                isContinuationExpected: false,
                allowNonValueAggregateQuery: true,
                hasLogicalPartitionKey: false,
                allowDCount: true,
                useSystemPrefix: false);

            return tryGetQueryPlan.Result;
        }

        public override OptimisticDirectExecutionTestOutput ExecuteTest(OptimisticDirectExecutionTestInput input)
        {
            // gets DocumentContainer
            IMonadicDocumentContainer monadicDocumentContainer = new InMemoryContainer(input.PartitionKeyDefinition);
            DocumentContainer documentContainer = new DocumentContainer(monadicDocumentContainer);

            SqlQuerySpec sqlQuerySpec = new SqlQuerySpec(input.Query);

            // gets query context
            string databaseId = "db1234";
            string resourceLink = string.Format("dbs/{0}/colls", databaseId);
            CosmosQueryContextCore cosmosQueryContextCore = new CosmosQueryContextCore(
                client: new TestCosmosQueryClient(),
                resourceTypeEnum: Documents.ResourceType.Document,
                operationType: Documents.OperationType.Query,
                resourceType: typeof(QueryResponseCore),
                resourceLink: resourceLink,
                isContinuationExpected: false,
                allowNonValueAggregateQuery: true,
                useSystemPrefix: false,
                correlatedActivityId: Guid.NewGuid());

            //  gets input parameters
            QueryRequestOptions queryRequestOptions = new QueryRequestOptions
            {
                MaxBufferedItemCount = 7000,
                TestSettings = new TestInjections(simulate429s: true, simulateEmptyPages: false, enableOptimisticDirectExecution: true, new TestInjections.ResponseStats())
            };

            CosmosSerializerCore serializerCore = new();
            using StreamReader streamReader = new(serializerCore.ToStreamSqlQuerySpec(sqlQuerySpec, Documents.ResourceType.Document));
            string sqlQuerySpecJsonString = streamReader.ReadToEnd();

            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo = GetPartitionedQueryExecutionInfo(sqlQuerySpecJsonString, input.PartitionKeyDefinition);
            if (input.PartitionKeyValue == default || input.PartitionKeyValue == Cosmos.PartitionKey.None)
            {
                input.PartitionKeyValue = Cosmos.PartitionKey.Null;
            }

            CosmosQueryExecutionContextFactory.InputParameters inputParameters = new CosmosQueryExecutionContextFactory.InputParameters(
                sqlQuerySpec: sqlQuerySpec,
                initialUserContinuationToken: null,
                initialFeedRange: null,
                maxConcurrency: queryRequestOptions.MaxConcurrency,
                maxItemCount: queryRequestOptions.MaxItemCount,
                maxBufferedItemCount: queryRequestOptions.MaxBufferedItemCount,
                partitionKey: input.PartitionKeyValue,
                properties: queryRequestOptions.Properties,
                partitionedQueryExecutionInfo: partitionedQueryExecutionInfo,
                executionEnvironment: null,
                returnResultsInDeterministicOrder: null,
                forcePassthrough: true,
                testInjections: queryRequestOptions.TestSettings);
          
            IQueryPipelineStage queryPipelineStage = CosmosQueryExecutionContextFactory.Create(
                      documentContainer,
                      cosmosQueryContextCore,
                      inputParameters,
                      NoOpTrace.Singleton);
            bool result = queryPipelineStage.MoveNextAsync(NoOpTrace.Singleton).Result;

            if (input.ExpectedOptimisticDirectExecution)
            {
                Assert.AreEqual(TestInjections.PipelineType.OptimisticDirectExecution, queryRequestOptions.TestSettings.Stats.PipelineType.Value);
            }
            else {
                Assert.AreNotEqual(TestInjections.PipelineType.OptimisticDirectExecution, queryRequestOptions.TestSettings.Stats.PipelineType.Value);
            }
            
            Assert.IsNotNull(queryPipelineStage);
            Assert.IsTrue(result);
           
            return new OptimisticDirectExecutionTestOutput(input.ExpectedOptimisticDirectExecution);
        }
    }

    public sealed class OptimisticDirectExecutionTestOutput : BaselineTestOutput
    {
        public OptimisticDirectExecutionTestOutput(bool executeAsOptimisticDirectExecution)
        {
            this.ExecuteAsOptimisticDirectExecution = executeAsOptimisticDirectExecution;
        }

        public bool ExecuteAsOptimisticDirectExecution { get; }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteStartElement(nameof(this.ExecuteAsOptimisticDirectExecution));
            xmlWriter.WriteValue(this.ExecuteAsOptimisticDirectExecution);
            xmlWriter.WriteEndElement();
        }
    }

    public sealed class OptimisticDirectExecutionTestInput : BaselineTestInput
    {
        internal PartitionKeyDefinition PartitionKeyDefinition { get; set; }
        internal SqlQuerySpec SqlQuerySpec { get; set; }
        internal Cosmos.PartitionKey PartitionKeyValue { get; set; }
        internal bool ExpectedOptimisticDirectExecution { get; set; }
        internal PartitionKeyRangeIdentity PartitionKeyRangeId { get; set; }
        internal string Query { get; set; }

        internal OptimisticDirectExecutionTestInput(
            string description,
            string query,
            SqlQuerySpec sqlQuerySpec,
            bool expectedOptimisticDirectExecution,
            string partitionKeyPath,
            Cosmos.PartitionKey partitionKeyValue)
            : base(description)
        {
            this.PartitionKeyDefinition = new PartitionKeyDefinition()
            {
                Paths = new Collection<string>()
                {
                    partitionKeyPath
                },
                Kind = PartitionKind.Hash,
                Version = PartitionKeyDefinitionVersion.V2,
            };
            this.SqlQuerySpec = sqlQuerySpec;
            this.ExpectedOptimisticDirectExecution = expectedOptimisticDirectExecution;
            this.Query = query;
            this.PartitionKeyValue = partitionKeyValue;
        }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteElementString("Description", this.Description);
            xmlWriter.WriteElementString("Query", this.SqlQuerySpec.QueryText);
            xmlWriter.WriteStartElement("PartitionKeys");
            if (this.PartitionKeyDefinition != null)
            {
                foreach (string path in this.PartitionKeyDefinition.Paths)
                {
                    xmlWriter.WriteElementString("Key", path);
                }
            }

            xmlWriter.WriteEndElement(); 
            if (this.PartitionKeyDefinition != null)
            {
                xmlWriter.WriteElementString(
                    "PartitionKeyType",
                    this.PartitionKeyDefinition.Kind == PartitionKind.Hash ? "Hash" : (
                        this.PartitionKeyDefinition.Kind == PartitionKind.MultiHash ? "MultiHash" : "Range"));
            }

            if (this.SqlQuerySpec.ShouldSerializeParameters())
            {
                xmlWriter.WriteStartElement("QueryParameters");
                xmlWriter.WriteCData(JsonConvert.SerializeObject(
                    this.SqlQuerySpec.Parameters,
                    Newtonsoft.Json.Formatting.Indented));
                xmlWriter.WriteEndElement();
            }
        }
    }

    internal class TestCosmosQueryClient : CosmosQueryClient
    {
        public override Action<IQueryable> OnExecuteScalarQueryCallback => throw new NotImplementedException();

        public override bool ByPassQueryParsing()
        {
            throw new NotImplementedException();
        }

        public override void ClearSessionTokenCache(string collectionFullName)
        {
            throw new NotImplementedException();
        }

        public override Task<TryCatch<QueryPage>> ExecuteItemQueryAsync(string resourceUri, ResourceType resourceType, OperationType operationType, Guid clientQueryCorrelationId, Cosmos.FeedRange feedRange, QueryRequestOptions requestOptions, SqlQuerySpec sqlQuerySpec, string continuationToken, bool isContinuationExpected, int pageSize, ITrace trace, CancellationToken cancellationToken)
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
            return Task.FromResult(new ContainerQueryProperties());
        }

        public override Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangeByFeedRangeAsync(string resourceLink, string collectionResourceId, PartitionKeyDefinition partitionKeyDefinition, FeedRangeInternal feedRangeInternal, bool forceRefresh, ITrace trace)
        {
            throw new NotImplementedException();
        }

        public override Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangesAsync(string resourceLink, string collectionResourceId, List<Range<string>> providedRanges, bool forceRefresh, ITrace trace)
        {
            return Task.FromResult(new List<PartitionKeyRange>{new PartitionKeyRange()
            {
                MinInclusive = PartitionKeyHash.V2.Hash("abc").ToString(),
                MaxExclusive = PartitionKeyHash.V2.Hash("def").ToString()
            }
            });
        }

        public override Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangesByEpkStringAsync(string resourceLink, string collectionResourceId, string effectivePartitionKeyString, bool forceRefresh, ITrace trace)
        {
            throw new NotImplementedException();
        }

        public override Task<IReadOnlyList<PartitionKeyRange>> TryGetOverlappingRangesAsync(string collectionResourceId, Range<string> range, bool forceRefresh = false)
        {
            throw new NotImplementedException();
        }

        public override Task<TryCatch<PartitionedQueryExecutionInfo>> TryGetPartitionedQueryExecutionInfoAsync(SqlQuerySpec sqlQuerySpec, ResourceType resourceType, PartitionKeyDefinition partitionKeyDefinition, bool requireFormattableOrderByQuery, bool isContinuationExpected, bool allowNonValueAggregateQuery, bool hasLogicalPartitionKey, bool allowDCount, bool useSystemPrefix, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
