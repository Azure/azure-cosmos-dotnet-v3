//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Tracing;
    using CacheKey = Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch.HybridSearchStatisticsCacheKey;

    // Caches independently aged full-text statistics for Global scoring. Use Create to obtain an instance;
    // disabled clients and queries that cannot use the cache get NoOpImpl instead of null.
    internal abstract class HybridSearchStatisticsCache
    {
        private const string TraceDatumKey = "Hybrid Search Statistics Cache";

        private HybridSearchStatisticsCache()
        {
        }

        public static HybridSearchStatisticsCache Create(TimeSpan? maxAge, int maxEntries)
        {
            return Create(maxAge, maxEntries, null);
        }

        // A null getElapsedTime uses a monotonic stopwatch.
        public static HybridSearchStatisticsCache Create(TimeSpan? maxAge, int maxEntries, Func<TimeSpan> getElapsedTime)
        {
            return maxAge is TimeSpan age && age > TimeSpan.Zero
                ? new Impl(age, maxEntries, getElapsedTime)
                : NoOpImpl.Instance;
        }

        public static HybridSearchStatisticsCache GetNoOpImpl()
        {
            return NoOpImpl.Instance;
        }

        public abstract TimeSpan GetElapsedTime();

        // Records a Hit or Miss trace datum when caching is enabled.
        public abstract bool TryGet(
            string containerResourceId,
            string containerETag,
            HybridSearchQueryInfo hybridSearchQueryInfo,
            ITrace trace,
            out GlobalFullTextSearchStatistics statistics);

        public abstract void Add(
            string containerResourceId,
            string containerETag,
            HybridSearchQueryInfo hybridSearchQueryInfo,
            GlobalFullTextSearchStatistics statistics,
            TimeSpan collectionStartedAt);

        internal static bool IsQuerySupported(HybridSearchQueryInfo hybridSearchQueryInfo)
        {
            if (hybridSearchQueryInfo == null || !hybridSearchQueryInfo.RequiresGlobalStatistics)
            {
                return false;
            }

            List<FullTextSearchTerms> fullTextSearchTerms = hybridSearchQueryInfo.FullTextSearchTerms;
            return fullTextSearchTerms != null && fullTextSearchTerms.Count > 0 &&
                !fullTextSearchTerms.Any(pathSearchTerms =>
                    pathSearchTerms?.Path == null || pathSearchTerms.Path.Count == 0 || pathSearchTerms.Path.Any(segment => segment == null) ||
                    pathSearchTerms.Terms == null || pathSearchTerms.Terms.Count == 0 ||
                    pathSearchTerms.Terms.Any(term => term?.Term == null || term.Distance < 0));
        }

        // Each count is read and written independently; a lookup may combine counts from different collections.
        private sealed class Impl : HybridSearchStatisticsCache
        {
            private readonly ConcurrentDictionary<CacheKey, CachedCount> counts = new ConcurrentDictionary<CacheKey, CachedCount>();
            private readonly TimeSpan maxAge;
            private readonly int maxEntries;
            private readonly Func<TimeSpan> getElapsedTime;

            public Impl(TimeSpan maxAge, int maxEntries, Func<TimeSpan> getElapsedTime)
            {
                if (maxEntries <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(maxEntries));
                }

                if (getElapsedTime == null)
                {
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    getElapsedTime = () => stopwatch.Elapsed;
                }

                this.maxAge = maxAge;
                this.maxEntries = maxEntries;
                this.getElapsedTime = getElapsedTime;
            }

            public override TimeSpan GetElapsedTime() => this.getElapsedTime();

            public override bool TryGet(
                string containerResourceId,
                string containerETag,
                HybridSearchQueryInfo hybridSearchQueryInfo,
                ITrace trace,
                out GlobalFullTextSearchStatistics statistics)
            {
                bool found = this.TryGetCore(containerResourceId, containerETag, hybridSearchQueryInfo, out statistics);
                trace.AddOrUpdateDatum(TraceDatumKey, found ? "Hit" : "Miss");
                return found;
            }

            private bool TryGetCore(
                string containerResourceId,
                string containerETag,
                HybridSearchQueryInfo hybridSearchQueryInfo,
                out GlobalFullTextSearchStatistics statistics)
            {
                statistics = null;
                if (containerResourceId == null ||
                    containerETag == null ||
                    !IsQuerySupported(hybridSearchQueryInfo))
                {
                    return false;
                }

                TimeSpan now = this.getElapsedTime();
                if (!this.TryGetLiveCount(CacheKey.CreateTotalDocumentCount(containerResourceId, containerETag), now, out long documentCount))
                {
                    return false;
                }

                List<FullTextSearchTerms> fullTextSearchTerms = hybridSearchQueryInfo.FullTextSearchTerms;
                FullTextStatistics[] fullTextStatistics = new FullTextStatistics[fullTextSearchTerms.Count];
                for (int pathIndex = 0; pathIndex < fullTextStatistics.Length; pathIndex++)
                {
                    FullTextSearchTerms pathSearchTerms = fullTextSearchTerms[pathIndex];

                    // Counts may come from different collections; reject combinations that are numerically impossible.
                    if (!this.TryGetLiveCount(CacheKey.CreateDocumentPathWordCount(containerResourceId, containerETag, pathSearchTerms.Path), now, out long wordCount) ||
                        (documentCount == 0 && wordCount != 0))
                    {
                        return false;
                    }

                    long[] hitCounts = new long[pathSearchTerms.Terms.Count];
                    for (int termIndex = 0; termIndex < hitCounts.Length; termIndex++)
                    {
                        FullTextSearchTerm term = pathSearchTerms.Terms[termIndex];
                        CacheKey hitCountKey = CacheKey.CreateDocumentPathTermHitCount(
                            containerResourceId, containerETag, pathSearchTerms.Path, term.Term, term.Distance);
                        if (!this.TryGetLiveCount(hitCountKey, now, out hitCounts[termIndex]) ||
                            hitCounts[termIndex] > documentCount)
                        {
                            return false;
                        }
                    }

                    fullTextStatistics[pathIndex] = new FullTextStatistics(wordCount, hitCounts);
                }

                statistics = new GlobalFullTextSearchStatistics(documentCount, fullTextStatistics);
                return true;
            }

            public override void Add(
                string containerResourceId,
                string containerETag,
                HybridSearchQueryInfo hybridSearchQueryInfo,
                GlobalFullTextSearchStatistics statistics,
                TimeSpan collectionStartedAt)
            {
                TimeSpan now = this.getElapsedTime();
                if (containerResourceId == null ||
                    containerETag == null ||
                    statistics == null ||
                    !IsQuerySupported(hybridSearchQueryInfo) ||
                    !this.IsFresh(collectionStartedAt, now))
                {
                    return;
                }

                List<FullTextSearchTerms> fullTextSearchTerms = hybridSearchQueryInfo.FullTextSearchTerms;
                if (statistics.FullTextStatistics.Count != fullTextSearchTerms.Count)
                {
                    return;
                }

                int entryCount = 1;
                for (int pathIndex = 0; pathIndex < fullTextSearchTerms.Count; pathIndex++)
                {
                    FullTextStatistics pathStatistics = statistics.FullTextStatistics[pathIndex];
                    if (pathStatistics == null || pathStatistics.HitCounts.Length != fullTextSearchTerms[pathIndex].Terms.Count)
                    {
                        return;
                    }

                    entryCount += 1 + pathStatistics.HitCounts.Length;
                }

                // A response that cannot fit would only evict other entries and then itself.
                if (entryCount > this.maxEntries)
                {
                    return;
                }

                this.Set(CacheKey.CreateTotalDocumentCount(containerResourceId, containerETag), statistics.DocumentCount, collectionStartedAt);
                for (int pathIndex = 0; pathIndex < fullTextSearchTerms.Count; pathIndex++)
                {
                    FullTextSearchTerms pathSearchTerms = fullTextSearchTerms[pathIndex];
                    FullTextStatistics pathStatistics = statistics.FullTextStatistics[pathIndex];
                    this.Set(
                        CacheKey.CreateDocumentPathWordCount(containerResourceId, containerETag, pathSearchTerms.Path),
                        pathStatistics.TotalWordCount,
                        collectionStartedAt);

                    ReadOnlySpan<long> hitCounts = pathStatistics.HitCounts.Span;
                    for (int termIndex = 0; termIndex < hitCounts.Length; termIndex++)
                    {
                        FullTextSearchTerm term = pathSearchTerms.Terms[termIndex];
                        this.Set(
                            CacheKey.CreateDocumentPathTermHitCount(containerResourceId, containerETag, pathSearchTerms.Path, term.Term, term.Distance),
                            hitCounts[termIndex],
                            collectionStartedAt);
                    }
                }

                this.EvictIfOverCapacity(now);
            }

            private void Set(CacheKey key, long value, TimeSpan collectionStartedAt)
            {
                CachedCount count = new CachedCount(value, collectionStartedAt);

                // A slower, older collection must not replace a newer count.
                this.counts.AddOrUpdate(
                    key,
                    count,
                    (_, existing) => existing.CollectionStartedAt >= collectionStartedAt ? existing : count);
            }

            private bool TryGetLiveCount(CacheKey key, TimeSpan now, out long value)
            {
                if (this.counts.TryGetValue(key, out CachedCount count) && this.IsFresh(count.CollectionStartedAt, now))
                {
                    value = count.Value;
                    return true;
                }

                value = 0;
                return false;
            }

            private bool IsFresh(TimeSpan collectionStartedAt, TimeSpan now)
            {
                return now >= collectionStartedAt && now - collectionStartedAt < this.maxAge;
            }

            // Best effort: concurrent writers can briefly exceed capacity or evict a just-refreshed count.
            private void EvictIfOverCapacity(TimeSpan now)
            {
                if (this.counts.Count <= this.maxEntries)
                {
                    return;
                }

                // ToArray takes one consistent snapshot; LINQ over the live dictionary can throw during concurrent writes.
                // Expired entries sort first, so one oldest-first pass removes them all and then only the remaining excess.
                KeyValuePair<CacheKey, CachedCount>[] entries = this.counts.ToArray();
                int excess = entries.Length - this.maxEntries;
                foreach (KeyValuePair<CacheKey, CachedCount> entry in entries.OrderBy(entry => entry.Value.CollectionStartedAt))
                {
                    if (excess <= 0 && this.IsFresh(entry.Value.CollectionStartedAt, now))
                    {
                        break;
                    }

                    this.counts.TryRemove(entry.Key, out _);
                    excess--;
                }
            }

            private readonly struct CachedCount
            {
                public CachedCount(long value, TimeSpan collectionStartedAt)
                {
                    this.Value = value;
                    this.CollectionStartedAt = collectionStartedAt;
                }

                public long Value { get; }

                public TimeSpan CollectionStartedAt { get; }
            }
        }

        private sealed class NoOpImpl : HybridSearchStatisticsCache
        {
            public static readonly NoOpImpl Instance = new NoOpImpl();

            private NoOpImpl()
            {
            }

            public override TimeSpan GetElapsedTime() => TimeSpan.Zero;

            public override bool TryGet(
                string containerResourceId,
                string containerETag,
                HybridSearchQueryInfo hybridSearchQueryInfo,
                ITrace trace,
                out GlobalFullTextSearchStatistics statistics)
            {
                statistics = null;
                return false;
            }

            public override void Add(
                string containerResourceId,
                string containerETag,
                HybridSearchQueryInfo hybridSearchQueryInfo,
                GlobalFullTextSearchStatistics statistics,
                TimeSpan collectionStartedAt)
            {
            }
        }
    }
}
