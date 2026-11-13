//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Tracing;
    using CacheKey = Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch.HybridSearchStatisticsCacheKey;

    internal abstract class HybridSearchStatisticsCache
    {
        private const string TraceDatumKey = "Hybrid Search Statistics Cache";
        private const string TraceDatumHit = "Hit";
        private const string TraceDatumMiss = "Miss";

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
            if (maxEntries < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEntries));
            }

            return maxAge.HasValue && maxAge.Value > TimeSpan.Zero && maxEntries > 0
                ? new Impl(maxAge.Value, maxEntries, getElapsedTime)
                : NoOpImpl.Instance;
        }

        public static HybridSearchStatisticsCache GetNoOpImpl()
        {
            return NoOpImpl.Instance;
        }

        public abstract TimeSpan GetElapsedTime();

        public abstract bool TryGet(
            string containerResourceId,
            string containerETag,
            HybridSearchQueryInfo queryInfo,
            ITrace trace,
            out GlobalFullTextSearchStatistics statistics);

        // Returns true if the statistics were stored.
        public abstract bool TryAdd(
            string containerResourceId,
            string containerETag,
            HybridSearchQueryInfo queryInfo,
            GlobalFullTextSearchStatistics statistics,
            TimeSpan collectionStartedAt);

        internal static bool IsQuerySupported(HybridSearchQueryInfo queryInfo)
        {
            if (queryInfo == null)
            {
                throw new ArgumentNullException(nameof(queryInfo));
            }

            return queryInfo.RequiresGlobalStatistics && queryInfo.FullTextSearchTerms != null && queryInfo.FullTextSearchTerms.Count > 0;
        }

        // Each count is read and written independently; a lookup may combine counts from different collections.
        private sealed class Impl : HybridSearchStatisticsCache
        {
            private readonly ConcurrentDictionary<CacheKey, CachedCount> counts = new ();
            private readonly TimeSpan maxAge;
            private readonly int maxEntries;
            private readonly Func<TimeSpan> getElapsedTime;

            public Impl(TimeSpan maxAge, int maxEntries, Func<TimeSpan> getElapsedTime)
            {
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
                HybridSearchQueryInfo queryInfo,
                ITrace trace,
                out GlobalFullTextSearchStatistics statistics)
            {
                bool found = this.TryGetCore(containerResourceId, containerETag, queryInfo, out statistics);
                trace.AddOrUpdateDatum(TraceDatumKey, found ? TraceDatumHit : TraceDatumMiss);
                return found;
            }

            private bool TryGetCore(
                string containerResourceId,
                string containerETag,
                HybridSearchQueryInfo queryInfo,
                out GlobalFullTextSearchStatistics statistics)
            {
                statistics = null;
                if (containerResourceId == null)
                {
                    throw new ArgumentNullException(nameof(containerResourceId));
                }

                if (containerETag == null)
                {
                    throw new ArgumentNullException(nameof(containerETag));
                }

                if (!IsQuerySupported(queryInfo))
                {
                    return false;
                }

                TimeSpan now = this.getElapsedTime();
                if (!this.TryGetLiveCount(CacheKey.CreateTotalDocumentCount(containerResourceId, containerETag), now, out long documentCount))
                {
                    return false;
                }

                List<FullTextSearchTerms> fullTextSearchTerms = queryInfo.FullTextSearchTerms;
                FullTextStatistics[] fullTextStatistics = new FullTextStatistics[fullTextSearchTerms.Count];
                for (int pathIndex = 0; pathIndex < fullTextStatistics.Length; pathIndex++)
                {
                    FullTextSearchTerms pathSearchTerms = fullTextSearchTerms[pathIndex];

                    if (!this.TryGetLiveCount(CacheKey.CreateDocumentPathWordCount(containerResourceId, containerETag, pathSearchTerms.Path), now, out long wordCount))
                    {
                        return false;
                    }

                    long[] hitCounts = new long[pathSearchTerms.Terms.Count];
                    for (int termIndex = 0; termIndex < hitCounts.Length; termIndex++)
                    {
                        FullTextSearchTerm term = pathSearchTerms.Terms[termIndex];
                        CacheKey hitCountKey = CacheKey.CreateDocumentPathTermHitCount(containerResourceId, containerETag, pathSearchTerms.Path, term.Term, term.Distance);
                        if (!this.TryGetLiveCount(hitCountKey, now, out hitCounts[termIndex]))
                        {
                            return false;
                        }
                    }

                    fullTextStatistics[pathIndex] = new FullTextStatistics(wordCount, hitCounts);
                }

                statistics = new GlobalFullTextSearchStatistics(documentCount, fullTextStatistics);
                return true;
            }

            public override bool TryAdd(
                string containerResourceId,
                string containerETag,
                HybridSearchQueryInfo queryInfo,
                GlobalFullTextSearchStatistics statistics,
                TimeSpan collectionStartedAt)
            {
                if (containerResourceId == null)
                {
                    throw new ArgumentNullException(nameof(containerResourceId));
                }

                if (containerETag == null)
                {
                    throw new ArgumentNullException(nameof(containerETag));
                }

                if (statistics == null)
                {
                    throw new ArgumentNullException(nameof(statistics));
                }

                TimeSpan now = this.getElapsedTime();
                if (!IsQuerySupported(queryInfo) || !this.IsFresh(collectionStartedAt, now))
                {
                    return false;
                }

                List<FullTextSearchTerms> fullTextSearchTerms = queryInfo.FullTextSearchTerms;
                Debug.Assert(statistics.FullTextStatistics.Count == fullTextSearchTerms.Count, "Statistics must have one entry per full-text path.");

                int entryCount = 1;
                for (int pathIndex = 0; pathIndex < fullTextSearchTerms.Count; pathIndex++)
                {
                    FullTextStatistics pathStatistics = statistics.FullTextStatistics[pathIndex];
                    Debug.Assert(pathStatistics.HitCounts.Length == fullTextSearchTerms[pathIndex].Terms.Count, "Statistics must have one hit count per term.");
                    entryCount += 1 + pathStatistics.HitCounts.Length;
                }

                // A response that cannot fit would only evict other entries and then itself.
                if (entryCount > this.maxEntries)
                {
                    return false;
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
                return true;
            }

            private void Set(CacheKey key, long value, TimeSpan collectionStartedAt)
            {
                CachedCount count = new (value, collectionStartedAt);

                // A slower, older collection must not replace a newer count.
                this.counts.AddOrUpdate(
                    key,
                    count,
                    (_, existing) => existing.CollectionStartedAt >= collectionStartedAt ? existing : count);
            }

            private bool TryGetLiveCount(CacheKey key, TimeSpan now, out long value)
            {
                value = 0;
                if (this.counts.TryGetValue(key, out CachedCount count) && this.IsFresh(count.CollectionStartedAt, now))
                {
                    value = count.Value;
                    return true;
                }

                // Removes an expired count. Best effort: a concurrent refresh of this key may also be removed, which only causes a later miss.
                this.counts.TryRemove(key, out _);
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

                // ToArray takes one consistent snapshot of the live dictionary.
                // Expired entries sort first, so one oldest-first pass removes them all and then only the remaining excess.
                KeyValuePair<CacheKey, CachedCount>[] entries = this.counts.ToArray();
                Array.Sort(entries, (left, right) => left.Value.CollectionStartedAt.CompareTo(right.Value.CollectionStartedAt));
                int excess = entries.Length - this.maxEntries;
                foreach (KeyValuePair<CacheKey, CachedCount> entry in entries)
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
            public static readonly NoOpImpl Instance = new ();

            private NoOpImpl()
            {
            }

            public override TimeSpan GetElapsedTime() => TimeSpan.Zero;

            public override bool TryGet(
                string containerResourceId,
                string containerETag,
                HybridSearchQueryInfo queryInfo,
                ITrace trace,
                out GlobalFullTextSearchStatistics statistics)
            {
                statistics = null;
                return false;
            }

            public override bool TryAdd(
                string containerResourceId,
                string containerETag,
                HybridSearchQueryInfo queryInfo,
                GlobalFullTextSearchStatistics statistics,
                TimeSpan collectionStartedAt)
            {
                return false;
            }
        }
    }
}
