﻿namespace Microsoft.Azure.Cosmos.Tests.Query
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
    using Microsoft.Azure.Cosmos.Routing;
    using System.Threading;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using System.IO;

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
        public void NegativePassThroughOutput()
        {
            List<PassThroughQueryTestInput> testVariations = new List<PassThroughQueryTestInput>
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

        private static PassThroughQueryTestInput CreateInput(
            string description,
            string query,
            bool expectedPassThrough,
            string partitionKeyPath,
            string partitionKeyValue)
        {
            PartitionKeyBuilder pkBuilder = new PartitionKeyBuilder();
            pkBuilder.Add(partitionKeyValue);

            return CreateInput(description, query, expectedPassThrough, partitionKeyPath, pkBuilder.Build());
        }

        private static PassThroughQueryTestInput CreateInput(
            string description,
            string query,
            bool expectedPassThrough,
            string partitionKeyPath,
            Cosmos.PartitionKey partitionKeyValue)
        {
            return new PassThroughQueryTestInput(description, query, new SqlQuerySpec(query), expectedPassThrough, partitionKeyPath, partitionKeyValue);
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

        public override PassThroughQueryTestOutput ExecuteTest(PassThroughQueryTestInput input)
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
            QueryRequestOptions queryRequestOptions = new QueryRequestOptions();

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
                testInjections: null);
          
            IQueryPipelineStage queryPipelineStage = CosmosQueryExecutionContextFactory.Create(
                      documentContainer,
                      cosmosQueryContextCore,
                      inputParameters,
                      NoOpTrace.Singleton);
            bool result = queryPipelineStage.MoveNextAsync(NoOpTrace.Singleton).Result; 

            Assert.AreEqual(input.ExpectedPassThrough, inputParameters.SqlQuerySpec.PassThrough);
            Assert.IsNotNull(queryPipelineStage);
            Assert.IsTrue(result);

            return new PassThroughQueryTestOutput(inputParameters.SqlQuerySpec.PassThrough);
        }
    }

    public sealed class PassThroughQueryTestOutput : BaselineTestOutput
    {
        public PassThroughQueryTestOutput(bool executeAsPassThrough)
        {
            this.ExecuteAsPassThrough = executeAsPassThrough;
        }

        public bool ExecuteAsPassThrough { get; }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteStartElement(nameof(this.ExecuteAsPassThrough));
            xmlWriter.WriteValue(this.ExecuteAsPassThrough);
            xmlWriter.WriteEndElement();
        }
    }

    public sealed class PassThroughQueryTestInput : BaselineTestInput
    {
        internal PartitionKeyDefinition PartitionKeyDefinition { get; set; }
        internal SqlQuerySpec SqlQuerySpec { get; set; }
        internal Cosmos.PartitionKey PartitionKeyValue { get; set; }
        internal bool ExpectedPassThrough { get; set; }
        internal PartitionKeyRangeIdentity PartitionKeyRangeId { get; set; }
        internal string Query { get; set; }

        internal PassThroughQueryTestInput(
            string description,
            string query,
            SqlQuerySpec sqlQuerySpec,
            bool expectedPassThrough,
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
            this.ExpectedPassThrough = expectedPassThrough;
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
