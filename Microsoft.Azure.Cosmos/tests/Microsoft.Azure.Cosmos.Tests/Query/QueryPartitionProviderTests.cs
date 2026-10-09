namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public class QueryPartitionProviderTests
    {
        private static readonly PartitionKeyDefinition PartitionKeyDefinition = new PartitionKeyDefinition()
        {
            Paths = new System.Collections.ObjectModel.Collection<string>()
            {
                "/id",
            },
            Kind = PartitionKind.Hash,
        };

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("Rrf")]
        [DataRow("CombinedScore")]
        public void ScoreCombinationKindRoundTripsThroughPlanningRoutes(string kind)
        {
            string discriminator = kind == null ? "" : $@"""scoreCombinationKind"":""{kind}"",";
            string json = $@"{{""hybridSearchQueryInfo"":{{{discriminator}""componentQueryInfos"":[{{}},{{}}]}},
                ""queryRanges"":[{{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}}]}}";
            ScoreCombinationKind? expected = kind == null ? null : Enum.Parse<ScoreCombinationKind>(kind);
            using QueryPartitionProvider provider = new QueryPartitionProvider(
                new Dictionary<string, object> { ["maxSqlQueryInputLength"] = 30720 });
            PartitionedQueryExecutionInfoInternal native = JsonConvert.DeserializeObject<PartitionedQueryExecutionInfoInternal>(json);
            PartitionedQueryExecutionInfo converted = provider.ConvertPartitionedQueryExecutionInfo(native, PartitionKeyDefinition);
            Assert.AreEqual(expected, converted.HybridSearchQueryInfo.ScoreCombinationKind);

            using MemoryStream thinClientStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            PartitionedQueryExecutionInfo thinClient = ThinClientQueryPlanHelper.DeserializeQueryPlanResponse(thinClientStream, PartitionKeyDefinition);
            Assert.AreEqual(expected, thinClient.HybridSearchQueryInfo.ScoreCombinationKind);

            using MemoryStream gatewayStream = new MemoryStream(Encoding.UTF8.GetBytes(converted.ToString()));
            PartitionedQueryExecutionInfo gateway = new CosmosSerializerCore().FromStream<PartitionedQueryExecutionInfo>(gatewayStream);
            Assert.AreEqual(expected, gateway.HybridSearchQueryInfo.ScoreCombinationKind);
            foreach (PartitionedQueryExecutionInfo plan in new[] { converted, thinClient, gateway })
            {
                Assert.IsTrue(PartitionedQueryExecutionInfo.TryParse(plan.ToString(), out PartitionedQueryExecutionInfo roundTrip));
                Assert.AreEqual(expected, roundTrip.HybridSearchQueryInfo.ScoreCombinationKind);
                Assert.AreEqual(kind != null, roundTrip.ToString().Contains("scoreCombinationKind"));
            }
        }

        [DataTestMethod]
        [DataRow(@"""Unknown""")]
        [DataRow(@"""1""")]
        [DataRow("1")]
        [DataRow("[]")]
        [DataRow("null")]
        [DataRow(@"""Rrf, CombinedScore""")]
        public void InvalidScoreCombinationKindIsRejected(string kind)
        {
            string json = $@"{{""hybridSearchQueryInfo"":{{""scoreCombinationKind"":{kind}}},
                ""queryRanges"":[{{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}}]}}";
            Assert.ThrowsException<JsonSerializationException>(() => JsonConvert.DeserializeObject<PartitionedQueryExecutionInfoInternal>(json));
            Assert.ThrowsException<JsonSerializationException>(() => JsonConvert.DeserializeObject<PartitionedQueryExecutionInfo>(json));
            using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            Assert.ThrowsException<JsonSerializationException>(() => ThinClientQueryPlanHelper.DeserializeQueryPlanResponse(stream, PartitionKeyDefinition));
            Assert.IsFalse(PartitionedQueryExecutionInfo.TryParse(json, out _));
        }

        [DataTestMethod]
        [DataRow("top")]
        [DataRow("offset")]
        [DataRow("limit")]
        public void CombinedScoreRejectsUnrepresentableCandidateCounts(string property)
        {
            string json = $@"{{""hybridSearchQueryInfo"":{{""scoreCombinationKind"":""CombinedScore"",
                ""componentQueryInfos"":[{{""{property}"":18446744073709551615}},{{}}]}},
                ""queryRanges"":[{{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}}]}}";
            Assert.ThrowsException<JsonSerializationException>(() => JsonConvert.DeserializeObject<PartitionedQueryExecutionInfoInternal>(json));
            Assert.IsFalse(PartitionedQueryExecutionInfo.TryParse(json, out _));
            using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            Assert.ThrowsException<JsonSerializationException>(() => ThinClientQueryPlanHelper.DeserializeQueryPlanResponse(stream, PartitionKeyDefinition));
        }

        [TestMethod]
        public void TestQueryPartitionProviderUpdate()
        {
            IDictionary<string, object> smallQueryConfiguration = new Dictionary<string, object>() { { "maxSqlQueryInputLength", 5 } };
            IDictionary<string, object> largeQueryConfiguration = new Dictionary<string, object>() { { "maxSqlQueryInputLength", 524288 } };

            QueryPartitionProvider queryPartitionProvider = new QueryPartitionProvider(smallQueryConfiguration);

            string sqlQuerySpec = JsonConvert.SerializeObject(new SqlQuerySpec("SELECT * FROM c"));

            TryCatch<PartitionedQueryExecutionInfo> tryGetQueryPlan = queryPartitionProvider.TryGetPartitionedQueryExecutionInfo(
                    querySpecJsonString: sqlQuerySpec,
                    partitionKeyDefinition: PartitionKeyDefinition,
                    vectorEmbeddingPolicy: null,
                    requireFormattableOrderByQuery: true,
                    isContinuationExpected: false,
                    allowNonValueAggregateQuery: true,
                    hasLogicalPartitionKey: false,
                    allowDCount: true,
                    hybridSearchSkipOrderByRewrite: false,
                    useSystemPrefix: false,
                    geospatialType: Cosmos.GeospatialType.Geography);

            Assert.IsTrue(tryGetQueryPlan.Failed);
            Assert.IsTrue(tryGetQueryPlan.Exception.ToString().Contains("The SQL query text exceeded the maximum limit of 5 characters"));

            queryPartitionProvider.Update(largeQueryConfiguration);

            tryGetQueryPlan = queryPartitionProvider.TryGetPartitionedQueryExecutionInfo(
                            querySpecJsonString: sqlQuerySpec,
                            partitionKeyDefinition: PartitionKeyDefinition,
                            vectorEmbeddingPolicy: null,
                            requireFormattableOrderByQuery: true,
                            isContinuationExpected: false,
                            allowNonValueAggregateQuery: true,
                            hasLogicalPartitionKey: false,
                            allowDCount: true,
                            hybridSearchSkipOrderByRewrite: false,
                            useSystemPrefix: false,
                            geospatialType: Cosmos.GeospatialType.Geography);

            Assert.IsTrue(tryGetQueryPlan.Succeeded);
        }

        [TestMethod]
        public void TestPartitionedQueryExecutionInfoDeserialization()
        {
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition()
            {
                Paths = new System.Collections.ObjectModel.Collection<string>()
                {
                    "/id",
                },
                Kind = PartitionKind.Hash,
            };

            (string, string)[] testCases = new (string, string)[]
            {
                (@"{""queryInfo"":{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[""CountIf""],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT VALUE [{\""item\"": COUNTIF(c.valid)}]\nFROM c"",""hasSelectValue"":true,""dCountInfo"":null,""hasNonStreamingOrderBy"":false},""queryRanges"":[{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}]}",
                    @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[""CountIf""],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT VALUE [{\""item\"": COUNTIF(c.valid)}]\nFROM c"",""hasSelectValue"":true,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),
                (@"{""queryInfo"":{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[],""groupByAliases"":[""$1""],""aggregates"":[],""groupByAliasToAggregateType"":{""$1"":""CountIf""},""rewrittenQuery"":""SELECT {\""$1\"": {\""item\"": COUNTIF(c.valid)}} AS payload\nFROM c"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false},""queryRanges"":[{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}]}",
                    @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[],""groupByAliases"":[""$1""],""aggregates"":[],""groupByAliasToAggregateType"":{""$1"":""CountIf""},""rewrittenQuery"":""SELECT {\""$1\"": {\""item\"": COUNTIF(c.valid)}} AS payload\nFROM c"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),
                (@"{""queryInfo"":{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[""c.zipcode""],""groupByAliases"":[""$1""],""aggregates"":[],""groupByAliasToAggregateType"":{""$1"":""CountIf""},""rewrittenQuery"":""SELECT [{\""item\"": c.zipcode}] AS groupByItems, {\""$1\"": {\""item\"": COUNTIF(c.valid)}} AS payload\nFROM c\nGROUP BY c.zipcode"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false},""queryRanges"":[{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}]}",
                    @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[""c.zipcode""],""groupByAliases"":[""$1""],""aggregates"":[],""groupByAliasToAggregateType"":{""$1"":""CountIf""},""rewrittenQuery"":""SELECT [{\""item\"": c.zipcode}] AS groupByItems, {\""$1\"": {\""item\"": COUNTIF(c.valid)}} AS payload\nFROM c\nGROUP BY c.zipcode"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),
                (@"{""queryInfo"":{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[""c.zipcode""],""groupByAliases"":[""zipcode"",""valid_count""],""aggregates"":[],""groupByAliasToAggregateType"":{""zipcode"":null,""valid_count"":""CountIf""},""rewrittenQuery"":""SELECT [{\""item\"": c.zipcode}] AS groupByItems, {\""zipcode\"": c.zipcode, \""valid_count\"": {\""item\"": COUNTIF(c.valid)}} AS payload\nFROM c\nGROUP BY c.zipcode"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false},""queryRanges"":[{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}]}",
                    @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""groupByExpressions"":[""c.zipcode""],""groupByAliases"":[""zipcode"",""valid_count""],""aggregates"":[],""groupByAliasToAggregateType"":{""zipcode"":null,""valid_count"":""CountIf""},""rewrittenQuery"":""SELECT [{\""item\"": c.zipcode}] AS groupByItems, {\""zipcode\"": c.zipcode, \""valid_count\"": {\""item\"": COUNTIF(c.valid)}} AS payload\nFROM c\nGROUP BY c.zipcode"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),

                (@"{""hybridSearchQueryInfo"":{""globalStatisticsQuery"":""SELECT COUNT(1) AS documentCount, [{\""totalWordCount\"": SUM(_FullTextWordCount(c.text)), \""hitCounts\"": [COUNTIF(FullTextContains(c.text, \""swim\"")), COUNTIF(FullTextContains(c.text, \""run\""))]}] AS fullTextStatistics\nFROM c"",""componentQueryInfos"":[{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}],""componentWithoutPayloadQueryInfos"":[],""projectionQueryInfo"":null,""componentWeights"":null,""skip"":null,""take"":10,""requiresGlobalStatistics"":true},""queryRanges"":[{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}]}",
                    @"{""globalStatisticsQuery"":""SELECT COUNT(1) AS documentCount, [{\""totalWordCount\"": SUM(_FullTextWordCount(c.text)), \""hitCounts\"": [COUNTIF(FullTextContains(c.text, \""swim\"")), COUNTIF(FullTextContains(c.text, \""run\""))]}] AS fullTextStatistics\nFROM c"",""componentQueryInfos"":[{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}],""componentWithoutPayloadQueryInfos"":[],""projectionQueryInfo"":null,""componentWeights"":null,""skip"":null,""take"":10,""requiresGlobalStatistics"":true}"),
                (@"{""hybridSearchQueryInfo"":{""globalStatisticsQuery"":""SELECT COUNT(1) AS documentCount, [{\""totalWordCount\"": SUM(_FullTextWordCount(c.text)), \""hitCounts\"": [COUNTIF(FullTextContains(c.text, \""swim\"")), COUNTIF(FullTextContains(c.text, \""run\""))]}] AS fullTextStatistics\nFROM c"",""componentQueryInfos"":[{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}), _VectorScore(c.image, [1, 2, 3])]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true},{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_VectorScore(c.image, [1, 2, 3])""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _VectorScore(c.image, [1, 2, 3])}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}), _VectorScore(c.image, [1, 2, 3])]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _VectorScore(c.image, [1, 2, 3]) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}],""componentWithoutPayloadQueryInfos"":[],""projectionQueryInfo"":null,""componentWeights"":null,""skip"":null,""take"":10,""requiresGlobalStatistics"":true},""queryRanges"":[{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}]}",
                    @"{""globalStatisticsQuery"":""SELECT COUNT(1) AS documentCount, [{\""totalWordCount\"": SUM(_FullTextWordCount(c.text)), \""hitCounts\"": [COUNTIF(FullTextContains(c.text, \""swim\"")), COUNTIF(FullTextContains(c.text, \""run\""))]}] AS fullTextStatistics\nFROM c"",""componentQueryInfos"":[{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}), _VectorScore(c.image, [1, 2, 3])]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true},{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_VectorScore(c.image, [1, 2, 3])""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _VectorScore(c.image, [1, 2, 3])}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}), _VectorScore(c.image, [1, 2, 3])]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _VectorScore(c.image, [1, 2, 3]) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}],""componentWithoutPayloadQueryInfos"":[],""projectionQueryInfo"":null,""componentWeights"":null,""skip"":null,""take"":10,""requiresGlobalStatistics"":true}"),
                (@"{""hybridSearchQueryInfo"":{""globalStatisticsQuery"":""SELECT COUNT(1) AS documentCount, [] AS fullTextStatistics\nFROM c"",""componentQueryInfos"":[{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_VectorScore(c.backup_image, [0.5, 0.2, 0.33])""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _VectorScore(c.backup_image, [0.5, 0.2, 0.33])}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [_VectorScore(c.backup_image, [0.5, 0.2, 0.33]), _VectorScore(c.image, [1, 2, 3])]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _VectorScore(c.backup_image, [0.5, 0.2, 0.33]) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true},{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_VectorScore(c.image, [1, 2, 3])""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _VectorScore(c.image, [1, 2, 3])}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [_VectorScore(c.backup_image, [0.5, 0.2, 0.33]), _VectorScore(c.image, [1, 2, 3])]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _VectorScore(c.image, [1, 2, 3]) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}],""componentWithoutPayloadQueryInfos"":[],""projectionQueryInfo"":null,""componentWeights"":null,""skip"":null,""take"":10,""requiresGlobalStatistics"":false},""queryRanges"":[{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}] }",
                    @"{""globalStatisticsQuery"":""SELECT COUNT(1) AS documentCount, [] AS fullTextStatistics\nFROM c"",""componentQueryInfos"":[{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_VectorScore(c.backup_image, [0.5, 0.2, 0.33])""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _VectorScore(c.backup_image, [0.5, 0.2, 0.33])}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [_VectorScore(c.backup_image, [0.5, 0.2, 0.33]), _VectorScore(c.image, [1, 2, 3])]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _VectorScore(c.backup_image, [0.5, 0.2, 0.33]) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true},{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_VectorScore(c.image, [1, 2, 3])""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _VectorScore(c.image, [1, 2, 3])}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [_VectorScore(c.backup_image, [0.5, 0.2, 0.33]), _VectorScore(c.image, [1, 2, 3])]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _VectorScore(c.image, [1, 2, 3]) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}],""componentWithoutPayloadQueryInfos"":[],""projectionQueryInfo"":null,""componentWeights"":null,""skip"":null,""take"":10,""requiresGlobalStatistics"":false}"),

                // Query with embedding parameters (GenerateEmbeddings feature) - validates QueryInfo.EmbeddingParameterMap round-trip
                (@"{""queryInfo"":{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""embeddingParameterMap"":{""@documentdb-hybridsearchquery-embedding-0"":""hello""},""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT VALUE @documentdb-hybridsearchquery-embedding-0"",""hasSelectValue"":true,""dCountInfo"":null,""hasNonStreamingOrderBy"":false},""queryRanges"":[{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}]}",
                    @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""embeddingParameterMap"":{""@documentdb-hybridsearchquery-embedding-0"":""hello""},""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT VALUE @documentdb-hybridsearchquery-embedding-0"",""hasSelectValue"":true,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),

                // Hybrid search query with embedding parameters on each component QueryInfo - validates EmbeddingParameterMap round-trip inside HybridSearchQueryInfo.componentQueryInfos
                (@"{""hybridSearchQueryInfo"":{""globalStatisticsQuery"":""SELECT COUNT(1) AS documentCount, [{\""totalWordCount\"": SUM(_FullTextWordCount(c.text)), \""hitCounts\"": [COUNTIF(FullTextContains(c.text, \""swim\"")), COUNTIF(FullTextContains(c.text, \""run\""))]}] AS fullTextStatistics\nFROM c"",""componentQueryInfos"":[{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""embeddingParameterMap"":{""@documentdb-hybridsearchquery-embedding-0"":""red car""},""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, c AS payload, [(_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) ?? -1), (_VectorScore(c.image, @documentdb-hybridsearchquery-embedding-0) ?? -1.79769e+308)] AS componentScores\nFROM c\nORDER BY _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false},{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""embeddingParameterMap"":{""@documentdb-hybridsearchquery-embedding-0"":""red car""},""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, c AS payload, [(_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) ?? -1), (_VectorScore(c.image, @documentdb-hybridsearchquery-embedding-0) ?? -1.79769e+308)] AS componentScores\nFROM c\nORDER BY _VectorScore(c.image, @documentdb-hybridsearchquery-embedding-0)"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}],""componentWithoutPayloadQueryInfos"":[],""projectionQueryInfo"":null,""componentWeights"":[],""skip"":null,""take"":10,""requiresGlobalStatistics"":true},""queryRanges"":[{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}]}",
                    @"{""globalStatisticsQuery"":""SELECT COUNT(1) AS documentCount, [{\""totalWordCount\"": SUM(_FullTextWordCount(c.text)), \""hitCounts\"": [COUNTIF(FullTextContains(c.text, \""swim\"")), COUNTIF(FullTextContains(c.text, \""run\""))]}] AS fullTextStatistics\nFROM c"",""componentQueryInfos"":[{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""embeddingParameterMap"":{""@documentdb-hybridsearchquery-embedding-0"":""red car""},""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, c AS payload, [(_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) ?? -1), (_VectorScore(c.image, @documentdb-hybridsearchquery-embedding-0) ?? -1.79769e+308)] AS componentScores\nFROM c\nORDER BY _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false},{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""embeddingParameterMap"":{""@documentdb-hybridsearchquery-embedding-0"":""red car""},""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, c AS payload, [(_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) ?? -1), (_VectorScore(c.image, @documentdb-hybridsearchquery-embedding-0) ?? -1.79769e+308)] AS componentScores\nFROM c\nORDER BY _VectorScore(c.image, @documentdb-hybridsearchquery-embedding-0)"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}],""componentWithoutPayloadQueryInfos"":[],""projectionQueryInfo"":null,""componentWeights"":[],""skip"":null,""take"":10,""requiresGlobalStatistics"":true}"),
            };

            IDictionary<string, object> configuration = new Dictionary<string, object>() { { "maxSqlQueryInputLength", 524288 } };

            QueryPartitionProvider queryPartitionProvider = new QueryPartitionProvider(configuration);

            foreach ((string queryPlan, string expected)in testCases)
            {
                PartitionedQueryExecutionInfoInternal queryInfoInternal =
                   JsonConvert.DeserializeObject<PartitionedQueryExecutionInfoInternal>(
                       queryPlan,
                       new JsonSerializerSettings
                       {
                           DateParseHandling = DateParseHandling.None,
                           MaxDepth = 64,
                       });

                PartitionedQueryExecutionInfo queryInfo = queryPartitionProvider.ConvertPartitionedQueryExecutionInfo(
                    queryInfoInternal,
                    partitionKeyDefinition);

                Assert.IsTrue(queryInfo.QueryInfo != null || queryInfo.HybridSearchQueryInfo != null);

                string actual;
                if (queryInfo.QueryInfo != null)
                {
                    actual = JsonConvert.SerializeObject(
                        queryInfo.QueryInfo,
                        new JsonSerializerSettings { Formatting = Formatting.None });
                }
                else
                {
                    actual = JsonConvert.SerializeObject(
                        queryInfo.HybridSearchQueryInfo,
                        new JsonSerializerSettings { Formatting = Formatting.None });
                }

                Assert.AreEqual(expected, actual);
            }
        }
    }
}
