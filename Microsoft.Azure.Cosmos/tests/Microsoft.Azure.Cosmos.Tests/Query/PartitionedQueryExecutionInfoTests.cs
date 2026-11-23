//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Tokens;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class PartitionedQueryExecutionInfoTests
    {
        public static IEnumerable<object[]> NormalizationCases
        {
            get
            {
                yield return Case("Interior point", new[] { 0 }, Interval("10", "20"), Range<string>.GetPointRange("15"));
                yield return Case("Container last", new[] { 1 }, Interval("15", "18"), Interval("10", "20"));
                yield return Case("Nested intervals", new[] { 2 }, Interval("14", "15"), Interval("12", "18"), Interval("10", "20"));
                yield return Case("Duplicates", new[] { 0 }, Interval("10", "20"), Interval("10", "20"));
                yield return Case("Equal minima, longer last", new[] { 1 }, Interval("10", "18"), Interval("10", "20"));
                yield return Case("Different minimum flags", new[] { 1, 0 }, Interval("10", "30", false), Interval("10", "20"));
                yield return Case("Uncovered maximum", new[] { 0, 1 }, Interval("10", "20"), Interval("12", "20", true, true));
                yield return Case("Covered maximum", new[] { 0 }, Interval("10", "20", true, true), Interval("12", "20"));
                yield return Case("Empty entries are retained", new[] { 0, 1, 2, 3 },
                    Interval("10", "20"), Interval("15", "15"), Interval("15", "15", false, true),
                    Interval("15", "15", false), Range<string>.GetPointRange("15"));
                yield return Case("Covered lower endpoint", new[] { 0, 2 },
                    Interval("10", "20"), Range<string>.GetPointRange("10"), Range<string>.GetPointRange("20"));
                yield return Case("Covered upper endpoint", new[] { 1, 0 },
                    Interval("10", "20", false, true), Range<string>.GetPointRange("10"), Range<string>.GetPointRange("20"));
                yield return Case("Disjoint and adjacent", new[] { 1, 0, 2 },
                    Interval("20", "30"), Interval("10", "20"), Interval("40", "50"));
                yield return Case("Full EPK range", new[] { 1 }, Range<string>.GetPointRange("00"), Interval("", "FF"));
                yield return Case("Partial overlap", new[] { 0, 1 }, Interval("10", "30"), Interval("20", "40"));
                yield return Case("Earlier covering range", new[] { 0, 1, 2 },
                    Interval("10", "50"), Interval("20", "60"), Interval("30", "30"), Interval("40", "45"));
                yield return Case("Joint coverage is not containment", new[] { 0, 2, 1 },
                    Interval("10", "30"), Interval("20", "40"), Interval("15", "35"));
                yield return Case("Ordinal, not length-aware ordering", new[] { 1, 0 }, Interval("2", "3"), Interval("10", "20"));
                yield return CaseWithComparers("Zero-extended minimum", new[] { 1, 0 }, new[] { 0 },
                    Interval("1000", "20"), Range<string>.GetPointRange("10"));
                yield return CaseWithComparers("Zero-extended maximum", new[] { 0, 1 }, new[] { 0 },
                    Interval("10", "20", true, true), Range<string>.GetPointRange("2000"));
                yield return CaseWithComparers("Zero-extended duplicate points", new[] { 0, 1 }, new[] { 0 },
                    Range<string>.GetPointRange("10"), Range<string>.GetPointRange("1000"));
                yield return CaseWithComparers("Zero-extended duplicate intervals", new[] { 0, 1 }, new[] { 0 },
                    Interval("10", "20"), Interval("1000", "2000"));
                yield return CaseWithComparers("Zero-extended excluded minimum", new[] { 0 }, new[] { 1, 0 },
                    Interval("10", "20", false, true), Range<string>.GetPointRange("1000"));
                yield return CaseWithComparers("Zero-extended excluded maximum", new[] { 0, 1 }, new[] { 0, 1 },
                    Interval("10", "20"), Range<string>.GetPointRange("2000"));
                yield return CaseWithComparers("Zero-extended empty range cannot cover a point", new[] { 0 }, new[] { 1, 0 },
                    Interval("10", "1000"), Range<string>.GetPointRange("10"));
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(NormalizationCases))]
        public void NormalizesOnlyContainedRanges(string name, string rangesJson, int[] ordinalIndices, int[] lengthAwareIndices)
        {
            List<Range<string>> original = JsonConvert.DeserializeObject<List<Range<string>>>(rangesJson);
            foreach (bool useLengthAwareComparer in new[] { false, true })
            {
                int[] retainedIndices = useLengthAwareComparer ? lengthAwareIndices : ordinalIndices;
                Range<string>[] expected = retainedIndices.Select(index => original[index]).ToArray();
                foreach (List<Range<string>> input in new[] { original, original.AsEnumerable().Reverse().ToList() })
                {
                    Range<string>[] snapshot = input.ToArray();
                    List<Range<string>> normalized = QueryRangeUtils.RemoveContainedRanges(input, useLengthAwareComparer);

                    CollectionAssert.AreEqual(expected, normalized, $"{name}, length-aware={useLengthAwareComparer}");
                    CollectionAssert.AreEqual(snapshot, input, "Caller-owned input must not be changed.");
                    AssertUnionPreserved(input, normalized, useLengthAwareComparer);
                    Assert.IsTrue(normalized.All(range => input.Any(item => ReferenceEquals(item, range))),
                        "Retained ranges must keep their original bounds and flags.");

                    CollectionAssert.AreEqual(normalized, QueryRangeUtils.RemoveContainedRanges(normalized, useLengthAwareComparer),
                        "Normalization must be idempotent.");
                    if (useLengthAwareComparer == ConfigurationManager.IsLengthAwareRangeComparatorEnabled())
                    {
                        PartitionedQueryExecutionInfo plan = new PartitionedQueryExecutionInfo { QueryRanges = input };
                        CollectionAssert.AreEqual(normalized, plan.QueryRanges, "Plan assignment must use the shared utility.");
                    }
                }
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ZeroExtendedBoundsUseConfiguredValidityAndEmptiness(bool useLengthAwareComparer)
        {
            Range<string> emptyRange = Interval("1000", "10");
            Range<string> point = Range<string>.GetPointRange("10");
            foreach (List<Range<string>> input in new[]
            {
                new List<Range<string>> { emptyRange, point },
                new List<Range<string>> { point, emptyRange },
            })
            {
                List<Range<string>> normalized = QueryRangeUtils.RemoveContainedRanges(input, useLengthAwareComparer);
                CollectionAssert.AreEqual(
                    useLengthAwareComparer ? new[] { point, emptyRange } : input.ToArray(),
                    normalized,
                    "Length-aware mode retains the empty interval; ordinal mode leaves reversed bounds untouched.");
                AssertUnionPreserved(input, normalized, useLengthAwareComparer);
            }
        }

        [DataTestMethod]
        [DataRow(0, 0, 1)]
        [DataRow(0, 1, 1)]
        [DataRow(0, 2, 1)]
        [DataRow(0, 3, 1)]
        [DataRow(1, 0, 1)]
        [DataRow(1, 1, 1)]
        [DataRow(1, 2, 2)]
        [DataRow(1, 3, 1)]
        [DataRow(2, 0, 1)]
        [DataRow(2, 1, 2)]
        [DataRow(2, 2, 1)]
        [DataRow(2, 3, 1)]
        [DataRow(3, 0, 1)]
        [DataRow(3, 1, 1)]
        [DataRow(3, 2, 1)]
        [DataRow(3, 3, 1)]
        public void EqualBoundsRespectAllEndpointFlags(int firstFlags, int secondFlags, int expectedCount)
        {
            List<Range<string>> input = new List<Range<string>>
            {
                Interval("10", "20", (firstFlags & 1) != 0, (firstFlags & 2) != 0),
                Interval("10", "20", (secondFlags & 1) != 0, (secondFlags & 2) != 0),
            };
            foreach (bool useLengthAwareComparer in new[] { false, true })
            {
                List<Range<string>> normalized = QueryRangeUtils.RemoveContainedRanges(input, useLengthAwareComparer);

                Assert.AreEqual(expectedCount, normalized.Count);
                AssertUnionPreserved(input, normalized, useLengthAwareComparer);
            }
        }

        [TestMethod]
        [DoNotParallelize]
        public void QueryRangesHonorsConfiguredComparison()
        {
            string originalSetting = Environment.GetEnvironmentVariable(ConfigurationManager.UseLengthAwareRangeComparator);
            try
            {
                foreach (bool enabled in new[] { false, true })
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.UseLengthAwareRangeComparator, enabled.ToString());
                    List<Range<string>> ranges = new List<Range<string>>
                    {
                        Interval("10", "20", true, true),
                        Range<string>.GetPointRange("2000"),
                    };
                    PartitionedQueryExecutionInfo plan = new PartitionedQueryExecutionInfo { QueryRanges = ranges };

                    Assert.AreEqual(enabled ? 1 : 2, plan.QueryRanges.Count);
                    AssertUnionPreserved(ranges, plan.QueryRanges, enabled);
                    string json = JsonConvert.SerializeObject(new { queryRanges = ranges });
                    foreach (string source in new[] { "JsonConvert", "TryParse", "GatewaySerializer", "LegacyContinuation" })
                    {
                        PartitionedQueryExecutionInfo deserialized = DeserializePlan(source, json);
                        CollectionAssert.AreEqual(plan.QueryRanges, deserialized.QueryRanges, source);
                    }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.UseLengthAwareRangeComparator, originalSetting);
            }
        }

        [DataTestMethod]
        [DataRow("JsonConvert")]
        [DataRow("TryParse")]
        [DataRow("GatewaySerializer")]
        [DataRow("LegacyContinuation")]
        public void DeserializationAndReassignmentNormalizeWithoutChangingMetadata(string source)
        {
            List<Range<string>> ranges = new List<Range<string>> { Range<string>.GetPointRange("15"), Interval("10", "20") };
            string json = JsonConvert.SerializeObject(new
            {
                partitionedQueryExecutionInfoVersion = 7,
                queryInfo = new QueryInfo { HasSelectValue = true, Top = 5 },
                queryRanges = ranges,
                hybridSearchQueryInfo = new HybridSearchQueryInfo
                {
                    RequiresGlobalStatistics = true,
                    GlobalStatisticsQuery = "SELECT COUNT(1) AS documentCount FROM c",
                    ComponentQueryInfos = new List<QueryInfo>(),
                    ComponentWeights = new List<double> { 2 },
                },
            });
            JObject raw = JObject.Parse(json);
            PartitionedQueryExecutionInfo first = DeserializePlan(source, json);
            PartitionedQueryExecutionInfo second = DeserializePlan(source, json);

            Assert.AreNotSame(first.QueryRanges, second.QueryRanges);
            foreach (PartitionedQueryExecutionInfo plan in new[] { first, second })
            {
                CollectionAssert.AreEqual(new[] { ranges[1] }, plan.QueryRanges);
                Assert.AreEqual(7, plan.Version);
                JObject serialized = JObject.Parse(plan.ToString());
                Assert.IsTrue(JToken.DeepEquals(raw["queryInfo"], serialized["queryInfo"]));
                Assert.IsTrue(JToken.DeepEquals(raw["hybridSearchQueryInfo"], serialized["hybridSearchQueryInfo"]));

                plan.QueryRanges = new List<Range<string>> { Range<string>.GetPointRange("25"), Interval("20", "30") };
                CollectionAssert.AreEqual(new[] { Interval("20", "30") }, plan.QueryRanges);
                PartitionedQueryExecutionInfo roundTripped = DeserializePlan(source, plan.ToString());
                CollectionAssert.AreEqual(plan.QueryRanges, roundTripped.QueryRanges);
            }
        }

        [DataTestMethod]
        [DataRow("{}")]
        [DataRow(@"{""queryRanges"":null}")]
        [DataRow(@"{""queryRanges"":[]}")]
        public void MissingNullAndEmptyRangesRemainCompatible(string json)
        {
            foreach (string source in new[] { "JsonConvert", "TryParse", "GatewaySerializer", "LegacyContinuation" })
            {
                PartitionedQueryExecutionInfo plan = DeserializePlan(source, json);
                if (json.Contains("[]"))
                {
                    Assert.AreEqual(0, plan.QueryRanges.Count);
                    plan.QueryRanges = new List<Range<string>>();
                    Assert.AreEqual(0, plan.QueryRanges.Count);
                }
                else
                {
                    Assert.IsNull(plan.QueryRanges);
                }

                plan.QueryRanges = null;
                Assert.IsNull(plan.QueryRanges);
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void MalformedEntriesAreNotSilentlyRemoved(bool nullEntry)
        {
            List<Range<string>> input = new List<Range<string>>
            {
                Interval("10", "30"),
                nullEntry ? null : Interval("20", "15"),
            };
            PartitionedQueryExecutionInfo plan = new PartitionedQueryExecutionInfo { QueryRanges = input };

            CollectionAssert.AreEqual(input, plan.QueryRanges);
            Assert.IsTrue(PartitionedQueryExecutionInfo.TryParse(plan.ToString(), out PartitionedQueryExecutionInfo parsed));
            CollectionAssert.AreEqual(input, parsed.QueryRanges);
        }

        [TestMethod]
        public void ThinClientInternalRangesNormalizeAfterEpkConversion()
        {
            PartitionKeyInternal prefix = PartitionKeyInternal.FromJsonString(@"[""tenant-1""]");
            PartitionKeyInternal point = PartitionKeyInternal.FromJsonString(@"[""tenant-1"",""item""]");
            Range<PartitionKeyInternal>[] rawRanges =
            {
                new Range<PartitionKeyInternal>(point, point, true, true),
                new Range<PartitionKeyInternal>(prefix, prefix, true, true),
            };
            PartitionKeyDefinition definition = MultiHashQueryPlanTestHelper.CreatePartitionKeyDefinition();
            Range<string> expected = PartitionKeyInternal.GetEffectivePartitionKeyRange(definition, rawRanges[1]);
            string json = JsonConvert.SerializeObject(new { queryInfo = new QueryInfo(), queryRanges = rawRanges });
            using MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

            PartitionedQueryExecutionInfo plan = ThinClientQueryPlanHelper.DeserializeQueryPlanResponse(stream, definition);

            CollectionAssert.AreEqual(new[] { expected }, plan.QueryRanges);
        }

        internal static PartitionedQueryExecutionInfo DeserializePlan(string source, string json)
        {
            switch (source)
            {
                case "JsonConvert":
                    return JsonConvert.DeserializeObject<PartitionedQueryExecutionInfo>(json);
                case "TryParse":
                    Assert.IsTrue(PartitionedQueryExecutionInfo.TryParse(json, out PartitionedQueryExecutionInfo parsed));
                    return parsed;
                case "GatewaySerializer":
                    using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    {
                        return new CosmosSerializerCore().FromStream<PartitionedQueryExecutionInfo>(stream);
                    }

                case "LegacyContinuation":
                    CosmosObject token = (CosmosObject)CosmosElement.Parse(JsonConvert.SerializeObject(new
                    {
                        Version = "1.1",
                        QueryPlan = json,
                        SourceContinuationToken = new object[0],
                    }));
                    Assert.IsTrue(PipelineContinuationTokenV1_1.TryCreateFromCosmosElement(token, out PipelineContinuationTokenV1_1 parsedToken));
                    return parsedToken.QueryPlan;
                default:
                    throw new ArgumentOutOfRangeException(nameof(source));
            }
        }

        private static object[] Case(string name, int[] retainedIndices, params Range<string>[] ranges) =>
            CaseWithComparers(name, retainedIndices, retainedIndices, ranges);

        private static object[] CaseWithComparers(string name, int[] ordinalIndices, int[] lengthAwareIndices, params Range<string>[] ranges) =>
            new object[] { name, JsonConvert.SerializeObject(ranges), ordinalIndices, lengthAwareIndices };

        private static Range<string> Interval(string min, string max, bool isMinInclusive = true, bool isMaxInclusive = false) =>
            new Range<string>(min, max, isMinInclusive, isMaxInclusive);

        private static void AssertUnionPreserved(IReadOnlyList<Range<string>> input, IReadOnlyList<Range<string>> output, bool useLengthAwareComparer)
        {
            string[] endpoints = input.Concat(output).SelectMany(range => new[] { range.Min, range.Max }).Distinct().ToArray();
            int width = endpoints.Max(endpoint => endpoint.Length);
            foreach (string probe in endpoints.Concat(endpoints.Select(
                endpoint => useLengthAwareComparer ? endpoint.PadRight(width, '0') + "1" : endpoint + "\0")))
            {
                Assert.AreEqual(
                    input.Any(range => Includes(range, probe, useLengthAwareComparer)),
                    output.Any(range => Includes(range, probe, useLengthAwareComparer)),
                    $"EPK union changed at {JsonConvert.SerializeObject(probe)}.");
            }
        }

        private static bool Includes(Range<string> range, string value, bool useLengthAwareComparer)
        {
            int minimum = CompareEndpoints(value, range.Min, useLengthAwareComparer);
            int maximum = CompareEndpoints(value, range.Max, useLengthAwareComparer);
            return (minimum > 0 || (minimum == 0 && range.IsMinInclusive))
                && (maximum < 0 || (maximum == 0 && range.IsMaxInclusive));
        }

        private static int CompareEndpoints(string left, string right, bool useLengthAwareComparer)
        {
            if (useLengthAwareComparer)
            {
                // Independently model zero-extension instead of reusing the production range comparers.
                int width = Math.Max(left.Length, right.Length);
                return string.CompareOrdinal(left.PadRight(width, '0'), right.PadRight(width, '0'));
            }

            return string.CompareOrdinal(left, right);
        }
    }
}
