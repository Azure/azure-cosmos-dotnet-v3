namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using System.Xml;
    using Microsoft.Azure.Cosmos.Test.BaselineTest;
    using Microsoft.Azure.Cosmos.Tests.Query.SampleQueryContainer;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Diagnostics;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Query.Core;
    using System.IO;

    [TestClass]
    public class SampleContainerSubpartitionTests : BaselineTests<SampleContainerSubpartitionTestInput, SampleContainerSubpartitionTestOutput>
    {
        [TestMethod]
        public void TestQueriesOnContainerSplitAtLevel1()
        {
            SampleContainer sampleContainer = SampleContainer.CreateContainerSplitAtLevel1();
            SampleContainerSubpartitionTestInput input = new SampleContainerSubpartitionTestInput("Test Queries on Container Split at Level 1");
            input.SetSampleContainer(sampleContainer);

            List<SampleContainerSubpartitionTestInput> inputs = new List<SampleContainerSubpartitionTestInput> { input };

            this.ExecuteTestSuite(inputs);
        }

        public override SampleContainerSubpartitionTestOutput ExecuteTest(SampleContainerSubpartitionTestInput input)
        {
            SampleMonadicContainer monadicContainer = new SampleMonadicContainer(input.SampleContainer);
            QueryRequestOptions queryRequestOptions = new QueryRequestOptions()
            {
                //PartitionKey = new PartitionKeyBuilder().Add("Tenant_1").Build()
            };

            List<FeedRangeEpk> containerRanges = monadicContainer.MonadicGetFeedRangesAsync(NoOpTrace.Singleton, cancellationToken: default).Result.Result;
            (CosmosQueryExecutionContextFactory.InputParameters inputParameters, CosmosQueryContextCore cosmosQueryContextCore) =
                CreateInputParamsAndQueryContext(input.SampleContainer, queryRequestOptions, containerRanges);
            IQueryPipelineStage queryPipelineStage = CosmosQueryExecutionContextFactory.Create(
                        new DocumentContainer(monadicContainer),
                        cosmosQueryContextCore,
                        inputParameters,
                        NoOpTrace.Singleton);
            List<CosmosElement> documents = new List<CosmosElement>();
            while (queryPipelineStage.MoveNextAsync(NoOpTrace.Singleton, cancellationToken: default).Result)
            {
                // bool result = queryPipelineStage.MoveNextAsync(NoOpTrace.Singleton).AsTask().GetAwaiter().GetResult();
                TryCatch<QueryPage> tryGetPage = queryPipelineStage.Current;

                if (tryGetPage.Failed)
                {
                    // failure should never come till here. Should be handled before
                    Assert.Fail("Unexpected error. Gone Exception should not reach till here");
                }

                documents.AddRange(tryGetPage.Result.Documents);
            }
            return new SampleContainerSubpartitionTestOutput(documents);
        }

        private static Tuple<CosmosQueryExecutionContextFactory.InputParameters, CosmosQueryContextCore> CreateInputParamsAndQueryContext(SampleContainer sampleContainer, QueryRequestOptions queryRequestOptions, List<FeedRangeEpk> containerRanges)
        {
            string query = @"SELECT * FROM c";
            CosmosElement continuationToken = null;
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition()
            {
                Paths = new System.Collections.ObjectModel.Collection<string>(sampleContainer.GetPartitionKeyDefinitionPaths().ToList()),
                Kind = PartitionKind.MultiHash,
                Version = PartitionKeyDefinitionVersion.V2,
            };

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

            return Tuple.Create(inputParameters, cosmosQueryContextCore);
        }

        internal static Tuple<PartitionedQueryExecutionInfo, QueryPartitionProvider> GetPartitionedQueryExecutionInfoAndPartitionProvider(string querySpecJsonString, PartitionKeyDefinition pkDefinition, bool clientDisableOde = false)
        {
            QueryPartitionProvider queryPartitionProvider = CreateCustomQueryPartitionProvider("clientDisableOptimisticDirectExecution", clientDisableOde.ToString().ToLower());
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

        private static QueryPartitionProvider CreateCustomQueryPartitionProvider(string key, string value)
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

            queryEngineConfiguration[key] = bool.TryParse(value, out bool boolValue) ? boolValue : value;

            return new QueryPartitionProvider(queryEngineConfiguration);
        }
    }

    public class SampleContainerSubpartitionTestInput : BaselineTestInput
    {
        public SampleContainerSubpartitionTestInput(string description)
            : base(description)
        {
        }

        internal SampleContainer SampleContainer { get; set; }

        /// <summary>
        /// We break immutability to avoid making SampleContainer and related classes public.
        /// </summary>
        internal void SetSampleContainer(SampleContainer sampleContainer)
        {
            Debug.Assert(this.SampleContainer == null, "SampleContainerSubPartitionTestInput Assert!", "sampleContainer can only be set once!");
            this.SampleContainer = sampleContainer;
        }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            Debug.Assert(this.SampleContainer != null, "SampleContainerSubPartitionTestInput Assert!", "sampleContainer must be set before serializing the input!");

            xmlWriter.WriteStartElement("Container");
            foreach (Partition partition in this.SampleContainer.Partitions)
            {
                xmlWriter.WriteStartElement("Partition");
                xmlWriter.WriteStartElement("Range");
                xmlWriter.WriteStartElement("Min");
                xmlWriter.WriteAttributeString("Inclusive", partition.LogicalPartitionRange.IsMinInclusive.ToString());
                this.WritePartitionKey(xmlWriter, partition.LogicalPartitionRange.Min);
                xmlWriter.WriteEndElement();
                xmlWriter.WriteStartElement("Max");
                xmlWriter.WriteAttributeString("Inclusive", partition.LogicalPartitionRange.IsMaxInclusive.ToString());
                this.WritePartitionKey(xmlWriter, partition.LogicalPartitionRange.Max);
                xmlWriter.WriteEndElement();
                xmlWriter.WriteEndElement();
                xmlWriter.WriteStartElement("Documents");
                StringBuilder sb = new StringBuilder();
                foreach (SampleDocument document in partition.Documents)
                {
                    sb.AppendLine($"{document.Id}|{document.LogicalPartitionKey.PhysicalPartitionKey.Hash}");
                }
                xmlWriter.WriteCData(sb.ToString());
                xmlWriter.WriteEndElement();
                xmlWriter.WriteEndElement();
            }
        }

        private void WritePartitionKey(XmlWriter xmlWriter, LogicalPartitionKey partitionKey)
        {
            if (partitionKey != null)
            {
                xmlWriter.WriteStartElement("PhysicalHash");
                xmlWriter.WriteValue(partitionKey.PhysicalPartitionKey.Hash);
                xmlWriter.WriteEndElement();
                xmlWriter.WriteStartElement("LogicalKey");
                xmlWriter.WriteCData($"{partitionKey.TenantId};{partitionKey.UserId};{partitionKey.SessionId}");
                xmlWriter.WriteEndElement();
            }
        }
    }

    public class SampleContainerSubpartitionTestOutput : BaselineTestOutput
    {
        private readonly List<CosmosElement> documents;

        internal SampleContainerSubpartitionTestOutput(IReadOnlyList<CosmosElement> documents)
        {
            this.documents = documents.ToList();
        }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteStartElement("Documents");
            string content = string.Join($",{Environment.NewLine}", this.documents.Select(doc => doc.ToString()));
            xmlWriter.WriteCData(content);
            xmlWriter.WriteEndElement();
        }
    }
}
