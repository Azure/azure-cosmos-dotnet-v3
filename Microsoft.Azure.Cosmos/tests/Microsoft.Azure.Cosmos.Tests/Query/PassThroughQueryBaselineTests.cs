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
        public void PassThroughQueryTest()
        {
            this.query = "SELECT c.key FROM c";
            List<PassThroughQueryTestInput> testVariations = new List<PassThroughQueryTestInput>
            {
                Hash( // dont use hash (maketest)
                @"Partition Key Field",
                this.@query,
                @"/key"),
            };
            this.ExecuteTestSuite(testVariations);
        }

        private static readonly PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition()
        {
            Paths = new Collection<string>()
            {
                "/pk"
            },
            Kind = PartitionKind.Hash,
            Version = PartitionKeyDefinitionVersion.V2,
        };

        private static async Task<IDocumentContainer> CreateDocumentContainerAsync(
            IReadOnlyList<CosmosObject> documents,
            int numPartitions = 3,
            FlakyDocumentContainer.FailureConfigs failureConfigs = null)
        {
            IMonadicDocumentContainer monadicDocumentContainer = new InMemoryContainer(partitionKeyDefinition);
            if (failureConfigs != null)
            {
                monadicDocumentContainer = new FlakyDocumentContainer(monadicDocumentContainer, failureConfigs);
            }

            DocumentContainer documentContainer = new DocumentContainer(monadicDocumentContainer);

            for (int i = 0; i < numPartitions; i++)
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

            foreach (CosmosObject document in documents)
            {
                while (true)
                {
                    TryCatch<Record> monadicCreateRecord = await documentContainer.MonadicCreateItemAsync(
                        document,
                        cancellationToken: default);
                    if (monadicCreateRecord.Succeeded)
                    {
                        break;
                    }
                }
            }

            return documentContainer;
        }

        private static PassThroughQueryTestInput Hash(
            string description,
            string query,
            params string[] partitionkeys) => new PassThroughQueryTestInput(
                description,
                CreateHashPartitionKey(partitionkeys),
                new SqlQuerySpec(query));

        private static PassThroughQueryTestInput Hash(
            string description,
            SqlQuerySpec query,
            params string[] partitionkeys) => new PassThroughQueryTestInput(
                description,
                CreateHashPartitionKey(partitionkeys),
                query);

        private static PartitionKeyDefinition CreateHashPartitionKey(
            params string[] partitionKeys) => new PartitionKeyDefinition()
            {
                Paths = new Collection<string>(partitionKeys),
                Kind = Microsoft.Azure.Documents.PartitionKind.Hash
            };

        private static PartitionKeyDefinition CreateRangePartitionKey(
            params string[] partitionKeys) => new PartitionKeyDefinition()
            {
                Paths = new Collection<string>(partitionKeys),
                Kind = PartitionKind.Range
            };

        public override PassThroughQueryTestOutput ExecuteTest(PassThroughQueryTestInput input)
        {
            // this gets DocumentContainer
            IMonadicDocumentContainer monadicDocumentContainer = new InMemoryContainer(partitionKeyDefinition);
            FlakyDocumentContainer.FailureConfigs failureConfigs = null;
            if (failureConfigs != null)
            {
                monadicDocumentContainer = new FlakyDocumentContainer(monadicDocumentContainer, failureConfigs);
            }
            DocumentContainer documentContainer = new DocumentContainer(monadicDocumentContainer);

            // this gets PartionedQueryExecutionContext
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo = new PartitionedQueryExecutionInfo()
            {
                QueryInfo = new QueryInfo()
                {
                    Aggregates = null,
                    DistinctType = DistinctQueryType.None,
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
            PartitionKeyDefinition PKDefinition = new PartitionKeyDefinition();
            const string resourceId = "resourceId";
            ContainerQueryProperties containerQueryProperties = new ContainerQueryProperties(
                    resourceId,
                    null,
                    PKDefinition);

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
            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey("pk");

            CosmosQueryExecutionContextFactory.InputParameters inputParameters = new CosmosQueryExecutionContextFactory.InputParameters(
            sqlQuerySpec: sqlQuerySpec,
            initialUserContinuationToken: null,
            initialFeedRange: null,
            maxConcurrency: queryRequestOptions.MaxConcurrency,
            maxItemCount: queryRequestOptions.MaxItemCount,
            maxBufferedItemCount: queryRequestOptions.MaxBufferedItemCount,
            partitionKey: partitionKey,
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
            Assert.IsTrue(isPassThrough);
            Assert.IsTrue(queryPipelineStage != null);

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
        internal PartitionKey PartitionKey { get; set; } 
        internal PartitionKeyRangeIdentity PartitionKeyRangeId { get; set; }

        internal PassThroughQueryTestInput(
            string description,
            PartitionKeyDefinition partitionKeyDefinition,
            SqlQuerySpec sqlQuerySpec)
            : base(description)
        {
            this.PartitionKeyDefinition = partitionKeyDefinition;
            this.SqlQuerySpec = sqlQuerySpec;
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
