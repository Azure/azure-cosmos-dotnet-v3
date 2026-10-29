//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Distinct;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Tests native query planning and routing without an account or emulator.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class QueryPlanInteropTests
    {
        private static readonly string[] Key = { "/key" };
        private static readonly SortOrder[] Asc = { SortOrder.Ascending };

        // Routing: local library versus gateway

        [DataTestMethod]
        [DataRow("LINUX", Architecture.X64, true, true)]
        [DataRow("LINUX", Architecture.Arm64, true, true)]
        [DataRow("LINUX", Architecture.X64, false, false)]
        [DataRow("LINUX", Architecture.Arm64, false, false)]
        [DataRow("LINUX", Architecture.X86, true, false)]
        [DataRow("LINUX", Architecture.Arm, true, false)]
        [DataRow("WINDOWS", Architecture.X64, true, false)]
        [DataRow("OSX", Architecture.Arm64, true, false)]
        public void LinuxPlanningRequiresSupportedPlatformAndOptIn(
            string platform, Architecture architecture, bool enabled, bool expected)
        {
            Assert.AreEqual(expected, QueryPlanInterop.SupportsLinuxPlanning(OSPlatform.Create(platform), architecture, enabled));
        }

        [TestMethod]
        public void LinuxRoutingFallsBackWithoutLoadingWhenDisabledOrUnsupported()
        {
            Lazy<bool> mustNotLoad = new Lazy<bool>(() => throw new AssertFailedException("Native library must not be loaded."));
            Assert.IsTrue(QueryPlanRetriever.BypassQueryParsingOnLinux(false, Architecture.X64, mustNotLoad));
            Assert.IsTrue(QueryPlanRetriever.BypassQueryParsingOnLinux(true, Architecture.X86, mustNotLoad));
            Assert.IsTrue(QueryPlanRetriever.BypassQueryParsingOnLinux(true, Architecture.Arm, mustNotLoad));
            Assert.IsTrue(QueryPlanRetriever.BypassQueryParsingOnLinux(true, Architecture.X64, new Lazy<bool>(() => false)));
            Assert.IsFalse(QueryPlanRetriever.BypassQueryParsingOnLinux(true, Architecture.X64, new Lazy<bool>(() => true)));
            Assert.IsFalse(QueryPlanRetriever.BypassQueryParsingOnLinux(true, Architecture.Arm64, new Lazy<bool>(() => true)));
        }

        [TestMethod]
        public void QueryPlanRoutingPreservesDefaultsAndForceBypass()
        {
            string originalOptIn = Environment.GetEnvironmentVariable(ConfigurationManager.UseQueryInterop);
            string originalBypass = Environment.GetEnvironmentVariable(ConfigurationManager.BypassQueryParsing);
            try
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.UseQueryInterop, null);
                Environment.SetEnvironmentVariable(ConfigurationManager.BypassQueryParsing, null);
                bool defaultBypass = QueryPlanRetriever.BypassQueryParsing();
                Assert.AreEqual(CustomTypeExtensions.ByPassQueryParsing(), defaultBypass);

                Environment.SetEnvironmentVariable(ConfigurationManager.UseQueryInterop, "false");
                Assert.AreEqual(defaultBypass, QueryPlanRetriever.BypassQueryParsing());

                Environment.SetEnvironmentVariable(ConfigurationManager.UseQueryInterop, "TrUe");
                Environment.SetEnvironmentVariable(ConfigurationManager.BypassQueryParsing, "true");
                Assert.IsTrue(QueryPlanRetriever.BypassQueryParsing());

                Environment.SetEnvironmentVariable(ConfigurationManager.BypassQueryParsing, null);
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Assert.AreEqual(defaultBypass, QueryPlanRetriever.BypassQueryParsing(), "Linux opt-in must not change other platforms.");
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.UseQueryInterop, originalOptIn);
                Environment.SetEnvironmentVariable(ConfigurationManager.BypassQueryParsing, originalBypass);
            }
        }

        [TestMethod]
        public void OptedInLinuxProcessUsesNativePlanning()
        {
            // Check inherited flags without changing the process environment.
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                || !ConfigurationManager.GetEnvironmentVariable(ConfigurationManager.UseQueryInterop, defaultValue: false))
            {
                Assert.Inconclusive($"Requires a Linux process started with {ConfigurationManager.UseQueryInterop}=true.");
            }

            Assert.IsFalse(ConfigurationManager.ForceBypassQueryParsing(), $"{ConfigurationManager.BypassQueryParsing} must not force the gateway.");
            Assert.IsTrue(QueryPlanInterop.LinuxLibraryAvailable.Value, "The packaged QueryPlanInterop library must load.");
            Assert.IsFalse(QueryPlanRetriever.BypassQueryParsing(), "An opted-in Linux process must use QueryPlanInterop instead of the gateway.");
        }

        [TestMethod]
        public void WideStringsUseUtf32ScalarsAndNullTermination()
        {
            CollectionAssert.AreEqual(
                new byte[] { 0x41, 0, 0, 0, 0xe9, 0, 0, 0, 0x42, 0xf6, 1, 0, 0, 0, 0, 0 },
                QueryPlanInterop.EncodeWideString("A\u00e9\U0001f642"));
            CollectionAssert.AreEqual(new byte[4], QueryPlanInterop.EncodeWideString(string.Empty));
            Assert.IsNull(QueryPlanInterop.EncodeWideString(null));
            Assert.ThrowsException<EncoderFallbackException>(() => QueryPlanInterop.EncodeWideString("\ud800"));
        }

        // Provider lifecycle and text encoding through the native library

        [TestMethod]
        public void ProviderHandlesUnicodeConfigUpdateLargeOutputAndVectorPolicy()
        {
            EnsureNativePlatform();

            string originalOptIn = Environment.GetEnvironmentVariable(ConfigurationManager.UseQueryInterop);
            string originalBypass = Environment.GetEnvironmentVariable(ConfigurationManager.BypassQueryParsing);
            try
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.UseQueryInterop, "true");
                Environment.SetEnvironmentVariable(ConfigurationManager.BypassQueryParsing, "false");
                Assert.IsFalse(QueryPlanRetriever.BypassQueryParsing(), "A supported, opted-in process must use native query planning.");

                string property = "caf\u00e9\U0001f642";
                string value = "\u6771\u4eac\U0001f642";
                PartitionKeyDefinition partitionKey = new PartitionKeyDefinition
                {
                    Paths = new Collection<string> { "/" + property },
                    Kind = PartitionKind.Hash,
                    Version = PartitionKeyDefinitionVersion.V2,
                };
                string projection = string.Join(", ", Enumerable.Range(0, 150).Select(i => $"c.field{i}"));
                string query = JsonConvert.SerializeObject(new SqlQuerySpec(
                    $"SELECT {projection} FROM c WHERE c[\"{property}\"] = \"{value}\" ORDER BY c.field0"));
                using QueryPartitionProvider provider = new QueryPartitionProvider(
                    new Dictionary<string, object> { { "maxSqlQueryInputLength", 5 } });

                TryCatch<PartitionedQueryExecutionInfoInternal> tooLong = provider.TryGetPartitionedQueryExecutionInfoInternal(
                    query, partitionKey, null, true, false, true, false, true, false, false, Cosmos.GeospatialType.Geography);
                Assert.IsTrue(tooLong.Failed);
                StringAssert.Contains(tooLong.Exception.ToString(), "maximum limit of 5 characters");

                provider.Update(new Dictionary<string, object> { { "maxSqlQueryInputLength", 524288 } });
                TryCatch<PartitionedQueryExecutionInfoInternal> plan = provider.TryGetPartitionedQueryExecutionInfoInternal(
                    query, partitionKey, null, true, false, true, false, true, false, false, Cosmos.GeospatialType.Geography);
                plan.ThrowIfFailed();
                Assert.IsTrue(plan.Result.QueryInfo.HasOrderBy);
                Assert.IsTrue(plan.Result.QueryInfo.RewrittenQuery.Length > 1024, "Exercise native output buffer resizing.");
                Range<PartitionKeyInternal> range = plan.Result.QueryRanges.Single();
                Assert.AreEqual(PartitionKeyInternal.FromJsonString(JsonConvert.SerializeObject(new[] { value })), range.Min);
                Assert.AreEqual(range.Min, range.Max);

                Cosmos.VectorEmbeddingPolicy vectorPolicy = new Cosmos.VectorEmbeddingPolicy(new Collection<Cosmos.Embedding>
                {
                    new Cosmos.Embedding
                    {
                        Path = "/" + property,
                        DataType = Cosmos.VectorDataType.Float32,
                        Dimensions = 3,
                        DistanceFunction = Cosmos.DistanceFunction.Cosine,
                    },
                });
                string vectorQuery = JsonConvert.SerializeObject(new SqlQuerySpec(
                    $"SELECT TOP 10 * FROM c ORDER BY VectorDistance(c[\"{property}\"], [1, 2, 3], true)"));
                TryCatch<PartitionedQueryExecutionInfoInternal> vectorPlan = provider.TryGetPartitionedQueryExecutionInfoInternal(
                    vectorQuery, partitionKey, vectorPolicy, true, false, true, false, true, false, false, Cosmos.GeospatialType.Geography);
                vectorPlan.ThrowIfFailed();
                Assert.AreEqual(10U, vectorPlan.Result.QueryInfo.Top);
                Assert.IsTrue(vectorPlan.Result.QueryInfo.HasNonStreamingOrderBy);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.UseQueryInterop, originalOptIn);
                Environment.SetEnvironmentVariable(ConfigurationManager.BypassQueryParsing, originalBypass);
            }
        }

        // Partitions targeted

        [DataTestMethod]
        [DataRow("SELECT 5")]
        [DataRow("SELECT TOP 2 5")]
        [DataRow("SELECT * FROM c WHERE false")]
        public void QueryThatReadsNoDocumentsTargetsEmptyKeyOnly(string query)
        {
            PartitionedQueryExecutionInfoInternal plan = Plan(query);
            AssertPointRanges(plan, new object[0]);
            AssertQueryInfo(plan, new Expected());
        }

        [DataTestMethod]
        [DataRow("SELECT * FROM c")]
        [DataRow("SELECT * FROM c WHERE true")]
        [DataRow("SELECT * FROM c WHERE IS_DEFINED(c.key)")]
        [DataRow("SELECT * FROM c WHERE c.other = 42")]
        [DataRow("SELECT * FROM c WHERE c.key > 42")]
        [DataRow("SELECT * FROM c WHERE c.key BETWEEN 1 AND 10")]
        [DataRow("SELECT * FROM c WHERE c.key LIKE 'prefix%'")]
        [DataRow("SELECT * FROM c WHERE ARRAY_CONTAINS(c.tags, 'hello')")]
        [DataRow("SELECT c.id FROM c JOIN item IN c.items WHERE item.price = 42")]
        [DataRow("SELECT * FROM c WHERE EXISTS(SELECT VALUE item FROM item IN c.items WHERE item.price = 42)")]
        [DataRow("SELECT * FROM c WHERE c.name LIKE '%test%'")]
        [DataRow("SELECT (SELECT * FROM c) FROM c")]
        [DataRow("SELECT * FROM c WHERE c.name = '\u4e16\u754c'")]
        [DataRow("SELECT * FROM c WHERE c.name = '\U0001F600'")]
        public void QueryWithoutKeyEqualityTargetsAllPartitions(string query)
        {
            PartitionedQueryExecutionInfoInternal plan = Plan(query);
            AssertFullRange(plan);
            AssertQueryInfo(plan, new Expected());
        }

        [DataTestMethod]
        [DataRow("SELECT * FROM c WHERE c.key = 'value'", "[\"value\"]")]
        [DataRow("SELECT * FROM c WHERE c.key = 5", "[5]")]
        [DataRow("SELECT * FROM c WHERE c.key = true", "[true]")]
        [DataRow("SELECT * FROM c WHERE c.key = null", "[null]")]
        [DataRow("SELECT * FROM c WHERE c.key = 1.23", "[1.23]")]
        [DataRow("SELECT * FROM c WHERE c.key = -99", "[-99]")]
        [DataRow("SELECT * FROM c WHERE c.key = false", "[false]")]
        [DataRow("SELECT * FROM c WHERE 'value' = c.key", "[\"value\"]")]
        [DataRow("SELECT * FROM c WHERE c.key = 5 AND c.other = 'hello'", "[5]")]
        public void KeyEqualityTargetsSinglePartitionKeyValue(string query, string keyJson)
        {
            PartitionedQueryExecutionInfoInternal plan = Plan(query);
            AssertPointRanges(plan, JArray.Parse(keyJson).Select(token => ((JValue)token).Value).ToArray());
            AssertQueryInfo(plan, new Expected());
        }

        [DataTestMethod]
        [DataRow("SELECT * FROM c WHERE c.key IN (1, 2, 3)")]
        [DataRow("SELECT * FROM c WHERE c.key IN (1, 2, 1, 3, 2)")]
        public void InListTargetsEachValue(string query)
        {
            PartitionedQueryExecutionInfoInternal plan = Plan(query);
            AssertPointRanges(plan, new object[] { 1 }, new object[] { 2 }, new object[] { 3 });
            AssertQueryInfo(plan, new Expected());
        }

        [DataTestMethod]
        [DataRow("SELECT * FROM c WHERE c.key = 1 OR c.key = 2")]
        [DataRow("SELECT * FROM c WHERE c.key = 1 OR c.key = 2 OR c.key = 1")]
        public void OrFilterTargetsEachValue(string query)
        {
            PartitionedQueryExecutionInfoInternal plan = Plan(query);
            AssertPointRanges(plan, new object[] { 1 }, new object[] { 2 });
            AssertQueryInfo(plan, new Expected());
        }

        [DataTestMethod]
        [DataRow("[\"value\"]")]
        [DataRow("[42]")]
        [DataRow("[false]")]
        [DataRow("[null]")]
        public void ParameterizedKeyEqualityTargetsSingleValue(string keyJson)
        {
            JToken value = JArray.Parse(keyJson).Single();
            PartitionedQueryExecutionInfoInternal plan = Plan(
                "SELECT * FROM c WHERE c.key = @key",
                parameters: new JArray(Param("@key", value)));
            AssertPointRanges(plan, new object[] { ((JValue)value).Value });
            AssertQueryInfo(plan, new Expected());
        }

        [DataTestMethod]
        [DataRow("SELECT c.name, c.age FROM c", false)]
        [DataRow("SELECT VALUE c.name FROM c", true)]
        [DataRow("SELECT VALUE udf.myFunc(c.name) FROM c", true)]
        [DataRow("SELECT VALUE {\"name\": c.name} FROM c", true)]
        public void ProjectionPreservesSelectValue(string query, bool hasSelectValue)
        {
            PartitionedQueryExecutionInfoInternal plan = Plan(query);
            AssertFullRange(plan);
            AssertQueryInfo(plan, new Expected { HasSelectValue = hasSelectValue });
        }

        [TestMethod]
        public void AbsFilterTargetsBothSigns()
        {
            PartitionedQueryExecutionInfoInternal plan = Plan("SELECT * FROM c WHERE ABS(c.key) = 1");
            AssertPointRanges(plan, new object[] { -1 }, new object[] { 1 });
            AssertQueryInfo(plan, new Expected());
        }

        [TestMethod]
        public void MultiplePathsTargetCombinedKey()
        {
            string[] paths = { "/a/b/c", "/a/c" };
            PartitionedQueryExecutionInfoInternal plan = Plan("SELECT * FROM Root r WHERE r.a.b.c = null AND r.a.c = false", paths: paths);
            AssertPointRanges(plan, new object[] { null, false });
            AssertQueryInfo(plan, new Expected());

            plan = Plan("SELECT * FROM Root r WHERE r.a.b.c", paths: paths);
            AssertFullRange(plan);
            AssertQueryInfo(plan, new Expected());
        }

        [TestMethod]
        public void UnicodePartitionKeyPathTargetsAllPartitions()
        {
            PartitionedQueryExecutionInfoInternal plan = Plan("SELECT * FROM c", paths: new[] { "/\u00fcser" });
            AssertFullRange(plan);
            AssertQueryInfo(plan, new Expected());
        }

        [TestMethod]
        public void HierarchicalKeyTargetsFullKeyOrPrefix()
        {
            string[] paths = { "/tenantId", "/userId" };
            PartitionedQueryExecutionInfoInternal plan = Plan(
                "SELECT * FROM c WHERE c.tenantId = 't1' AND c.userId = 'u1'", paths: paths, kind: PartitionKind.MultiHash);
            AssertPointRanges(plan, new object[] { "t1", "u1" });
            AssertQueryInfo(plan, new Expected());

            plan = Plan("SELECT * FROM c WHERE c.tenantId = 't1'", paths: paths, kind: PartitionKind.MultiHash);
            AssertPointRanges(plan, new object[] { "t1" });
        }

        [TestMethod]
        public void HierarchicalKeyTargetsThreeLevelKeyAndPrefix()
        {
            string[] paths = { "/tenantId", "/userId", "/sessionId" };
            PartitionedQueryExecutionInfoInternal plan = Plan(
                "SELECT * FROM c WHERE c.tenantId = 't1' AND c.userId = 'u1' AND c.sessionId = 's1'",
                paths: paths,
                kind: PartitionKind.MultiHash);
            AssertPointRanges(plan, new object[] { "t1", "u1", "s1" });
            AssertQueryInfo(plan, new Expected());

            plan = Plan(
                "SELECT * FROM c WHERE c.tenantId = 't1' AND c.userId = 'u1'",
                paths: paths,
                kind: PartitionKind.MultiHash);
            AssertPointRanges(plan, new object[] { "t1", "u1" });
            AssertQueryInfo(plan, new Expected());
        }

        [TestMethod]
        public void SubqueryFilterOnlyNarrowsOuterQuery()
        {
            PartitionedQueryExecutionInfoInternal plan = Plan("SELECT (SELECT * FROM c) FROM c WHERE c.key = 42");
            AssertPointRanges(plan, new object[] { 42 });
            AssertQueryInfo(plan, new Expected());

            plan = Plan("SELECT (SELECT * FROM c WHERE c.key = 42) FROM c");
            AssertFullRange(plan);
            AssertQueryInfo(plan, new Expected());

            plan = Plan("SELECT * FROM c WHERE (c.blah = (SELECT * FROM c WHERE c.key = 42 and c.id = 5)) and c.key = 32");
            AssertPointRanges(plan, new object[0]);
            AssertQueryInfo(plan, new Expected());
        }

        // TOP / OFFSET / LIMIT

        [DataTestMethod]
        [DataRow("SELECT TOP 5 * FROM c")]
        [DataRow("SELECT TOP 5 * FROM c WHERE c.blah = 5")]
        public void Top(string query)
        {
            PartitionedQueryExecutionInfoInternal plan = Plan(query);
            AssertFullRange(plan);
            AssertQueryInfo(plan, new Expected { Top = 5 });
        }

        [TestMethod]
        public void TopParameterized()
        {
            AssertQueryInfo(
                Plan("SELECT TOP @TOPCOUNT * FROM c", parameters: new JArray(Param("@TOPCOUNT", 42))),
                new Expected { Top = 42 });
        }

        [TestMethod]
        public void TopWithPartitionFilter()
        {
            PartitionedQueryExecutionInfoInternal plan = Plan("SELECT TOP 5 * FROM c WHERE c.key = 5");
            AssertPointRanges(plan, new object[] { 5 });
            AssertQueryInfo(plan, new Expected { Top = 5 });
        }

        [DataTestMethod]
        [DataRow("SELECT TOP 5 * FROM c ORDER BY c.blah")]
        [DataRow("SELECT TOP 5 c.blah FROM c ORDER BY c.blah")]
        public void TopWithOrderBy(string query)
        {
            AssertQueryInfo(
                Plan(query),
                new Expected { Top = 5, OrderBy = Asc, OrderByExpressions = new[] { "c.blah" } });
        }

        [TestMethod]
        public void OffsetLimit()
        {
            AssertQueryInfo(
                Plan("SELECT * FROM c ORDER BY c.blah OFFSET 5 LIMIT 10"),
                new Expected { Offset = 5, Limit = 10, OrderBy = Asc, OrderByExpressions = new[] { "c.blah" } });
        }

        [TestMethod]
        public void OffsetLimitParameterized()
        {
            AssertQueryInfo(
                Plan(
                    "SELECT * FROM c ORDER BY c.blah OFFSET @skip LIMIT @take",
                    parameters: new JArray(Param("@skip", 10), Param("@take", 20))),
                new Expected { Offset = 10, Limit = 20, OrderBy = Asc, OrderByExpressions = new[] { "c.blah" } });
        }

        // ORDER BY

        [TestMethod]
        public void OrderByRewritesQuery()
        {
            PartitionedQueryExecutionInfoInternal plan = Plan("SELECT * FROM c ORDER BY c.name");
            Assert.IsFalse(string.IsNullOrEmpty(plan.QueryInfo.RewrittenQuery), "Cross-partition ORDER BY should produce a rewritten query.");
            AssertQueryInfo(plan, new Expected { OrderBy = Asc, OrderByExpressions = new[] { "c.name" } });
        }

        [DataTestMethod]
        [DataRow("c.blah")]
        [DataRow("c.key")]
        public void OrderByAscending(string expression)
        {
            PartitionedQueryExecutionInfoInternal plan = Plan($"SELECT * FROM c ORDER BY {expression}");
            AssertFullRange(plan);
            AssertQueryInfo(plan, new Expected { OrderBy = Asc, OrderByExpressions = new[] { expression } });
        }

        [TestMethod]
        public void QueryPlanJsonRoundTrip()
        {
            PartitionedQueryExecutionInfoInternal plan = Plan("SELECT TOP 5 c.name FROM c ORDER BY c.name");
            string json = JsonConvert.SerializeObject(plan);
            PartitionedQueryExecutionInfoInternal roundTripped = JsonConvert.DeserializeObject<PartitionedQueryExecutionInfoInternal>(
                json,
                new JsonSerializerSettings { DateParseHandling = DateParseHandling.None });

            Assert.AreEqual(json, JsonConvert.SerializeObject(roundTripped));
            Assert.AreEqual(plan.QueryInfo.RewrittenQuery, roundTripped.QueryInfo.RewrittenQuery);
            AssertFullRange(roundTripped);
            AssertQueryInfo(roundTripped, new Expected { Top = 5, OrderBy = Asc, OrderByExpressions = new[] { "c.name" } });
        }

        [TestMethod]
        public void OrderByDescending()
        {
            AssertQueryInfo(
                Plan("SELECT * FROM c ORDER BY c.blah DESC"),
                new Expected { OrderBy = new[] { SortOrder.Descending }, OrderByExpressions = new[] { "c.blah" } });
        }

        [TestMethod]
        public void OrderByMultipleFields()
        {
            AssertQueryInfo(
                Plan("SELECT * FROM c ORDER BY c.a ASC, c.b DESC"),
                new Expected
                {
                    OrderBy = new[] { SortOrder.Ascending, SortOrder.Descending },
                    OrderByExpressions = new[] { "c.a", "c.b" },
                });
        }

        // DISTINCT

        [DataTestMethod]
        [DataRow("SELECT DISTINCT * FROM c")]
        [DataRow("SELECT DISTINCT c.blah FROM c")]
        public void DistinctWithoutOrderByIsUnordered(string query)
        {
            AssertQueryInfo(Plan(query), new Expected { DistinctType = DistinctQueryType.Unordered });
        }

        [TestMethod]
        public void DistinctValueWithMatchingOrderByIsOrdered()
        {
            AssertQueryInfo(
                Plan("SELECT DISTINCT VALUE c.blah FROM c ORDER BY c.blah"),
                new Expected
                {
                    DistinctType = DistinctQueryType.Ordered,
                    OrderBy = Asc,
                    OrderByExpressions = new[] { "c.blah" },
                    HasSelectValue = true,
                });
        }

        [TestMethod]
        public void CountOfDistinctUsesDCount()
        {
            AssertQueryInfo(
                Plan("SELECT VALUE COUNT(1) FROM (SELECT DISTINCT VALUE c.blah FROM c)"),
                new Expected { DistinctType = DistinctQueryType.Unordered, HasSelectValue = true, HasDCount = true });
        }

        // Aggregates

        [DataTestMethod]
        [DataRow("AVG(c.blah)", nameof(AggregateOperator.Average))]
        [DataRow("COUNT(1)", nameof(AggregateOperator.Count))]
        [DataRow("MIN(c.blah)", nameof(AggregateOperator.Min))]
        [DataRow("MAX(c.blah)", nameof(AggregateOperator.Max))]
        [DataRow("SUM(c.blah)", nameof(AggregateOperator.Sum))]
        [DataRow("MAKELIST(c.blah)", nameof(AggregateOperator.MakeList))]
        [DataRow("MAKESET(c.blah)", nameof(AggregateOperator.MakeSet))]
        public void ValueAggregate(string aggregate, string expected)
        {
            AssertQueryInfo(
                Plan($"SELECT VALUE {aggregate} FROM c"),
                new Expected { Aggregates = new[] { (AggregateOperator)Enum.Parse(typeof(AggregateOperator), expected) }, HasSelectValue = true });
        }

        [TestMethod]
        public void AggregateWithoutPartitionKeyPaths()
        {
            PartitionedQueryExecutionInfoInternal plan = Plan("SELECT VALUE AVG(c.blah) FROM c", paths: new string[0]);
            AssertFullRange(plan);
            AssertQueryInfo(plan, new Expected { Aggregates = new[] { AggregateOperator.Average }, HasSelectValue = true });
        }

        [TestMethod]
        public void AggregateWithPartitionFilter()
        {
            PartitionedQueryExecutionInfoInternal plan = Plan("SELECT VALUE AVG(c.blah) FROM c WHERE c.key = 5");
            AssertPointRanges(plan, new object[] { 5 });
            AssertQueryInfo(plan, new Expected { Aggregates = new[] { AggregateOperator.Average }, HasSelectValue = true });
        }

        [TestMethod]
        public void AggregateWithJoin()
        {
            AssertQueryInfo(
                Plan("SELECT VALUE AVG(j) FROM c JOIN j IN c.blah"),
                new Expected { Aggregates = new[] { AggregateOperator.Average }, HasSelectValue = true });
        }

        [TestMethod]
        public void AggregateWithTop()
        {
            AssertQueryInfo(
                Plan("SELECT TOP 5 VALUE AVG(c.blah) FROM c"),
                new Expected { Top = 5, Aggregates = new[] { AggregateOperator.Average }, HasSelectValue = true });
        }

        // Aggregates without VALUE fill the alias map instead of Aggregates.

        [TestMethod]
        public void NonValueAggregateUsesGeneratedAlias()
        {
            Expected expected = new Expected
            {
                GroupByAliases = new[] { "$1" },
                GroupByAliasToAggregateType = new Dictionary<string, AggregateOperator?> { { "$1", AggregateOperator.Min } },
            };

            PartitionedQueryExecutionInfoInternal plan = Plan("SELECT MIN(c.blah) FROM c");
            AssertFullRange(plan);
            AssertQueryInfo(plan, expected);

            plan = Plan("SELECT MIN(c.blah) FROM c WHERE c.key = 1");
            AssertPointRanges(plan, new object[] { 1 });
            AssertQueryInfo(plan, expected);
        }

        [TestMethod]
        public void NonValueAggregatesUseGeneratedAliases()
        {
            AssertQueryInfo(
                Plan("SELECT MIN(c.blah), MAX(c.blah) FROM c"),
                new Expected
                {
                    GroupByAliases = new[] { "$1", "$2" },
                    GroupByAliasToAggregateType = new Dictionary<string, AggregateOperator?>
                    {
                        { "$1", AggregateOperator.Min },
                        { "$2", AggregateOperator.Max },
                    },
                });
        }

        [TestMethod]
        public void NonValueAggregateUsesExplicitAlias()
        {
            AssertQueryInfo(
                Plan("SELECT MIN(c.blah) AS minBlah FROM c"),
                new Expected
                {
                    GroupByAliases = new[] { "minBlah" },
                    GroupByAliasToAggregateType = new Dictionary<string, AggregateOperator?> { { "minBlah", AggregateOperator.Min } },
                });
        }

        // GROUP BY

        [TestMethod]
        public void GroupBy()
        {
            AssertQueryInfo(
                Plan("SELECT c.age, c.name FROM c GROUP BY c.age, c.name"),
                new Expected
                {
                    GroupByExpressions = new[] { "c.age", "c.name" },
                    GroupByAliases = new[] { "age", "name" },
                    GroupByAliasToAggregateType = new Dictionary<string, AggregateOperator?> { { "age", null }, { "name", null } },
                });
        }

        [TestMethod]
        public void GroupByWithAggregates()
        {
            AssertQueryInfo(
                Plan("SELECT c.team, COUNT(1) AS count, AVG(c.age) AS avg_age FROM c GROUP BY c.team"),
                new Expected
                {
                    GroupByExpressions = new[] { "c.team" },
                    GroupByAliases = new[] { "team", "count", "avg_age" },
                    GroupByAliasToAggregateType = new Dictionary<string, AggregateOperator?>
                    {
                        { "team", null },
                        { "count", AggregateOperator.Count },
                        { "avg_age", AggregateOperator.Average },
                    },
                });
        }

        [TestMethod]
        public void GroupByWithValueAggregate()
        {
            AssertQueryInfo(
                Plan("SELECT VALUE COUNT(1) FROM c GROUP BY c.age"),
                new Expected
                {
                    Aggregates = new[] { AggregateOperator.Count },
                    HasSelectValue = true,
                    GroupByExpressions = new[] { "c.age" },
                });
        }

        [TestMethod]
        public void GroupByScalarExpression()
        {
            AssertQueryInfo(
                Plan("SELECT UPPER(c.name) AS name, AVG(c.income) AS income FROM c GROUP BY UPPER(c.name)"),
                new Expected
                {
                    GroupByExpressions = new[] { "UPPER(c.name)" },
                    GroupByAliases = new[] { "name", "income" },
                    GroupByAliasToAggregateType = new Dictionary<string, AggregateOperator?>
                    {
                        { "name", null },
                        { "income", AggregateOperator.Average },
                    },
                });
        }

        // LIKE

        [TestMethod]
        public void LikeParameterized()
        {
            PartitionedQueryExecutionInfoInternal plan = Plan(
                "SELECT * FROM c WHERE c.name LIKE @pattern", parameters: new JArray(Param("@pattern", "%test%")));
            AssertFullRange(plan);
            AssertQueryInfo(plan, new Expected());
        }

        [TestMethod]
        public void LikeWithPartitionFilter()
        {
            PartitionedQueryExecutionInfoInternal plan = Plan("SELECT * FROM c WHERE c.key = 'abc' AND c.name LIKE '%test%'");
            AssertPointRanges(plan, new object[] { "abc" });
            AssertQueryInfo(plan, new Expected());
        }

        [TestMethod]
        public void HybridRrfNegativeWeightReversesComponentOrder()
        {
            const string query = "SELECT TOP 15 c.id FROM c ORDER BY RANK RRF(FullTextScore(c.title, 'John'), FullTextScore(c.text, 'United States'), [1, -0.5])";
            PartitionedQueryExecutionInfoInternal plan = Plan(query);
            HybridSearchQueryInfo hybrid = plan.HybridSearchQueryInfo;
            Assert.IsNotNull(hybrid, "RRF queries must produce a hybrid search plan.");
            CollectionAssert.AreEqual(new[] { 1.0, 0.5 }, hybrid.ComponentWeights, "Component weights must be the absolute query weights.");
            Assert.AreEqual(2, hybrid.ComponentQueryInfos.Count);
            Assert.AreEqual(SortOrder.Descending, hybrid.ComponentQueryInfos[0].OrderBy.Single(), "A positive weight ranks the component by descending score.");
            Assert.AreEqual(SortOrder.Ascending, hybrid.ComponentQueryInfos[1].OrderBy.Single(), "A negative weight reverses the component to ascending score.");

            // Optimized plans reverse negative-weight scores rather than component sort order.
            HybridSearchQueryInfo optimized = Plan(query, hybridSearchSkipOrderByRewrite: true).HybridSearchQueryInfo;
            CollectionAssert.AreEqual(new[] { 1.0, 0.5 }, optimized.ComponentWeights, "Component weights must be the absolute query weights.");
            Assert.AreEqual(2, optimized.ComponentQueryInfos.Count);
            foreach (QueryInfo component in optimized.ComponentQueryInfos)
            {
                Assert.IsFalse(component.HasOrderBy, "The optimized plan must leave component ranking to the SDK.");
                string componentScores = component.RewrittenQuery.Substring(component.RewrittenQuery.IndexOf("AS payload", StringComparison.Ordinal));
                Assert.AreEqual(1, Regex.Matches(componentScores, @"-1 \*").Count, "Only the negative-weight component score must be negated.");
                StringAssert.Matches(componentScores, new Regex(@"\[\(_FullTextScore\(c\.title.*\), \(-1 \* \(_FullTextScore\(c\.text"), "The second component score must be negated.");
            }
        }

        // Errors

        [TestMethod]
        public void UnknownFunctionReturnsQueryError()
        {
            TryCatch<PartitionedQueryExecutionInfoInternal> result = TryPlan("SELECT BADFUNC(r.age) FROM Root r", Key, PartitionKind.Hash, null);
            Assert.IsTrue(result.Failed, "Unrecognized function should fail.");
            StringAssert.Contains(result.Exception.ToString(), "BADFUNC", "The native error message should reach the caller.");
        }

        private static void EnsureNativePlatform()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Assert.IsTrue(QueryPlanInterop.LinuxLibraryAvailable.Value, "The packaged Linux library and its dependencies must load.");
            }
            else if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Inconclusive("Native query planning is only available on Windows and Linux.");
            }
        }

        private static JObject Param(string name, JToken value)
        {
            return new JObject { { "name", name }, { "value", value } };
        }

        private static PartitionedQueryExecutionInfoInternal Plan(
            string query,
            string[] paths = null,
            PartitionKind kind = PartitionKind.Hash,
            JArray parameters = null,
            bool hybridSearchSkipOrderByRewrite = false)
        {
            TryCatch<PartitionedQueryExecutionInfoInternal> result = TryPlan(query, paths ?? Key, kind, parameters, hybridSearchSkipOrderByRewrite);
            result.ThrowIfFailed();
            return result.Result;
        }

        private static TryCatch<PartitionedQueryExecutionInfoInternal> TryPlan(
            string query,
            string[] paths,
            PartitionKind kind,
            JArray parameters,
            bool hybridSearchSkipOrderByRewrite = false)
        {
            EnsureNativePlatform();
            JObject querySpec = new JObject { { "query", query }, { "parameters", parameters ?? new JArray() } };
            PartitionKeyDefinition partitionKey = new PartitionKeyDefinition
            {
                Paths = new Collection<string>(paths),
                Kind = kind,
                Version = PartitionKeyDefinitionVersion.V2,
            };

            return QueryPartitionProviderTestInstance.Object.TryGetPartitionedQueryExecutionInfoInternal(
                querySpecJsonString: querySpec.ToString(),
                partitionKeyDefinition: partitionKey,
                vectorEmbeddingPolicy: null,
                requireFormattableOrderByQuery: true,
                isContinuationExpected: false,
                allowNonValueAggregateQuery: true,
                hasLogicalPartitionKey: false,
                allowDCount: true,
                useSystemPrefix: false,
                hybridSearchSkipOrderByRewrite: hybridSearchSkipOrderByRewrite,
                geospatialType: Cosmos.GeospatialType.Geography);
        }

        /// <summary>
        /// Asserts the plan targets the whole container: one range from the empty key (inclusive) to infinity (exclusive).
        /// </summary>
        private static void AssertFullRange(PartitionedQueryExecutionInfoInternal plan)
        {
            Range<PartitionKeyInternal> range = plan.QueryRanges.Single();
            Assert.AreEqual(PartitionKeyInternal.Empty, range.Min, "Full range must start at the empty key.");
            Assert.AreEqual(PartitionKeyInternal.ExclusiveMaximum, range.Max, "Full range must end at infinity.");
            Assert.IsTrue(range.IsMinInclusive);
            Assert.IsFalse(range.IsMaxInclusive);
        }

        /// <summary>
        /// Asserts the plan targets exactly the given partition key values (in any order), each as a single-key range.
        /// </summary>
        private static void AssertPointRanges(PartitionedQueryExecutionInfoInternal plan, params object[][] keys)
        {
            foreach (Range<PartitionKeyInternal> range in plan.QueryRanges)
            {
                Assert.AreEqual(range.Min, range.Max, "Expected a single-key range.");
                Assert.IsTrue(range.IsMinInclusive && range.IsMaxInclusive, "Single-key range must include its key.");
            }

            CollectionAssert.AreEquivalent(
                keys.Select(key => PartitionKeyInternal.FromObjectArray(key, strict: true).ToJsonString()).ToArray(),
                plan.QueryRanges.Select(range => range.Min.ToJsonString()).ToArray());
        }

        /// <summary>
        /// Compares query metadata without rewritten SQL; treats null and empty collections equally.
        /// </summary>
        private static void AssertQueryInfo(PartitionedQueryExecutionInfoInternal plan, Expected expected)
        {
            QueryInfo actual = plan.QueryInfo;
            Assert.IsNotNull(actual, "QueryInfo must be returned.");
            Assert.AreEqual(expected.DistinctType, actual.DistinctType, nameof(QueryInfo.DistinctType));
            Assert.AreEqual(expected.Top, actual.Top, nameof(QueryInfo.Top));
            Assert.AreEqual(expected.Offset, actual.Offset, nameof(QueryInfo.Offset));
            Assert.AreEqual(expected.Limit, actual.Limit, nameof(QueryInfo.Limit));
            CollectionAssert.AreEqual(expected.OrderBy ?? new SortOrder[0], (actual.OrderBy ?? new SortOrder[0]).ToArray(), nameof(QueryInfo.OrderBy));
            CollectionAssert.AreEqual(expected.OrderByExpressions ?? new string[0], (actual.OrderByExpressions ?? new string[0]).ToArray(), nameof(QueryInfo.OrderByExpressions));
            CollectionAssert.AreEqual(expected.GroupByExpressions ?? new string[0], (actual.GroupByExpressions ?? new string[0]).ToArray(), nameof(QueryInfo.GroupByExpressions));
            CollectionAssert.AreEqual(expected.GroupByAliases ?? new string[0], (actual.GroupByAliases ?? new string[0]).ToArray(), nameof(QueryInfo.GroupByAliases));
            CollectionAssert.AreEqual(expected.Aggregates ?? new AggregateOperator[0], (actual.Aggregates ?? new AggregateOperator[0]).ToArray(), nameof(QueryInfo.Aggregates));
            CollectionAssert.AreEquivalent(
                (expected.GroupByAliasToAggregateType ?? new Dictionary<string, AggregateOperator?>()).ToArray(),
                (actual.GroupByAliasToAggregateType ?? new Dictionary<string, AggregateOperator?>()).ToArray(),
                nameof(QueryInfo.GroupByAliasToAggregateType));
            Assert.AreEqual(expected.HasSelectValue, actual.HasSelectValue, nameof(QueryInfo.HasSelectValue));
            Assert.AreEqual(expected.HasDCount, actual.HasDCount, nameof(QueryInfo.DCountInfo));
            Assert.IsFalse(actual.HasNonStreamingOrderBy, nameof(QueryInfo.HasNonStreamingOrderBy));
            Assert.IsFalse(actual.HasEmbeddingParameters, nameof(QueryInfo.EmbeddingParameterMap));
        }

        private sealed class Expected
        {
            public DistinctQueryType DistinctType { get; set; } = DistinctQueryType.None;

            public uint? Top { get; set; }

            public uint? Offset { get; set; }

            public uint? Limit { get; set; }

            public SortOrder[] OrderBy { get; set; }

            public string[] OrderByExpressions { get; set; }

            public string[] GroupByExpressions { get; set; }

            public string[] GroupByAliases { get; set; }

            public AggregateOperator[] Aggregates { get; set; }

            public Dictionary<string, AggregateOperator?> GroupByAliasToAggregateType { get; set; }

            public bool HasSelectValue { get; set; }

            public bool HasDCount { get; set; }
        }
    }
}
