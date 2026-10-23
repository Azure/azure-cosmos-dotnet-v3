//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;

    // The caller must restrict lookup and admission to Global full-text statistics.
    internal sealed class HybridSearchStatisticsCache : IDisposable
    {
        private readonly Dictionary<(string ResourceId, string ETag), ContainerStatistics> containerCaches =
            new Dictionary<(string ResourceId, string ETag), ContainerStatistics>();
        private readonly object lockObject = new object();
        private readonly TimeSpan maxAge;
        private readonly int maxEntries;
        private readonly Func<TimeSpan> getElapsedTime;
        private int entryCount;
        private bool disposed;

        public HybridSearchStatisticsCache(TimeSpan maxAge, int maxEntries, Func<TimeSpan> getElapsedTime = null)
        {
            if (maxAge <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAge));
            }

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

        public int Count
        {
            get
            {
                lock (this.lockObject)
                {
                    return this.entryCount;
                }
            }
        }

        public int ContainerRevisionCount
        {
            get
            {
                lock (this.lockObject)
                {
                    return this.containerCaches.Count;
                }
            }
        }

        public TimeSpan GetElapsedTime() => this.getElapsedTime();

        public bool TryGet(
            string containerResourceId,
            string containerETag,
            HybridSearchQueryInfo hybridSearchQueryInfo,
            out GlobalFullTextSearchStatistics statistics)
        {
            if (hybridSearchQueryInfo == null)
            {
                throw new ArgumentNullException(nameof(hybridSearchQueryInfo));
            }

            statistics = null;
            if (string.IsNullOrEmpty(containerResourceId) || string.IsNullOrEmpty(containerETag) ||
                !TryCreateKeys(hybridSearchQueryInfo, out List<(CosmosObject Path, CosmosObject[] Terms)> keys))
            {
                return false;
            }

            long documentCount;
            long[] wordCounts = new long[keys.Count];
            long[][] hitCounts = keys.Select(key => new long[key.Terms.Length]).ToArray();
            (string ResourceId, string ETag) containerRevision = (containerResourceId, containerETag);
            lock (this.lockObject)
            {
                if (this.disposed || !this.containerCaches.TryGetValue(containerRevision, out ContainerStatistics containerCache))
                {
                    return false;
                }

                TimeSpan now = this.getElapsedTime();
                if (!this.IsFresh(containerCache.DocumentCount.CollectionStartedAt, now))
                {
                    this.RemoveExpiredEntries(now);
                    return false;
                }

                documentCount = containerCache.DocumentCount.Value;
                for (int pathIndex = 0; pathIndex < keys.Count; pathIndex++)
                {
                    if (!containerCache.Paths.TryGetValue(keys[pathIndex].Path, out FullTextPathStatisticsCache pathCache))
                    {
                        return false;
                    }

                    if (!this.IsFresh(pathCache.TotalWordCount.CollectionStartedAt, now))
                    {
                        this.RemoveExpiredEntries(now);
                        return false;
                    }

                    wordCounts[pathIndex] = pathCache.TotalWordCount.Value;
                    if (!IsValidWordCount(documentCount, wordCounts[pathIndex]))
                    {
                        return false;
                    }

                    for (int termIndex = 0; termIndex < keys[pathIndex].Terms.Length; termIndex++)
                    {
                        if (!pathCache.HitCounts.TryGetValue(keys[pathIndex].Terms[termIndex], out CachedCount hitCount))
                        {
                            return false;
                        }

                        if (!this.IsFresh(hitCount.CollectionStartedAt, now))
                        {
                            this.RemoveExpiredEntries(now);
                            return false;
                        }

                        if (hitCount.Value > documentCount)
                        {
                            return false;
                        }

                        hitCounts[pathIndex][termIndex] = hitCount.Value;
                    }
                }
            }

            FullTextStatistics[] fullTextStatistics = new FullTextStatistics[keys.Count];
            for (int index = 0; index < fullTextStatistics.Length; index++)
            {
                fullTextStatistics[index] = new FullTextStatistics(wordCounts[index], hitCounts[index]);
            }

            statistics = new GlobalFullTextSearchStatistics(documentCount, Array.AsReadOnly(fullTextStatistics));
            return true;
        }

        public bool TryAdd(
            string containerResourceId,
            string containerETag,
            HybridSearchQueryInfo hybridSearchQueryInfo,
            GlobalFullTextSearchStatistics statistics,
            TimeSpan collectionStartedAt)
        {
            if (hybridSearchQueryInfo == null)
            {
                throw new ArgumentNullException(nameof(hybridSearchQueryInfo));
            }

            if (statistics == null)
            {
                throw new ArgumentNullException(nameof(statistics));
            }

            if (collectionStartedAt < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(collectionStartedAt));
            }

            if (string.IsNullOrEmpty(containerResourceId) || string.IsNullOrEmpty(containerETag) ||
                !this.IsFresh(collectionStartedAt, this.getElapsedTime()) ||
                !TryCreateKeys(hybridSearchQueryInfo, out List<(CosmosObject Path, CosmosObject[] Terms)> keys) ||
                !this.TryCreateSnapshot(keys, statistics, collectionStartedAt, out ContainerStatistics snapshot))
            {
                return false;
            }

            (string ResourceId, string ETag) containerRevision = (containerResourceId, containerETag);
            lock (this.lockObject)
            {
                TimeSpan now = this.getElapsedTime();
                if (this.disposed || !this.IsFresh(collectionStartedAt, now))
                {
                    return false;
                }

                this.RemoveExpiredEntries(now);
                this.containerCaches.TryGetValue(containerRevision, out ContainerStatistics containerCache);
                int newTermCount = 0;
                foreach (KeyValuePair<CosmosObject, FullTextPathStatisticsCache> path in snapshot.Paths)
                {
                    foreach (CosmosObject term in path.Value.HitCounts.Keys)
                    {
                        if (containerCache == null || !containerCache.Paths.TryGetValue(path.Key, out FullTextPathStatisticsCache pathCache) ||
                            !pathCache.HitCounts.ContainsKey(term))
                        {
                            newTermCount++;
                        }
                    }
                }

                while (this.entryCount > this.maxEntries - newTermCount)
                {
                    this.EvictOldestEntry(containerRevision, snapshot);
                }

                if (!this.containerCaches.TryGetValue(containerRevision, out containerCache))
                {
                    this.containerCaches.Add(containerRevision, snapshot);
                    this.entryCount += newTermCount;
                    return true;
                }

                bool updated = false;
                if (containerCache.DocumentCount.CollectionStartedAt < collectionStartedAt)
                {
                    containerCache.DocumentCount = snapshot.DocumentCount;
                    updated = true;
                }

                foreach (KeyValuePair<CosmosObject, FullTextPathStatisticsCache> path in snapshot.Paths)
                {
                    if (!containerCache.Paths.TryGetValue(path.Key, out FullTextPathStatisticsCache pathCache))
                    {
                        containerCache.Paths.Add(path.Key, path.Value);
                        updated = true;
                        continue;
                    }

                    if (pathCache.TotalWordCount.CollectionStartedAt < collectionStartedAt)
                    {
                        pathCache.TotalWordCount = path.Value.TotalWordCount;
                        updated = true;
                    }

                    foreach (KeyValuePair<CosmosObject, CachedCount> term in path.Value.HitCounts)
                    {
                        if (!pathCache.HitCounts.TryGetValue(term.Key, out CachedCount existingCount) ||
                            existingCount.CollectionStartedAt < collectionStartedAt)
                        {
                            pathCache.HitCounts[term.Key] = term.Value;
                            updated = true;
                        }
                    }
                }

                this.entryCount += newTermCount;
                return updated;
            }
        }

        public void Clear()
        {
            lock (this.lockObject)
            {
                this.containerCaches.Clear();
                this.entryCount = 0;
            }
        }

        public void Dispose()
        {
            lock (this.lockObject)
            {
                this.disposed = true;
                this.containerCaches.Clear();
                this.entryCount = 0;
            }
        }

        private static bool TryCreateKeys(
            HybridSearchQueryInfo hybridSearchQueryInfo,
            out List<(CosmosObject Path, CosmosObject[] Terms)> keys)
        {
            keys = null;
            List<FullTextSearchTerms> fullTextSearchTerms = hybridSearchQueryInfo.FullTextSearchTerms;
            if (!hybridSearchQueryInfo.RequiresGlobalStatistics || fullTextSearchTerms == null || fullTextSearchTerms.Count == 0 ||
                fullTextSearchTerms.Any(pathSearchTerms =>
                    pathSearchTerms?.Path == null || pathSearchTerms.Path.Count == 0 || pathSearchTerms.Path.Any(segment => segment == null) ||
                    pathSearchTerms.Terms == null || pathSearchTerms.Terms.Count == 0 ||
                    pathSearchTerms.Terms.Any(term => term?.Term == null || term.Distance < 0)))
            {
                return false;
            }

            keys = fullTextSearchTerms.Select(pathSearchTerms =>
                (
                    CosmosObject.Create(new Dictionary<string, CosmosElement>
                    {
                        ["path"] = CosmosArray.Create(pathSearchTerms.Path.Select(segment => CosmosString.Create(segment))),
                    }),
                    pathSearchTerms.Terms.Select(term => CosmosObject.Create(new Dictionary<string, CosmosElement>
                    {
                        ["term"] = CosmosString.Create(term.Term),
                        ["distance"] = term.Distance.HasValue ? CosmosNumber64.Create(term.Distance.Value) : CosmosNull.Create(),
                    })).ToArray()
                )).ToList();
            return true;
        }

        private static bool IsValidWordCount(long documentCount, long wordCount)
        {
            return wordCount >= 0 && (documentCount != 0 || wordCount == 0);
        }

        private bool TryCreateSnapshot(
            IReadOnlyList<(CosmosObject Path, CosmosObject[] Terms)> keys,
            GlobalFullTextSearchStatistics statistics,
            TimeSpan collectionStartedAt,
            out ContainerStatistics snapshot)
        {
            snapshot = null;
            if (statistics.DocumentCount < 0 || statistics.FullTextStatistics.Count != keys.Count)
            {
                return false;
            }

            ContainerStatistics result = new ContainerStatistics(new CachedCount(statistics.DocumentCount, collectionStartedAt));
            int termCount = 0;
            for (int pathIndex = 0; pathIndex < keys.Count; pathIndex++)
            {
                FullTextStatistics source = statistics.FullTextStatistics[pathIndex];
                if (source == null || source.HitCounts.Length != keys[pathIndex].Terms.Length ||
                    !IsValidWordCount(statistics.DocumentCount, source.TotalWordCount))
                {
                    return false;
                }

                if (!result.Paths.TryGetValue(keys[pathIndex].Path, out FullTextPathStatisticsCache pathCache))
                {
                    pathCache = new FullTextPathStatisticsCache(new CachedCount(source.TotalWordCount, collectionStartedAt));
                    result.Paths.Add(keys[pathIndex].Path, pathCache);
                }
                else if (pathCache.TotalWordCount.Value != source.TotalWordCount)
                {
                    return false;
                }

                for (int termIndex = 0; termIndex < keys[pathIndex].Terms.Length; termIndex++)
                {
                    long hitCount = source.HitCounts.Span[termIndex];
                    if (hitCount < 0 || hitCount > statistics.DocumentCount)
                    {
                        return false;
                    }

                    CosmosObject term = keys[pathIndex].Terms[termIndex];
                    if (pathCache.HitCounts.TryGetValue(term, out CachedCount existingCount))
                    {
                        if (existingCount.Value != hitCount)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if (termCount == this.maxEntries)
                        {
                            return false;
                        }

                        pathCache.HitCounts.Add(term, new CachedCount(hitCount, collectionStartedAt));
                        termCount++;
                    }
                }
            }

            snapshot = result;
            return true;
        }

        private bool IsFresh(TimeSpan collectionStartedAt, TimeSpan now)
        {
            return now >= collectionStartedAt && now - collectionStartedAt < this.maxAge;
        }

        private void RemoveExpiredEntries(TimeSpan now)
        {
            List<((string ResourceId, string ETag) ContainerRevision, CosmosObject Path, CosmosObject Term)> expiredEntries = null;
            foreach (KeyValuePair<(string ResourceId, string ETag), ContainerStatistics> containerCache in this.containerCaches)
            {
                foreach (KeyValuePair<CosmosObject, FullTextPathStatisticsCache> path in containerCache.Value.Paths)
                {
                    foreach (KeyValuePair<CosmosObject, CachedCount> term in path.Value.HitCounts)
                    {
                        if (!this.IsFresh(containerCache.Value.DocumentCount.CollectionStartedAt, now) ||
                            !this.IsFresh(path.Value.TotalWordCount.CollectionStartedAt, now) ||
                            !this.IsFresh(term.Value.CollectionStartedAt, now))
                        {
                            expiredEntries ??= new List<((string ResourceId, string ETag), CosmosObject, CosmosObject)>();
                            expiredEntries.Add((containerCache.Key, path.Key, term.Key));
                        }
                    }
                }
            }

            if (expiredEntries != null)
            {
                foreach (((string ResourceId, string ETag) containerRevision, CosmosObject path, CosmosObject term) in expiredEntries)
                {
                    this.RemoveEntry(containerRevision, path, term);
                }
            }
        }

        private void EvictOldestEntry((string ResourceId, string ETag) targetRevision, ContainerStatistics incoming)
        {
            (string ResourceId, string ETag) oldestContainerRevision = default;
            CosmosObject oldestPath = null;
            CosmosObject oldestTerm = null;
            TimeSpan oldestCollectionStartedAt = TimeSpan.MaxValue;
            foreach (KeyValuePair<(string ResourceId, string ETag), ContainerStatistics> containerCache in this.containerCaches)
            {
                foreach (KeyValuePair<CosmosObject, FullTextPathStatisticsCache> path in containerCache.Value.Paths)
                {
                    foreach (KeyValuePair<CosmosObject, CachedCount> term in path.Value.HitCounts)
                    {
                        // Keep already-cached terms needed by this publication when making room for its new terms.
                        if (containerCache.Key == targetRevision && incoming.Paths.TryGetValue(path.Key, out FullTextPathStatisticsCache incomingPath) &&
                            incomingPath.HitCounts.ContainsKey(term.Key))
                        {
                            continue;
                        }

                        if (oldestTerm == null || term.Value.CollectionStartedAt < oldestCollectionStartedAt)
                        {
                            oldestContainerRevision = containerCache.Key;
                            oldestPath = path.Key;
                            oldestTerm = term.Key;
                            oldestCollectionStartedAt = term.Value.CollectionStartedAt;
                        }
                    }
                }
            }

            this.RemoveEntry(oldestContainerRevision, oldestPath, oldestTerm);
        }

        private void RemoveEntry((string ResourceId, string ETag) containerRevision, CosmosObject path, CosmosObject term)
        {
            ContainerStatistics containerCache = this.containerCaches[containerRevision];
            FullTextPathStatisticsCache pathCache = containerCache.Paths[path];
            pathCache.HitCounts.Remove(term);
            this.entryCount--;
            if (pathCache.HitCounts.Count == 0)
            {
                containerCache.Paths.Remove(path);
            }

            if (containerCache.Paths.Count == 0)
            {
                this.containerCaches.Remove(containerRevision);
            }
        }

        private sealed class ContainerStatistics
        {
            public ContainerStatistics(CachedCount documentCount)
            {
                this.DocumentCount = documentCount;
            }

            public CachedCount DocumentCount { get; set; }

            public Dictionary<CosmosObject, FullTextPathStatisticsCache> Paths { get; } =
                new Dictionary<CosmosObject, FullTextPathStatisticsCache>();
        }

        private sealed class FullTextPathStatisticsCache
        {
            public FullTextPathStatisticsCache(CachedCount totalWordCount)
            {
                this.TotalWordCount = totalWordCount;
            }

            public CachedCount TotalWordCount { get; set; }

            public Dictionary<CosmosObject, CachedCount> HitCounts { get; } = new Dictionary<CosmosObject, CachedCount>();
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
}
