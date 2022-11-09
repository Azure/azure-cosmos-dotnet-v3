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
        public void PositiveOptimisticDirectExecutionOutput()
        {
            List<OptimisticDirectExecutionTestInput> testVariations = new List<OptimisticDirectExecutionTestInput>
            {
                CreateInput(
                    description: @"Partition Key + Value and Distinct",
                    query: "SELECT DISTINCT c.key FROM c",
                    expectedOptimisticDirectExecution: true,
                    partitionKeyPath: @"/pk",
                    partitionKeyValue: @"value"),

                CreateInput(
                    description: @"Partition Key + Value and Min Aggregate",
                    query: "SELECT VALUE MIN(c.key) FROM c",
                    expectedOptimisticDirectExecution: true,
                    partitionKeyPath: @"/pk",
                    partitionKeyValue: @"value"),

                CreateInput(
                    description: @"Partition Key + Value Fields",
                    query: "SELECT c.key FROM c",
                    expectedOptimisticDirectExecution: true,
                    partitionKeyPath: @"/pk",
                    partitionKeyValue: @"value"),
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
                    description: @"Null Partition Key Value",
                    query: "SELECT * FROM c",
                    expectedOptimisticDirectExecution: false,
                    partitionKeyPath: @"/pk",
                    partitionKeyValue: Cosmos.PartitionKey.Null),

                CreateInput(
                    description: @"None Partition Key Value",
                    query: "SELECT * FROM c",
                    expectedOptimisticDirectExecution: false,
                    partitionKeyPath: @"/pk",
                    partitionKeyValue: Cosmos.PartitionKey.None),

                CreateInput(
                    description: @"C# Null Partition Key Value",
                    query: "SELECT * FROM c",
                    expectedOptimisticDirectExecution: false,
                    partitionKeyPath: @"/pk",
                    partitionKeyValue: null),
            };
            this.ExecuteTestSuite(testVariations);
        }

        // This test confirms that TestInjection.EnableOptimisticDirectExection is set to false from default. 
        // Check test "TestPipelineForDistributedQueryAsync" to understand why this is done
        [TestMethod]
        public async Task TestDefaultTestInjectionSettings()
        {
            TestInjections testInjection = new TestInjections(simulate429s: false, simulateEmptyPages: false);

            Assert.AreEqual(testInjection.EnableOptimisticDirectExecution, false);
        }

        [TestMethod]
        [Owner("akotalwar")]
        public async Task TestMonadicCreateODEPipeline()
        {
            int numItems = 10;
            bool multiPartition = false;
            string query = "SELECT * FROM c";

            // null continuation token
            Assert.IsTrue(await TryMonadicCreate(numItems, multiPartition, query, targetRange: FeedRangeEpk.FullRange, continuationToken: null));

            // default continuation token
            Assert.IsTrue(await TryMonadicCreate(numItems, multiPartition, query, targetRange: FeedRangeEpk.FullRange, continuationToken: default));

            CosmosElement cosmosElementContinuationToken = CosmosElement.Parse(
                "{\"OptimisticDirectExecutionToken\":{\"token\":\"{\\\"resourceId\\\":\\\"AQAAAMmFOw8LAAAAAAAAAA==\\\",\\\"skipCount\\\":1}\"," +
                "\"range\":{\"min\":\"\",\"max\":\"FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF\"}}}");
            Range<string> range = new Documents.Routing.Range<string>("", "FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF", isMinInclusive: true, isMaxInclusive: false);

            // single continuation token
            Assert.IsTrue(await TryMonadicCreate(numItems, multiPartition, query, targetRange: new FeedRangeEpk(range), continuationToken: cosmosElementContinuationToken));
        }

        // test checks that the pipeline can take a query to the backend and returns its associated document(s). 
        [TestMethod]
        public async Task TestPipelineForBackendDocumentsOnSinglePartitionAsync()
        {
            int numItems = 100;
            string query = "SELECT VALUE COUNT(1) FROM c";
            DocumentContainer inMemoryCollection = await CreateDocumentContainerAsync(numItems, multiPartition: false);
            IQueryPipelineStage queryPipelineStage = await CreateOptimisticDirectExecutionPipelineStateAsync(inMemoryCollection, query, continuationToken: null);
            int documentCountInSinglePartition = 0;

            while (await queryPipelineStage.MoveNextAsync(NoOpTrace.Singleton))
            {
                TryCatch<QueryPage> tryGetPage = queryPipelineStage.Current;
                tryGetPage.ThrowIfFailed();

                documentCountInSinglePartition += Int32.Parse(tryGetPage.Result.Documents[0].ToString());
            }

            Assert.AreEqual(documentCountInSinglePartition, 100);
        }

        // test checks that the pipeline can take a query to the backend and returns its associated document(s) + continuation token.
        [TestMethod]
        public async Task TestPipelineForContinuationTokenOnSinglePartitionAsync()
        {
            int numItems = 100;
            int result = await this.GetResultsFromHydratingPipelineWithContinuation(
                numItems: numItems, 
                isMultiPartition: false, 
                query: "SELECT * FROM c");

            Assert.AreEqual(result, numItems);
        }

        // test to check if pipeline handles a 410 exception properly and returns all the documents.
        [TestMethod]
        public async Task TestPipelineForGoneExceptionOnSingleAndMultiplePartitionAsync()
        {
            Assert.IsTrue(await ExecuteGoneExceptionOnODEPipeline(isMultiPartition: false));

            Assert.IsTrue(await ExecuteGoneExceptionOnODEPipeline(isMultiPartition: true));
        }

        // The reason we have the below test is to show the missing capabilities of the OptimisticDirectExecution pipeline.
        // Currently this pipeline cannot handle distributed queries as it does not have the logic to sum up the values it gets from the backend in partial results.
        // This functionality is available for other pipelines such as the ParallelCrossPartitionQueryPipelineStage.
        [TestMethod]
        public async Task TestPipelinesForDistributedQueryAsync()
        {
            int numItems = 100;
            int result = await this.GetResultsFromHydratingPipelineWithContinuation(
                            numItems: numItems, 
                            isMultiPartition: false, 
                            query: "SELECT AVG(c) FROM c");

            // TODO: These values will not equal each other until aggregate logic is added
            Assert.AreNotEqual(result, numItems);
        }

        private static async Task<bool> TryMonadicCreate(int numItems, bool multiPartition, string query, FeedRangeEpk targetRange, CosmosElement continuationToken)
        {
            DocumentContainer inMemoryCollection = await CreateDocumentContainerAsync(numItems, multiPartition);

            TryCatch<IQueryPipelineStage> monadicQueryPipelineStage = OptimisticDirectExecutionQueryPipelineStage.MonadicCreate(
                documentContainer: inMemoryCollection,
                sqlQuerySpec: new SqlQuerySpec(query),
                targetRange: targetRange,
                queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: 10),
                partitionKey: null,
                cancellationToken: default,
                continuationToken: continuationToken);

            return monadicQueryPipelineStage.Succeeded;
        }

        private static async Task<IQueryPipelineStage> CreateOptimisticDirectExecutionPipelineStateAsync(DocumentContainer documentContainer, string query, CosmosElement continuationToken)
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

        private async Task<int> GetResultsFromHydratingPipelineWithContinuation(int numItems, bool isMultiPartition, string query)
        {
            DocumentContainer inMemoryCollection = await CreateDocumentContainerAsync(numItems, multiPartition: isMultiPartition);
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

            return documents.Count;
        }

            // it creates a gone exception after the first MoveNexyAsync() call. This allows for the pipeline to return some documents before failing
            // TODO: With the addition of the merge/split support, this queryPipelineStage should be able to return all documents regardless of a gone exception happening 
            private static async Task<bool> ExecuteGoneExceptionOnODEPipeline(bool isMultiPartition)
        {
            int numItems = 100;
            string query = "SELECT * FROM c";
            List<CosmosElement> documents = new List<CosmosElement>();
            string errorMessage = $"Epk Range: Partition does not exist at the given range.";
            CosmosException goneException = new CosmosException(
                message: errorMessage,
                statusCode: System.Net.HttpStatusCode.Gone,
                subStatusCode: (int)SubStatusCodes.PartitionKeyRangeGone,
                activityId: Guid.NewGuid().ToString(),
                requestCharge: default);

            int moveNextAsyncCounter = 0;
            DocumentContainer inMemoryCollection = await CreateDocumentContainerAsync(
                numItems,
                multiPartition: isMultiPartition,
                failureConfigs: new FlakyDocumentContainer.FailureConfigs(
                    inject429s: false,
                    injectEmptyPages: false,
                    shouldReturnFailure: () => Task.FromResult<Exception>(moveNextAsyncCounter != 1 ? null : goneException)));

            IQueryPipelineStage queryPipelineStage = await CreateOptimisticDirectExecutionPipelineStateAsync(inMemoryCollection, query, continuationToken: null);
            while (await queryPipelineStage.MoveNextAsync(NoOpTrace.Singleton))
            {
                moveNextAsyncCounter++;
                try
                {
                    TryCatch<QueryPage> tryGetPage = queryPipelineStage.Current;
                    tryGetPage.ThrowIfFailed();

                    documents.AddRange(tryGetPage.Result.Documents);
                }
                catch
                {
                    Assert.IsTrue(queryPipelineStage.Current.Failed);
                    Assert.AreEqual(queryPipelineStage.Current.InnerMostException.Message, errorMessage);
                    break;
                }
            }

            // Once fallback plan is implemented, this test should be able to return all 100 documents
            Assert.AreEqual(documents.Count, 10);
            return true;
        }

        private static async Task<DocumentContainer> CreateDocumentContainerAsync(
            int numItems,
            bool multiPartition,
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

            int exponentPartitionKeyRanges = 2; // a value of 2 would lead to 4 partitions (2 * 2). 4 partitions are used because theyre easy to manage + demonstrates multi partition use case

            IReadOnlyList<FeedRangeInternal> ranges;

            for (int i = 0; i < exponentPartitionKeyRanges; i++)
            {
                ranges = await documentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);

                if (multiPartition)
                {
                    foreach (FeedRangeInternal range in ranges)
                    {
                        await documentContainer.SplitAsync(range, cancellationToken: default);
                    }
                }

                await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
            }

            ranges = await documentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);
            
            int rangeCount = multiPartition == true ? 4 : 1;

            Assert.AreEqual(rangeCount, ranges.Count);

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

        private static OptimisticDirectExecutionTestInput CreateInput(
            string description,
            string query,
            bool expectedOptimisticDirectExecution,
            string partitionKeyPath,
            string partitionKeyValue,
            CosmosElement continuationToken = null)
        {
            PartitionKeyBuilder pkBuilder = new PartitionKeyBuilder();
            pkBuilder.Add(partitionKeyValue);

            return CreateInput(description, query, expectedOptimisticDirectExecution, partitionKeyPath, pkBuilder.Build(), continuationToken);
        }

        private static OptimisticDirectExecutionTestInput CreateInput(
            string description,
            string query,
            bool expectedOptimisticDirectExecution,
            string partitionKeyPath,
            Cosmos.PartitionKey partitionKeyValue,
            CosmosElement continuationToken = null)
        {
            return new OptimisticDirectExecutionTestInput(description, query, new SqlQuerySpec(query), expectedOptimisticDirectExecution, partitionKeyPath, partitionKeyValue, continuationToken);
        }

        private static PartitionedQueryExecutionInfo GetPartitionedQueryExecutionInfo(string querySpecJsonString, PartitionKeyDefinition pkDefinition)
        {
            TryCatch<PartitionedQueryExecutionInfo> tryGetQueryPlan = QueryPartitionProviderTestInstance.Object.TryGetPartitionedQueryExecutionInfo(
                querySpecJsonString: querySpecJsonString,
                partitionKeyDefinition: pkDefinition,
                requireFormattableOrderByQuery: true,
                isContinuationExpected: true,
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
            string resourceLink = $"dbs/{databaseId}/colls";
            CosmosQueryContextCore cosmosQueryContextCore = new CosmosQueryContextCore(
                client: new TestCosmosQueryClient(),
                resourceTypeEnum: Documents.ResourceType.Document,
                operationType: Documents.OperationType.Query,
                resourceType: typeof(QueryResponseCore),
                resourceLink: resourceLink,
                isContinuationExpected: true,
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
                initialUserContinuationToken: input.ContinuationToken,
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
            else
            {
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
        internal CosmosElement ContinuationToken { get; set; }

        internal OptimisticDirectExecutionTestInput(
            string description,
            string query,
            SqlQuerySpec sqlQuerySpec,
            bool expectedOptimisticDirectExecution,
            string partitionKeyPath,
            Cosmos.PartitionKey partitionKeyValue,
            CosmosElement continuationToken = null)
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
            this.ContinuationToken = continuationToken;
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
                MinInclusive = PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                MaxExclusive = PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey
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
