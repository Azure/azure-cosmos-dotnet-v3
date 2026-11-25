//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    internal static class MultiHashQueryPlanTestHelper
    {
        internal const string ContainerLink = "dbs/db/colls/container";
        internal const string HighId = "abcdef12-3456-7890-abcd-ef1234567890";
        internal const string LowId = "11223344-5566-7788-9900-1122334455ab";

        internal static PartitionKeyDefinition CreatePartitionKeyDefinition() => new PartitionKeyDefinition
        {
            Paths = new Collection<string> { "/_pk", "/id" },
            Kind = PartitionKind.MultiHash,
            Version = PartitionKeyDefinitionVersion.V2,
        };

        internal static IEnumerable<object[]> Cases()
        {
            foreach (string name in new[] { "HighHigh", "LowLow", "Mixed", "MixedReversed", "Concat", "CaseSensitive", "FiveSixLetters" })
            {
                yield return new object[] { name, JsonConvert.SerializeObject(CreateQuery(name)) };
            }

            foreach (string predicate in new[]
            {
                @"LOWER(c[""id""]) = ""1122ab"" OR LOWER(c[""id""]) = ""abcdef12""",
                @"UPPER(c[""id""]) = ""1122AB"" OR UPPER(c[""id""]) = ""ABCDEF12""",
                @"STRINGEQUALS(c[""id""], ""1122ab"", true) OR STRINGEQUALS(c[""id""], ""abcdef12"", true)",
                @"c[""id""] = ""exact"" OR c[""id""] > ""m""",
                @"c[""id""] = ""exact"" OR (c[""id""] >= ""m"" AND c[""id""] <= ""z"")",
                @"c[""id""] = ""exact"" OR STARTSWITH(c[""id""], ""pre"")",
                @"c[""id""] = ""exact"" OR c[""id""] LIKE ""abc%""",
                @"c[""id""] = ""exact"" OR CONTAINS(c[""id""], ""sub"")",
                @"c[""id""] = ""exact"" OR ENDSWITH(c[""id""], ""ab"")",
                @"c[""id""] = ""exact"" OR RegexMatch(c[""id""], ""^ab"")",
                @"c[""id""] = ""exact"" OR LEFT(c[""id""], 2) = ""ab""",
                @"c[""id""] = ""exact"" OR SUBSTRING(c[""id""], 0, 2) = ""ab""",
                @"c[""id""] = 1 OR FLOOR(c[""id""]) = 5",
                @"c[""id""] = 1 OR CEILING(c[""id""]) = 5",
                @"c[""id""] = 1 OR ROUND(c[""id""]) = 5",
                @"c[""id""] = 1 OR TRUNC(c[""id""]) = 5",
                @"c[""id""] = ""exact"" OR IS_NUMBER(c[""id""])",
                @"c[""id""] = ""2020-01-01T00:00:00Z"" OR DateTimePart(""yyyy"", c[""id""]) = 2025",
            })
            {
                yield return new object[] { predicate, JsonConvert.SerializeObject(new SqlQuerySpec($@"SELECT * FROM c WHERE c[""_pk""] = ""t1"" AND ({predicate})")) };
            }
        }

        internal static SqlQuerySpec CreateQuery(string name)
        {
            string first = LowId;
            string second = HighId;
            switch (name)
            {
                case "HighHigh":
                    first = HighId;
                    second = "fedcba98-7654-3210-fedc-ba9876543210";
                    break;
                case "LowLow":
                    second = "11223344-5566-7788-9900-1122334455cd";
                    break;
                case "MixedReversed":
                    first = HighId;
                    second = LowId;
                    break;
                case "FiveSixLetters":
                    first = "11223344-5566-7788-9900-1122334abcde";
                    second = "11223344-5566-7788-9900-112233abcdef";
                    break;
                case "Mixed":
                case "Concat":
                case "CaseSensitive":
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown repro case.");
            }

            string id = name == "Concat" ? @"CONCAT(c[""id""], """")" : @"c[""id""]";
            return new SqlQuerySpec(
                $@"SELECT * FROM c WHERE c[""_pk""] = @p0
AND NOT IS_DEFINED(c[""_isSoftDeleted""])
AND NOT IS_DEFINED(c[""auxStoreSoftDeleteDateTime""])
AND ((STRINGEQUALS({id}, @p1, @p2) AND IS_DEFINED(c[""id""]) AND c[""id""] != null)
OR (STRINGEQUALS({id}, @p3, @p4) AND IS_DEFINED(c[""id""]) AND c[""id""] != null))",
                new SqlParameterCollection
                {
                    new SqlParameter("@p0", "tenant-1"),
                    new SqlParameter("@p1", first),
                    new SqlParameter("@p2", name != "CaseSensitive"),
                    new SqlParameter("@p3", second),
                    new SqlParameter("@p4", name != "CaseSensitive"),
                });
        }

        internal static QueryPartitionProvider CreateProvider()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            {
                Assert.Inconclusive("This reproduction measures Windows x64 ServiceInterop.dll output.");
            }

            return new QueryPartitionProvider(
                QueryPartitionProviderTestInstance.DefaultQueryEngineConfiguration.ToDictionary(pair => pair.Key, pair => pair.Value));
        }

        internal static PartitionedQueryExecutionInfo GetNativePlan(
            QueryPartitionProvider provider,
            SqlQuerySpec query,
            TestContext testContext)
        {
            PartitionKeyDefinition definition = CreatePartitionKeyDefinition();
            TryCatch<PartitionedQueryExecutionInfoInternal> result = provider.TryGetPartitionedQueryExecutionInfoInternal(
                JsonConvert.SerializeObject(query), definition, vectorEmbeddingPolicy: null,
                requireFormattableOrderByQuery: true, isContinuationExpected: false,
                allowNonValueAggregateQuery: true, hasLogicalPartitionKey: false, allowDCount: true,
                useSystemPrefix: false, hybridSearchSkipOrderByRewrite: false,
                geospatialType: Cosmos.GeospatialType.Geography);
            Assert.IsTrue(result.Succeeded, result.Failed ? result.Exception.ToString() : string.Empty);
            Assert.IsTrue(result.Result.QueryRanges.Count > 0);

            testContext.WriteLine($"Actual Windows native planner; {RuntimeInformation.FrameworkDescription}; {RuntimeInformation.ProcessArchitecture}");
            using Process process = Process.GetCurrentProcess();
            foreach (ProcessModule module in process.Modules)
            {
                if (module.ModuleName.EndsWith("ServiceInterop.dll", StringComparison.OrdinalIgnoreCase))
                {
                    testContext.WriteLine($"Native module: {module.FileName}; version={module.FileVersionInfo.FileVersion}");
                }
            }

            testContext.WriteLine($"Query engine configuration: {JsonConvert.SerializeObject(QueryPartitionProviderTestInstance.DefaultQueryEngineConfiguration)}");
            testContext.WriteLine($"Query: {JsonConvert.SerializeObject(query)}");
            testContext.WriteLine($"Raw native ranges: {JsonConvert.SerializeObject(result.Result.QueryRanges)}");
            List<Range<string>> converted = result.Result.QueryRanges
                .Select(range => PartitionKeyInternal.GetEffectivePartitionKeyRange(definition, range)).ToList();
            WriteRanges(converted, testContext);
            return provider.ConvertPartitionedQueryExecutionInfo(result.Result, definition);
        }

        internal static void WriteRanges(IReadOnlyList<Range<string>> ranges, TestContext testContext)
        {
            testContext.WriteLine($"Pre-wrapper EPK ranges: {JsonConvert.SerializeObject(ranges)}");
            for (int i = 0; i < ranges.Count; i++)
            {
                for (int j = i + 1; j < ranges.Count; j++)
                {
                    Range<string> left = ranges[i];
                    Range<string> right = ranges[j];
                    if (Range<string>.CheckOverlapping(left, right))
                    {
                        testContext.WriteLine($"Overlap: {left} and {right}; containment={Contains(left, right) || Contains(right, left)}");
                    }
                }
            }
        }

        internal static Mock<CosmosClientContext> CreateContext(MockDocumentClient documentClient)
        {
            ContainerProperties properties = ContainerProperties.CreateWithResourceId("rid");
            properties.Id = "container";
            properties.PartitionKey = CreatePartitionKeyDefinition();
            Mock<CosmosClientContext> context = new Mock<CosmosClientContext>(MockBehavior.Strict);
            context.SetupGet(client => client.DocumentClient).Returns(documentClient);
            context.SetupGet(client => client.ClientOptions).Returns(new CosmosClientOptions());
            context.SetupGet(client => client.SerializerCore).Returns(new CosmosSerializerCore());
            context.SetupGet(client => client.HybridSearchStatisticsCacheFactory)
                .Returns(Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch.HybridSearchStatisticsCacheFactory.Create(null, 0));
            context.Setup(client => client.GetCachedContainerPropertiesAsync(
                ContainerLink, It.IsAny<ITrace>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(properties);
            return context;
        }

        private static bool Contains(Range<string> outer, Range<string> inner) =>
            (string.CompareOrdinal(outer.Min, inner.Min) < 0
                || (outer.Min == inner.Min && (outer.IsMinInclusive || !inner.IsMinInclusive)))
            && (string.CompareOrdinal(outer.Max, inner.Max) > 0
                || (outer.Max == inner.Max && (outer.IsMaxInclusive || !inner.IsMaxInclusive)));
    }
}
