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

    [TestClass]
    public class PassThroughQueryBaselineTests : BaselineTests<PassThroughQueryTestInput, PassThroughQueryTestOutput>
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

        [TestMethod]
        [Owner("akotalwar")]
        public void PassThroughQueryTest()
        {
            List<PassThroughQueryTestInput> testVariations = new List<PassThroughQueryTestInput>
            {
                Hash( // dont use hash (maketest)
                @"Aggregate Partition Key Field",
                @"SELECT VALUE MIN(c.key) FROM c",
                @"/key"),
            };
            this.ExecuteTestSuite(testVariations);
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

            // this gets input parameters
            QueryRequestOptions queryRequestOptions = new QueryRequestOptions();
            SqlQuerySpec sqlQuerySpec = new SqlQuerySpec("SELECT VALUE MIN(c.key) FROM c");
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo = new PartitionedQueryExecutionInfo();
            CosmosQueryExecutionContextFactory.InputParameters inputParameters = new CosmosQueryExecutionContextFactory.InputParameters(
            sqlQuerySpec: sqlQuerySpec,
            initialUserContinuationToken: null,
            initialFeedRange: null,
            maxConcurrency: queryRequestOptions.MaxConcurrency,
            maxItemCount: queryRequestOptions.MaxItemCount,
            maxBufferedItemCount: queryRequestOptions.MaxBufferedItemCount,
            partitionKey: queryRequestOptions.PartitionKey,
            properties: queryRequestOptions.Properties,
            partitionedQueryExecutionInfo: partitionedQueryExecutionInfo,
            executionEnvironment: null,
            returnResultsInDeterministicOrder: null,
            forcePassthrough: true,
            testInjections: null);

            // gets query context
            Mock<CosmosQueryClient> mockCosmosQueryClient = new Mock<CosmosQueryClient>();
            CosmosQueryContextCore cosmosQueryContextCore = new CosmosQueryContextCore(
                client: mockCosmosQueryClient.Object,
                resourceTypeEnum: Documents.ResourceType.Document,
                operationType: Documents.OperationType.Query,
                resourceType: typeof(QueryResponseCore),
                resourceLink: null,
                isContinuationExpected: false,
                allowNonValueAggregateQuery: true,
                correlatedActivityId: Guid.NewGuid());

            // make call to Create
            IQueryPipelineStage info = CosmosQueryExecutionContextFactory.Create(
                documentContainer,
                cosmosQueryContextCore,
                inputParameters,
                It.IsAny<ITrace>());

            Assert.IsTrue(info!=null);
            // add an assert to check if TryCreateFromPartitionedQueryExecutionInfoAsync's output tells us if query is PassThrough or not
            return (PassThroughQueryTestOutput)info;
        }

    }

    public abstract class PassThroughQueryTestOutput : BaselineTestOutput
    {
    }
    public sealed class PassThroughQueryTestInput : BaselineTestInput
    {
        internal PartitionKeyDefinition PartitionKeyDefinition { get; set; }
        internal SqlQuerySpec SqlQuerySpec { get; set; }

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
    internal sealed class PassThroughQueryTestNegativeOutput : PassThroughQueryTestOutput
    {
        public PassThroughQueryTestNegativeOutput(Exception exception)
        {
            this.Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteElementString(nameof(this.Exception), this.Exception.Message);
        }

        public Exception Exception { get; }
    }
    internal sealed class PassThroughQueryTestPositiveOutput : PassThroughQueryTestOutput
    {
        public PassThroughQueryTestPositiveOutput(PartitionedQueryExecutionInfoInternal info)
        {
            this.Info = info ?? throw new ArgumentNullException(nameof(info));
        }

        public PartitionedQueryExecutionInfoInternal Info { get; }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteStartElement(nameof(PartitionedQueryExecutionInfoInternal));
            WritePartitionQueryExecutionInfoAsXML(this.Info, xmlWriter);
            xmlWriter.WriteEndElement();
        }

        private static void WriteQueryInfoAsXML(QueryInfo queryInfo, XmlWriter writer)
        {
            writer.WriteStartElement(nameof(QueryInfo));
            writer.WriteElementString(nameof(queryInfo.DistinctType), queryInfo.DistinctType.ToString());
            writer.WriteElementString(nameof(queryInfo.Top), queryInfo.Top.ToString());
            writer.WriteElementString(nameof(queryInfo.Offset), queryInfo.Offset.ToString());
            writer.WriteElementString(nameof(queryInfo.Limit), queryInfo.Limit.ToString());
            writer.WriteStartElement(nameof(queryInfo.GroupByExpressions));
            foreach (string GroupByExpression in queryInfo.GroupByExpressions)
            {
                writer.WriteElementString(nameof(GroupByExpression), GroupByExpression);
            }
            writer.WriteEndElement();
            writer.WriteStartElement(nameof(queryInfo.OrderBy));
            foreach (SortOrder sortorder in queryInfo.OrderBy)
            {
                writer.WriteElementString(nameof(SortOrder), sortorder.ToString());
            }
            writer.WriteEndElement();
            writer.WriteStartElement(nameof(queryInfo.OrderByExpressions));
            foreach (string OrderByExpression in queryInfo.OrderByExpressions)
            {
                writer.WriteElementString(nameof(OrderByExpression), OrderByExpression);
            }
            writer.WriteEndElement();
            writer.WriteStartElement(nameof(queryInfo.Aggregates));
            foreach (AggregateOperator aggregate in queryInfo.Aggregates)
            {
                writer.WriteElementString(nameof(AggregateOperator), aggregate.ToString());
            }
            writer.WriteEndElement();
            writer.WriteStartElement(nameof(queryInfo.GroupByAliasToAggregateType));
            foreach (KeyValuePair<string, AggregateOperator?> kvp in queryInfo.GroupByAliasToAggregateType)
            {
                writer.WriteStartElement("AliasToAggregateType");
                writer.WriteElementString("Alias", kvp.Key);
                writer.WriteElementString(nameof(AggregateOperator), kvp.Value.HasValue ? kvp.Value.ToString() : "null");
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteStartElement(nameof(queryInfo.GroupByAliases));
            foreach (string alias in queryInfo.GroupByAliases)
            {
                writer.WriteElementString("Alias", alias);
            }
            writer.WriteEndElement();
            writer.WriteElementString(nameof(queryInfo.HasSelectValue), queryInfo.HasSelectValue.ToString());
            writer.WriteEndElement();
        }

        private static void WriteQueryRangesAsXML(List<Range<PartitionKeyInternal>> queryRanges, XmlWriter writer)
        {
            writer.WriteStartElement("QueryRanges");
            writer.WriteStartElement("Range");
            foreach (Range<PartitionKeyInternal> range in queryRanges)
            {
                string minBound = range.IsMinInclusive ? "[" : "(";
                string maxBound = range.IsMaxInclusive ? "]" : ")";
                JsonSerializerSettings settings = new JsonSerializerSettings
                {
                    DateParseHandling = DateParseHandling.None,
                    Formatting = Newtonsoft.Json.Formatting.None
                };
                string minRangeString = JsonConvert.SerializeObject(JsonConvert.DeserializeObject(range.Min.ToJsonString(), settings), settings);
                string maxRangeString = JsonConvert.SerializeObject(JsonConvert.DeserializeObject(range.Max.ToJsonString(), settings), settings);
                writer.WriteElementString("Range", $"{minBound}{minRangeString},{maxRangeString}{maxBound}");
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteRewrittenQueryAsXML(string query, XmlWriter writer)
        {
            writer.WriteStartElement("RewrittenQuery");
            writer.WriteCData(query);
            writer.WriteEndElement();
        }

        private static void WritePartitionQueryExecutionInfoAsXML(PartitionedQueryExecutionInfoInternal info, XmlWriter writer)
        {
            if (info.QueryInfo != null)
            {
                WriteQueryInfoAsXML(info.QueryInfo, writer);
            }

            if (info.QueryRanges != null)
            {
                WriteQueryRangesAsXML(info.QueryRanges, writer);
            }

            if (info.QueryInfo.RewrittenQuery != null)
            {
                WriteRewrittenQueryAsXML(info.QueryInfo.RewrittenQuery, writer);
            }
        }
    }
    


}
