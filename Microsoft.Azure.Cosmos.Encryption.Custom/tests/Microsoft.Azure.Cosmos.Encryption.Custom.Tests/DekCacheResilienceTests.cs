//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Caching.Distributed;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    /// <summary>
    /// Behavioral specification for the distributed-cache resilience feature added by PR #5428.
    ///
    /// Stated goal of the feature:
    ///   "When the in-process DekCache TTL expires, the next encrypted request would normally
    ///    refetch DataEncryptionKeyProperties from Cosmos metadata. If Cosmos is unavailable
    ///    at that moment, the request fails. This PR adds an IDistributedCache L2 so a
    ///    peer-populated cache entry can rescue the request."
    ///
    /// The tests below describe what that feature must do from a user's perspective, not what
    /// the current implementation happens to do. They are intended to pass after the bugs in
    /// the current implementation are fixed and to fail while the bugs remain.
    ///
    /// Each test uses:
    ///   - <see cref="ClockControlledDistributedCache"/> — a test double for IDistributedCache
    ///     that respects the same injectable clock as DekCache, so the test-double's own
    ///     expiry logic cannot silently skew the scenario.
    ///   - Explicit wall-clock control via a <c>Func&lt;DateTime&gt; utcNow</c> so each test
    ///     describes a precise timeline without any real sleep.
    /// </summary>
    [TestClass]
    public class DekCacheResilienceTests
    {
        private const string DekId = "testDek";
        private const string DefaultCachePrefix = "dek";
        private const string DefaultCacheKey = DefaultCachePrefix + ":v1:" + DekId;

        private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(30);

        // ------------------------------------------------------------
        // Group 1 — Cold-miss path (L1 has no entry)
        // ------------------------------------------------------------

        /// <summary>
        /// Baseline: on a cold L1 miss, if L2 holds a fresh entry, the cache must serve
        /// from L2 without invoking Cosmos. This is the only path that works in the current
        /// implementation and it anchors the rest of the suite.
        /// </summary>
        [TestMethod]
        public async Task ColdL1_L2HasFreshEntry_ServesFromL2()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            // Peer has written a fresh L2 entry.
            SeedL2(l2, DefaultCacheKey, expiry: now.AddMinutes(30), clock: () => now);

            int cosmosCalls = 0;
            DataEncryptionKeyProperties result = await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => { cosmosCalls++; return Task.FromResult(MakeDekProperties(id)); },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(DekId, result.Id);
            Assert.AreEqual(0, cosmosCalls, "L2 had a fresh entry; Cosmos must not be called.");
        }

        /// <summary>
        /// Cold L1, no L2 entry, Cosmos available — must fetch from Cosmos and populate both caches.
        /// </summary>
        [TestMethod]
        public async Task ColdL1_NoL2Entry_CosmosAvailable_FetchesAndPopulatesBothCaches()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            int cosmosCalls = 0;
            await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => { cosmosCalls++; return Task.FromResult(MakeDekProperties(id)); },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(1, cosmosCalls);
            Assert.IsTrue(l2.ContainsKey(DefaultCacheKey), "L2 must be populated on first fetch.");
        }

        /// <summary>
        /// Cold L1, no L2 entry, Cosmos unavailable — the request must surface Cosmos's error;
        /// there is no cached value anywhere. This establishes the negative path so we know
        /// other failing-Cosmos tests are succeeding only because of L2.
        /// </summary>
        [TestMethod]
        public async Task ColdL1_NoL2Entry_CosmosFails_SurfacesCosmosError()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            InvalidOperationException thrown = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => cache.GetOrAddDekPropertiesAsync(
                    DekId,
                    FailingFetcher,
                    CosmosDiagnosticsContext.Create(null),
                    CancellationToken.None));

            StringAssert.Contains(thrown.Message, "simulated cosmos outage");
        }

        // ------------------------------------------------------------
        // Group 2 — L1 TTL-expiry path (the core resilience claim of PR #5428)
        // ------------------------------------------------------------

        /// <summary>
        /// Resilience premise: L1 expired, L2 has a fresh peer-written entry, Cosmos is down.
        /// The request must succeed using the L2 entry.
        /// </summary>
        [TestMethod]
        public async Task ExpiredL1_L2HasFreshEntry_CosmosFails_ServesFromL2()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            // Warm L1 (and L2) with an initial Cosmos fetch.
            await cache.GetOrAddDekPropertiesAsync(
                DekId, HealthyFetcher, CosmosDiagnosticsContext.Create(null), CancellationToken.None);

            // Advance past L1's expiry.
            now = now.AddMinutes(31);

            // A peer writes a fresh entry to L2 (expires well after our clock).
            SeedL2(l2, DefaultCacheKey, expiry: now.AddMinutes(30), clock: () => now);

            int cosmosCalls = 0;
            DataEncryptionKeyProperties result = await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => { cosmosCalls++; throw new InvalidOperationException("simulated cosmos outage"); },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(DekId, result.Id);
            Assert.AreEqual(0, cosmosCalls, "L2 had a fresh entry; Cosmos must not be called on the expiry path.");
        }

        /// <summary>
        /// L1 expired, L2 has a fresh entry, Cosmos available — the cache SHOULD prefer L2
        /// to minimize load on Cosmos metadata during metadata backpressure. (A peer just
        /// populated L2 milliseconds ago; re-fetching would be wasted work.)
        /// </summary>
        [TestMethod]
        public async Task ExpiredL1_L2HasFreshEntry_CosmosAvailable_PrefersL2()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            await cache.GetOrAddDekPropertiesAsync(
                DekId, HealthyFetcher, CosmosDiagnosticsContext.Create(null), CancellationToken.None);

            now = now.AddMinutes(31);
            SeedL2(l2, DefaultCacheKey, expiry: now.AddMinutes(30), clock: () => now);

            int cosmosCalls = 0;
            await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => { cosmosCalls++; return Task.FromResult(MakeDekProperties(id)); },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(0, cosmosCalls, "A fresh L2 entry on the expiry path must be used in preference to Cosmos.");
        }

        /// <summary>
        /// L1 expired, L2 is empty, Cosmos available — the cache must fall through to Cosmos
        /// and repopulate both layers.
        /// </summary>
        [TestMethod]
        public async Task ExpiredL1_NoL2Entry_CosmosAvailable_FetchesFromCosmosAndRepopulates()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            await cache.GetOrAddDekPropertiesAsync(
                DekId, HealthyFetcher, CosmosDiagnosticsContext.Create(null), CancellationToken.None);

            // Clear L2 to simulate "no peer has populated yet or L2 entry expired".
            l2.RemoveForTest(DefaultCacheKey);
            now = now.AddMinutes(31);

            int cosmosCalls = 0;
            await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => { cosmosCalls++; return Task.FromResult(MakeDekProperties(id)); },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(1, cosmosCalls, "With L2 empty the expiry path must fetch from Cosmos.");
            Assert.IsTrue(l2.ContainsKey(DefaultCacheKey), "The fresh fetch must repopulate L2.");
        }

        /// <summary>
        /// L1 expired, L2 is empty, Cosmos unavailable — the request must surface Cosmos's error.
        /// (L2 is not a magic wand when nobody populated it.)
        /// </summary>
        [TestMethod]
        public async Task ExpiredL1_NoL2Entry_CosmosFails_SurfacesCosmosError()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            await cache.GetOrAddDekPropertiesAsync(
                DekId, HealthyFetcher, CosmosDiagnosticsContext.Create(null), CancellationToken.None);

            l2.RemoveForTest(DefaultCacheKey);
            now = now.AddMinutes(31);

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => cache.GetOrAddDekPropertiesAsync(
                    DekId, FailingFetcher, CosmosDiagnosticsContext.Create(null), CancellationToken.None));
        }

        // ------------------------------------------------------------
        // Group 3 — L2 TTL coupling (the second blocker)
        // ------------------------------------------------------------

        /// <summary>
        /// When the cache writes to L2, L2's absolute expiry must outlive L1's expiry.
        /// Otherwise the L2 entry is gone at the exact moment another peer needs it,
        /// and the feature cannot activate. The test writes via the public API and
        /// asserts the raw AbsoluteExpiration option used at the IDistributedCache boundary.
        /// </summary>
        [TestMethod]
        public async Task SetDekProperties_L2AbsoluteExpirationOutlivesL1Expiry()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            cache.SetDekProperties(DekId, MakeDekProperties(DekId));
            await cache.LastDistributedCacheWriteTask;

            Assert.IsTrue(l2.TryGetAbsoluteExpiration(DefaultCacheKey, out DateTimeOffset? absExp));
            Assert.IsTrue(absExp.HasValue);

            DateTime l1Expiry = now + DefaultTtl;
            Assert.IsTrue(
                absExp.Value.UtcDateTime > l1Expiry,
                $"L2 absolute expiry ({absExp.Value.UtcDateTime:o}) must be strictly greater than L1 expiry ({l1Expiry:o}) so L2 can rescue peers after L1 expiry.");
        }

        /// <summary>
        /// When reading from L2, an entry whose server-side "freshness" timestamp has just
        /// passed must still be usable as a last-resort rescue when Cosmos is unreachable.
        /// The read-side validity check must not couple to the same timestamp that drives
        /// L1's expiry.
        /// </summary>
        [TestMethod]
        public async Task ExpiredL1_L2EntryJustPassedFreshness_CosmosFails_StillServesFromL2()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            cache.SetDekProperties(DekId, MakeDekProperties(DekId));
            await cache.LastDistributedCacheWriteTask;
            Assert.IsTrue(l2.ContainsKey(DefaultCacheKey), "Setup: L2 must be populated.");

            // Advance clock just past L1/freshness expiry.
            now = now.AddMinutes(31);

            int cosmosCalls = 0;
            DataEncryptionKeyProperties result = await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => { cosmosCalls++; throw new InvalidOperationException("simulated cosmos outage"); },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(DekId, result.Id, "The recently-stored L2 entry must serve as a fallback.");
            Assert.AreEqual(0, cosmosCalls, "L2 must be consulted before Cosmos on the expiry path.");
        }

        // ------------------------------------------------------------
        // Group 4 — Integrity & correctness around L2 data
        // ------------------------------------------------------------

        /// <summary>
        /// A corrupted (unparseable) L2 payload must be treated as a miss and fall through
        /// to Cosmos. It must not surface as an opaque error at encrypt/decrypt time.
        /// </summary>
        [TestMethod]
        public async Task L2ReturnsCorruptedJson_FallsThroughToCosmos()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            l2.SetRawForTest(DefaultCacheKey, Encoding.UTF8.GetBytes("not-json-at-all"));

            int cosmosCalls = 0;
            DataEncryptionKeyProperties result = await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => { cosmosCalls++; return Task.FromResult(MakeDekProperties(id)); },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(DekId, result.Id);
            Assert.AreEqual(1, cosmosCalls, "Corrupted L2 must behave as a miss.");
        }

        /// <summary>
        /// A valid-shape-but-partial L2 payload (e.g. missing WrappedDataEncryptionKey) must
        /// be treated as a miss and fall through to Cosmos rather than bubbling a
        /// NullReferenceException from the unwrap pipeline.
        /// </summary>
        [TestMethod]
        public async Task L2ReturnsPartialDekProperties_FallsThroughToCosmos()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            // Build a JSON whose ServerProperties has no WrappedDataEncryptionKey / metadata.
            string partialJson = JsonConvert.SerializeObject(new
            {
                v = 1,
                serverProperties = new { id = DekId, _self = "x" },
                serverPropertiesExpiryUtc = now.AddHours(1),
            });
            l2.SetRawForTest(DefaultCacheKey, Encoding.UTF8.GetBytes(partialJson));

            int cosmosCalls = 0;
            DataEncryptionKeyProperties result = await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => { cosmosCalls++; return Task.FromResult(MakeDekProperties(id)); },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(DekId, result.Id);
            Assert.AreEqual(1, cosmosCalls, "Partial L2 payload must be rejected and treated as a miss.");
        }

        /// <summary>
        /// A future-version peer's entry must survive a present-version peer's read/write
        /// cycle. A rolling-upgrade fleet that shares one distributed cache must not see a
        /// v1 SDK overwrite a v2 SDK's entry on every read.
        /// The version is scoped into the cache key itself ("{prefix}:v{N}:{id}") so v1 and
        /// v2 writers occupy disjoint slots and cannot downgrade each other.
        /// </summary>
        [TestMethod]
        public async Task L2ContainsFutureVersion_DoesNotOverwriteOnFallbackFetch()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            // A peer SDK at format version 2 writes to its own slot. The current SDK (v1) must
            // not touch this slot — it reads/writes at ":v1:{id}" only.
            string v2Slot = DefaultCachePrefix + ":v2:" + DekId;
            byte[] originalV2 = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
            {
                v = 2,
                serverProperties = MakeDekProperties(DekId),
                serverPropertiesExpiryUtc = now.AddHours(1),
            }));
            l2.SetRawForTest(v2Slot, originalV2);

            await cache.GetOrAddDekPropertiesAsync(
                DekId, HealthyFetcher, CosmosDiagnosticsContext.Create(null), CancellationToken.None);

            byte[] afterRead = l2.GetRawForTest(v2Slot);
            CollectionAssert.AreEqual(
                originalV2,
                afterRead,
                "A v1 SDK must not touch a v2 peer's L2 slot — the cache key must scope by format version.");
        }

        /// <summary>
        /// Two providers pointing at different DEK containers that share one distributed cache
        /// must not collide on identical DEK ids. The feature accomplishes this by REQUIRING a
        /// distinct cacheKeyPrefix from every caller that enables the distributed cache; the
        /// API must refuse to construct without one so the collision cannot silently occur.
        /// Positive proof that distinct prefixes do isolate entries is covered by
        /// DekCacheDistributedCacheTests.DekCache_MultiTenantScenario_IsolatesCacheEntriesByPrefix.
        /// </summary>
        [TestMethod]
        public void TwoProvidersWithDefaults_ConstructionRequiresExplicitPrefix()
        {
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => DateTime.UtcNow);

            // Attempting to construct a DekCache that participates in a distributed cache without
            // supplying a cacheKeyPrefix must fail loudly. This forces callers to partition the
            // keyspace explicitly (e.g. by container RID) rather than inheriting a collision-prone
            // default. Null is rejected with ArgumentNullException.
            ArgumentException ex = Assert.ThrowsException<ArgumentNullException>(
                () => new DekCache(
                    dekPropertiesTimeToLive: DefaultTtl,
                    distributedCache: l2,
                    cacheKeyPrefix: null));
            Assert.AreEqual("cacheKeyPrefix", ex.ParamName);
        }

        // ------------------------------------------------------------
        // Group 5 — Lifecycle / race conditions
        // ------------------------------------------------------------

        /// <summary>
        /// Background proactive-refresh failure must not poison L1. A subsequent call must
        /// still have either a cached value or a clean re-fetch; the previous good value
        /// must not be replaced by a faulted AsyncLazy.
        /// </summary>
        [TestMethod]
        public async Task ProactiveRefreshFailure_DoesNotPoisonL1()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: DefaultTtl,
                distributedCache: l2,
                refreshBeforeExpiry: TimeSpan.FromMinutes(5),
                cacheKeyPrefix: DefaultCachePrefix,
                utcNow: () => now);

            await cache.GetOrAddDekPropertiesAsync(
                DekId, HealthyFetcher, CosmosDiagnosticsContext.Create(null), CancellationToken.None);

            // Move into the proactive-refresh window (<5 min remaining).
            now = now.AddMinutes(26);

            // The background refresh fetcher will signal this TCS immediately before throwing, so
            // we can deterministically wait for the fault to land without a real-clock delay.
            TaskCompletionSource<bool> bgRefreshInvoked = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            DataEncryptionKeyProperties synchronous = await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) =>
                {
                    bgRefreshInvoked.TrySetResult(true);
                    throw new InvalidOperationException("simulated cosmos outage in bg refresh");
                },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);
            Assert.AreEqual(DekId, synchronous.Id, "The synchronous call within the refresh window returns the still-fresh cached value.");

            // Wait deterministically for the background refresh fetcher to have been invoked.
            bool fired = await bgRefreshInvoked.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(fired, "Background refresh fetcher must have been invoked.");

            // A subsequent call while the cached value is still fresh must not surface the bg-refresh error.
            int cosmosCalls = 0;
            DataEncryptionKeyProperties next = await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => { cosmosCalls++; return Task.FromResult(MakeDekProperties(id)); },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(DekId, next.Id);
            Assert.IsTrue(
                cosmosCalls <= 1,
                "A faulted background refresh must not require a synchronous re-fetch beyond one clean attempt.");
        }

        // ------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------

        private static DateTime NewClock()
        {
            return new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        private static DekCache NewCache(TimeSpan ttl, IDistributedCache l2, Func<DateTime> utcNow)
        {
            return new DekCache(
                dekPropertiesTimeToLive: ttl,
                distributedCache: l2,
                cacheKeyPrefix: DefaultCachePrefix,
                utcNow: utcNow);
        }

        private static DataEncryptionKeyProperties MakeDekProperties(string id, byte[] wrappedKey = null)
        {
            return new DataEncryptionKeyProperties(
                id,
                "AEAD_AES_256_CBC_HMAC_SHA256",
                wrappedKey ?? new byte[] { 1, 2, 3 },
                new EncryptionKeyWrapMetadata("test", "test", "RSA-OAEP", "test"),
                DateTime.UtcNow);
        }

        private static Task<DataEncryptionKeyProperties> HealthyFetcher(string id, CosmosDiagnosticsContext ctx, CancellationToken ct)
        {
            return Task.FromResult(MakeDekProperties(id));
        }

        private static Task<DataEncryptionKeyProperties> FailingFetcher(string id, CosmosDiagnosticsContext ctx, CancellationToken ct)
        {
            throw new InvalidOperationException("simulated cosmos outage");
        }

        /// <summary>
        /// Seeds an L2 entry using the same JSON shape DekCache produces, with a caller-chosen
        /// freshness expiry. Decouples L2's stamped expiry from L1's expiry so tests can exercise
        /// the "peer wrote fresh L2 a moment ago" scenario precisely.
        /// </summary>
        private static void SeedL2(IDistributedCache l2, string cacheKey, DateTime expiry, Func<DateTime> clock)
        {
            object payload = new
            {
                v = 1,
                serverProperties = MakeDekProperties(DekId),
                serverPropertiesExpiryUtc = expiry,
            };

            string json = JsonConvert.SerializeObject(
                payload,
                new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.None,
                    DateFormatHandling = DateFormatHandling.IsoDateFormat,
                    DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                });

            l2.SetAsync(
                cacheKey,
                Encoding.UTF8.GetBytes(json),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpiration = new DateTimeOffset(expiry, TimeSpan.Zero),
                }).GetAwaiter().GetResult();
        }

        /// <summary>
        /// IDistributedCache test double that honours an injected clock for auto-expiration and
        /// exposes raw byte / absolute-expiration inspection for assertions. Mirrors the shape of
        /// real IDistributedCache implementations without coupling test outcomes to real time.
        /// </summary>
        private sealed class ClockControlledDistributedCache : IDistributedCache
        {
            private readonly ConcurrentDictionary<string, Entry> store = new ConcurrentDictionary<string, Entry>();
            private readonly Func<DateTime> utcNow;

            public ClockControlledDistributedCache(Func<DateTime> utcNow)
            {
                this.utcNow = utcNow;
            }

            public byte[] Get(string key) => this.GetAsync(key).GetAwaiter().GetResult();

            public Task<byte[]> GetAsync(string key, CancellationToken token = default)
            {
                if (this.store.TryGetValue(key, out Entry entry))
                {
                    if (entry.AbsoluteExpiration.HasValue
                        && entry.AbsoluteExpiration.Value.UtcDateTime <= this.utcNow())
                    {
                        this.store.TryRemove(key, out _);
                        return Task.FromResult<byte[]>(null);
                    }

                    return Task.FromResult(entry.Value);
                }

                return Task.FromResult<byte[]>(null);
            }

            public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
                => this.SetAsync(key, value, options).GetAwaiter().GetResult();

            public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
            {
                this.store[key] = new Entry
                {
                    Value = value,
                    AbsoluteExpiration = options?.AbsoluteExpiration,
                };

                return Task.CompletedTask;
            }

            public void Remove(string key) => this.store.TryRemove(key, out _);

            public Task RemoveAsync(string key, CancellationToken token = default)
            {
                this.Remove(key);
                return Task.CompletedTask;
            }

            public void Refresh(string key) { }

            public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

            // ---- test-only inspection helpers ----

            public bool ContainsKey(string key) => this.store.ContainsKey(key);

            public void RemoveForTest(string key) => this.store.TryRemove(key, out _);

            public void SetRawForTest(string key, byte[] bytes)
            {
                this.store[key] = new Entry { Value = bytes, AbsoluteExpiration = null };
            }

            public byte[] GetRawForTest(string key)
            {
                return this.store.TryGetValue(key, out Entry entry) ? entry.Value : null;
            }

            public bool TryGetAbsoluteExpiration(string key, out DateTimeOffset? absoluteExpiration)
            {
                if (this.store.TryGetValue(key, out Entry entry))
                {
                    absoluteExpiration = entry.AbsoluteExpiration;
                    return true;
                }

                absoluteExpiration = null;
                return false;
            }

            private sealed class Entry
            {
                public byte[] Value { get; set; }

                public DateTimeOffset? AbsoluteExpiration { get; set; }
            }
        }
    }
}
