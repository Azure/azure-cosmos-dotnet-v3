namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
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
                            useSystemPrefix: false,
                            geospatialType: Cosmos.GeospatialType.Geography);

            Assert.IsTrue(tryGetQueryPlan.Succeeded);
        }

        [TestMethod]
        public void TestHybridSearchQueryInfoDeserialization()
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
                (@"{""hybridSearchQueryInfo"":{""globalStatisticsQuery"":""SELECT COUNT(1) AS documentCount, [{\""totalWordCount\"": SUM(_FullTextWordCount(c.text)), \""hitCounts\"": [COUNTIF(FullTextContains(c.text, \""swim\"")), COUNTIF(FullTextContains(c.text, \""run\""))]}] AS fullTextStatistics\nFROM c"",""componentQueryInfos"":[{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}],""componentWithoutPayloadQueryInfos"":[],""projectionQueryInfo"":null,""skip"":null,""take"":10,""requiresGlobalStatistics"":true},""queryRanges"":[{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}]}",
                    @"{""globalStatisticsQuery"":""SELECT COUNT(1) AS documentCount, [{\""totalWordCount\"": SUM(_FullTextWordCount(c.text)), \""hitCounts\"": [COUNTIF(FullTextContains(c.text, \""swim\"")), COUNTIF(FullTextContains(c.text, \""run\""))]}] AS fullTextStatistics\nFROM c"",""componentQueryInfos"":[{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}],""componentWithoutPayloadQueryInfos"":[],""projectionQueryInfo"":null,""skip"":null,""take"":10,""requiresGlobalStatistics"":true}"),
                (@"{""hybridSearchQueryInfo"":{""globalStatisticsQuery"":""SELECT COUNT(1) AS documentCount, [{\""totalWordCount\"": SUM(_FullTextWordCount(c.text)), \""hitCounts\"": [COUNTIF(FullTextContains(c.text, \""swim\"")), COUNTIF(FullTextContains(c.text, \""run\""))]}] AS fullTextStatistics\nFROM c"",""componentQueryInfos"":[{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}), _VectorScore(c.image, [1, 2, 3])]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true},{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_VectorScore(c.image, [1, 2, 3])""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _VectorScore(c.image, [1, 2, 3])}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}), _VectorScore(c.image, [1, 2, 3])]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _VectorScore(c.image, [1, 2, 3]) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}],""componentWithoutPayloadQueryInfos"":[],""projectionQueryInfo"":null,""skip"":null,""take"":10,""requiresGlobalStatistics"":true},""queryRanges"":[{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}]}",
                    @"{""globalStatisticsQuery"":""SELECT COUNT(1) AS documentCount, [{\""totalWordCount\"": SUM(_FullTextWordCount(c.text)), \""hitCounts\"": [COUNTIF(FullTextContains(c.text, \""swim\"")), COUNTIF(FullTextContains(c.text, \""run\""))]}] AS fullTextStatistics\nFROM c"",""componentQueryInfos"":[{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}), _VectorScore(c.image, [1, 2, 3])]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true},{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_VectorScore(c.image, [1, 2, 3])""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _VectorScore(c.image, [1, 2, 3])}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}), _VectorScore(c.image, [1, 2, 3])]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _VectorScore(c.image, [1, 2, 3]) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}],""componentWithoutPayloadQueryInfos"":[],""projectionQueryInfo"":null,""skip"":null,""take"":10,""requiresGlobalStatistics"":true}"),
                (@"{""hybridSearchQueryInfo"":{""globalStatisticsQuery"":""SELECT COUNT(1) AS documentCount, [] AS fullTextStatistics\nFROM c"",""componentQueryInfos"":[{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_VectorScore(c.backup_image, [0.5, 0.2, 0.33])""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _VectorScore(c.backup_image, [0.5, 0.2, 0.33])}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [_VectorScore(c.backup_image, [0.5, 0.2, 0.33]), _VectorScore(c.image, [1, 2, 3])]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _VectorScore(c.backup_image, [0.5, 0.2, 0.33]) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true},{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_VectorScore(c.image, [1, 2, 3])""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _VectorScore(c.image, [1, 2, 3])}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [_VectorScore(c.backup_image, [0.5, 0.2, 0.33]), _VectorScore(c.image, [1, 2, 3])]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _VectorScore(c.image, [1, 2, 3]) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}],""componentWithoutPayloadQueryInfos"":[],""projectionQueryInfo"":null,""skip"":null,""take"":10,""requiresGlobalStatistics"":false},""queryRanges"":[{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}] }",
                    @"{""globalStatisticsQuery"":""SELECT COUNT(1) AS documentCount, [] AS fullTextStatistics\nFROM c"",""componentQueryInfos"":[{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_VectorScore(c.backup_image, [0.5, 0.2, 0.33])""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _VectorScore(c.backup_image, [0.5, 0.2, 0.33])}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [_VectorScore(c.backup_image, [0.5, 0.2, 0.33]), _VectorScore(c.image, [1, 2, 3])]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _VectorScore(c.backup_image, [0.5, 0.2, 0.33]) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true},{""distinctType"":""None"",""top"":120,""offset"":null,""limit"":null,""orderBy"":[""Descending""],""orderByExpressions"":[""_VectorScore(c.image, [1, 2, 3])""],""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, [{\""item\"": _VectorScore(c.image, [1, 2, 3])}] AS orderByItems, {\""payload\"": c, \""componentScores\"": [_VectorScore(c.backup_image, [0.5, 0.2, 0.33]), _VectorScore(c.image, [1, 2, 3])]} AS payload\nFROM c\nWHERE ({documentdb-formattableorderbyquery-filter})\nORDER BY _VectorScore(c.image, [1, 2, 3]) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":true}],""componentWithoutPayloadQueryInfos"":[],""projectionQueryInfo"":null,""skip"":null,""take"":10,""requiresGlobalStatistics"":false}"),
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

                Assert.IsNotNull(queryInfo.HybridSearchQueryInfo);
                string actual = JsonConvert.SerializeObject(
                    queryInfo.HybridSearchQueryInfo,
                    new JsonSerializerSettings { Formatting = Formatting.None });
                Assert.AreEqual(expected, actual);
            }
        }
    }
}
