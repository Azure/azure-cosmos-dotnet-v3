//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    [TestClass]
    public class QueryPlanRetrieverTests
    {
        private static readonly IDictionary<string, object> DefaultQueryEngineConfiguration =
            new Dictionary<string, object>()
                {
                    {"maxSqlQueryInputLength", 30720},
                    {"maxJoinsPerSqlQuery", 5},
                    {"maxLogicalAndPerSqlQuery", 200},
                    {"maxLogicalOrPerSqlQuery", 200},
                    {"maxUdfRefPerSqlQuery", 6},
                    {"maxInExpressionItemsCount", 8000},
                    {"queryMaxInMemorySortDocumentCount", 500},
                    {"maxQueryRequestTimeoutFraction", 0.90},
                    {"sqlAllowNonFiniteNumbers", false},
                    {"sqlAllowAggregateFunctions", true},
                    {"sqlAllowSubQuery", false},
                    {"allowNewKeywords", true},
                    {"sqlAllowLike", true},
                    {"sqlAllowGroupByClause", false},
                    {"queryEnableMongoNativeRegex", true},
                    {"queryEnableDynamicDataMasking", true},
                    {"maxSpatialQueryCells", 12},
                    {"spatialMaxGeometryPointCount", 256},
                    {"sqlDisableOptimizationFlags", 0},
                    {"sqlQueryILDisableOptimizationFlags", 0},
                    {"sqlEnableParameterExpansionCheck", true},
                    {"clientDisableOptimisticDirectExecution", false},
                    {"queryEnableFullText", true}
                };

        private static readonly PartitionKeyDefinition PartitionKeyDefinition = new PartitionKeyDefinition
        {
            Paths = new Collection<string> { "/id" },
            Kind = PartitionKind.Hash,
            Version = PartitionKeyDefinitionVersion.V2
        };

        [TestMethod]
        public async Task ServiceInterop_BadRequestContainsInnerException()
        {
            ExpectedQueryPartitionProviderException innerException = new ExpectedQueryPartitionProviderException("some parsing error");
            Mock<CosmosQueryClient> queryClient = new Mock<CosmosQueryClient>();

            queryClient.Setup(c => c.TryGetPartitionedQueryExecutionInfoAsync(
                It.IsAny<SqlQuerySpec>(),
                It.IsAny<ResourceType>(),
                It.IsAny<Documents.PartitionKeyDefinition>(),
                It.IsAny<Cosmos.VectorEmbeddingPolicy>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Cosmos.GeospatialType>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(TryCatch<PartitionedQueryExecutionInfo>.FromException(innerException));

            CosmosException cosmosException = await Assert.ThrowsExceptionAsync<CosmosException>(() => QueryPlanRetriever.GetQueryPlanWithServiceInteropAsync(
                queryClient.Object,
                new SqlQuerySpec("selectttttt * from c"),
                ResourceType.Document,
                new Documents.PartitionKeyDefinition() { Paths = new Collection<string>() { "/id" } },
                vectorEmbeddingPolicy:null,
                hasLogicalPartitionKey: false,
                geospatialType: Cosmos.GeospatialType.Geography,
                useSystemPrefix: false,
                isHybridSearchQueryPlanOptimizationDisabled: false,
                NoOpTrace.Singleton));

            Assert.AreEqual(HttpStatusCode.BadRequest, cosmosException.StatusCode);
            Assert.AreEqual(innerException, cosmosException.InnerException);
            Assert.IsNotNull(cosmosException.Trace);
            Assert.IsNotNull(cosmosException.Diagnostics);
        }

        [TestMethod]
        public async Task ServiceInterop_BadRequestContainsOriginalCosmosException()
        {
            CosmosException expectedException = new CosmosException("Some message", (HttpStatusCode)429, (int)Documents.SubStatusCodes.Unknown, Guid.NewGuid().ToString(), 0);
            Mock<CosmosQueryClient> queryClient = new Mock<CosmosQueryClient>();

            queryClient.Setup(c => c.TryGetPartitionedQueryExecutionInfoAsync(
                It.IsAny<SqlQuerySpec>(),
                It.IsAny<ResourceType>(),
                It.IsAny<Documents.PartitionKeyDefinition>(),
                It.IsAny<Cosmos.VectorEmbeddingPolicy>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Cosmos.GeospatialType>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(TryCatch<PartitionedQueryExecutionInfo>.FromException(expectedException));

            Mock<ITrace> trace = new Mock<ITrace>();
            CosmosException cosmosException = await Assert.ThrowsExceptionAsync<CosmosException>(() => QueryPlanRetriever.GetQueryPlanWithServiceInteropAsync(
                queryClient.Object,
                new SqlQuerySpec("selectttttt * from c"),
                ResourceType.Document,
                new Documents.PartitionKeyDefinition() { Paths = new Collection<string>() { "/id" } },
                vectorEmbeddingPolicy: null,
                hasLogicalPartitionKey: false,
                geospatialType: Cosmos.GeospatialType.Geography,
                useSystemPrefix: false,
                isHybridSearchQueryPlanOptimizationDisabled: false,
                trace.Object,
                default));

            Assert.AreEqual(expectedException, cosmosException);
        }

        [TestMethod]
        public async Task ServiceInterop_E_UNEXPECTED()
        {
            UnexpectedQueryPartitionProviderException innerException = new UnexpectedQueryPartitionProviderException("E_UNEXPECTED");
            Mock<CosmosQueryClient> queryClient = new Mock<CosmosQueryClient>();

            queryClient.Setup(c => c.TryGetPartitionedQueryExecutionInfoAsync(
                It.IsAny<SqlQuerySpec>(),
                It.IsAny<ResourceType>(),
                It.IsAny<Documents.PartitionKeyDefinition>(),
                It.IsAny<Cosmos.VectorEmbeddingPolicy>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Cosmos.GeospatialType>(),
                It.IsAny<CancellationToken>())).ReturnsAsync(TryCatch<PartitionedQueryExecutionInfo>.FromException(innerException));

            CosmosException cosmosException = await Assert.ThrowsExceptionAsync<CosmosException>(() => QueryPlanRetriever.GetQueryPlanWithServiceInteropAsync(
                queryClient.Object,
                new SqlQuerySpec("Super secret query that triggers bug"),
                ResourceType.Document,
                new Documents.PartitionKeyDefinition() { Paths = new Collection<string>() { "/id" } },
                vectorEmbeddingPolicy: null,
                hasLogicalPartitionKey: false,
                geospatialType: Cosmos.GeospatialType.Geography,
                useSystemPrefix: false,
                isHybridSearchQueryPlanOptimizationDisabled: false,
                NoOpTrace.Singleton));

            Assert.AreEqual(HttpStatusCode.InternalServerError, cosmosException.StatusCode);
            Assert.AreEqual(innerException, cosmosException.InnerException);
            Assert.IsNotNull(cosmosException.Trace);
            Assert.IsNotNull(cosmosException.Diagnostics);
        }

        [TestMethod]
        public void TestBypassQueryParsing()
        {
            Assert.IsFalse(QueryPlanRetriever.BypassQueryParsing());

            foreach ((string name, string value, bool expectedValue) in new[]
                {
                    // Environment variables are case insensitive in windows
                    ("AZURE_COSMOS_BYPASS_QUERY_PARSING", "true", true),
                    ("AZURE_COSMOS_bypass_query_parsing", "True", true),
                    ("azure_cosmos_bypass_query_parsing", "TRUE", true),
                    ("Azure_Cosmos_Bypass_Query_Parsing", "truE", true),

                    ("AZURE_COSMOS_BYPASS_QUERY_PARSING", "false", false),
                    ("AZURE_COSMOS_bypass_query_parsing", "False", false),
                    ("azure_cosmos_bypass_query_parsing", "FALSE", false),
                    ("Azure_Cosmos_Bypass_Query_Parsing", "falsE", false),

                    ("AZURE_COSMOS_BYPASS_QUERY_PARSING", string.Empty, false)
                })
            {
                try
                {
                    // Test new value
                    Environment.SetEnvironmentVariable(name, value);
                    Assert.AreEqual(
                        expectedValue,
                        QueryPlanRetriever.BypassQueryParsing(),
                        $"EnvironmentVariable:'{name}', value:'{value}', expected:'{expectedValue}', actual:'{QueryPlanRetriever.BypassQueryParsing()}'");
                }
                finally
                {
                    // Remove side effects.
                    Environment.SetEnvironmentVariable(name, null);
                }
            }

            foreach (string value in new[]
                {
                    "'",
                    "-",
                    "asdf",
                    "'true'",
                    "'false'"
                })
            {
                bool receivedException = false;
                try
                {
                    // Test new value
                    Environment.SetEnvironmentVariable("AZURE_COSMOS_BYPASS_QUERY_PARSING", value);
                    bool _ = QueryPlanRetriever.BypassQueryParsing();
                }
                catch (FormatException fe)
                {
                    Assert.IsTrue(fe.ToString().Contains($@"String '{value}' was not recognized as a valid Boolean."));
                    receivedException = true;
                }
                finally
                {
                    // Remove side effects.
                    Environment.SetEnvironmentVariable("AZURE_COSMOS_BYPASS_QUERY_PARSING", null);
                }

                Assert.IsTrue(receivedException, $"Expected exception was not received for value '{value}'");
            }
        }

        [TestMethod]
        public async Task SanityTests()
        {
            TestCase[] testCases = new TestCase[]
            {
                MakeTest(
                    query: "SELECT * FROM c ORDER BY c.id",
                    expected: @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[""Ascending""],""orderByExpressions"":[""c.id""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT c._rid, [{\""item\"": c.id}] AS orderByItems, c AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY c.id"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),
                MakeTest(
                    query: "SELECT c.id FROM c GROUP BY c.id",
                    expected: @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[""c.id""],""groupByAliases"":[""id""],""aggregates"":[],""groupByAliasToAggregateType"":{""id"":null},""rewrittenQuery"":""SELECT [{\""item\"": c.id}] AS groupByItems, {\""id\"": c.id} AS payload\nFROM c\nGROUP BY c.id"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),
                MakeTest(
                    query: "SELECT DISTINCT VALUE c.id FROM c",
                    expected: @"{""distinctType"":""Unordered"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":"""",""hasSelectValue"":true,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),
                MakeTest(
                    query: "SELECT c.id, VectorDistance([-0.008861,0.097097,0.100236,0.070044,-0.079279,0.000923,-0.012829,0.064301,-0.029405], c.vector1, true, {dataType:'Float32', distanceFunction:'DotProduct'}) AS VectorDistance" +
                        " FROM c" +
                        " ORDER BY VectorDistance([-0.008861,0.097097,0.100236,0.070044,-0.079279,0.000923,-0.012829,0.064301,-0.029405], c.vector1, true, {dataType:'Float32', distanceFunction:'DotProduct'})",
                    expected: @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""VectorDistance([-0.008861, 0.097097, 0.100236, 0.070044, -0.079279, 0.000923, -0.012829, 0.064301, -0.029405], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""DotProduct\""})""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT c._rid, [{\""item\"": VectorDistance([-0.008861, 0.097097, 0.100236, 0.070044, -0.079279, 0.000923, -0.012829, 0.064301, -0.029405], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""DotProduct\""})}] AS orderByItems, {\""id\"": c.id, \""VectorDistance\"": VectorDistance([-0.008861, 0.097097, 0.100236, 0.070044, -0.079279, 0.000923, -0.012829, 0.064301, -0.029405], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""DotProduct\""})} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY VectorDistance([-0.008861, 0.097097, 0.100236, 0.070044, -0.079279, 0.000923, -0.012829, 0.064301, -0.029405], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""DotProduct\""})"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}"),
                MakeTest(
                    query: "SELECT c._ts" +
                        " FROM c" +
                        " ORDER BY VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {dataType:'Float32', distanceFunction:'DotProduct'})",
                    expected: @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""DotProduct\""})""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT c._rid, [{\""item\"": VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""DotProduct\""})}] AS orderByItems, {\""_ts\"": c._ts} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""DotProduct\""})"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}"),
                MakeTest(
                    query: "SELECT c._ts" +
                        " FROM c" +
                        " ORDER BY VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {dataType:'Float32', distanceFunction:'Euclidean'})",
                    expected: @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[""Ascending""],""orderByExpressions"":[""VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""Euclidean\""})""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT c._rid, [{\""item\"": VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""Euclidean\""})}] AS orderByItems, {\""_ts\"": c._ts} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""Euclidean\""})"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}"),
                MakeTest(
                    query: "SELECT c._ts" +
                        " FROM c" +
                        " ORDER BY VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {dataType:'Float32', distanceFunction:'DotProduct'})" +
                        " OFFSET 5 LIMIT 7",
                    expected: @"{""distinctType"":""None"",""top"":null,""offset"":5,""limit"":7,""orderBy"":[""Descending""],""orderByExpressions"":[""VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""DotProduct\""})""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT c._rid, [{\""item\"": VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""DotProduct\""})}] AS orderByItems, {\""_ts\"": c._ts} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""DotProduct\""})\nOFFSET 0 LIMIT 12"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}"),
                MakeTest(
                    query: "SELECT c._ts" +
                        " FROM c" +
                        " ORDER BY VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {dataType:'Float32', distanceFunction:'Euclidean'})" +
                        " OFFSET 5 LIMIT 7",
                    expected: @"{""distinctType"":""None"",""top"":null,""offset"":5,""limit"":7,""orderBy"":[""Ascending""],""orderByExpressions"":[""VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""Euclidean\""})""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT c._rid, [{\""item\"": VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""Euclidean\""})}] AS orderByItems, {\""_ts\"": c._ts} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""Euclidean\""})\nOFFSET 0 LIMIT 12"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}"),
                MakeTest(
                    query: "SELECT TOP 10 c._ts" +
                        " FROM c" +
                        " ORDER BY VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {dataType:'Float32', distanceFunction:'DotProduct'})",
                    expected: @"{""distinctType"":""None"",""top"":10,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""DotProduct\""})""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 10 c._rid, [{\""item\"": VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""DotProduct\""})}] AS orderByItems, {\""_ts\"": c._ts} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""DotProduct\""})"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}"),
                MakeTest(
                    query: "SELECT TOP 10 c._ts" +
                        " FROM c" +
                        " ORDER BY VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {dataType:'Float32', distanceFunction:'Euclidean'})",
                    expected: @"{""distinctType"":""None"",""top"":10,""offset"":null,""limit"":null,""orderBy"":[""Ascending""],""orderByExpressions"":[""VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""Euclidean\""})""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 10 c._rid, [{\""item\"": VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""Euclidean\""})}] AS orderByItems, {\""_ts\"": c._ts} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""Euclidean\""})"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}"),
                MakeTest(
                    query: "SELECT TOP 10 c._ts" +
                        " FROM c" +
                        " ORDER BY VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {dataType:'Float32'})",
                    @"{""vectorEmbeddings"": [{""path"": ""/vector1"", ""dataType"": ""float32"", ""dimensions"": 128, ""distanceFunction"": ""euclidean"" }]}",
                    expected: @"{""distinctType"":""None"",""top"":10,""offset"":null,""limit"":null,""orderBy"":[""Ascending""],""orderByExpressions"":[""VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\""})""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 10 c._rid, [{\""item\"": VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\""})}] AS orderByItems, {\""_ts\"": c._ts} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\""})"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}"),
                MakeTest(
                    query: "SELECT TOP 10 c._ts" +
                        " FROM c" +
                        " ORDER BY VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {dataType:'Float32'})",
                    @"{""vectorEmbeddings"": [{""path"": ""/vector1"", ""dataType"": ""float32"", ""dimensions"": 128, ""distanceFunction"": ""cosine"" }]}",
                    expected: @"{""distinctType"":""None"",""top"":10,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\""})""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 10 c._rid, [{\""item\"": VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\""})}] AS orderByItems, {\""_ts\"": c._ts} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\""})"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}"),
                MakeTest(
                    query: "SELECT TOP 10 c._ts" +
                        " FROM c" +
                        " ORDER BY VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {dataType:'Float32'})",
                    @"{""vectorEmbeddings"": [{""path"": ""/vector1"", ""dataType"": ""float32"", ""dimensions"": 128, ""distanceFunction"": ""dotproduct"" }]}",
                    expected: @"{""distinctType"":""None"",""top"":10,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\""})""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 10 c._rid, [{\""item\"": VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\""})}] AS orderByItems, {\""_ts\"": c._ts} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\""})"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}"),
                MakeTest(
                    query: "SELECT TOP 10 c._ts" +
                        " FROM c" +
                        " ORDER BY VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {dataType:'Float32', distanceFunction:'Cosine'})",
                    @"{""vectorEmbeddings"": [{""path"": ""/vector1"", ""dataType"": ""float32"", ""dimensions"": 128, ""distanceFunction"": ""euclidean"" }]}",
                    expected: @"{""distinctType"":""None"",""top"":10,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""Cosine\""})""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 10 c._rid, [{\""item\"": VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""Cosine\""})}] AS orderByItems, {\""_ts\"": c._ts} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""Cosine\""})"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}"),
                MakeTest(
                    query: "SELECT TOP 10 c._ts" +
                        " FROM c" +
                        " ORDER BY VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {dataType:'Float32', distanceFunction:'DotProduct'})",
                    @"{""vectorEmbeddings"": [{""path"": ""/vector1"", ""dataType"": ""float32"", ""dimensions"": 128, ""distanceFunction"": ""euclidean"" }]}",
                    expected: @"{""distinctType"":""None"",""top"":10,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""DotProduct\""})""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 10 c._rid, [{\""item\"": VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""DotProduct\""})}] AS orderByItems, {\""_ts\"": c._ts} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""DotProduct\""})"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}"),
                MakeTest(
                    query: "SELECT TOP 10 c._ts" +
                        " FROM c" +
                        " ORDER BY VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {dataType:'Float32', distanceFunction:'Euclidean'})",
                    @"{""vectorEmbeddings"": [{""path"": ""/vector1"", ""dataType"": ""float32"", ""dimensions"": 128, ""distanceFunction"": ""dotproduct"" }]}",
                    expected: @"{""distinctType"":""None"",""top"":10,""offset"":null,""limit"":null,""orderBy"":[""Ascending""],""orderByExpressions"":[""VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""Euclidean\""})""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 10 c._rid, [{\""item\"": VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""Euclidean\""})}] AS orderByItems, {\""_ts\"": c._ts} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY VectorDistance([0.66178012, -0.37191326, 0.97474534, -0.07378916, 0.19075451], c.vector1, true, {\""dataType\"": \""Float32\"", \""distanceFunction\"": \""Euclidean\""})"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}"),
                MakeTest(
                    query: "SELECT VALUE MAKELIST(c.id) FROM c",
                    expected: @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[""MakeList""],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT VALUE [{\""item\"": MAKELIST(c.id)}]\nFROM c"",""hasSelectValue"":true,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),
                MakeTest(
                    query: "SELECT VALUE MAKESET(c.id) FROM c",
                    expected: @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[""MakeSet""],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT VALUE [{\""item\"": MAKESET(c.id)}]\nFROM c"",""hasSelectValue"":true,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),
                MakeTest(
                    query: "SELECT MAKELIST(c.id) FROM c",
                    expected: @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[],""groupByAliases"":[""$1""],""aggregates"":[],""groupByAliasToAggregateType"":{""$1"":""MakeList""},""rewrittenQuery"":""SELECT {\""$1\"": {\""item\"": MAKELIST(c.id)}} AS payload\nFROM c"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),
                MakeTest(
                    query: "SELECT MAKESET(c.id) FROM c",
                    expected: @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[],""groupByAliases"":[""$1""],""aggregates"":[],""groupByAliasToAggregateType"":{""$1"":""MakeSet""},""rewrittenQuery"":""SELECT {\""$1\"": {\""item\"": MAKESET(c.id)}} AS payload\nFROM c"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),
                MakeTest(
                    query: "SELECT MAKELIST(c.id) FROM c GROUP BY c.zipcode",
                    expected: @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[""c.zipcode""],""groupByAliases"":[""$1""],""aggregates"":[],""groupByAliasToAggregateType"":{""$1"":""MakeList""},""rewrittenQuery"":""SELECT [{\""item\"": c.zipcode}] AS groupByItems, {\""$1\"": {\""item\"": MAKELIST(c.id)}} AS payload\nFROM c\nGROUP BY c.zipcode"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),
                MakeTest(
                    query: "SELECT MAKESET(c.id) FROM c GROUP BY c.zipcode",
                    expected: @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[""c.zipcode""],""groupByAliases"":[""$1""],""aggregates"":[],""groupByAliasToAggregateType"":{""$1"":""MakeSet""},""rewrittenQuery"":""SELECT [{\""item\"": c.zipcode}] AS groupByItems, {\""$1\"": {\""item\"": MAKESET(c.id)}} AS payload\nFROM c\nGROUP BY c.zipcode"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),
                MakeTest(
                    query: "SELECT c.zipcode, MAX(c.age) FROM c GROUP BY c.zipcode",
                    expected: @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[""c.zipcode""],""groupByAliases"":[""zipcode"",""$1""],""aggregates"":[],""groupByAliasToAggregateType"":{""$1"":""Max"",""zipcode"":null},""rewrittenQuery"":""SELECT [{\""item\"": c.zipcode}] AS groupByItems, {\""zipcode\"": c.zipcode, \""$1\"": {\""item\"": MAX(c.age), \""item2\"": {\""max\"": MAX(c.age), \""count\"": COUNT(c.age)}}} AS payload\nFROM c\nGROUP BY c.zipcode"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),
                MakeTest(
                    query: "SELECT c.region, c.zipcode, MIN(c.age) AS min_age, MAX(c.age) AS max_age FROM c GROUP BY c.region, c.zipcode",
                    expected: @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[""c.region"",""c.zipcode""],""groupByAliases"":[""region"",""zipcode"",""min_age"",""max_age""],""aggregates"":[],""groupByAliasToAggregateType"":{""min_age"":""Min"",""max_age"":""Max"",""region"":null,""zipcode"":null},""rewrittenQuery"":""SELECT [{\""item\"": c.region}, {\""item\"": c.zipcode}] AS groupByItems, {\""region\"": c.region, \""zipcode\"": c.zipcode, \""min_age\"": {\""item\"": MIN(c.age), \""item2\"": {\""min\"": MIN(c.age), \""count\"": COUNT(c.age)}}, \""max_age\"": {\""item\"": MAX(c.age), \""item2\"": {\""max\"": MAX(c.age), \""count\"": COUNT(c.age)}}} AS payload\nFROM c\nGROUP BY c.region, c.zipcode"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),
                MakeTest(
                    query: "SELECT c.zipcode FROM c GROUP BY c.zipcode",
                    expected: @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[""c.zipcode""],""groupByAliases"":[""zipcode""],""aggregates"":[],""groupByAliasToAggregateType"":{""zipcode"":null},""rewrittenQuery"":""SELECT [{\""item\"": c.zipcode}] AS groupByItems, {\""zipcode\"": c.zipcode} AS payload\nFROM c\nGROUP BY c.zipcode"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),
                MakeTest(
                    query: "SELECT VALUE COUNTIF(c.valid) FROM c",
                    expected: @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[""CountIf""],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT VALUE [{\""item\"": COUNTIF(c.valid)}]\nFROM c"",""hasSelectValue"":true,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),
                MakeTest(
                    query: "SELECT COUNTIF(c.valid) FROM c",
                    expected: @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[],""groupByAliases"":[""$1""],""aggregates"":[],""groupByAliasToAggregateType"":{""$1"":""CountIf""},""rewrittenQuery"":""SELECT {\""$1\"": {\""item\"": COUNTIF(c.valid)}} AS payload\nFROM c"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),
                MakeTest(
                    query: "SELECT COUNTIF(c.valid) FROM c GROUP BY c.zipcode",
                    expected: @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[""c.zipcode""],""groupByAliases"":[""$1""],""aggregates"":[],""groupByAliasToAggregateType"":{""$1"":""CountIf""},""rewrittenQuery"":""SELECT [{\""item\"": c.zipcode}] AS groupByItems, {\""$1\"": {\""item\"": COUNTIF(c.valid)}} AS payload\nFROM c\nGROUP BY c.zipcode"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),
                MakeTest(
                    query: "SELECT c.zipcode, COUNTIF(c.valid) AS valid_count FROM c GROUP BY c.zipcode",
                    expected: @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[""c.zipcode""],""groupByAliases"":[""zipcode"",""valid_count""],""aggregates"":[],""groupByAliasToAggregateType"":{""zipcode"":null,""valid_count"":""CountIf""},""rewrittenQuery"":""SELECT [{\""item\"": c.zipcode}] AS groupByItems, {\""zipcode\"": c.zipcode, \""valid_count\"": {\""item\"": COUNTIF(c.valid)}} AS payload\nFROM c\nGROUP BY c.zipcode"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),
                MakeTest(
                    query: "SELECT TOP 10 * FROM c ORDER BY RANK FullTextScore(c.text, \"swim\", \"run\")",
                    expected: @"{""globalStatisticsQuery"":""SELECT COUNT(1) AS documentCount, [{\""totalWordCount\"": SUM(_FullTextWordCount(c.text)), \""hitCounts\"": [COUNTIF(FullTextContains(c.text, \""swim\"")), COUNTIF(FullTextContains(c.text, \""run\""))]}] AS fullTextStatistics\nFROM c"",""componentQueryInfos"":[{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [(_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) ?? -1)]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}],""componentWithoutPayloadQueryInfos"":[],""projectionQueryInfo"":null,""componentWeights"":[],""skip"":null,""take"":10,""requiresGlobalStatistics"":true}"),
                MakeTest(
                    query: "SELECT TOP 10 * FROM c ORDER BY RANK RRF(FullTextScore(c.text,  \"swim\", \"run\"), VectorDistance(c.image, [1, 2, 3]))",
                    expected: @"{""globalStatisticsQuery"":""SELECT COUNT(1) AS documentCount, [{\""totalWordCount\"": SUM(_FullTextWordCount(c.text)), \""hitCounts\"": [COUNTIF(FullTextContains(c.text, \""swim\"")), COUNTIF(FullTextContains(c.text, \""run\""))]}] AS fullTextStatistics\nFROM c"",""componentQueryInfos"":[{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [(_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) ?? -1), (_VectorScore(c.image, [1, 2, 3]) ?? -1.79769e+308)]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true},{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_VectorScore(c.image, [1, 2, 3])""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _VectorScore(c.image, [1, 2, 3])}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [(_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) ?? -1), (_VectorScore(c.image, [1, 2, 3]) ?? -1.79769e+308)]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _VectorScore(c.image, [1, 2, 3])"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}],""componentWithoutPayloadQueryInfos"":[],""projectionQueryInfo"":null,""componentWeights"":[],""skip"":null,""take"":10,""requiresGlobalStatistics"":true}"),
                MakeTest(
                    query: "SELECT TOP 10 * FROM c ORDER BY RANK RRF(VectorDistance(c.backup_image, [0.5, 0.2, 0.33]), VectorDistance(c.image, [1, 2, 3]))",
                    expected: @"{""globalStatisticsQuery"":""SELECT COUNT(1) AS documentCount, [] AS fullTextStatistics\nFROM c"",""componentQueryInfos"":[{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_VectorScore(c.backup_image, [0.5, 0.2, 0.33])""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _VectorScore(c.backup_image, [0.5, 0.2, 0.33])}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [(_VectorScore(c.backup_image, [0.5, 0.2, 0.33]) ?? -1.79769e+308), (_VectorScore(c.image, [1, 2, 3]) ?? -1.79769e+308)]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _VectorScore(c.backup_image, [0.5, 0.2, 0.33])"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true},{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_VectorScore(c.image, [1, 2, 3])""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _VectorScore(c.image, [1, 2, 3])}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [(_VectorScore(c.backup_image, [0.5, 0.2, 0.33]) ?? -1.79769e+308), (_VectorScore(c.image, [1, 2, 3]) ?? -1.79769e+308)]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _VectorScore(c.image, [1, 2, 3])"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}],""componentWithoutPayloadQueryInfos"":[],""projectionQueryInfo"":null,""componentWeights"":[],""skip"":null,""take"":10,""requiresGlobalStatistics"":false}"),
                MakeTest(
                    query: "SELECT * FROM c WHERE FullTextContains(c.abstract, 'calculation')",
                    expected: @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":"""",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),
                MakeTest(
                    query: "SELECT COUNTIF(_FullTextWordCount(c.abstract) > 10), COUNTIF(FullTextContains(c.abstract, 'inspiration')) FROM c",
                    expected: @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[],""groupByAliases"":[""$1"",""$2""],""aggregates"":[],""groupByAliasToAggregateType"":{""$1"":""CountIf"",""$2"":""CountIf""},""rewrittenQuery"":""SELECT {\""$1\"": {\""item\"": COUNTIF((_FullTextWordCount(c.abstract) > 10))}, \""$2\"": {\""item\"": COUNTIF(FullTextContains(c.abstract, \""inspiration\""))}} AS payload\nFROM c"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),
                MakeHybridSearchOptimizationTest(
                    query: "SELECT TOP 10 * FROM c ORDER BY RANK FullTextScore(c.text, \"swim\", \"run\")",
                    expected: @"{""globalStatisticsQuery"":""SELECT COUNT(1) AS documentCount, [{\""totalWordCount\"": SUM(_FullTextWordCount(c.text)), \""hitCounts\"": [COUNTIF(FullTextContains(c.text, \""swim\"")), COUNTIF(FullTextContains(c.text, \""run\""))]}] AS fullTextStatistics\nFROM c"",""componentQueryInfos"":[{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})}] AS orderByItems, {\""payload\"": c, \""componentScores\"": []} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}],""componentWithoutPayloadQueryInfos"":[],""projectionQueryInfo"":null,""componentWeights"":[],""skip"":null,""take"":10,""requiresGlobalStatistics"":true}"),
                MakeHybridSearchOptimizationTest(
                    query: "SELECT TOP 10 * FROM c ORDER BY RANK RRF(FullTextScore(c.text, \"swim\", \"run\"), VectorDistance(c.image, [1, 2, 3]))",
                    expected: @"{""globalStatisticsQuery"":""SELECT COUNT(1) AS documentCount, [{\""totalWordCount\"": SUM(_FullTextWordCount(c.text)), \""hitCounts\"": [COUNTIF(FullTextContains(c.text, \""swim\"")), COUNTIF(FullTextContains(c.text, \""run\""))]}] AS fullTextStatistics\nFROM c"",""componentQueryInfos"":[{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, c AS payload, [(_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) ?? -1), (_VectorScore(c.image, [1, 2, 3]) ?? -1.79769e+308)] AS componentScores\nFROM c\nORDER BY _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false},{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, c AS payload, [(_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) ?? -1), (_VectorScore(c.image, [1, 2, 3]) ?? -1.79769e+308)] AS componentScores\nFROM c\nORDER BY _VectorScore(c.image, [1, 2, 3])"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}],""componentWithoutPayloadQueryInfos"":[],""projectionQueryInfo"":null,""componentWeights"":[],""skip"":null,""take"":10,""requiresGlobalStatistics"":true}"),
                MakeTest(
                    query: "SELECT TOP 10 * FROM c ORDER BY RANK RRF(VectorDistance(c.backup_image, [0.5, 0.2, 0.33]), VectorDistance(c.image, [1, 2, 3]), [1, -0.3])",
                    expected: @"{""globalStatisticsQuery"":""SELECT COUNT(1) AS documentCount, [] AS fullTextStatistics\nFROM c"",""componentQueryInfos"":[{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_VectorScore(c.backup_image, [0.5, 0.2, 0.33])""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _VectorScore(c.backup_image, [0.5, 0.2, 0.33])}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [(_VectorScore(c.backup_image, [0.5, 0.2, 0.33]) ?? -1.79769e+308), (_VectorScore(c.image, [1, 2, 3]) ?? 1.79769e+308)]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _VectorScore(c.backup_image, [0.5, 0.2, 0.33])"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true},{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Ascending""],""orderByExpressions"":[""_VectorScore(c.image, [1, 2, 3])""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _VectorScore(c.image, [1, 2, 3])}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [(_VectorScore(c.backup_image, [0.5, 0.2, 0.33]) ?? -1.79769e+308), (_VectorScore(c.image, [1, 2, 3]) ?? 1.79769e+308)]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _VectorScore(c.image, [1, 2, 3]) ASC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}],""componentWithoutPayloadQueryInfos"":[],""projectionQueryInfo"":null,""componentWeights"":[1.0,0.3],""skip"":null,""take"":10,""requiresGlobalStatistics"":false}"),
                MakeHybridSearchOptimizationTest(
                    query: "SELECT TOP 10 * FROM c ORDER BY RANK RRF(VectorDistance(c.backup_image, [0.5, 0.2, 0.33]), VectorDistance(c.image, [1, 2, 3]), [1, -0.3])",
                    expected: @"{""globalStatisticsQuery"":""SELECT COUNT(1) AS documentCount, [] AS fullTextStatistics\nFROM c"",""componentQueryInfos"":[{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, c AS payload, [(_VectorScore(c.backup_image, [0.5, 0.2, 0.33]) ?? -1.79769e+308), (-1 * (_VectorScore(c.image, [1, 2, 3]) ?? 1.79769e+308))] AS componentScores\nFROM c\nORDER BY _VectorScore(c.backup_image, [0.5, 0.2, 0.33])"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false},{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, c AS payload, [(_VectorScore(c.backup_image, [0.5, 0.2, 0.33]) ?? -1.79769e+308), (-1 * (_VectorScore(c.image, [1, 2, 3]) ?? 1.79769e+308))] AS componentScores\nFROM c\nORDER BY _VectorScore(c.image, [1, 2, 3]) ASC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}],""componentWithoutPayloadQueryInfos"":[],""projectionQueryInfo"":null,""componentWeights"":[1.0,0.3],""skip"":null,""take"":10,""requiresGlobalStatistics"":false}"),
            };

            QueryPartitionProvider queryPartitionProvider = new QueryPartitionProvider(DefaultQueryEngineConfiguration);
            CosmosQueryClient queryClient = new MockCosmosQueryClient(queryPartitionProvider);

            foreach (TestCase testCase in testCases)
            {
                await RunTestAsync(queryClient, testCase);
            }
        }

        private static async Task RunTestAsync(CosmosQueryClient queryClient, TestCase testCase)
        {
            DebugTraceHelper.Trace(testCase);

            Cosmos.VectorEmbeddingPolicy vectorEmbeddingPolicy = testCase.VectorEmbeddingPolicy != null ?
                JsonConvert.DeserializeObject<Cosmos.VectorEmbeddingPolicy>(testCase.VectorEmbeddingPolicy) :
                new Cosmos.VectorEmbeddingPolicy(new Collection<Cosmos.Embedding>());

            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo = await QueryPlanRetriever.GetQueryPlanWithServiceInteropAsync(
                queryClient,
                testCase.Query,
                ResourceType.Document,
                PartitionKeyDefinition,
                vectorEmbeddingPolicy,
                testCase.HasLogicalPartitionKey,
                Cosmos.GeospatialType.Geography,
                useSystemPrefix: false,
                isHybridSearchQueryPlanOptimizationDisabled: testCase.HybridSearchQueryPlanOptimizationDisabled,
                NoOpTrace.Singleton);

            JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.None,
            };
            string queryPlan = partitionedQueryExecutionInfo.QueryInfo != null ?
                JsonConvert.SerializeObject(partitionedQueryExecutionInfo.QueryInfo, jsonSerializerSettings) :
                JsonConvert.SerializeObject(partitionedQueryExecutionInfo.HybridSearchQueryInfo, jsonSerializerSettings);
            DebugTraceHelper.TraceQueryPlan(queryPlan);

            Assert.AreEqual(testCase.ExpectedQueryPlan, queryPlan);
        }

        private static TestCase MakeTest(
            string query,
            string expected)
        {
            return new TestCase(
                new SqlQuerySpec(query),
                hasLogicalPartitionKey: false,
                vectorEmbeddingPolicy: null,
                hybridSearchQueryPlanOptimizationDisabled: true,
                expected);
        }

        private static TestCase MakeHybridSearchOptimizationTest(
            string query,
            string expected)
        {
            return new TestCase(
                new SqlQuerySpec(query),
                hasLogicalPartitionKey: false,
                vectorEmbeddingPolicy: null,
                hybridSearchQueryPlanOptimizationDisabled: false,
                expected);
        }

        private static TestCase MakeTest(
            string query,
            string vectorEmbeddingPolicy,
            string expected)
        {
            return new TestCase(
                new SqlQuerySpec(query),
                hasLogicalPartitionKey: false,
                vectorEmbeddingPolicy,
                hybridSearchQueryPlanOptimizationDisabled: true,
                expected);
        }

        private sealed class TestCase
        {
            public SqlQuerySpec Query { get; }

            public bool HasLogicalPartitionKey { get; }

            public string VectorEmbeddingPolicy { get; }

            public bool HybridSearchQueryPlanOptimizationDisabled { get; }

            public string ExpectedQueryPlan { get; }

            public TestCase(
                SqlQuerySpec query,
                bool hasLogicalPartitionKey,
                string vectorEmbeddingPolicy,
                bool hybridSearchQueryPlanOptimizationDisabled,
                string expectedQueryPlan)
            {
                this.Query = query ?? throw new ArgumentNullException(nameof(query));
                this.HasLogicalPartitionKey = hasLogicalPartitionKey;
                this.VectorEmbeddingPolicy = vectorEmbeddingPolicy;
                this.HybridSearchQueryPlanOptimizationDisabled = hybridSearchQueryPlanOptimizationDisabled;
                this.ExpectedQueryPlan = expectedQueryPlan;
            }
        }

        private static class DebugTraceHelper
        {
            private const bool Enabled = true;

#pragma warning disable CS0162 // Unreachable code detected
            public static void Trace(TestCase testCase)
            {
                if (Enabled)
                {
                    System.Diagnostics.Trace.WriteLine(Environment.NewLine);
                    System.Diagnostics.Trace.WriteLine("Executing test case:");
                    System.Diagnostics.Trace.WriteLine("  Query: " + testCase.Query.QueryText);
                    System.Diagnostics.Trace.WriteLine("  HasLogicalPartitionKey: " + testCase.HasLogicalPartitionKey);
                    System.Diagnostics.Trace.WriteLine("  VectorEmbeddingPolicy: " + testCase.VectorEmbeddingPolicy);
                    System.Diagnostics.Trace.WriteLine("  HybridSearchQueryPlanOptimizationDisabled: " + testCase.HybridSearchQueryPlanOptimizationDisabled);
                    System.Diagnostics.Trace.WriteLine("  ExpectedQueryPlan: " + testCase.ExpectedQueryPlan);
                }
            }

            public static void TraceQueryPlan(string actualQueryPlan)
            {
                if (Enabled)
                {
                    System.Diagnostics.Trace.WriteLine("QueryPlan: "+ actualQueryPlan);
                }
            }

            public static void TraceException(CosmosException exception)
            {
                if (Enabled)
                {
                    System.Diagnostics.Trace.WriteLine("Encountered Exception:");
                    System.Diagnostics.Trace.WriteLine("  " + exception.Message);
                }
            }
#pragma warning restore CS0162 // Unreachable code detected
        }

        private class MockCosmosQueryClient : CosmosQueryClient
        {
            private readonly QueryPartitionProvider queryPartitionProvider;

            private readonly CosmosSerializerCore serializer = new CosmosSerializerCore();

            public MockCosmosQueryClient(QueryPartitionProvider queryPartitionProvider)
            {
                this.queryPartitionProvider = queryPartitionProvider ?? throw new ArgumentNullException(nameof(queryPartitionProvider));
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
                throw new NotImplementedException();
            }

            public override Task ForceRefreshCollectionCacheAsync(string collectionLink, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public override Task<ContainerQueryProperties> GetCachedContainerQueryPropertiesAsync(string containerLink, Cosmos.PartitionKey? partitionKey, ITrace trace, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public override Task<bool> GetClientDisableOptimisticDirectExecutionAsync()
            {
                throw new NotImplementedException();
            }

            public override Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangeByFeedRangeAsync(string resourceLink, string collectionResourceId, PartitionKeyDefinition partitionKeyDefinition, FeedRangeInternal feedRangeInternal, bool forceRefresh, ITrace trace)
            {
                throw new NotImplementedException();
            }

            public override Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangesAsync(string resourceLink, string collectionResourceId, IReadOnlyList<Range<string>> providedRanges, bool forceRefresh, ITrace trace, PartitionKeyDefinition partitionKeyDefinition)
            {
                throw new NotImplementedException();
            }

            public override Task<IReadOnlyList<PartitionKeyRange>> TryGetOverlappingRangesAsync(string collectionResourceId, Range<string> range, PartitionKeyDefinition partitionKeyDefinition, bool forceRefresh = false)
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
                bool isHybridSearchQueryPlanOptimizationDisabled,
                Cosmos.GeospatialType geospatialType,
                CancellationToken cancellationToken)
            {
                string queryString = null;
                if (sqlQuerySpec != null)
                {
                    using (Stream stream = this.serializer.ToStreamSqlQuerySpec(sqlQuerySpec, resourceType))
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            queryString = reader.ReadToEnd();
                        }
                    }
                }
            
                return Task.FromResult(this.queryPartitionProvider.TryGetPartitionedQueryExecutionInfo(
                    querySpecJsonString: queryString,
                    partitionKeyDefinition: partitionKeyDefinition,
                    vectorEmbeddingPolicy: vectorEmbeddingPolicy,
                    requireFormattableOrderByQuery: requireFormattableOrderByQuery,
                    isContinuationExpected: isContinuationExpected,
                    allowNonValueAggregateQuery: allowNonValueAggregateQuery,
                    hasLogicalPartitionKey: hasLogicalPartitionKey,
                    allowDCount: allowDCount,
                    useSystemPrefix: useSystemPrefix,
                    hybridSearchSkipOrderByRewrite: !isHybridSearchQueryPlanOptimizationDisabled,
                    geospatialType: geospatialType));
            }
        }
    }
}
