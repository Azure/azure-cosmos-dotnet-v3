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
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Distinct;
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
    using System.Threading;

    [TestClass]
    public class PassThroughQueryBaselineTests : BaselineTests<PassThroughQueryTestInput, PassThroughQueryTestOutput>
    {
        [TestMethod]
        [Owner("akotalwar")]
        public void PositivePassThroughOutput()
        { 
            List<PassThroughQueryTestInput> testVariations = new List<PassThroughQueryTestInput>
            {
                CreateInput(
                @"Partition Key Field",
                "SELECT c.key FROM c",
                true,
                @"/key"),
            };
            this.ExecuteTestSuite(testVariations);
        }
        
        [TestMethod]
        [Owner("akotalwar")]
        public void NegativePassThroughOutput()
        {
            List<PassThroughQueryTestInput> testVariations = new List<PassThroughQueryTestInput>
            {
                CreateInput(
                @"Partition Key and Distinct",
                "SELECT DISTINCT c.key FROM c",
                false,
                @"/key"),

                CreateInput(
                @"Partition Key and Min Aggregate",
                "SELECT VALUE MIN(c.key) FROM c",
                false,
                @"/key"),

                CreateInput(
                @"No Partition Key",
                "SELECT * FROM c",
                false),
            };
            this.ExecuteTestSuite(testVariations);
        }
 
        private static PassThroughQueryTestInput CreateInput(
            string description,
            string query,
            bool expectedValueForTest,
            params string[] partitionkeys) => new PassThroughQueryTestInput(
                description,
                query,
                new SqlQuerySpec(query),
                expectedValueForTest,
                partitionkeys);

        private static PartitionedQueryExecutionInfo GetPartitionedQueryExecutionInfo(SqlQuerySpec sqlQuerySpec, PartitionKeyDefinition pkDefinition)
        {
            TryCatch<PartitionedQueryExecutionInfo> tryGetQueryPlan = QueryPartitionProviderTestInstance.Object.TryGetPartitionedQueryExecutionInfo(
                querySpec: sqlQuerySpec,
                partitionKeyDefinition: pkDefinition,
                requireFormattableOrderByQuery: true,
                isContinuationExpected: false,
                allowNonValueAggregateQuery: true,
                hasLogicalPartitionKey: false,
                allowDCount: true);

            return tryGetQueryPlan.Result;
        }

        public override PassThroughQueryTestOutput ExecuteTest(PassThroughQueryTestInput input)
        {
            // this gets DocumentContainer
            IMonadicDocumentContainer monadicDocumentContainer = new InMemoryContainer(input.PartitionKeyDefinition);
            DocumentContainer documentContainer = new DocumentContainer(monadicDocumentContainer);

            SqlQuerySpec sqlQuerySpec = new SqlQuerySpec(input.Query);

            // gets container query properties
            const string resourceId = "resourceId";
            ContainerQueryProperties containerQueryProperties = new ContainerQueryProperties(
                resourceId,
                null,
                input.PartitionKeyDefinition);

            // gets query context
            string databaseId = "db1234";
            string resourceLink = string.Format("dbs/{0}/colls", databaseId);
            Mock<CosmosQueryClient> mockCosmosQueryClient = new Mock<CosmosQueryClient>();
            CosmosQueryContextCore cosmosQueryContextCore = new CosmosQueryContextCore(
                client: mockCosmosQueryClient.Object,
                resourceTypeEnum: Documents.ResourceType.Document,
                operationType: Documents.OperationType.Query,
                resourceType: typeof(QueryResponseCore),
                resourceLink: resourceLink,
                isContinuationExpected: false,
                allowNonValueAggregateQuery: true,
                correlatedActivityId: Guid.NewGuid());

            //  gets input parameters
            QueryRequestOptions queryRequestOptions = new QueryRequestOptions();
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo = GetPartitionedQueryExecutionInfo(sqlQuerySpec, input.PartitionKeyDefinition);

            CosmosQueryExecutionContextFactory.InputParameters inputParameters = new CosmosQueryExecutionContextFactory.InputParameters(
                sqlQuerySpec: sqlQuerySpec,
                initialUserContinuationToken: null,
                initialFeedRange: null,
                maxConcurrency: queryRequestOptions.MaxConcurrency,
                maxItemCount: queryRequestOptions.MaxItemCount,
                maxBufferedItemCount: queryRequestOptions.MaxBufferedItemCount,
                partitionKey: input.PartitionKey,
                properties: queryRequestOptions.Properties,
                partitionedQueryExecutionInfo: partitionedQueryExecutionInfo,
                executionEnvironment: null,
                returnResultsInDeterministicOrder: null,
                forcePassthrough: true,
                testInjections: null);

            Task<TryCatch<IQueryPipelineStage>> queryPipelineStage = CosmosQueryExecutionContextFactory.TryCreateFromPartitionedQueryExecutionInfoAsync(
                documentContainer,
                partitionedQueryExecutionInfo,
                containerQueryProperties,
                cosmosQueryContextCore,
                inputParameters,
                NoOpTrace.Singleton,
                default);
                       
            Assert.AreEqual(input.ExpectedValueFromTest, inputParameters.SqlQuerySpec.options.IsPassThrough);
            Assert.IsNotNull(queryPipelineStage);

            return new PassThroughQueryTestOutput(inputParameters.SqlQuerySpec.options.IsPassThrough);
        }
    }

    public sealed class PassThroughQueryTestOutput : BaselineTestOutput
    {
        public PassThroughQueryTestOutput(bool tryExecuteOnBE)
        {
            this.TryExecuteOnBE = tryExecuteOnBE;
        }

        public bool TryExecuteOnBE { get; }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteStartElement(nameof(this.TryExecuteOnBE));
            xmlWriter.WriteValue(this.TryExecuteOnBE);
            xmlWriter.WriteEndElement();
        }
    }

    public sealed class PassThroughQueryTestInput : BaselineTestInput
    {
        internal PartitionKeyDefinition PartitionKeyDefinition { get; set; }
        internal SqlQuerySpec SqlQuerySpec { get; set; }
        internal Cosmos.PartitionKey PartitionKey { get; set; }
        internal bool ExpectedValueFromTest { get; set; }
        internal PartitionKeyRangeIdentity PartitionKeyRangeId { get; set; }
        internal string Query { get; set; }

        internal PassThroughQueryTestInput(
            string description,
            string query,
            SqlQuerySpec sqlQuerySpec,
            bool expectedValueFromTest,
            string[] partitionKeys
            )
            : base(description)
        {
            this.PartitionKeyDefinition = partitionKeys == null ? null : new PartitionKeyDefinition()
            {
                Paths = new Collection<string>()
            {
                "/key"
            },
                Kind = PartitionKind.Hash,
                Version = PartitionKeyDefinitionVersion.V2,
            };
            this.SqlQuerySpec = sqlQuerySpec;
            this.ExpectedValueFromTest = expectedValueFromTest;
            this.Query = query;
            this.PartitionKey = partitionKeys.Length == 0 ? Cosmos.PartitionKey.None : new Cosmos.PartitionKey(partitionKeys[0]);
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
}
