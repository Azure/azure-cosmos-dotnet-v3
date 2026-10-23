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
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Cache = Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch.HybridSearchStatisticsCache;

    [TestClass]
    public class HybridSearchStatisticsCacheTests
    {
        [DataTestMethod]
        [DataRow(0L, 1)]
        [DataRow(-1L, 1)]
        [DataRow(1L, 0)]
        [DataRow(1L, -1)]
        public void InvalidSettingsAreRejected(long maxAgeTicks, int maxEntries)
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => new Cache(TimeSpan.FromTicks(maxAgeTicks), maxEntries));
        }

        [TestMethod]
        public void DefaultClockIsMonotonic()
        {
            using Cache cache = new Cache(TimeSpan.MaxValue, 1);
            TimeSpan startedAt = cache.GetElapsedTime();
            Assert.IsTrue(startedAt >= TimeSpan.Zero);
            Assert.IsTrue(cache.GetElapsedTime() >= startedAt);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan(), CreateStatistics(), startedAt));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan(), out GlobalFullTextSearchStatistics result));
            Assert.AreEqual(10L, result.DocumentCount);
        }

        [TestMethod]
        public void MissingEntriesDoNotCreateContainerBuckets()
        {
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 2, () => TimeSpan.Zero);
            for (int index = 0; index < 10; index++)
            {
                Assert.IsFalse(cache.TryGet(index.ToString(), "etag", CreatePlan(), out GlobalFullTextSearchStatistics result));
                Assert.IsNull(result);
            }

            Assert.AreEqual(0, cache.Count);
            Assert.AreEqual(0, cache.ContainerRevisionCount);
        }

        [DataTestMethod]
        [DataRow(null, "etag")]
        [DataRow("", "etag")]
        [DataRow("rid", null)]
        [DataRow("rid", "")]
        public void MissingContainerIdentityBypassesStorage(string resourceId, string etag)
        {
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 1, () => TimeSpan.Zero);
            Assert.IsFalse(cache.TryGet(resourceId, etag, CreatePlan(), out GlobalFullTextSearchStatistics result));
            Assert.IsNull(result);
            Assert.IsFalse(cache.TryAdd(resourceId, etag, CreatePlan(), CreateStatistics(), TimeSpan.Zero));
            Assert.AreEqual(0, cache.Count);
            Assert.AreEqual(0, cache.ContainerRevisionCount);
        }

        [TestMethod]
        public void InvalidArgumentsAreRejected()
        {
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 1, () => TimeSpan.Zero);
            Assert.ThrowsException<ArgumentNullException>(() => cache.TryGet("rid", "etag", null, out _));
            Assert.ThrowsException<ArgumentNullException>(() => cache.TryAdd("rid", "etag", null, CreateStatistics(), TimeSpan.Zero));
            Assert.ThrowsException<ArgumentNullException>(() => cache.TryAdd("rid", "etag", CreatePlan(), null, TimeSpan.Zero));
            Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => cache.TryAdd("rid", "etag", CreatePlan(), CreateStatistics(), TimeSpan.FromTicks(-1)));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("null")]
        [DataRow("[]")]
        [DataRow("[null]")]
        [DataRow("[{}]")]
        [DataRow(@"[{""terms"":[{""term"":""run""}]}]")]
        [DataRow(@"[{""path"":null,""terms"":[{""term"":""run""}]}]")]
        [DataRow(@"[{""path"":[],""terms"":[{""term"":""run""}]}]")]
        [DataRow(@"[{""path"":[null],""terms"":[{""term"":""run""}]}]")]
        [DataRow(@"[{""path"":[""text""]}]")]
        [DataRow(@"[{""path"":[""text""],""terms"":null}]")]
        [DataRow(@"[{""path"":[""text""],""terms"":[]}]")]
        [DataRow(@"[{""path"":[""text""],""terms"":[null]}]")]
        [DataRow(@"[{""path"":[""text""],""terms"":[{}]}]")]
        [DataRow(@"[{""path"":[""text""],""terms"":[{""term"":null}]}]")]
        [DataRow(@"[{""path"":[""text""],""terms"":[{""parameter"":""@term""}]}]")]
        [DataRow(@"[{""path"":[""text""],""terms"":[{""term"":""run"",""distance"":-1}]}]")]
        public void MissingOrIncompleteMetadataBypassesLookupAndAdmission(string metadata)
        {
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 2, () => TimeSpan.Zero);
            HybridSearchQueryInfo plan = ParsePlan(metadata);
            Assert.IsFalse(cache.TryGet("rid", "etag", plan, out GlobalFullTextSearchStatistics result));
            Assert.IsNull(result);
            Assert.IsFalse(cache.TryAdd("rid", "etag", plan, CreateStatistics(), TimeSpan.Zero));
            Assert.AreEqual(0, cache.Count);
            Assert.AreEqual(0, cache.ContainerRevisionCount);
        }

        [TestMethod]
        public void UnneededStatisticsBypassesLookupAndAdmission()
        {
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 1, () => TimeSpan.Zero);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan(), CreateStatistics(), TimeSpan.Zero));
            HybridSearchQueryInfo plan = CreatePlan();
            plan.RequiresGlobalStatistics = false;
            Assert.IsFalse(cache.TryGet("rid", "etag", plan, out _));
            Assert.IsFalse(cache.TryAdd("rid", "etag", plan, CreateStatistics(), TimeSpan.Zero));
            Assert.AreEqual(1, cache.Count);
        }

        [TestMethod]
        public void ContainerRevisionsIsolateCounts()
        {
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 4, () => TimeSpan.Zero);
            foreach ((string rid, string etag, long count) in new[]
            {
                ("rid", "etag", 10L),
                ("other-rid", "etag", 20L),
                ("rid", "other-etag", 30L),
                ("RID", "etag", 40L),
            })
            {
                Assert.IsTrue(cache.TryAdd(rid, etag, CreatePlan(), CreateStatistics(count), TimeSpan.Zero));
            }

            foreach ((string rid, string etag, long count) in new[]
            {
                ("rid", "etag", 10L),
                ("other-rid", "etag", 20L),
                ("rid", "other-etag", 30L),
                ("RID", "etag", 40L),
            })
            {
                Assert.IsTrue(cache.TryGet(rid, etag, CreatePlan(), out GlobalFullTextSearchStatistics result));
                Assert.AreEqual(count, result.DocumentCount);
            }

            Assert.AreEqual(4, cache.Count);
            Assert.AreEqual(4, cache.ContainerRevisionCount);
        }

        [TestMethod]
        public void ReusesSubsetsReorderedTermsDuplicatesAndRepeatedPaths()
        {
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 2, () => TimeSpan.Zero);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan("swim", "run"), Statistics(10, 100, 2, 3), TimeSpan.Zero));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("run"), out GlobalFullTextSearchStatistics subset));
            CollectionAssert.AreEqual(new long[] { 3 }, subset.FullTextStatistics[0].HitCounts.ToArray());
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("run", "swim", "run"), out GlobalFullTextSearchStatistics reordered));
            CollectionAssert.AreEqual(new long[] { 3, 2, 3 }, reordered.FullTextStatistics[0].HitCounts.ToArray());

            HybridSearchQueryInfo regrouped = CreatePlan("run");
            regrouped.FullTextSearchTerms.Add(CreatePlan("swim", "run").FullTextSearchTerms[0]);
            Assert.IsTrue(cache.TryGet("rid", "etag", regrouped, out GlobalFullTextSearchStatistics result));
            Assert.AreEqual(2, result.FullTextStatistics.Count);
            Assert.AreEqual(100L, result.FullTextStatistics[0].TotalWordCount);
            Assert.AreEqual(100L, result.FullTextStatistics[1].TotalWordCount);
            CollectionAssert.AreEqual(new long[] { 3 }, result.FullTextStatistics[0].HitCounts.ToArray());
            CollectionAssert.AreEqual(new long[] { 2, 3 }, result.FullTextStatistics[1].HitCounts.ToArray());
            Assert.AreEqual(2, cache.Count);
        }

        [TestMethod]
        public void IndependentlyCollectedCountsCanBeCombined()
        {
            TestClock clock = new TestClock();
            using Cache cache = new Cache(TimeSpan.FromSeconds(10), 3, clock.Read);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan("run"), Statistics(10, 100, 3), clock.Now));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("run"), out GlobalFullTextSearchStatistics selected));
            clock.Now = TimeSpan.FromSeconds(2);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan("swim"), Statistics(20, 200, 5), clock.Now));
            clock.Now = TimeSpan.FromSeconds(4);
            HybridSearchQueryInfo title = CreatePlan("energy");
            title.FullTextSearchTerms[0].Path[0] = "title";
            Assert.IsTrue(cache.TryAdd("rid", "etag", title, Statistics(30, 90, 6), clock.Now));

            HybridSearchQueryInfo combined = CreatePlan("swim", "run");
            combined.FullTextSearchTerms.Insert(0, title.FullTextSearchTerms[0]);
            Assert.IsTrue(cache.TryGet("rid", "etag", combined, out GlobalFullTextSearchStatistics result));
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
            using Cache cache = new Cache(TimeSpan.FromSeconds(10), 1, clock.Read);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan(), Statistics(0, 0, 0), clock.Now));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan(), out GlobalFullTextSearchStatistics empty));
            Assert.AreEqual(0L, empty.DocumentCount);
            Assert.AreEqual(0L, empty.FullTextStatistics[0].TotalWordCount);
            CollectionAssert.AreEqual(new long[] { 0 }, empty.FullTextStatistics[0].HitCounts.ToArray());
            clock.Now = TimeSpan.FromSeconds(1);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan(), Statistics(long.MaxValue, long.MaxValue, long.MaxValue), clock.Now));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan(), out GlobalFullTextSearchStatistics maximum));
            Assert.AreEqual(long.MaxValue, maximum.DocumentCount);
            Assert.AreEqual(long.MaxValue, maximum.FullTextStatistics[0].TotalWordCount);
            CollectionAssert.AreEqual(new[] { long.MaxValue }, maximum.FullTextStatistics[0].HitCounts.ToArray());
        }

        [TestMethod]
        public void RefreshingOtherCountsDoesNotRenewOldHitCounts()
        {
            TestClock clock = new TestClock();
            using Cache cache = new Cache(TimeSpan.FromSeconds(10), 2, clock.Read);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan("run"), Statistics(10, 100, 3), clock.Now));
            clock.Now = TimeSpan.FromSeconds(9);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan("swim"), Statistics(20, 200, 5), clock.Now));
            clock.Now = TimeSpan.FromSeconds(10) - TimeSpan.FromTicks(1);
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("run", "swim"), out _));
            clock.Now = TimeSpan.FromSeconds(10);
            Assert.IsFalse(cache.TryGet("rid", "etag", CreatePlan("swim", "run"), out GlobalFullTextSearchStatistics result));
            Assert.IsNull(result);
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("swim"), out GlobalFullTextSearchStatistics live));
            Assert.AreEqual(20L, live.DocumentCount);
            Assert.AreEqual(200L, live.FullTextStatistics[0].TotalWordCount);
            Assert.AreEqual(1, cache.Count);
        }

        [TestMethod]
        public void RefreshingAnotherPathDoesNotRenewOldPathCounts()
        {
            TestClock clock = new TestClock();
            using Cache cache = new Cache(TimeSpan.FromSeconds(10), 2, clock.Read);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan(), CreateStatistics(), clock.Now));
            clock.Now = TimeSpan.FromSeconds(5);
            HybridSearchQueryInfo otherPath = CreatePlan();
            otherPath.FullTextSearchTerms[0].Path[0] = "title";
            Assert.IsTrue(cache.TryAdd("rid", "etag", otherPath, CreateStatistics(20), clock.Now));
            clock.Now = TimeSpan.FromSeconds(10);
            Assert.IsFalse(cache.TryGet("rid", "etag", CreatePlan(), out _));
            Assert.IsTrue(cache.TryGet("rid", "etag", otherPath, out _));
            Assert.AreEqual(1, cache.Count);
            clock.Now = TimeSpan.FromSeconds(15);
            Assert.IsFalse(cache.TryGet("rid", "etag", otherPath, out _));
            Assert.AreEqual(0, cache.Count);
            Assert.AreEqual(0, cache.ContainerRevisionCount);
        }

        [TestMethod]
        public void NumericallyInconsistentMixedCountsCauseAMissWithoutClamping()
        {
            TestClock clock = new TestClock();
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 2, clock.Read);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan("run"), Statistics(10, 100, 8), clock.Now));
            clock.Now = TimeSpan.FromSeconds(1);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan("swim"), Statistics(3, 30, 2), clock.Now));
            Assert.IsFalse(cache.TryGet("rid", "etag", CreatePlan("run", "swim"), out GlobalFullTextSearchStatistics invalid));
            Assert.IsNull(invalid);
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("swim"), out _));
            clock.Now = TimeSpan.FromSeconds(2);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan("run", "swim"), Statistics(3, 30, 1, 2), clock.Now));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("run", "swim"), out GlobalFullTextSearchStatistics refreshed));
            CollectionAssert.AreEqual(new long[] { 1, 2 }, refreshed.FullTextStatistics[0].HitCounts.ToArray());
        }

        [TestMethod]
        public void ZeroDocumentCountWithOlderNonzeroWordCountCausesAMiss()
        {
            TestClock clock = new TestClock();
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 2, clock.Read);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan(), Statistics(10, 100, 0), clock.Now));
            clock.Now = TimeSpan.FromSeconds(1);
            HybridSearchQueryInfo otherPath = CreatePlan();
            otherPath.FullTextSearchTerms[0].Path[0] = "title";
            Assert.IsTrue(cache.TryAdd("rid", "etag", otherPath, Statistics(0, 0, 0), clock.Now));
            Assert.IsFalse(cache.TryGet("rid", "etag", CreatePlan(), out _));
            Assert.IsTrue(cache.TryGet("rid", "etag", otherPath, out _));
        }

        [DataTestMethod]
        [DataRow(-1L, 10L, 0L)]
        [DataRow(10L, -1L, 0L)]
        [DataRow(10L, 10L, -1L)]
        [DataRow(10L, 10L, 11L)]
        [DataRow(0L, 10L, 0L)]
        public void InvalidCountsAreNotAdmitted(long documents, long words, long hits)
        {
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 2, () => TimeSpan.Zero);
            Assert.IsFalse(cache.TryAdd("rid", "etag", CreatePlan(), Statistics(documents, words, hits), TimeSpan.Zero));
            Assert.AreEqual(0, cache.Count);
            Assert.AreEqual(0, cache.ContainerRevisionCount);
        }

        [TestMethod]
        public void InvalidDimensionsAndConflictingDuplicatesDoNotPartiallyUpdate()
        {
            TestClock clock = new TestClock();
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 3, clock.Read);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan(), Statistics(10, 100, 2), clock.Now));
            clock.Now = TimeSpan.FromSeconds(1);
            HybridSearchQueryInfo twoPaths = CreatePlan();
            twoPaths.FullTextSearchTerms.Add(CreatePlan("swim").FullTextSearchTerms[0]);
            (HybridSearchQueryInfo Plan, GlobalFullTextSearchStatistics Statistics)[] invalidCases =
            {
                (twoPaths, CreateStatistics(20)),
                (CreatePlan(), new GlobalFullTextSearchStatistics(20, Array.Empty<FullTextStatistics>())),
                (CreatePlan(), new GlobalFullTextSearchStatistics(20, new FullTextStatistics[] { null })),
                (CreatePlan("run", "swim"), CreateStatistics(20)),
                (CreatePlan(), Statistics(20, 200, 1, 2)),
                (twoPaths, new GlobalFullTextSearchStatistics(20, new[]
                {
                    new FullTextStatistics(200, new long[] { 4 }),
                    new FullTextStatistics(201, new long[] { 5 }),
                })),
                (CreatePlan("run", "run"), Statistics(20, 200, 4, 5)),
            };

            foreach ((HybridSearchQueryInfo plan, GlobalFullTextSearchStatistics statistics) in invalidCases)
            {
                Assert.IsFalse(cache.TryAdd("rid", "etag", plan, statistics, clock.Now));
                Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan(), out GlobalFullTextSearchStatistics original));
                Assert.AreEqual(10L, original.DocumentCount);
                Assert.AreEqual(100L, original.FullTextStatistics[0].TotalWordCount);
                CollectionAssert.AreEqual(new long[] { 2 }, original.FullTextStatistics[0].HitCounts.ToArray());
                Assert.AreEqual(1, cache.Count);
            }
        }

        [TestMethod]
        public void CountsAreIsolatedFromInputModelsAndReturnedSnapshots()
        {
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 2, () => TimeSpan.Zero);
            HybridSearchQueryInfo plan = CreatePlan("swim", "run");
            long[] hits = { 2, 3 };
            List<FullTextStatistics> paths = new List<FullTextStatistics> { new FullTextStatistics(100, hits) };
            Assert.IsTrue(cache.TryAdd("rid", "etag", plan, new GlobalFullTextSearchStatistics(10, paths), TimeSpan.Zero));
            hits[0] = 999;
            paths.Clear();
            plan.FullTextSearchTerms[0].Path[0] = "changed";
            plan.FullTextSearchTerms[0].Terms[0].Term = "changed";
            plan.FullTextSearchTerms[0].Terms[0].Distance = 2;
            plan.FullTextSearchTerms.Clear();
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("swim", "run"), out GlobalFullTextSearchStatistics first));
            CollectionAssert.AreEqual(new long[] { 2, 3 }, first.FullTextStatistics[0].HitCounts.ToArray());
            Assert.ThrowsException<NotSupportedException>(() => ((IList<FullTextStatistics>)first.FullTextStatistics).Clear());
            Assert.IsTrue(MemoryMarshal.TryGetArray(first.FullTextStatistics[0].HitCounts, out ArraySegment<long> returned));
            returned.Array[returned.Offset] = 999;
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("swim", "run"), out GlobalFullTextSearchStatistics second));
            Assert.AreNotSame(first, second);
            CollectionAssert.AreEqual(new long[] { 2, 3 }, second.FullTextStatistics[0].HitCounts.ToArray());
        }

        [DataTestMethod]
        [DataRow(@"[{""path"":[""title""],""terms"":[{""term"":""swim"",""distance"":2}]}]")]
        [DataRow(@"[{""path"":[""Text""],""terms"":[{""term"":""swim"",""distance"":2}]}]")]
        [DataRow(@"[{""path"":[""text""],""terms"":[{""term"":""Swim"",""distance"":2}]}]")]
        [DataRow(@"[{""path"":[""text""],""terms"":[{""term"":""swim"",""distance"":1}]}]")]
        [DataRow(@"[{""path"":[""text""],""terms"":[{""term"":""swim""}]}]")]
        [DataRow(@"[{""path"":[""text""],""terms"":[{""term"":""run"",""distance"":0}]}]")]
        [DataRow(@"[{""path"":[""text""],""terms"":[{""term"":""swim run"",""distance"":2}]}]")]
        public void DistinctPathsTermsAndDistancesDoNotShareCounts(string metadata)
        {
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 2, () => TimeSpan.Zero);
            HybridSearchQueryInfo original = ParsePlan(@"[{""path"":[""text""],""terms"":[{""term"":""swim"",""distance"":2},{""term"":""run""}]}]");
            Assert.IsTrue(cache.TryAdd("rid", "etag", original, Statistics(10, 100, 2, 3), TimeSpan.Zero));
            Assert.IsFalse(cache.TryGet("rid", "etag", ParsePlan(metadata), out _));
        }

        [TestMethod]
        public void PathSegmentsPhrasesAndDateLikeStringsRemainExact()
        {
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 4, () => TimeSpan.Zero);
            string[] metadata =
            {
                @"[{""path"":[""a/b""],""terms"":[{""term"":""United States""}]}]",
                @"[{""path"":[""a"",""b""],""terms"":[{""term"":""United States""}]}]",
                @"[{""path"":[""tags"",""[0]""],""terms"":[{""term"":""""}]}]",
                @"[{""path"":[""2026-10-20T12:00:00Z""],""terms"":[{""term"":""2026-10-20T12:00:00+01:00"",""distance"":2147483647}]}]",
            };
            for (int index = 0; index < metadata.Length; index++)
            {
                Assert.IsTrue(cache.TryAdd("rid", "etag", ParsePlan(metadata[index]), Statistics(10, 100, index), TimeSpan.Zero));
            }

            for (int index = 0; index < metadata.Length; index++)
            {
                Assert.IsTrue(cache.TryGet("rid", "etag", ParsePlan(metadata[index]), out GlobalFullTextSearchStatistics result));
                CollectionAssert.AreEqual(new long[] { index }, result.FullTextStatistics[0].HitCounts.ToArray());
            }

            Assert.AreEqual(4, cache.Count);
        }

        [DataTestMethod]
        [DataRow(@"[{""terms"":[{""distance"":2,""term"":""swim""},{""term"":""run""}],""path"":[""text""]}]")]
        [DataRow(@"[{""path"":[""text""],""terms"":[{""term"":""swim"",""distance"":2},{""term"":""run"",""distance"":null}]}]")]
        [DataRow(@"[{""path"":[""text""],""terms"":[{""term"":""swim"",""distance"":""2""},{""term"":""run""}]}]")]
        [DataRow(@"[{""path"":[""text""],""terms"":[{""term"":""swim"",""distance"":2,""future"":true},{""term"":""run""}],""future"":true}]")]
        public void EquivalentMetadataUsesNormalQueryPlanDeserialization(string metadata)
        {
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 2, () => TimeSpan.Zero);
            HybridSearchQueryInfo plan = ParsePlan(@"[{""path"":[""text""],""terms"":[{""term"":""swim"",""distance"":2},{""term"":""run""}]}]");
            Assert.IsTrue(cache.TryAdd("rid", "etag", plan, Statistics(10, 100, 2, 3), TimeSpan.Zero));
            Assert.IsTrue(cache.TryGet("rid", "etag", ParsePlan(metadata), out GlobalFullTextSearchStatistics result));
            CollectionAssert.AreEqual(new long[] { 2, 3 }, result.FullTextStatistics[0].HitCounts.ToArray());
        }

        [TestMethod]
        public void IdentityIgnoresStatisticsQueryTextAndCurrentCulture()
        {
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 1, () => TimeSpan.Zero);
            HybridSearchQueryInfo plan = CreatePlan();
            plan.GlobalStatisticsQuery = "SELECT COUNT(1) FROM c";
            Assert.IsTrue(cache.TryAdd("rid", "etag", plan, CreateStatistics(), TimeSpan.Zero));
            plan.GlobalStatisticsQuery = null;
            CultureInfo original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                Assert.IsTrue(cache.TryGet("rid", "etag", plan, out _));
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
            using Cache cache = new Cache(TimeSpan.FromSeconds(10), 1, clock.Read);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan(), CreateStatistics(), TimeSpan.Zero));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan(), out GlobalFullTextSearchStatistics selected));
            clock.Now = TimeSpan.FromSeconds(10) - TimeSpan.FromTicks(1);
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan(), out _));
            clock.Now = TimeSpan.FromSeconds(10);
            Assert.IsFalse(cache.TryGet("rid", "etag", CreatePlan(), out _));
            Assert.AreEqual(0, cache.Count);
            Assert.AreEqual(0, cache.ContainerRevisionCount);
            Assert.AreEqual(10L, selected.DocumentCount);
        }

        [TestMethod]
        public void ExpiredOrFutureCollectionsAreNotAdmitted()
        {
            TestClock clock = new TestClock { Now = TimeSpan.FromSeconds(10) };
            using Cache cache = new Cache(TimeSpan.FromSeconds(10), 1, clock.Read);
            Assert.IsFalse(cache.TryAdd("rid", "etag", CreatePlan(), CreateStatistics(), TimeSpan.Zero));
            Assert.IsFalse(cache.TryAdd("rid", "etag", CreatePlan(), CreateStatistics(), clock.Now + TimeSpan.FromTicks(1)));
            Assert.AreEqual(0, cache.Count);
        }

        [TestMethod]
        public void AdmissionRechecksAgeAfterReadingStatistics()
        {
            TestClock clock = new TestClock();
            using Cache cache = new Cache(TimeSpan.FromSeconds(10), 1, clock.Read);
            CallbackStatisticsList source = new CallbackStatisticsList(() => clock.Now = TimeSpan.FromSeconds(10));
            Assert.IsFalse(cache.TryAdd("rid", "etag", CreatePlan(), new GlobalFullTextSearchStatistics(10, source), TimeSpan.Zero));
            Assert.AreEqual(0, cache.Count);
        }

        [TestMethod]
        public void LargeAgesAndTimestampsDoNotOverflow()
        {
            TestClock clock = new TestClock { Now = TimeSpan.MaxValue - TimeSpan.FromTicks(1) };
            using Cache cache = new Cache(TimeSpan.MaxValue, 1, clock.Read);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan(), CreateStatistics(), TimeSpan.Zero));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan(), out _));
            clock.Now = TimeSpan.MaxValue;
            Assert.IsFalse(cache.TryGet("rid", "etag", CreatePlan(), out _));
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan(), CreateStatistics(), TimeSpan.MaxValue - TimeSpan.FromTicks(5)));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan(), out _));
        }

        [TestMethod]
        public void SeparateCachesHaveIndependentPoliciesAndContents()
        {
            TestClock clock = new TestClock();
            using Cache shortLived = new Cache(TimeSpan.FromSeconds(5), 1, clock.Read);
            using Cache longLived = new Cache(TimeSpan.FromSeconds(10), 1, clock.Read);
            Assert.IsTrue(shortLived.TryAdd("rid", "etag", CreatePlan(), CreateStatistics(), clock.Now));
            Assert.IsFalse(longLived.TryGet("rid", "etag", CreatePlan(), out _));
            Assert.IsTrue(longLived.TryAdd("rid", "etag", CreatePlan(), CreateStatistics(20), clock.Now));
            clock.Now = TimeSpan.FromSeconds(5);
            Assert.IsFalse(shortLived.TryGet("rid", "etag", CreatePlan(), out _));
            Assert.IsTrue(longLived.TryGet("rid", "etag", CreatePlan(), out GlobalFullTextSearchStatistics result));
            Assert.AreEqual(20L, result.DocumentCount);
        }

        [TestMethod]
        public void CapacityCountsDistinctTermsAcrossPathsContainersAndRevisions()
        {
            TestClock clock = new TestClock();
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 3, clock.Read);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan("run", "swim"), Statistics(10, 100, 2, 3), clock.Now));
            clock.Now = TimeSpan.FromSeconds(1);
            Assert.IsTrue(cache.TryAdd("rid", "other-etag", CreatePlan(), CreateStatistics(), clock.Now));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("run", "swim"), out _));
            clock.Now = TimeSpan.FromSeconds(2);
            HybridSearchQueryInfo otherPath = CreatePlan("energy");
            otherPath.FullTextSearchTerms[0].Path[0] = "title";
            Assert.IsTrue(cache.TryAdd("other-rid", "etag", otherPath, CreateStatistics(), clock.Now));
            Assert.AreEqual(3, cache.Count);
            Assert.AreEqual(3, cache.ContainerRevisionCount);
            Assert.IsFalse(cache.TryGet("rid", "etag", CreatePlan("run", "swim"), out _));
            Assert.IsTrue(cache.TryGet("rid", "other-etag", CreatePlan(), out _));
            Assert.IsTrue(cache.TryGet("other-rid", "etag", otherPath, out _));
        }

        [TestMethod]
        public void PublicationPreservesItsOverlappingTermsWhenMakingRoom()
        {
            TestClock clock = new TestClock();
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 2, clock.Read);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan("run"), Statistics(10, 100, 2), clock.Now));
            clock.Now = TimeSpan.FromSeconds(1);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan("bike"), Statistics(10, 100, 4), clock.Now));
            clock.Now = TimeSpan.FromSeconds(2);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan("run", "swim"), Statistics(10, 100, 3, 5), clock.Now));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("run", "swim"), out GlobalFullTextSearchStatistics result));
            CollectionAssert.AreEqual(new long[] { 3, 5 }, result.FullTextStatistics[0].HitCounts.ToArray());
            Assert.IsFalse(cache.TryGet("rid", "etag", CreatePlan("bike"), out _));
            Assert.AreEqual(2, cache.Count);
        }

        [TestMethod]
        public void RepeatedSlotsDoNotConsumeExtraCapacityAndOversizedPublicationsAreSkipped()
        {
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 1, () => TimeSpan.Zero);
            HybridSearchQueryInfo repeated = CreatePlan("run", "run");
            repeated.FullTextSearchTerms.Add(CreatePlan("run").FullTextSearchTerms[0]);
            Assert.IsTrue(cache.TryAdd("rid", "etag", repeated, new GlobalFullTextSearchStatistics(10, new[]
            {
                new FullTextStatistics(100, new long[] { 2, 2 }),
                new FullTextStatistics(100, new long[] { 2 }),
            }), TimeSpan.Zero));
            Assert.AreEqual(1, cache.Count);
            Assert.IsTrue(cache.TryGet("rid", "etag", repeated, out _));
            Assert.IsFalse(cache.TryAdd("other", "etag", CreatePlan("swim", "bike"), Statistics(20, 200, 3, 4), TimeSpan.Zero));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan(), out _));
            Assert.AreEqual(1, cache.Count);
            Assert.AreEqual(1, cache.ContainerRevisionCount);
        }

        [TestMethod]
        public void EvictionUsesCollectionTimeRatherThanPublicationOrder()
        {
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 2, () => TimeSpan.FromSeconds(10));
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan("newer"), CreateStatistics(), TimeSpan.FromSeconds(5)));
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan("older"), CreateStatistics(), TimeSpan.FromSeconds(1)));
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan("latest"), CreateStatistics(), TimeSpan.FromSeconds(8)));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("newer"), out _));
            Assert.IsFalse(cache.TryGet("rid", "etag", CreatePlan("older"), out _));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("latest"), out _));
        }

        [DataTestMethod]
        [DataRow("rid")]
        [DataRow("other-rid")]
        public void CapacityOneCleansUpAndRecreatesParentCaches(string resourceId)
        {
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 1, () => TimeSpan.Zero);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan("run"), CreateStatistics(), TimeSpan.Zero));
            Assert.IsTrue(cache.TryAdd(resourceId, "etag", CreatePlan("swim"), CreateStatistics(), TimeSpan.Zero));
            Assert.IsFalse(cache.TryGet("rid", "etag", CreatePlan("run"), out _));
            Assert.IsTrue(cache.TryGet(resourceId, "etag", CreatePlan("swim"), out _));
            Assert.AreEqual(1, cache.Count);
            Assert.AreEqual(1, cache.ContainerRevisionCount);
        }

        [TestMethod]
        public void InsertionRemovesExpiredCountsAndEmptyParents()
        {
            TestClock clock = new TestClock();
            using Cache cache = new Cache(TimeSpan.FromSeconds(10), 4, clock.Read);
            Assert.IsTrue(cache.TryAdd("old", "etag", CreatePlan("run", "swim"), Statistics(10, 100, 2, 3), clock.Now));
            Assert.IsTrue(cache.TryAdd("old", "other-etag", CreatePlan(), CreateStatistics(), clock.Now));
            clock.Now = TimeSpan.FromSeconds(5);
            Assert.IsTrue(cache.TryAdd("fresh", "etag", CreatePlan(), CreateStatistics(), clock.Now));
            clock.Now = TimeSpan.FromSeconds(10);
            Assert.IsTrue(cache.TryAdd("new", "etag", CreatePlan(), CreateStatistics(), clock.Now));
            Assert.IsTrue(cache.TryGet("fresh", "etag", CreatePlan(), out _));
            Assert.AreEqual(2, cache.Count);
            Assert.AreEqual(2, cache.ContainerRevisionCount);
        }

        [TestMethod]
        public void OlderPublicationsOnlyFillMissingCountsWithoutRegressingNewerOnes()
        {
            TestClock clock = new TestClock { Now = TimeSpan.FromSeconds(10) };
            using Cache cache = new Cache(TimeSpan.FromSeconds(10), 3, clock.Read);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan("run"), Statistics(20, 200, 8), TimeSpan.FromSeconds(5)));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan(), out GlobalFullTextSearchStatistics selected));
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan("run", "swim"), Statistics(10, 100, 2, 3), TimeSpan.FromSeconds(1)));
            Assert.IsFalse(cache.TryAdd("rid", "etag", CreatePlan("run"), Statistics(20, 200, 9), TimeSpan.FromSeconds(5)));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("run", "swim"), out GlobalFullTextSearchStatistics result));
            Assert.AreEqual(20L, result.DocumentCount);
            Assert.AreEqual(200L, result.FullTextStatistics[0].TotalWordCount);
            CollectionAssert.AreEqual(new long[] { 8, 3 }, result.FullTextStatistics[0].HitCounts.ToArray());
            Assert.AreEqual(20L, selected.DocumentCount);
            Assert.AreEqual(2, cache.Count);
            clock.Now = TimeSpan.FromSeconds(11);
            Assert.IsFalse(cache.TryGet("rid", "etag", CreatePlan("swim"), out _));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("run"), out _));
            Assert.AreEqual(1, cache.Count);
        }

        [TestMethod]
        public void LateOldRevisionPublicationDoesNotReplaceTheNewRevision()
        {
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 2, () => TimeSpan.FromSeconds(10));
            Assert.IsTrue(cache.TryAdd("rid", "new", CreatePlan(), CreateStatistics(20), TimeSpan.FromSeconds(5)));
            Assert.IsTrue(cache.TryAdd("rid", "old", CreatePlan(), CreateStatistics(10), TimeSpan.Zero));
            Assert.IsTrue(cache.TryGet("rid", "new", CreatePlan(), out GlobalFullTextSearchStatistics current));
            Assert.AreEqual(20L, current.DocumentCount);
            Assert.IsTrue(cache.TryGet("rid", "old", CreatePlan(), out GlobalFullTextSearchStatistics old));
            Assert.AreEqual(10L, old.DocumentCount);
        }

        [TestMethod]
        public void ClearAndDisposePreserveSelectedSnapshots()
        {
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 1, () => TimeSpan.Zero);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan(), CreateStatistics(), TimeSpan.Zero));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan(), out GlobalFullTextSearchStatistics selected));
            cache.Clear();
            Assert.AreEqual(0, cache.Count);
            Assert.AreEqual(0, cache.ContainerRevisionCount);
            Assert.IsTrue(cache.TryAdd("rid", "etag", CreatePlan(), CreateStatistics(), TimeSpan.Zero));
            cache.Dispose();
            cache.Dispose();
            cache.Clear();
            Assert.IsFalse(cache.TryGet("rid", "etag", CreatePlan(), out _));
            Assert.IsFalse(cache.TryAdd("rid", "etag", CreatePlan(), CreateStatistics(), TimeSpan.Zero));
            Assert.AreEqual(0, cache.Count);
            Assert.AreEqual(0, cache.ContainerRevisionCount);
            Assert.AreEqual(10L, selected.DocumentCount);
        }

        [TestMethod]
        public async Task DisposalDuringSnapshotCopyPreventsAdmissionWithoutHoldingTheLock()
        {
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 1, () => TimeSpan.Zero);
            using ManualResetEventSlim resumeCopy = new ManualResetEventSlim();
            TaskCompletionSource<bool> copying = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            CallbackStatisticsList source = new CallbackStatisticsList(() =>
            {
                copying.SetResult(true);
                Assert.IsTrue(resumeCopy.Wait(TimeSpan.FromSeconds(10)));
            });
            Task<bool> admission = Task.Run(() => cache.TryAdd(
                "rid", "etag", CreatePlan(), new GlobalFullTextSearchStatistics(10, source), TimeSpan.Zero));
            try
            {
                await copying.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await Task.Run(cache.Dispose).WaitAsync(TimeSpan.FromSeconds(10));
            }
            finally
            {
                resumeCopy.Set();
                await admission;
            }

            Assert.IsFalse(await admission);
            Assert.AreEqual(0, cache.Count);
        }

        [TestMethod]
        public async Task ConcurrentPublicationKeepsTheNewestCounts()
        {
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 1, () => TimeSpan.FromSeconds(10));
            await Task.WhenAll(Enumerable.Range(0, 64).Select(index => Task.Run(() =>
            {
                cache.TryAdd("rid", "etag", CreatePlan(), Statistics(index, index * 10, index), TimeSpan.FromTicks(index));
                Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan(), out GlobalFullTextSearchStatistics result));
                Assert.AreEqual(result.DocumentCount, result.FullTextStatistics[0].HitCounts.Span[0]);
            })));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan(), out GlobalFullTextSearchStatistics newest));
            Assert.AreEqual(63L, newest.DocumentCount);
            Assert.AreEqual(1, cache.Count);
        }

        [TestMethod]
        public async Task ConcurrentContainerOperationsMaintainTheGlobalTermBound()
        {
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 8, () => TimeSpan.FromSeconds(10));
            await Task.WhenAll(Enumerable.Range(0, 64).Select(index => Task.Run(() =>
            {
                string resourceId = (index % 4).ToString();
                HybridSearchQueryInfo plan = CreatePlan(index.ToString());
                Assert.IsTrue(cache.TryAdd(resourceId, "etag", plan, Statistics(100, 1000, index), TimeSpan.FromTicks(index)));
                if (cache.TryGet(resourceId, "etag", plan, out GlobalFullTextSearchStatistics result))
                {
                    Assert.AreEqual((long)index, result.FullTextStatistics[0].HitCounts.Span[0]);
                }
            })));
            Assert.AreEqual(8, cache.Count);
            Assert.IsTrue(cache.ContainerRevisionCount > 0 && cache.ContainerRevisionCount <= 4);
        }

        [TestMethod]
        public async Task ConcurrentOverlappingPublicationsPreserveSharedTermsAndCapacity()
        {
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 8, () => TimeSpan.FromSeconds(10));
            await Task.WhenAll(Enumerable.Range(0, 64).Select(index => Task.Run(() =>
            {
                HybridSearchQueryInfo plan = CreatePlan("shared", index.ToString());
                Assert.IsTrue(cache.TryAdd("rid", "etag", plan, Statistics(100, 1000, index, index), TimeSpan.FromTicks(index)));
                Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("shared"), out _));
            })));
            Assert.IsTrue(cache.TryGet("rid", "etag", CreatePlan("shared"), out GlobalFullTextSearchStatistics result));
            CollectionAssert.AreEqual(new long[] { 63 }, result.FullTextStatistics[0].HitCounts.ToArray());
            Assert.AreEqual(8, cache.Count);
            Assert.AreEqual(1, cache.ContainerRevisionCount);
        }

        [TestMethod]
        public void NativePlansReuseSubsetsAndRegroupCountsAcrossQueryAndParameterChanges()
        {
            using Cache cache = new Cache(TimeSpan.FromMinutes(1), 2, () => TimeSpan.Zero);
            HybridSearchQueryInfo first = GetNativePlan(new SqlQuerySpec(
                "SELECT TOP 10 c.id FROM c ORDER BY RANK RRF(FullTextScore(c.text, @first, @second), VectorDistance(c.image, @vector))",
                new SqlParameterCollection
                {
                    new SqlParameter("@first", "swim"),
                    new SqlParameter("@second", "run"),
                    new SqlParameter("@vector", new[] { 1.0, 2.0, 3.0 }),
                    new SqlParameter("@unused", 1),
                }));
            Assert.IsTrue(cache.TryAdd("rid", "etag", first, Statistics(10, 100, 2, 3), TimeSpan.Zero));
            HybridSearchQueryInfo subset = GetNativePlan(new SqlQuerySpec(
                "SELECT TOP 20 c.title FROM c ORDER BY RANK RRF(FullTextScore(c.text, @term), VectorDistance(c.image, @vector))",
                new SqlParameterCollection
                {
                    new SqlParameter("@unused", "different"),
                    new SqlParameter("@vector", new[] { 3.0, 2.0, 1.0 }),
                    new SqlParameter("@term", "run"),
                }));
            Assert.IsTrue(cache.TryGet("rid", "etag", subset, out GlobalFullTextSearchStatistics subsetResult));
            CollectionAssert.AreEqual(new long[] { 3 }, subsetResult.FullTextStatistics[0].HitCounts.ToArray());

            HybridSearchQueryInfo regrouped = GetNativePlan(new SqlQuerySpec(
                "SELECT TOP 10 * FROM c ORDER BY RANK RRF(FullTextScore(c.text, 'run'), FullTextScore(c.text, 'swim', 'run'))"));
            Assert.IsTrue(cache.TryGet("rid", "etag", regrouped, out GlobalFullTextSearchStatistics groupedResult));
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

        private sealed class TestClock
        {
            public TimeSpan Now { get; set; }

            public TimeSpan Read() => this.Now;
        }

        private sealed class CallbackStatisticsList : IReadOnlyList<FullTextStatistics>
        {
            private readonly Action beforeRead;

            public CallbackStatisticsList(Action beforeRead)
            {
                this.beforeRead = beforeRead;
            }

            public int Count => 1;

            public FullTextStatistics this[int index]
            {
                get
                {
                    this.beforeRead();
                    return new FullTextStatistics(100, new long[] { 1 });
                }
            }

            public IEnumerator<FullTextStatistics> GetEnumerator()
            {
                yield return this[0];
            }

            IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
        }
    }
}
