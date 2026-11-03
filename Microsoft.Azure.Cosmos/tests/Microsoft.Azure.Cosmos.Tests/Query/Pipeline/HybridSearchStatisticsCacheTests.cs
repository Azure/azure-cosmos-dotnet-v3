//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Cache = Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch.HybridSearchStatisticsCache;

    [TestClass]
    public class HybridSearchStatisticsCacheTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void InvalidSettingsAreRejected()
        {
            List<SettingsTestCase> testCases = new List<SettingsTestCase>
            {
                new SettingsTestCase("Zero capacity", 1, 0),
                new SettingsTestCase("Negative capacity", 1, -1),
            };
            for (int index = 0; index < testCases.Count; index++)
            {
                SettingsTestCase testCase = testCases[index];
                this.TestContext.WriteLine($"Case {index}: {testCase}");
                Assert.ThrowsException<ArgumentOutOfRangeException>(
                    () => Cache.Create(TimeSpan.FromTicks(testCase.MaxAgeTicks), testCase.MaxEntries), testCase.ToString());
            }
        }

        [TestMethod]
        public void CreateReturnsNoOpUnlessMaxAgeIsPositive()
        {
            List<TimeSpan?> disabledAges = new List<TimeSpan?> { null, TimeSpan.Zero, TimeSpan.FromTicks(-1) };
            for (int index = 0; index < disabledAges.Count; index++)
            {
                TimeSpan? maxAge = disabledAges[index];
                this.TestContext.WriteLine($"Case {index}: MaxAge={maxAge?.ToString() ?? "<null>"}");
                HybridSearchStatisticsCache disabled = HybridSearchStatisticsCache.Create(maxAge, 3);
                Assert.AreSame(HybridSearchStatisticsCache.GetNoOpImpl(), disabled);

                using Cosmos.Tracing.Trace trace = Cosmos.Tracing.Trace.GetRootTrace("Disabled cache");
                disabled.Add("rid", "etag", CreatePlan(), CreateStatistics(), disabled.GetElapsedTime());
                Assert.IsFalse(disabled.TryGet("rid", "etag", CreatePlan(), trace, out GlobalFullTextSearchStatistics result));
                Assert.IsNull(result);
                Assert.IsFalse(trace.Data.ContainsKey("Hybrid Search Statistics Cache"));
            }

            HybridSearchStatisticsCache enabled = HybridSearchStatisticsCache.Create(TimeSpan.FromMinutes(1), 3);
            Assert.AreNotSame(HybridSearchStatisticsCache.GetNoOpImpl(), enabled);
            using Cosmos.Tracing.Trace missTrace = Cosmos.Tracing.Trace.GetRootTrace("Enabled cache miss");
            Assert.IsFalse(enabled.TryGet("rid", "etag", CreatePlan(), missTrace, out _));
            Assert.AreEqual("Miss", missTrace.Data["Hybrid Search Statistics Cache"]);

            using Cosmos.Tracing.Trace hitTrace = Cosmos.Tracing.Trace.GetRootTrace("Enabled cache hit");
            enabled.Add("rid", "etag", CreatePlan(), CreateStatistics(), enabled.GetElapsedTime());
            Assert.IsTrue(enabled.TryGet("rid", "etag", CreatePlan(), hitTrace, out _));
            Assert.AreEqual("Hit", hitTrace.Data["Hybrid Search Statistics Cache"]);
        }

        [TestMethod]
        public void DefaultClockIsMonotonic()
        {
            Cache cache = Cache.Create(TimeSpan.MaxValue, 3);
            TimeSpan startedAt = cache.GetElapsedTime();
            Assert.IsTrue(startedAt >= TimeSpan.Zero);
            Assert.IsTrue(cache.GetElapsedTime() >= startedAt);
            cache.Add("rid", "etag", CreatePlan(), CreateStatistics(), startedAt);
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan(), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics result));
            Assert.AreEqual(10L, result.DocumentCount);
        }

        [TestMethod]
        public void MissesDoNotPopulateCounters()
        {
            Cache cache = Cache.Create(TimeSpan.FromMinutes(1), 2, () => TimeSpan.Zero);
            for (int index = 0; index < 10; index++)
            {
                Assert.IsFalse(cache.TryGet(index.ToString(), "etag", CreatePlan(), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics result));
                Assert.IsNull(result);
            }

            Assert.AreEqual(0, StoredCount(cache));
        }

        [TestMethod]
        public void MissingContainerIdentityBypassesStorage()
        {
            List<ContainerIdentityTestCase> testCases = new List<ContainerIdentityTestCase>
            {
                new ContainerIdentityTestCase("Null RID", null, "etag", 10),
                new ContainerIdentityTestCase("Null ETag", "rid", null, 10),
            };
            for (int index = 0; index < testCases.Count; index++)
            {
                ContainerIdentityTestCase testCase = testCases[index];
                this.TestContext.WriteLine($"Case {index}: {testCase}");
                Cache cache = Cache.Create(TimeSpan.FromMinutes(1), 3, () => TimeSpan.Zero);
                Assert.IsFalse(cache.TryGet(testCase.ResourceId, testCase.ETag, CreatePlan(), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics result));
                Assert.IsNull(result);
                cache.Add(testCase.ResourceId, testCase.ETag, CreatePlan(), CreateStatistics(testCase.DocumentCount), TimeSpan.Zero);
                Assert.AreEqual(0, StoredCount(cache));
            }
        }

        [TestMethod]
        public void MissingOrIncompleteMetadataBypassesLookupAndAdmission()
        {
            List<MetadataTestCase> testCases = new List<MetadataTestCase>
            {
                new MetadataTestCase("Absent metadata", null),
                new MetadataTestCase("Null metadata", "null"),
                new MetadataTestCase("Empty metadata", "[]"),
                new MetadataTestCase("Null entry", "[null]"),
                new MetadataTestCase("Empty entry", "[{}]"),
                new MetadataTestCase("Missing path", @"[{""terms"":[{""term"":""run""}]}]"),
                new MetadataTestCase("Null path", @"[{""path"":null,""terms"":[{""term"":""run""}]}]"),
                new MetadataTestCase("Empty path", @"[{""path"":[],""terms"":[{""term"":""run""}]}]"),
                new MetadataTestCase("Null path segment", @"[{""path"":[null],""terms"":[{""term"":""run""}]}]"),
                new MetadataTestCase("Missing terms", @"[{""path"":[""text""]}]"),
                new MetadataTestCase("Null terms", @"[{""path"":[""text""],""terms"":null}]"),
                new MetadataTestCase("Empty terms", @"[{""path"":[""text""],""terms"":[]}]"),
                new MetadataTestCase("Null term entry", @"[{""path"":[""text""],""terms"":[null]}]"),
                new MetadataTestCase("Empty term entry", @"[{""path"":[""text""],""terms"":[{}]}]"),
                new MetadataTestCase("Null term text", @"[{""path"":[""text""],""terms"":[{""term"":null}]}]"),
                new MetadataTestCase("Unresolved parameter", @"[{""path"":[""text""],""terms"":[{""parameter"":""@term""}]}]"),
                new MetadataTestCase("Negative distance", @"[{""path"":[""text""],""terms"":[{""term"":""run"",""distance"":-1}]}]"),
            };
            for (int index = 0; index < testCases.Count; index++)
            {
                MetadataTestCase testCase = testCases[index];
                this.TestContext.WriteLine($"Case {index}: {testCase}");
                Cache cache = Cache.Create(TimeSpan.FromMinutes(1), 8, () => TimeSpan.Zero);
                HybridSearchQueryInfo plan = ParsePlan(testCase.Metadata);
                Assert.IsFalse(cache.TryGet("rid", "etag", plan, NoOpTrace.Singleton, out GlobalFullTextSearchStatistics result));
                Assert.IsNull(result);
                cache.Add("rid", "etag", plan, CreateStatistics(), TimeSpan.Zero);
                Assert.AreEqual(0, StoredCount(cache));
            }
        }

        [TestMethod]
        public void UnneededStatisticsBypassesLookupAndAdmission()
        {
            Cache cache = Cache.Create(TimeSpan.FromMinutes(1), 3, () => TimeSpan.Zero);
            cache.Add("rid", "etag", CreatePlan(), CreateStatistics(), TimeSpan.Zero);
            HybridSearchQueryInfo plan = CreatePlan();
            plan.RequiresGlobalStatistics = false;
            Assert.IsFalse(cache.TryGet("rid", "etag", plan, NoOpTrace.Singleton, out _));
            cache.Add("rid", "etag", plan, CreateStatistics(), TimeSpan.Zero);
            Assert.AreEqual(3, StoredCount(cache));
        }

        [TestMethod]
        public void ContainerRevisionsIsolateCounts()
        {
            Cache cache = Cache.Create(TimeSpan.FromMinutes(1), 12, () => TimeSpan.Zero);
            List<ContainerIdentityTestCase> testCases = new List<ContainerIdentityTestCase>
            {
                new ContainerIdentityTestCase("Original container", "rid", "etag", 10),
                new ContainerIdentityTestCase("Different container", "other-rid", "etag", 20),
                new ContainerIdentityTestCase("Different revision", "rid", "other-etag", 30),
                new ContainerIdentityTestCase("Case-sensitive RID", "RID", "etag", 40),
            };
            for (int index = 0; index < testCases.Count; index++)
            {
                ContainerIdentityTestCase testCase = testCases[index];
                this.TestContext.WriteLine($"Publish case {index}: {testCase}");
                cache.Add(testCase.ResourceId, testCase.ETag, CreatePlan(), CreateStatistics(testCase.DocumentCount), TimeSpan.Zero);
            }

            for (int index = 0; index < testCases.Count; index++)
            {
                ContainerIdentityTestCase testCase = testCases[index];
                this.TestContext.WriteLine($"Read case {index}: {testCase}");
                Assert.IsTrue(cache.TryGet(testCase.ResourceId, testCase.ETag, CreatePlan(), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics result));
                Assert.AreEqual(testCase.DocumentCount, result.DocumentCount);
            }

            Assert.AreEqual(12, StoredCount(cache));
        }

        [TestMethod]
        public void ReusesSubsetsReorderedTermsDuplicatesAndRepeatedPaths()
        {
            Cache cache = Cache.Create(TimeSpan.FromMinutes(1), 4, () => TimeSpan.Zero);
            cache.Add("rid", "etag", CreatePlan("swim", "run"), Statistics(10, 100, 2, 3), TimeSpan.Zero);
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("run"), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics subset));
            CollectionAssert.AreEqual(new long[] { 3 }, subset.FullTextStatistics[0].HitCounts.ToArray());
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("run", "swim", "run"), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics reordered));
            CollectionAssert.AreEqual(new long[] { 3, 2, 3 }, reordered.FullTextStatistics[0].HitCounts.ToArray());

            HybridSearchQueryInfo regrouped = CreatePlan("run");
            regrouped.FullTextSearchTerms.Add(CreatePlan("swim", "run").FullTextSearchTerms[0]);
            Assert.IsTrue(cache.TryGet("rid", "etag", regrouped, NoOpTrace.Singleton, out GlobalFullTextSearchStatistics result));
            Assert.AreEqual(2, result.FullTextStatistics.Count);
            Assert.AreEqual(100L, result.FullTextStatistics[0].TotalWordCount);
            Assert.AreEqual(100L, result.FullTextStatistics[1].TotalWordCount);
            CollectionAssert.AreEqual(new long[] { 3 }, result.FullTextStatistics[0].HitCounts.ToArray());
            CollectionAssert.AreEqual(new long[] { 2, 3 }, result.FullTextStatistics[1].HitCounts.ToArray());
            Assert.AreEqual(4, StoredCount(cache));
        }

        [TestMethod]
        public void IndependentlyCollectedCountsCanBeCombined()
        {
            TestClock clock = new TestClock();
            Cache cache = Cache.Create(TimeSpan.FromSeconds(10), 6, clock.Read);
            cache.Add("rid", "etag", CreatePlan("run"), Statistics(10, 100, 3), clock.Now);
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("run"), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics selected));
            clock.Now = TimeSpan.FromSeconds(2);
            cache.Add("rid", "etag", CreatePlan("swim"), Statistics(20, 200, 5), clock.Now);
            clock.Now = TimeSpan.FromSeconds(4);
            HybridSearchQueryInfo title = CreatePlan("energy");
            title.FullTextSearchTerms[0].Path[0] = "title";
            cache.Add("rid", "etag", title, Statistics(30, 90, 6), clock.Now);

            HybridSearchQueryInfo combined = CreatePlan("swim", "run");
            combined.FullTextSearchTerms.Insert(0, title.FullTextSearchTerms[0]);
            Assert.IsTrue(cache.TryGet("rid", "etag", combined, NoOpTrace.Singleton, out GlobalFullTextSearchStatistics result));
            Assert.AreEqual(30L, result.DocumentCount);
            Assert.AreEqual(90L, result.FullTextStatistics[0].TotalWordCount);
            Assert.AreEqual(200L, result.FullTextStatistics[1].TotalWordCount);
            CollectionAssert.AreEqual(new long[] { 6 }, result.FullTextStatistics[0].HitCounts.ToArray());
            CollectionAssert.AreEqual(new long[] { 5, 3 }, result.FullTextStatistics[1].HitCounts.ToArray());
            Assert.AreEqual(10L, selected.DocumentCount);
            Assert.AreEqual(100L, selected.FullTextStatistics[0].TotalWordCount);
            CollectionAssert.AreEqual(new long[] { 3 }, selected.FullTextStatistics[0].HitCounts.ToArray());
        }

        [TestMethod]
        public void EmptyCorpusAndMaximumCountsAreSupported()
        {
            TestClock clock = new TestClock();
            Cache cache = Cache.Create(TimeSpan.FromSeconds(10), 3, clock.Read);
            cache.Add("rid", "etag", CreatePlan(), Statistics(0, 0, 0), clock.Now);
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan(), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics empty));
            Assert.AreEqual(0L, empty.DocumentCount);
            Assert.AreEqual(0L, empty.FullTextStatistics[0].TotalWordCount);
            CollectionAssert.AreEqual(new long[] { 0 }, empty.FullTextStatistics[0].HitCounts.ToArray());
            clock.Now = TimeSpan.FromSeconds(1);
            cache.Add("rid", "etag", CreatePlan(), Statistics(long.MaxValue, long.MaxValue, long.MaxValue), clock.Now);
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan(), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics maximum));
            Assert.AreEqual(long.MaxValue, maximum.DocumentCount);
            Assert.AreEqual(long.MaxValue, maximum.FullTextStatistics[0].TotalWordCount);
            CollectionAssert.AreEqual(new[] { long.MaxValue }, maximum.FullTextStatistics[0].HitCounts.ToArray());
        }

        [TestMethod]
        public void RefreshingOtherCountsDoesNotRenewOldHitCounts()
        {
            TestClock clock = new TestClock();
            Cache cache = Cache.Create(TimeSpan.FromSeconds(10), 4, clock.Read);
            cache.Add("rid", "etag", CreatePlan("run"), Statistics(10, 100, 3), clock.Now);
            clock.Now = TimeSpan.FromSeconds(9);
            cache.Add("rid", "etag", CreatePlan("swim"), Statistics(20, 200, 5), clock.Now);
            clock.Now = TimeSpan.FromSeconds(10) - TimeSpan.FromTicks(1);
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("run", "swim"), NoOpTrace.Singleton, out _));
            clock.Now = TimeSpan.FromSeconds(10);
            Assert.IsFalse(cache.TryGet("rid", "etag", CreatePlan("swim", "run"), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics result));
            Assert.IsNull(result);
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("swim"), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics live));
            Assert.AreEqual(20L, live.DocumentCount);
            Assert.AreEqual(200L, live.FullTextStatistics[0].TotalWordCount);
        }

        [TestMethod]
        public void RefreshingAnotherPathDoesNotRenewOldPathCounts()
        {
            TestClock clock = new TestClock();
            Cache cache = Cache.Create(TimeSpan.FromSeconds(10), 5, clock.Read);
            cache.Add("rid", "etag", CreatePlan(), CreateStatistics(), clock.Now);
            clock.Now = TimeSpan.FromSeconds(5);
            HybridSearchQueryInfo otherPath = CreatePlan();
            otherPath.FullTextSearchTerms[0].Path[0] = "title";
            cache.Add("rid", "etag", otherPath, CreateStatistics(20), clock.Now);
            clock.Now = TimeSpan.FromSeconds(10);
            Assert.IsFalse(cache.TryGet("rid", "etag", CreatePlan(), NoOpTrace.Singleton, out _));
            Assert.IsTrue(cache.TryGet("rid", "etag", otherPath, NoOpTrace.Singleton, out _));
            clock.Now = TimeSpan.FromSeconds(15);
            Assert.IsFalse(cache.TryGet("rid", "etag", otherPath, NoOpTrace.Singleton, out _));
        }

        [TestMethod]
        public void NumericallyInconsistentMixedCountsCauseAMissWithoutClamping()
        {
            TestClock clock = new TestClock();
            Cache cache = Cache.Create(TimeSpan.FromMinutes(1), 4, clock.Read);
            cache.Add("rid", "etag", CreatePlan("run"), Statistics(10, 100, 8), clock.Now);
            clock.Now = TimeSpan.FromSeconds(1);
            cache.Add("rid", "etag", CreatePlan("swim"), Statistics(3, 30, 2), clock.Now);
            Assert.IsFalse(cache.TryGet("rid", "etag", CreatePlan("run", "swim"), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics invalid));
            Assert.IsNull(invalid);
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("swim"), NoOpTrace.Singleton, out _));
            clock.Now = TimeSpan.FromSeconds(2);
            cache.Add("rid", "etag", CreatePlan("run", "swim"), Statistics(3, 30, 1, 2), clock.Now);
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("run", "swim"), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics refreshed));
            CollectionAssert.AreEqual(new long[] { 1, 2 }, refreshed.FullTextStatistics[0].HitCounts.ToArray());
        }

        [TestMethod]
        public void ZeroDocumentCountWithOlderNonzeroWordCountCausesAMiss()
        {
            TestClock clock = new TestClock();
            Cache cache = Cache.Create(TimeSpan.FromMinutes(1), 5, clock.Read);
            cache.Add("rid", "etag", CreatePlan(), Statistics(10, 100, 0), clock.Now);
            clock.Now = TimeSpan.FromSeconds(1);
            HybridSearchQueryInfo otherPath = CreatePlan();
            otherPath.FullTextSearchTerms[0].Path[0] = "title";
            cache.Add("rid", "etag", otherPath, Statistics(0, 0, 0), clock.Now);
            Assert.IsFalse(cache.TryGet("rid", "etag", CreatePlan(), NoOpTrace.Singleton, out _));
            Assert.IsTrue(cache.TryGet("rid", "etag", otherPath, NoOpTrace.Singleton, out _));
        }

        [TestMethod]
        public void InvalidDimensionsAreNotAdmitted()
        {
            TestClock clock = new TestClock();
            Cache cache = Cache.Create(TimeSpan.FromMinutes(1), 8, clock.Read);
            cache.Add("rid", "etag", CreatePlan(), Statistics(10, 100, 2), clock.Now);
            clock.Now = TimeSpan.FromSeconds(1);
            HybridSearchQueryInfo twoPaths = CreatePlan();
            twoPaths.FullTextSearchTerms.Add(CreatePlan("swim").FullTextSearchTerms[0]);
            List<StatisticsShapeTestCase> testCases = new List<StatisticsShapeTestCase>
            {
                new StatisticsShapeTestCase("Missing path statistics", twoPaths, CreateStatistics(20)),
                new StatisticsShapeTestCase("Empty statistics", CreatePlan(), new GlobalFullTextSearchStatistics(20, Array.Empty<FullTextStatistics>())),
                new StatisticsShapeTestCase("Null path statistics", CreatePlan(), new GlobalFullTextSearchStatistics(20, new FullTextStatistics[] { null })),
                new StatisticsShapeTestCase("Missing term count", CreatePlan("run", "swim"), CreateStatistics(20)),
                new StatisticsShapeTestCase("Extra term count", CreatePlan(), Statistics(20, 200, 1, 2)),
            };

            for (int index = 0; index < testCases.Count; index++)
            {
                StatisticsShapeTestCase testCase = testCases[index];
                this.TestContext.WriteLine($"Case {index}: {testCase}");
                cache.Add("rid", "etag", testCase.Plan, testCase.Statistics, clock.Now);
                Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan(), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics original));
                Assert.AreEqual(10L, original.DocumentCount);
                Assert.AreEqual(100L, original.FullTextStatistics[0].TotalWordCount);
                CollectionAssert.AreEqual(new long[] { 2 }, original.FullTextStatistics[0].HitCounts.ToArray());
                Assert.AreEqual(3, StoredCount(cache));
            }
        }

        [TestMethod]
        public void CountsAreIsolatedFromInputStatisticsAndReturnedSnapshots()
        {
            Cache cache = Cache.Create(TimeSpan.FromMinutes(1), 4, () => TimeSpan.Zero);
            HybridSearchQueryInfo plan = CreatePlan("swim", "run");
            long[] hits = { 2, 3 };
            List<FullTextStatistics> paths = new List<FullTextStatistics> { new FullTextStatistics(100, hits) };
            cache.Add("rid", "etag", plan, new GlobalFullTextSearchStatistics(10, paths), TimeSpan.Zero);
            hits[0] = 999;
            paths.Clear();
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("swim", "run"), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics first));
            CollectionAssert.AreEqual(new long[] { 2, 3 }, first.FullTextStatistics[0].HitCounts.ToArray());
            Assert.IsTrue(MemoryMarshal.TryGetArray(first.FullTextStatistics[0].HitCounts, out ArraySegment<long> returned));
            returned.Array[returned.Offset] = 999;
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("swim", "run"), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics second));
            Assert.AreNotSame(first, second);
            CollectionAssert.AreEqual(new long[] { 2, 3 }, second.FullTextStatistics[0].HitCounts.ToArray());
        }

        [TestMethod]
        public void DistinctPathsTermsAndDistancesDoNotShareCounts()
        {
            List<MetadataTestCase> testCases = new List<MetadataTestCase>
            {
                new MetadataTestCase("Different path", @"[{""path"":[""title""],""terms"":[{""term"":""swim"",""distance"":2}]}]"),
                new MetadataTestCase("Path casing", @"[{""path"":[""Text""],""terms"":[{""term"":""swim"",""distance"":2}]}]"),
                new MetadataTestCase("Term casing", @"[{""path"":[""text""],""terms"":[{""term"":""Swim"",""distance"":2}]}]"),
                new MetadataTestCase("Different distance", @"[{""path"":[""text""],""terms"":[{""term"":""swim"",""distance"":1}]}]"),
                new MetadataTestCase("Missing distance", @"[{""path"":[""text""],""terms"":[{""term"":""swim""}]}]"),
                new MetadataTestCase("Zero versus missing distance", @"[{""path"":[""text""],""terms"":[{""term"":""run"",""distance"":0}]}]"),
                new MetadataTestCase("Phrase versus separate terms", @"[{""path"":[""text""],""terms"":[{""term"":""swim run"",""distance"":2}]}]"),
            };
            for (int index = 0; index < testCases.Count; index++)
            {
                MetadataTestCase testCase = testCases[index];
                this.TestContext.WriteLine($"Case {index}: {testCase}");
                Cache cache = Cache.Create(TimeSpan.FromMinutes(1), 4, () => TimeSpan.Zero);
                HybridSearchQueryInfo original = ParsePlan(@"[{""path"":[""text""],""terms"":[{""term"":""swim"",""distance"":2},{""term"":""run""}]}]");
                cache.Add("rid", "etag", original, Statistics(10, 100, 2, 3), TimeSpan.Zero);
                Assert.IsFalse(cache.TryGet("rid", "etag", ParsePlan(testCase.Metadata), NoOpTrace.Singleton, out _));
            }
        }

        [TestMethod]
        public void PathSegmentsPhrasesAndDateLikeStringsRemainExact()
        {
            Cache cache = Cache.Create(TimeSpan.FromMinutes(1), 9, () => TimeSpan.Zero);
            List<MetadataTestCase> testCases = new List<MetadataTestCase>
            {
                new MetadataTestCase("Slash in a single segment", @"[{""path"":[""a/b""],""terms"":[{""term"":""United States""}]}]"),
                new MetadataTestCase("Separate path segments", @"[{""path"":[""a"",""b""],""terms"":[{""term"":""United States""}]}]"),
                new MetadataTestCase("Numeric indexer and empty term", @"[{""path"":[""tags"",""[0]""],""terms"":[{""term"":""""}]}]"),
                new MetadataTestCase("Date-like strings and maximum distance", @"[{""path"":[""2026-10-20T12:00:00Z""],""terms"":[{""term"":""2026-10-20T12:00:00+01:00"",""distance"":2147483647}]}]"),
            };
            for (int index = 0; index < testCases.Count; index++)
            {
                MetadataTestCase testCase = testCases[index];
                this.TestContext.WriteLine($"Publish case {index}: {testCase}, HitCount={index}");
                cache.Add("rid", "etag", ParsePlan(testCase.Metadata), Statistics(10, 100, index), TimeSpan.Zero);
            }

            for (int index = 0; index < testCases.Count; index++)
            {
                MetadataTestCase testCase = testCases[index];
                this.TestContext.WriteLine($"Read case {index}: {testCase}, ExpectedHitCount={index}");
                Assert.IsTrue(cache.TryGet("rid", "etag", ParsePlan(testCase.Metadata), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics result));
                CollectionAssert.AreEqual(new long[] { index }, result.FullTextStatistics[0].HitCounts.ToArray());
            }

            Assert.AreEqual(9, StoredCount(cache));
        }

        [TestMethod]
        public void EquivalentMetadataUsesNormalQueryPlanDeserialization()
        {
            List<MetadataTestCase> testCases = new List<MetadataTestCase>
            {
                new MetadataTestCase("Property ordering", @"[{""terms"":[{""distance"":2,""term"":""swim""},{""term"":""run""}],""path"":[""text""]}]"),
                new MetadataTestCase("Null distance", @"[{""path"":[""text""],""terms"":[{""term"":""swim"",""distance"":2},{""term"":""run"",""distance"":null}]}]"),
                new MetadataTestCase("String distance", @"[{""path"":[""text""],""terms"":[{""term"":""swim"",""distance"":""2""},{""term"":""run""}]}]"),
                new MetadataTestCase("Unknown fields", @"[{""path"":[""text""],""terms"":[{""term"":""swim"",""distance"":2,""future"":true},{""term"":""run""}],""future"":true}]"),
            };
            for (int index = 0; index < testCases.Count; index++)
            {
                MetadataTestCase testCase = testCases[index];
                this.TestContext.WriteLine($"Case {index}: {testCase}");
                Cache cache = Cache.Create(TimeSpan.FromMinutes(1), 4, () => TimeSpan.Zero);
                HybridSearchQueryInfo plan = ParsePlan(@"[{""path"":[""text""],""terms"":[{""term"":""swim"",""distance"":2},{""term"":""run""}]}]");
                cache.Add("rid", "etag", plan, Statistics(10, 100, 2, 3), TimeSpan.Zero);
                Assert.IsTrue(cache.TryGet("rid", "etag", ParsePlan(testCase.Metadata), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics result));
                CollectionAssert.AreEqual(new long[] { 2, 3 }, result.FullTextStatistics[0].HitCounts.ToArray());
            }
        }

        [TestMethod]
        public void IdentityIgnoresStatisticsQueryTextAndCurrentCulture()
        {
            Cache cache = Cache.Create(TimeSpan.FromMinutes(1), 3, () => TimeSpan.Zero);
            HybridSearchQueryInfo plan = CreatePlan();
            plan.GlobalStatisticsQuery = "SELECT COUNT(1) FROM c";
            cache.Add("rid", "etag", plan, CreateStatistics(), TimeSpan.Zero);
            plan.GlobalStatisticsQuery = null;
            CultureInfo original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                Assert.IsTrue(cache.TryGet("rid", "etag", plan, NoOpTrace.Singleton, out _));
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        [TestMethod]
        public void ExpiryIsAbsoluteAndUsesCollectionStart()
        {
            TestClock clock = new TestClock { Now = TimeSpan.FromSeconds(8) };
            Cache cache = Cache.Create(TimeSpan.FromSeconds(10), 3, clock.Read);
            cache.Add("rid", "etag", CreatePlan(), CreateStatistics(), TimeSpan.Zero);
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan(), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics selected));
            clock.Now = TimeSpan.FromSeconds(10) - TimeSpan.FromTicks(1);
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan(), NoOpTrace.Singleton, out _));
            clock.Now = TimeSpan.FromSeconds(10);
            Assert.IsFalse(cache.TryGet("rid", "etag", CreatePlan(), NoOpTrace.Singleton, out _));
            Assert.AreEqual(10L, selected.DocumentCount);
        }

        [TestMethod]
        public void ExpiredOrFutureCollectionsAreNotAdmitted()
        {
            TestClock clock = new TestClock { Now = TimeSpan.FromSeconds(10) };
            Cache cache = Cache.Create(TimeSpan.FromSeconds(10), 3, clock.Read);
            cache.Add("rid", "etag", CreatePlan(), CreateStatistics(), TimeSpan.Zero);
            cache.Add("rid", "etag", CreatePlan(), CreateStatistics(), clock.Now + TimeSpan.FromTicks(1));
            Assert.AreEqual(0, StoredCount(cache));
        }

        [TestMethod]
        public void LargeAgesAndTimestampsDoNotOverflow()
        {
            TestClock clock = new TestClock { Now = TimeSpan.MaxValue - TimeSpan.FromTicks(1) };
            Cache cache = Cache.Create(TimeSpan.MaxValue, 3, clock.Read);
            cache.Add("rid", "etag", CreatePlan(), CreateStatistics(), TimeSpan.Zero);
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan(), NoOpTrace.Singleton, out _));
            clock.Now = TimeSpan.MaxValue;
            Assert.IsFalse(cache.TryGet("rid", "etag", CreatePlan(), NoOpTrace.Singleton, out _));
            cache.Add("rid", "etag", CreatePlan(), CreateStatistics(), TimeSpan.MaxValue - TimeSpan.FromTicks(5));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan(), NoOpTrace.Singleton, out _));
        }

        [TestMethod]
        public void SeparateCachesHaveIndependentPoliciesAndContents()
        {
            TestClock clock = new TestClock();
            Cache shortLived = Cache.Create(TimeSpan.FromSeconds(5), 3, clock.Read);
            Cache longLived = Cache.Create(TimeSpan.FromSeconds(10), 3, clock.Read);
            shortLived.Add("rid", "etag", CreatePlan(), CreateStatistics(), clock.Now);
            Assert.IsFalse(longLived.TryGet("rid", "etag", CreatePlan(), NoOpTrace.Singleton, out _));
            longLived.Add("rid", "etag", CreatePlan(), CreateStatistics(20), clock.Now);
            clock.Now = TimeSpan.FromSeconds(5);
            Assert.IsFalse(shortLived.TryGet("rid", "etag", CreatePlan(), NoOpTrace.Singleton, out _));
            Assert.IsTrue(longLived.TryGet("rid", "etag", CreatePlan(), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics result));
            Assert.AreEqual(20L, result.DocumentCount);
        }

        [TestMethod]
        public void CapacityCountsAllScalarKindsAcrossContainersAndRevisions()
        {
            TestClock clock = new TestClock();
            Cache cache = Cache.Create(TimeSpan.FromMinutes(1), 8, clock.Read);
            cache.Add("rid", "etag", CreatePlan("run", "swim"), Statistics(10, 100, 2, 3), clock.Now);
            clock.Now = TimeSpan.FromSeconds(1);
            cache.Add("rid", "other-etag", CreatePlan(), CreateStatistics(), clock.Now);
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("run", "swim"), NoOpTrace.Singleton, out _));
            clock.Now = TimeSpan.FromSeconds(2);
            HybridSearchQueryInfo otherPath = CreatePlan("energy");
            otherPath.FullTextSearchTerms[0].Path[0] = "title";
            cache.Add("other-rid", "etag", otherPath, CreateStatistics(), clock.Now);
            Assert.AreEqual(8, StoredCount(cache));
            Assert.IsFalse(cache.TryGet("rid", "etag", CreatePlan("run", "swim"), NoOpTrace.Singleton, out _));
            Assert.IsTrue(cache.TryGet("rid", "other-etag", CreatePlan(), NoOpTrace.Singleton, out _));
            Assert.IsTrue(cache.TryGet("other-rid", "etag", otherPath, NoOpTrace.Singleton, out _));
        }

        [TestMethod]
        public void PublicationPreservesItsOverlappingTermsWhenMakingRoom()
        {
            TestClock clock = new TestClock();
            Cache cache = Cache.Create(TimeSpan.FromMinutes(1), 4, clock.Read);
            cache.Add("rid", "etag", CreatePlan("run"), Statistics(10, 100, 2), clock.Now);
            clock.Now = TimeSpan.FromSeconds(1);
            cache.Add("rid", "etag", CreatePlan("bike"), Statistics(10, 100, 4), clock.Now);
            clock.Now = TimeSpan.FromSeconds(2);
            cache.Add("rid", "etag", CreatePlan("run", "swim"), Statistics(10, 100, 3, 5), clock.Now);
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("run", "swim"), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics result));
            CollectionAssert.AreEqual(new long[] { 3, 5 }, result.FullTextStatistics[0].HitCounts.ToArray());
            Assert.IsFalse(cache.TryGet("rid", "etag", CreatePlan("bike"), NoOpTrace.Singleton, out _));
            Assert.AreEqual(4, StoredCount(cache));
        }

        [TestMethod]
        public void RepeatedSlotsShareEntriesAndOversizedPublicationsAreSkipped()
        {
            Cache cache = Cache.Create(TimeSpan.FromMinutes(1), 6, () => TimeSpan.Zero);
            HybridSearchQueryInfo repeated = CreatePlan("run", "run");
            repeated.FullTextSearchTerms.Add(CreatePlan("run").FullTextSearchTerms[0]);
            cache.Add("rid", "etag", repeated, new GlobalFullTextSearchStatistics(10, new[]
            {
                new FullTextStatistics(100, new long[] { 2, 2 }),
                new FullTextStatistics(100, new long[] { 2 }),
            }), TimeSpan.Zero);
            Assert.AreEqual(3, StoredCount(cache));
            Assert.IsTrue(cache.TryGet("rid", "etag", repeated, NoOpTrace.Singleton, out _));
            cache.Add("other", "etag", CreatePlan("a", "b", "c", "d", "e", "f"), Statistics(20, 200, 1, 1, 1, 1, 1, 1), TimeSpan.Zero);
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan(), NoOpTrace.Singleton, out _));
            Assert.AreEqual(3, StoredCount(cache));
        }

        [TestMethod]
        public void CapacityMustFitTheCompleteScalarSet()
        {
            List<SettingsTestCase> testCases = new List<SettingsTestCase>
            {
                new SettingsTestCase("Document count only", TimeSpan.FromMinutes(1).Ticks, 1),
                new SettingsTestCase("Document and word counts only", TimeSpan.FromMinutes(1).Ticks, 2),
            };
            for (int index = 0; index < testCases.Count; index++)
            {
                SettingsTestCase testCase = testCases[index];
                this.TestContext.WriteLine($"Case {index}: {testCase}");
                Cache cache = Cache.Create(TimeSpan.FromTicks(testCase.MaxAgeTicks), testCase.MaxEntries, () => TimeSpan.Zero);
                cache.Add("rid", "etag", CreatePlan(), CreateStatistics(), TimeSpan.Zero);
                Assert.IsFalse(cache.TryGet("rid", "etag", CreatePlan(), NoOpTrace.Singleton, out _));
                Assert.AreEqual(0, StoredCount(cache));
            }
        }

        [TestMethod]
        public void EvictedRequiredScalarsCauseAMissWithoutDiscardingOtherLiveEntries()
        {
            TestClock clock = new TestClock();
            Cache cache = Cache.Create(TimeSpan.FromMinutes(1), 4, clock.Read);
            cache.Add("first", "etag", CreatePlan(), CreateStatistics(), clock.Now);
            clock.Now = TimeSpan.FromSeconds(1);
            cache.Add("second", "etag", CreatePlan(), CreateStatistics(20), clock.Now);
            Assert.AreEqual(4, StoredCount(cache));
            Assert.IsFalse(cache.TryGet("first", "etag", CreatePlan(), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics missing));
            Assert.IsNull(missing);
            Assert.IsTrue(cache.TryGet("second", "etag", CreatePlan(), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics present));
            Assert.AreEqual(20L, present.DocumentCount);
            Assert.AreEqual(4, StoredCount(cache), "Independent live scalars are retained even if related counters were evicted.");
        }

        [TestMethod]
        public void EvictionUsesCollectionTimeRatherThanPublicationOrder()
        {
            Cache cache = Cache.Create(TimeSpan.FromMinutes(1), 4, () => TimeSpan.FromSeconds(10));
            cache.Add("rid", "etag", CreatePlan("newer"), CreateStatistics(), TimeSpan.FromSeconds(5));
            cache.Add("rid", "etag", CreatePlan("older"), CreateStatistics(), TimeSpan.FromSeconds(1));
            cache.Add("rid", "etag", CreatePlan("latest"), CreateStatistics(), TimeSpan.FromSeconds(8));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("newer"), NoOpTrace.Singleton, out _));
            Assert.IsFalse(cache.TryGet("rid", "etag", CreatePlan("older"), NoOpTrace.Singleton, out _));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("latest"), NoOpTrace.Singleton, out _));
        }

        [TestMethod]
        public void MinimumCompleteCapacityEvictsOlderCounters()
        {
            List<ContainerIdentityTestCase> testCases = new List<ContainerIdentityTestCase>
            {
                new ContainerIdentityTestCase("Same container", "rid", "etag", 10),
                new ContainerIdentityTestCase("Different container", "other-rid", "etag", 10),
            };
            for (int index = 0; index < testCases.Count; index++)
            {
                ContainerIdentityTestCase testCase = testCases[index];
                this.TestContext.WriteLine($"Case {index}: {testCase}");
                Cache cache = Cache.Create(TimeSpan.FromMinutes(1), 3, () => TimeSpan.FromSeconds(1));
                cache.Add("rid", "etag", CreatePlan("run"), CreateStatistics(), TimeSpan.Zero);
                cache.Add(testCase.ResourceId, testCase.ETag, CreatePlan("swim"), CreateStatistics(testCase.DocumentCount), TimeSpan.FromSeconds(1));
                Assert.IsFalse(cache.TryGet("rid", "etag", CreatePlan("run"), NoOpTrace.Singleton, out _));
                Assert.IsTrue(cache.TryGet(testCase.ResourceId, testCase.ETag, CreatePlan("swim"), NoOpTrace.Singleton, out _));
                Assert.AreEqual(3, StoredCount(cache));
            }
        }

        [TestMethod]
        public void InsertionRemovesExpiredScalarsBeforeEviction()
        {
            TestClock clock = new TestClock();
            Cache cache = Cache.Create(TimeSpan.FromSeconds(10), 10, clock.Read);
            cache.Add("old", "etag", CreatePlan("run", "swim"), Statistics(10, 100, 2, 3), clock.Now);
            cache.Add("old", "other-etag", CreatePlan(), CreateStatistics(), clock.Now);
            clock.Now = TimeSpan.FromSeconds(5);
            cache.Add("fresh", "etag", CreatePlan(), CreateStatistics(), clock.Now);
            clock.Now = TimeSpan.FromSeconds(10);
            cache.Add("new", "etag", CreatePlan(), CreateStatistics(), clock.Now);
            Assert.IsTrue(cache.TryGet("fresh", "etag", CreatePlan(), NoOpTrace.Singleton, out _));
            Assert.AreEqual(6, StoredCount(cache));
        }

        [TestMethod]
        public void OlderPublicationsOnlyFillMissingCountsWithoutRegressingNewerOnes()
        {
            TestClock clock = new TestClock { Now = TimeSpan.FromSeconds(10) };
            Cache cache = Cache.Create(TimeSpan.FromSeconds(10), 4, clock.Read);
            cache.Add("rid", "etag", CreatePlan("run"), Statistics(20, 200, 8), TimeSpan.FromSeconds(5));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan(), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics selected));
            cache.Add("rid", "etag", CreatePlan("run", "swim"), Statistics(10, 100, 2, 3), TimeSpan.FromSeconds(1));
            cache.Add("rid", "etag", CreatePlan("run"), Statistics(20, 200, 9), TimeSpan.FromSeconds(5));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("run", "swim"), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics result));
            Assert.AreEqual(20L, result.DocumentCount);
            Assert.AreEqual(200L, result.FullTextStatistics[0].TotalWordCount);
            CollectionAssert.AreEqual(new long[] { 8, 3 }, result.FullTextStatistics[0].HitCounts.ToArray());
            Assert.AreEqual(20L, selected.DocumentCount);
            Assert.AreEqual(4, StoredCount(cache));
            clock.Now = TimeSpan.FromSeconds(11);
            Assert.IsFalse(cache.TryGet("rid", "etag", CreatePlan("swim"), NoOpTrace.Singleton, out _));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("run"), NoOpTrace.Singleton, out _));
        }

        [TestMethod]
        public void LateOldRevisionPublicationDoesNotReplaceTheNewRevision()
        {
            Cache cache = Cache.Create(TimeSpan.FromMinutes(1), 6, () => TimeSpan.FromSeconds(10));
            cache.Add("rid", "new", CreatePlan(), CreateStatistics(20), TimeSpan.FromSeconds(5));
            cache.Add("rid", "old", CreatePlan(), CreateStatistics(10), TimeSpan.Zero);
            Assert.IsTrue(cache.TryGet("rid", "new", CreatePlan(), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics current));
            Assert.AreEqual(20L, current.DocumentCount);
            Assert.IsTrue(cache.TryGet("rid", "old", CreatePlan(), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics old));
            Assert.AreEqual(10L, old.DocumentCount);
        }

        [TestMethod]
        public async Task ConcurrentPublicationKeepsTheNewestCounts()
        {
            Cache cache = Cache.Create(TimeSpan.FromMinutes(1), 4, () => TimeSpan.FromSeconds(10));
            await Task.WhenAll(Enumerable.Range(0, 64).Select(index => Task.Run(() =>
            {
                HybridSearchQueryInfo plan = CreatePlan("run", "run");
                cache.Add("rid", "etag", plan, Statistics(index, index * 10, index, index), TimeSpan.FromTicks(index));
                if (cache.TryGet("rid", "etag", plan, NoOpTrace.Singleton, out GlobalFullTextSearchStatistics result))
                {
                    Assert.IsTrue(result.FullTextStatistics[0].HitCounts.Span[0] <= result.DocumentCount,
                        $"Publication {index}: a lookup combining independent counts must still satisfy numeric invariants.");
                }
            })));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan(), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics newest));
            Assert.AreEqual(63L, newest.DocumentCount);
            Assert.AreEqual(630L, newest.FullTextStatistics[0].TotalWordCount);
            Assert.AreEqual(63L, newest.FullTextStatistics[0].HitCounts.Span[0]);
            Assert.AreEqual(3, StoredCount(cache));
        }

        [TestMethod]
        public async Task ConcurrentContainerOperationsUseBestEffortCapacity()
        {
            Cache cache = Cache.Create(TimeSpan.FromMinutes(1), 8, () => TimeSpan.FromSeconds(10));
            await Task.WhenAll(Enumerable.Range(0, 64).Select(index => Task.Run(() =>
            {
                string resourceId = (index % 4).ToString();
                HybridSearchQueryInfo plan = CreatePlan(index.ToString());
                cache.Add(resourceId, "etag", plan, Statistics(100, 1000, index), TimeSpan.FromTicks(index));
                if (cache.TryGet(resourceId, "etag", plan, NoOpTrace.Singleton, out GlobalFullTextSearchStatistics result))
                {
                    Assert.AreEqual((long)index, result.FullTextStatistics[0].HitCounts.Span[0]);
                }
            })));
            cache.Add("settled", "etag", CreatePlan(), Statistics(100, 1000, 1), TimeSpan.FromSeconds(1));
            Assert.IsTrue(StoredCount(cache) <= 8, "A publication without concurrent writers must bring retention within the target.");
        }

        [TestMethod]
        public async Task ConcurrentOverlappingPublicationsRemainConsistentAndRecoverCapacity()
        {
            Cache cache = Cache.Create(TimeSpan.FromMinutes(1), 8, () => TimeSpan.FromSeconds(10));
            await Task.WhenAll(Enumerable.Range(0, 64).Select(index => Task.Run(() =>
            {
                HybridSearchQueryInfo plan = CreatePlan("shared", index.ToString());
                cache.Add("rid", "etag", plan, Statistics(100, 1000, index, index), TimeSpan.FromTicks(index));
                if (cache.TryGet("rid", "etag", CreatePlan("shared"), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics selected))
                {
                    Assert.IsTrue(selected.FullTextStatistics[0].HitCounts.Span[0] <= selected.DocumentCount,
                        $"Publication {index}: a concurrent hit must contain numerically compatible counts.");
                }
            })));

            // Concurrent eviction may remove a just-refreshed shared count; one settled publication restores it.
            cache.Add("rid", "etag", CreatePlan("shared"), Statistics(100, 1000, 63), TimeSpan.FromSeconds(1));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("shared"), NoOpTrace.Singleton, out GlobalFullTextSearchStatistics result));
            CollectionAssert.AreEqual(new long[] { 63 }, result.FullTextStatistics[0].HitCounts.ToArray());
            Assert.IsTrue(StoredCount(cache) <= 8, "A publication without concurrent writers must bring retention within the target.");
        }

        [TestMethod]
        public void NativePlansReuseSubsetsAndRegroupCountsAcrossQueryAndParameterChanges()
        {
            Cache cache = Cache.Create(TimeSpan.FromMinutes(1), 4, () => TimeSpan.Zero);
            HybridSearchQueryInfo first = GetNativePlan(new SqlQuerySpec(
                "SELECT TOP 10 c.id FROM c ORDER BY RANK RRF(FullTextScore(c.text, @first, @second), VectorDistance(c.image, @vector))",
                new SqlParameterCollection
                {
                    new SqlParameter("@first", "swim"),
                    new SqlParameter("@second", "run"),
                    new SqlParameter("@vector", new[] { 1.0, 2.0, 3.0 }),
                    new SqlParameter("@unused", 1),
                }));
            cache.Add("rid", "etag", first, Statistics(10, 100, 2, 3), TimeSpan.Zero);
            HybridSearchQueryInfo subset = GetNativePlan(new SqlQuerySpec(
                "SELECT TOP 20 c.title FROM c ORDER BY RANK RRF(FullTextScore(c.text, @term), VectorDistance(c.image, @vector))",
                new SqlParameterCollection
                {
                    new SqlParameter("@unused", "different"),
                    new SqlParameter("@vector", new[] { 3.0, 2.0, 1.0 }),
                    new SqlParameter("@term", "run"),
                }));
            Assert.IsTrue(cache.TryGet("rid", "etag", subset, NoOpTrace.Singleton, out GlobalFullTextSearchStatistics subsetResult));
            CollectionAssert.AreEqual(new long[] { 3 }, subsetResult.FullTextStatistics[0].HitCounts.ToArray());

            HybridSearchQueryInfo regrouped = GetNativePlan(new SqlQuerySpec(
                "SELECT TOP 10 * FROM c ORDER BY RANK RRF(FullTextScore(c.text, 'run'), FullTextScore(c.text, 'swim', 'run'))"));
            Assert.IsTrue(cache.TryGet("rid", "etag", regrouped, NoOpTrace.Singleton, out GlobalFullTextSearchStatistics groupedResult));
            Assert.AreEqual(2, groupedResult.FullTextStatistics.Count);
            CollectionAssert.AreEqual(new long[] { 3 }, groupedResult.FullTextStatistics[0].HitCounts.ToArray());
            CollectionAssert.AreEqual(new long[] { 2, 3 }, groupedResult.FullTextStatistics[1].HitCounts.ToArray());
        }

        private static HybridSearchQueryInfo GetNativePlan(SqlQuerySpec query)
        {
            string serializedQuery;
            using (Stream stream = new CosmosSerializerCore().ToStreamSqlQuerySpec(query, ResourceType.Document))
            using (StreamReader reader = new StreamReader(stream))
            {
                serializedQuery = reader.ReadToEnd();
            }

            TryCatch<PartitionedQueryExecutionInfo> result = QueryPartitionProviderTestInstance.Object.TryGetPartitionedQueryExecutionInfo(
                serializedQuery,
                new PartitionKeyDefinition { Paths = new Collection<string> { "/id" }, Kind = PartitionKind.Hash },
                vectorEmbeddingPolicy: null,
                requireFormattableOrderByQuery: true,
                isContinuationExpected: false,
                allowNonValueAggregateQuery: true,
                hasLogicalPartitionKey: false,
                allowDCount: true,
                useSystemPrefix: false,
                hybridSearchSkipOrderByRewrite: false,
                geospatialType: Cosmos.GeospatialType.Geography);
            Assert.IsTrue(result.Succeeded, result.Failed ? result.Exception.ToString() : string.Empty);
            return result.Result.HybridSearchQueryInfo;
        }

        private static HybridSearchQueryInfo ParsePlan(string metadata)
        {
            string property = metadata == null ? string.Empty : ",\"fullTextSearchTerms\":" + metadata;
            string json = "{\"hybridSearchQueryInfo\":{\"requiresGlobalStatistics\":true" + property + "}}";
            Assert.IsTrue(PartitionedQueryExecutionInfo.TryParse(json, out PartitionedQueryExecutionInfo plan));
            return plan.HybridSearchQueryInfo;
        }

        private static HybridSearchQueryInfo CreatePlan(params string[] terms)
        {
            return new HybridSearchQueryInfo
            {
                RequiresGlobalStatistics = true,
                FullTextSearchTerms = new List<FullTextSearchTerms>
                {
                    new FullTextSearchTerms
                    {
                        Path = new List<string> { "text" },
                        Terms = (terms.Length == 0 ? new[] { "run" } : terms)
                            .Select(term => new FullTextSearchTerm { Term = term }).ToList(),
                    },
                },
            };
        }

        private static GlobalFullTextSearchStatistics CreateStatistics(long documentCount = 10)
        {
            return Statistics(documentCount, documentCount * 10, documentCount == 0 ? 0 : 1);
        }

        private static GlobalFullTextSearchStatistics Statistics(long documents, long words, params long[] hits)
        {
            return new GlobalFullTextSearchStatistics(documents, new[] { new FullTextStatistics(words, hits) });
        }

        private static int StoredCount(Cache cache)
        {
            FieldInfo field = cache.GetType().GetField("counts", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            return ((ICollection)field.GetValue(cache)).Count;
        }

        private readonly struct SettingsTestCase
        {
            public SettingsTestCase(string name, long maxAgeTicks, int maxEntries)
            {
                this.Name = name;
                this.MaxAgeTicks = maxAgeTicks;
                this.MaxEntries = maxEntries;
            }

            public string Name { get; }

            public long MaxAgeTicks { get; }

            public int MaxEntries { get; }

            public override string ToString() => $"{this.Name}: MaxAgeTicks={this.MaxAgeTicks}, MaxEntries={this.MaxEntries}";
        }

        private readonly struct ContainerIdentityTestCase
        {
            public ContainerIdentityTestCase(string name, string resourceId, string etag, long documentCount)
            {
                this.Name = name;
                this.ResourceId = resourceId;
                this.ETag = etag;
                this.DocumentCount = documentCount;
            }

            public string Name { get; }

            public string ResourceId { get; }

            public string ETag { get; }

            public long DocumentCount { get; }

            public override string ToString() =>
                $"{this.Name}: RID={this.ResourceId ?? "<null>"}, ETag={this.ETag ?? "<null>"}, DocumentCount={this.DocumentCount}";
        }

        private readonly struct MetadataTestCase
        {
            public MetadataTestCase(string name, string metadata)
            {
                this.Name = name;
                this.Metadata = metadata;
            }

            public string Name { get; }

            public string Metadata { get; }

            public override string ToString() => $"{this.Name}: Metadata={this.Metadata ?? "<absent>"}";
        }

        private readonly struct StatisticsShapeTestCase
        {
            public StatisticsShapeTestCase(string name, HybridSearchQueryInfo plan, GlobalFullTextSearchStatistics statistics)
            {
                this.Name = name;
                this.Plan = plan;
                this.Statistics = statistics;
            }

            public string Name { get; }

            public HybridSearchQueryInfo Plan { get; }

            public GlobalFullTextSearchStatistics Statistics { get; }

            public override string ToString() =>
                $"{this.Name}: PlanTermCounts=[{string.Join(",", this.Plan.FullTextSearchTerms.Select(path => path.Terms.Count))}], " +
                $"DocumentCount={this.Statistics.DocumentCount}, Statistics=[" +
                string.Join(";", this.Statistics.FullTextStatistics.Select(path => path == null
                    ? "<null>"
                    : $"Words={path.TotalWordCount}, Hits=[{string.Join(",", path.HitCounts.ToArray())}]")) + "]";
        }

        private sealed class TestClock
        {
            public TimeSpan Now { get; set; }

            public TimeSpan Read() => this.Now;
        }
    }
}
