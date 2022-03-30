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
        private string query = "";

        [TestMethod]
        [Owner("akotalwar")]
        public void PartitionKeyPassThrough()
        {
            this.query = "SELECT c.key FROM c";
            List<PassThroughQueryTestInput> testVariations = new List<PassThroughQueryTestInput>
            {
                CreateInput(
                @"Partition Key Field",
                this.query,
                true,
                @"/key"),
            };
            this.ExecuteTestSuite(testVariations);
        }

        [TestMethod]
        [Owner("akotalwar")]
        public void DistinctPassThrough()
        {
            this.query = "SELECT DISTINCT c.key FROM c";
            List<PassThroughQueryTestInput> testVariations = new List<PassThroughQueryTestInput>
            {
                CreateInput(
                @"Partition Key and Distinct",
                this.query,
                false,
                @"/key"),
            };
            this.ExecuteTestSuite(testVariations);
        }

        [TestMethod]
        [Owner("akotalwar")]
        public void AggregatePassThrough()
        {
            this.query = "SELECT VALUE MIN(c.key) FROM c";
            List<PassThroughQueryTestInput> testVariations = new List<PassThroughQueryTestInput>
            {
                CreateInput( 
                @"Partition Key and Min Aggregate",
                this.query,
                false,
                @"/key"),
            };
            this.ExecuteTestSuite(testVariations);
        }

        [TestMethod]
        [Owner("akotalwar")]
        public void NoPartitionKeyPassThrough()
        {
            this.query = "SELECT * FROM c";
            List<PassThroughQueryTestInput> testVariations = new List<PassThroughQueryTestInput>
            {
                CreateInput(
                @"No Partition Key",
                this.query,
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
                new SqlQuerySpec(query),
                expectedValueForTest,
                partitionkeys);

        public override PassThroughQueryTestOutput ExecuteTest(PassThroughQueryTestInput input)
        {
            // this gets DocumentContainer
            IMonadicDocumentContainer monadicDocumentContainer = new InMemoryContainer(input.PartitionKeyDefinition);
            DocumentContainer documentContainer = new DocumentContainer(monadicDocumentContainer);

            // this gets PartionedQueryExecutionContext
            DistinctQueryType distinctType = this.query.Contains("DISTINCT") ? DistinctQueryType.Ordered : DistinctQueryType.None;
            List<AggregateOperator> aggregates = this.query.Contains("MIN") ? new List<AggregateOperator>() { AggregateOperator.Min } : null;
           
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo = new PartitionedQueryExecutionInfo()
            {
                QueryInfo = new QueryInfo()
                {
                    Aggregates = aggregates,
                    DistinctType = distinctType,
                    GroupByAliases = null,
                    GroupByAliasToAggregateType = null,
                    GroupByExpressions = null,
                    HasSelectValue = false,
                    Limit = null,
                    Offset = null,
                    OrderBy = null,
                    OrderByExpressions = null,
                    RewrittenQuery = null,
                    Top = null,
                },
                QueryRanges = new List<Documents.Routing.Range<string>>(),
            };

            // this is container query properties
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

            // this gets input parameters
            QueryRequestOptions queryRequestOptions = new QueryRequestOptions();
            SqlQuerySpec sqlQuerySpec = new SqlQuerySpec(this.query);

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
            
            ITrace trace = NoOpTrace.Singleton;
            CancellationToken cancellationToken = new CancellationToken();

            // make call to Create
            Task<TryCatch<IQueryPipelineStage>> queryPipelineStage = CosmosQueryExecutionContextFactory.TryCreateFromPartitionedQueryExecutionInfoAsync(
                documentContainer,
                partitionedQueryExecutionInfo,
                containerQueryProperties,
                cosmosQueryContextCore,
                inputParameters,
                trace,
                cancellationToken);
            
            bool isPassThrough = inputParameters.SqlQuerySpec.options.IsPassThrough;
            if (input.ExpectedValueFromTest)
            {
                Assert.IsTrue(isPassThrough, "Expected true for PassThrough query");
            }
            else 
            {
                Assert.IsFalse(isPassThrough, "Expected false for PassThrough query");
            }

            Assert.IsTrue(queryPipelineStage != null, "Expected queryPipelineStage to not be null");

            return new PassThroughQueryTestOutput(isPassThrough);
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

        internal PassThroughQueryTestInput(
            string description,
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
