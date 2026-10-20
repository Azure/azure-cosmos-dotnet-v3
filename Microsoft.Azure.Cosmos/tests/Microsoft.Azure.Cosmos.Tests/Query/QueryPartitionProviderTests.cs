namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml.Linq;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using XmlWriter = System.Xml.XmlWriter;

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

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void TestFullTextSearchTermsFromServiceInterop(bool hybridSearchSkipOrderByRewrite)
        {
            Dictionary<string, object> configuration = QueryPartitionProviderTestInstance.DefaultQueryEngineConfiguration
                .ToDictionary(entry => entry.Key, entry => entry.Value);
            configuration["queryEnableFullTextPreviewFeatures"] = true;

            using QueryPartitionProvider provider = new QueryPartitionProvider(configuration);

            (SqlQuerySpec Query, string Expected)[] testCases =
            {
                (new SqlQuerySpec(@"SELECT TOP 10 * FROM c ORDER BY RANK FullTextScore(c.text, 'swim', 'run')"),
                    @"[{""path"":[""text""],""terms"":[{""term"":""swim""},{""term"":""run""}]}]"),
                (new SqlQuerySpec(@"SELECT TOP 10 * FROM c ORDER BY RANK FullTextScore(c.author.bio, 'energy')"),
                    @"[{""path"":[""author"",""bio""],""terms"":[{""term"":""energy""}]}]"),
                (new SqlQuerySpec(@"SELECT TOP 10 * FROM c ORDER BY RANK FullTextScore(c[""author""][""bio""], 'energy')"),
                    @"[{""path"":[""author"",""bio""],""terms"":[{""term"":""energy""}]}]"),
                (new SqlQuerySpec(@"SELECT TOP 10 * FROM c ORDER BY RANK FullTextScore(c.tags[0], 'energy')"),
                    @"[{""path"":[""tags"",""[0]""],""terms"":[{""term"":""energy""}]}]"),
                (new SqlQuerySpec(@"SELECT TOP 10 * FROM c ORDER BY RANK FullTextScore(c[""a/b""], 'energy')"),
                    @"[{""path"":[""a/b""],""terms"":[{""term"":""energy""}]}]"),
                (new SqlQuerySpec(@"SELECT TOP 10 * FROM c ORDER BY RANK RRF(FullTextScore(c.text, 'swim'), FullTextScore(c.text, 'run'))"),
                    @"[{""path"":[""text""],""terms"":[{""term"":""swim""}]},{""path"":[""text""],""terms"":[{""term"":""run""}]}]"),
                (new SqlQuerySpec(@"SELECT TOP 10 * FROM c ORDER BY RANK RRF(FullTextScore(c.title, 'energy'), FullTextScore(c.text, 'swim', 'run'), VectorDistance(c.image, [1, 2, 3]))"),
                    @"[{""path"":[""title""],""terms"":[{""term"":""energy""}]},{""path"":[""text""],""terms"":[{""term"":""swim""},{""term"":""run""}]}]"),
                (new SqlQuerySpec(@"SELECT TOP 10 * FROM c ORDER BY RANK FullTextScore(c.text, {'term': 'swim', 'distance': 2}, 'run')"),
                    @"[{""path"":[""text""],""terms"":[{""term"":""swim"",""distance"":2},{""term"":""run""}]}]"),
                (new SqlQuerySpec(@"SELECT TOP 10 * FROM c ORDER BY RANK FullTextScore(c.text, 'run', 'swim', 'run', 'United States')"),
                    @"[{""path"":[""text""],""terms"":[{""term"":""run""},{""term"":""swim""},{""term"":""run""},{""term"":""United States""}]}]"),
                (new SqlQuerySpec(
                    @"SELECT TOP 10 * FROM c ORDER BY RANK FullTextScore(c[@path], @needle, @plain)",
                    new SqlParameterCollection
                    {
                        new SqlParameter("@path", "text"),
                        new SqlParameter("@needle", new { term = "swim", distance = 2 }),
                        new SqlParameter("@plain", "run"),
                    }),
                    @"[{""path"":[""text""],""terms"":[{""term"":""swim"",""distance"":2},{""term"":""run""}]}]"),
                (new SqlQuerySpec(@"SELECT TOP 10 FullTextScore(c.text, 'swim') AS score FROM c WHERE FullTextScore(c.text, 'swim') > 0 ORDER BY RANK FullTextScore(c.text, 'swim')"),
                    @"[{""path"":[""text""],""terms"":[{""term"":""swim""}]}]"),
                (new SqlQuerySpec(@"SELECT TOP 10 FullTextScore(c.title, 'energy') AS score FROM c WHERE FullTextScore(c.text, 'swim') > 0 ORDER BY RANK FullTextScore(c.title, 'energy')"),
                    @"[{""path"":[""title""],""terms"":[{""term"":""energy""}]},{""path"":[""text""],""terms"":[{""term"":""swim""}]}]"),
                (new SqlQuerySpec(@"SELECT TOP 10 * FROM c ORDER BY RANK RRF(VectorDistance(c.image, [1, 2, 3]), VectorDistance(c.backup, [3, 2, 1]))"),
                    @"[]"),
            };

            foreach ((SqlQuerySpec query, string expected) in testCases)
            {
                TryCatch<PartitionedQueryExecutionInfoInternal> result = provider.TryGetPartitionedQueryExecutionInfoInternal(
                    querySpecJsonString: JsonConvert.SerializeObject(query),
                    partitionKeyDefinition: PartitionKeyDefinition,
                    vectorEmbeddingPolicy: null,
                    requireFormattableOrderByQuery: true,
                    isContinuationExpected: false,
                    allowNonValueAggregateQuery: true,
                    hasLogicalPartitionKey: false,
                    allowDCount: true,
                    useSystemPrefix: false,
                    hybridSearchSkipOrderByRewrite: hybridSearchSkipOrderByRewrite,
                    geospatialType: Cosmos.GeospatialType.Geography);

                Assert.IsTrue(result.Succeeded, $"{query.QueryText}: {(result.Failed ? result.Exception.ToString() : string.Empty)}");
                Assert.IsNotNull(result.Result.HybridSearchQueryInfo, query.QueryText);
                JToken actual = result.Result.HybridSearchQueryInfo.FullTextSearchTerms;
                Assert.IsNotNull(actual, $"ServiceInterop did not emit fullTextSearchTerms for {query.QueryText}.");
                JArray expectedMetadata = JArray.Parse(expected);
                Assert.IsTrue(
                    JToken.DeepEquals(expectedMetadata, actual),
                    $"{query.QueryText}{Environment.NewLine}Expected: {expected}{Environment.NewLine}Actual: {actual}");

                PartitionedQueryExecutionInfo converted = provider.ConvertPartitionedQueryExecutionInfo(result.Result, PartitionKeyDefinition);
                Assert.IsTrue(PartitionedQueryExecutionInfo.TryParse(converted.ToString(), out PartitionedQueryExecutionInfo roundTripped));
                Assert.IsTrue(JToken.DeepEquals(actual, roundTripped.HybridSearchQueryInfo.FullTextSearchTerms));

                StringBuilder xml = new StringBuilder();
                using (XmlWriter writer = XmlWriter.Create(xml))
                {
                    new QueryPlanBaselineTestPositiveOutput(result.Result).SerializeAsXml(writer);
                }

                XElement metadataXml = XElement.Parse(xml.ToString())
                    .Element(nameof(HybridSearchQueryInfo))
                    .Element(nameof(HybridSearchQueryInfo.FullTextSearchTerms));
                Assert.IsNotNull(metadataXml);
                XElement[] entries = metadataXml.Elements("Entry").ToArray();
                Assert.AreEqual(expectedMetadata.Count, entries.Length);
                for (int index = 0; index < entries.Length; index++)
                {
                    CollectionAssert.AreEqual(
                        expectedMetadata[index]["path"].Values<string>().ToArray(),
                        entries[index].Element("Path").Elements("Segment").Select(segment => segment.Value).ToArray());
                    JToken[] expectedTerms = expectedMetadata[index]["terms"].ToArray();
                    XElement[] terms = entries[index].Element("Terms").Elements("Term").ToArray();
                    Assert.AreEqual(expectedTerms.Length, terms.Length);
                    for (int termIndex = 0; termIndex < terms.Length; termIndex++)
                    {
                        Assert.AreEqual(expectedTerms[termIndex]["term"].Value<string>(), terms[termIndex].Element("Text").Value);
                        Assert.AreEqual(expectedTerms[termIndex]["distance"]?.ToString(Formatting.None), terms[termIndex].Element("Distance")?.Value);
                    }
                }
            }
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("null")]
        [DataRow("[]")]
        [DataRow("42")]
        [DataRow(@"""unsupported""")]
        [DataRow(@"{""future"":true}")]
        [DataRow(@"[{""path"":""not-an-array"",""terms"":null}]")]
        [DataRow(@"[{""path"":[""text""],""terms"":[{""term"":""swim"",""distance"":2}],""future"":true}]")]
        [DataRow(@"[{""path"":[""2026-10-20T12:00:00Z""],""terms"":[{""term"":""2026-10-20T12:00:00+01:00""}]}]")]
        public void TestFullTextSearchTermsDeserialization(string metadata)
        {
            const string statisticsQuery = "SELECT COUNT(1) AS documentCount FROM c";
            string property = metadata == null ? string.Empty : $",\"fullTextSearchTerms\":{metadata}";
            string queryPlan = $"{{\"hybridSearchQueryInfo\":{{\"globalStatisticsQuery\":\"{statisticsQuery}\",\"requiresGlobalStatistics\":true{property}}}}}";

            Assert.IsTrue(PartitionedQueryExecutionInfo.TryParse(queryPlan, out PartitionedQueryExecutionInfo parsed));
            using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(queryPlan));
            PartitionedQueryExecutionInfo gatewayPlan = new CosmosSerializerCore().FromStream<PartitionedQueryExecutionInfo>(stream);
            PartitionedQueryExecutionInfoInternal internalPlan = JsonConvert.DeserializeObject<PartitionedQueryExecutionInfoInternal>(queryPlan);

            foreach (HybridSearchQueryInfo hybridPlan in new[] { parsed.HybridSearchQueryInfo, gatewayPlan.HybridSearchQueryInfo, internalPlan.HybridSearchQueryInfo })
            {
                Assert.AreEqual(statisticsQuery, hybridPlan.GlobalStatisticsQuery);
                Assert.IsTrue(hybridPlan.RequiresGlobalStatistics);
                if (metadata == null || metadata == "null")
                {
                    Assert.IsNull(hybridPlan.FullTextSearchTerms);
                    Assert.IsFalse(JsonConvert.SerializeObject(hybridPlan).Contains("fullTextSearchTerms"));
                }
                else
                {
                    Assert.AreEqual(metadata, hybridPlan.FullTextSearchTerms.ToString(Formatting.None));
                    string serialized = JsonConvert.SerializeObject(hybridPlan);
                    HybridSearchQueryInfo roundTripped = JsonConvert.DeserializeObject<HybridSearchQueryInfo>(serialized);
                    Assert.IsTrue(JToken.DeepEquals(hybridPlan.FullTextSearchTerms, roundTripped.FullTextSearchTerms));
                }
            }
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
                (@"{""queryInfo"":{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""embeddingParameterMap"":{""@documentdb-hybridsearchquery-embedding-0"":{""path"":[""nested"",""vector""],""needle"":""hello""}},""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT VALUE @documentdb-hybridsearchquery-embedding-0"",""hasSelectValue"":true,""dCountInfo"":null,""hasNonStreamingOrderBy"":false},""queryRanges"":[{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}]}",
                    @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""embeddingParameterMap"":{""@documentdb-hybridsearchquery-embedding-0"":{""path"":[""nested"",""vector""],""needle"":""hello""}},""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT VALUE @documentdb-hybridsearchquery-embedding-0"",""hasSelectValue"":true,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),

                // Hybrid search query with embedding parameters on each component QueryInfo - validates EmbeddingParameterMap round-trip inside HybridSearchQueryInfo.componentQueryInfos
                (@"{""hybridSearchQueryInfo"":{""globalStatisticsQuery"":""SELECT COUNT(1) AS documentCount, [{\""totalWordCount\"": SUM(_FullTextWordCount(c.text)), \""hitCounts\"": [COUNTIF(FullTextContains(c.text, \""swim\"")), COUNTIF(FullTextContains(c.text, \""run\""))]}] AS fullTextStatistics\nFROM c"",""componentQueryInfos"":[{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""embeddingParameterMap"":{""@documentdb-hybridsearchquery-embedding-0"":{""path"":[""image""],""needle"":""red car""}},""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, c AS payload, [(_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) ?? -1), (_VectorScore(c.image, @documentdb-hybridsearchquery-embedding-0) ?? -1.79769e+308)] AS componentScores\nFROM c\nORDER BY _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false},{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""embeddingParameterMap"":{""@documentdb-hybridsearchquery-embedding-0"":{""path"":[""image""],""needle"":""red car""}},""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, c AS payload, [(_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) ?? -1), (_VectorScore(c.image, @documentdb-hybridsearchquery-embedding-0) ?? -1.79769e+308)] AS componentScores\nFROM c\nORDER BY _VectorScore(c.image, @documentdb-hybridsearchquery-embedding-0)"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}],""componentWithoutPayloadQueryInfos"":[],""projectionQueryInfo"":null,""componentWeights"":[],""skip"":null,""take"":10,""requiresGlobalStatistics"":true},""queryRanges"":[{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}]}",
                    @"{""globalStatisticsQuery"":""SELECT COUNT(1) AS documentCount, [{\""totalWordCount\"": SUM(_FullTextWordCount(c.text)), \""hitCounts\"": [COUNTIF(FullTextContains(c.text, \""swim\"")), COUNTIF(FullTextContains(c.text, \""run\""))]}] AS fullTextStatistics\nFROM c"",""componentQueryInfos"":[{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""embeddingParameterMap"":{""@documentdb-hybridsearchquery-embedding-0"":{""path"":[""image""],""needle"":""red car""}},""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, c AS payload, [(_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) ?? -1), (_VectorScore(c.image, @documentdb-hybridsearchquery-embedding-0) ?? -1.79769e+308)] AS componentScores\nFROM c\nORDER BY _FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) DESC"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false},{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":[],""orderByExpressions"":[],""embeddingParameterMap"":{""@documentdb-hybridsearchquery-embedding-0"":{""path"":[""image""],""needle"":""red car""}},""groupByExpressions"":[],""groupByAliases"":[],""aggregates"":[],""groupByAliasToAggregateType"":{},""rewrittenQuery"":""SELECT TOP 120 c._rid, c AS payload, [(_FullTextScore(c.text, [\""swim\"", \""run\""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) ?? -1), (_VectorScore(c.image, @documentdb-hybridsearchquery-embedding-0) ?? -1.79769e+308)] AS componentScores\nFROM c\nORDER BY _VectorScore(c.image, @documentdb-hybridsearchquery-embedding-0)"",""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}],""componentWithoutPayloadQueryInfos"":[],""projectionQueryInfo"":null,""componentWeights"":[],""skip"":null,""take"":10,""requiresGlobalStatistics"":true}"),
                (@"{""queryInfo"":{""embeddingParameterMap"":{""@documentdb-hybridsearchquery-embedding-0"":{""path"":[""nested"",""vector""],""needle"":""red car""},""@documentdb-hybridsearchquery-embedding-1"":{""path"":[""other/vector"",""quoted\""path"",""back\\slash""],""needle"":""red car""}}},""queryRanges"":[{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}]}",
                    @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":null,""orderByExpressions"":null,""embeddingParameterMap"":{""@documentdb-hybridsearchquery-embedding-0"":{""path"":[""nested"",""vector""],""needle"":""red car""},""@documentdb-hybridsearchquery-embedding-1"":{""path"":[""other/vector"",""quoted\""path"",""back\\slash""],""needle"":""red car""}},""groupByExpressions"":null,""groupByAliases"":null,""aggregates"":null,""groupByAliasToAggregateType"":null,""rewrittenQuery"":null,""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),
                (@"{""hybridSearchQueryInfo"":{""componentQueryInfos"":[{""embeddingParameterMap"":{""@documentdb-hybridsearchquery-embedding-0"":{""path"":[""nested"",""vector""],""needle"":""red car""},""@documentdb-hybridsearchquery-embedding-1"":{""path"":[""other/vector"",""quoted\""path"",""back\\slash""],""needle"":""red car""}}}],""componentWithoutPayloadQueryInfos"":[{""embeddingParameterMap"":{""@documentdb-hybridsearchquery-embedding-0"":{""path"":[""nested"",""vector""],""needle"":""red car""},""@documentdb-hybridsearchquery-embedding-1"":{""path"":[""other/vector"",""quoted\""path"",""back\\slash""],""needle"":""red car""}}}],""projectionQueryInfo"":{""embeddingParameterMap"":{""@documentdb-hybridsearchquery-embedding-0"":{""path"":[""nested"",""vector""],""needle"":""red car""},""@documentdb-hybridsearchquery-embedding-1"":{""path"":[""other/vector"",""quoted\""path"",""back\\slash""],""needle"":""red car""}}}},""queryRanges"":[{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}]}",
                    @"{""globalStatisticsQuery"":null,""componentQueryInfos"":[{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":null,""orderByExpressions"":null,""embeddingParameterMap"":{""@documentdb-hybridsearchquery-embedding-0"":{""path"":[""nested"",""vector""],""needle"":""red car""},""@documentdb-hybridsearchquery-embedding-1"":{""path"":[""other/vector"",""quoted\""path"",""back\\slash""],""needle"":""red car""}},""groupByExpressions"":null,""groupByAliases"":null,""aggregates"":null,""groupByAliasToAggregateType"":null,""rewrittenQuery"":null,""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}],""componentWithoutPayloadQueryInfos"":[{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":null,""orderByExpressions"":null,""embeddingParameterMap"":{""@documentdb-hybridsearchquery-embedding-0"":{""path"":[""nested"",""vector""],""needle"":""red car""},""@documentdb-hybridsearchquery-embedding-1"":{""path"":[""other/vector"",""quoted\""path"",""back\\slash""],""needle"":""red car""}},""groupByExpressions"":null,""groupByAliases"":null,""aggregates"":null,""groupByAliasToAggregateType"":null,""rewrittenQuery"":null,""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}],""projectionQueryInfo"":{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":null,""orderByExpressions"":null,""embeddingParameterMap"":{""@documentdb-hybridsearchquery-embedding-0"":{""path"":[""nested"",""vector""],""needle"":""red car""},""@documentdb-hybridsearchquery-embedding-1"":{""path"":[""other/vector"",""quoted\""path"",""back\\slash""],""needle"":""red car""}},""groupByExpressions"":null,""groupByAliases"":null,""aggregates"":null,""groupByAliasToAggregateType"":null,""rewrittenQuery"":null,""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false},""componentWeights"":null,""skip"":null,""take"":null,""requiresGlobalStatistics"":false}"),

                (@"{""queryInfo"":{},""queryRanges"":[{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}]}",
                    @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":null,""orderByExpressions"":null,""groupByExpressions"":null,""groupByAliases"":null,""aggregates"":null,""groupByAliasToAggregateType"":null,""rewrittenQuery"":null,""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),
                (@"{""queryInfo"":{""embeddingParameterMap"":null},""queryRanges"":[{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}]}",
                    @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":null,""orderByExpressions"":null,""groupByExpressions"":null,""groupByAliases"":null,""aggregates"":null,""groupByAliasToAggregateType"":null,""rewrittenQuery"":null,""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),
                (@"{""queryInfo"":{""embeddingParameterMap"":{}},""queryRanges"":[{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}]}",
                    @"{""distinctType"":""None"",""top"":null,""offset"":null,""limit"":null,""orderBy"":null,""orderByExpressions"":null,""embeddingParameterMap"":{},""groupByExpressions"":null,""groupByAliases"":null,""aggregates"":null,""groupByAliasToAggregateType"":null,""rewrittenQuery"":null,""hasSelectValue"":false,""dCountInfo"":null,""hasNonStreamingOrderBy"":false}"),

                (@"{""hybridSearchQueryInfo"":{},""queryRanges"":[{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}]}",
                    @"{""globalStatisticsQuery"":null,""componentQueryInfos"":null,""componentWithoutPayloadQueryInfos"":null,""projectionQueryInfo"":null,""componentWeights"":null,""skip"":null,""take"":null,""requiresGlobalStatistics"":false}"),
                (@"{""hybridSearchQueryInfo"":{""scoreCombinationKind"":null},""queryRanges"":[{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}]}",
                    @"{""globalStatisticsQuery"":null,""componentQueryInfos"":null,""componentWithoutPayloadQueryInfos"":null,""projectionQueryInfo"":null,""componentWeights"":null,""skip"":null,""take"":null,""requiresGlobalStatistics"":false}"),
                (@"{""hybridSearchQueryInfo"":{""scoreCombinationKind"":""Rrf""},""queryRanges"":[{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}]}",
                    @"{""globalStatisticsQuery"":null,""componentQueryInfos"":null,""componentWithoutPayloadQueryInfos"":null,""projectionQueryInfo"":null,""componentWeights"":null,""skip"":null,""take"":null,""requiresGlobalStatistics"":false,""scoreCombinationKind"":""Rrf""}"),
                (@"{""hybridSearchQueryInfo"":{""scoreCombinationKind"":""CombinedScore""},""queryRanges"":[{""min"":[],""max"":""Infinity"",""isMinInclusive"":true,""isMaxInclusive"":false}]}",
                    @"{""globalStatisticsQuery"":null,""componentQueryInfos"":null,""componentWithoutPayloadQueryInfos"":null,""projectionQueryInfo"":null,""componentWeights"":null,""skip"":null,""take"":null,""requiresGlobalStatistics"":false,""scoreCombinationKind"":""CombinedScore""}"),

                // A null expected payload denotes a schema that must be rejected.
                (@"{""queryInfo"":{""embeddingParameterMap"":{""@embedding"":""red car""}}}", null),
                (@"{""queryInfo"":{""embeddingParameterMap"":{""@embedding"":{""path"":""/vector"",""needle"":""red car""}}}}", null),
                (@"{""hybridSearchQueryInfo"":{""scoreCombinationKind"":""Unknown""}}", null),
            };

            IDictionary<string, object> configuration = new Dictionary<string, object>() { { "maxSqlQueryInputLength", 524288 } };

            QueryPartitionProvider queryPartitionProvider = new QueryPartitionProvider(configuration);

            foreach ((string queryPlan, string expected)in testCases)
            {
                if (expected == null)
                {
                    Assert.ThrowsException<JsonSerializationException>(
                        () => JsonConvert.DeserializeObject<PartitionedQueryExecutionInfoInternal>(queryPlan));
                    Assert.IsFalse(PartitionedQueryExecutionInfo.TryParse(queryPlan, out PartitionedQueryExecutionInfo result));
                    Assert.IsNull(result);
                    continue;
                }

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

                string serializedQueryPlan = queryInfo.ToString();
                Assert.IsTrue(PartitionedQueryExecutionInfo.TryParse(serializedQueryPlan, out PartitionedQueryExecutionInfo parsedQueryPlan));
                Assert.AreEqual(serializedQueryPlan, parsedQueryPlan.ToString());
            }
        }
    }
}
