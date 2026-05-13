//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Caching.Distributed;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Contract-shaped behavioral specification for the DekCache distributed-cache (L2) feature:
    /// behaviors the SDK must honor for any conformant <see cref="IDistributedCache"/>
    /// implementation. Uses a deterministic clock and an in-memory test double; no wall-clock sleeps.
    /// </summary>
    [TestClass]
    public class DekCacheDistributedCacheContractTests
    {
        private const string DekId = "testDek";
        private const string DefaultCachePrefix = "dek";
        private const string DefaultCacheKey = DefaultCachePrefix + ":v1:" + DekId;

        private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(30);

        // ------------------------------------------------------------
        // Group A — Null / empty return semantics of GetAsync
        // ------------------------------------------------------------

        [TestMethod]
        public async Task GetAsync_ReturnsEmptyByteArray_IsTreatedAsGracefulMiss()
        {
            DateTime now = NewClock();
            SpyDistributedCache l2 = new SpyDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            l2.SetRawForTest(DefaultCacheKey, Array.Empty<byte>());

            int cosmosCalls = 0;
            DataEncryptionKeyProperties result = await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => { cosmosCalls++; return Task.FromResult(MakeDekProperties(id)); },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.IsNotNull(result, "Empty L2 payload must not cause a null result to the caller.");
            Assert.AreEqual(DekId, result.Id);
            Assert.AreEqual(1, cosmosCalls, "Empty L2 payload must behave as a miss and fall through to the source fetcher.");
        }

        // ------------------------------------------------------------
        // Group B — Exception semantics from L2 GetAsync
        // ------------------------------------------------------------

        [TestMethod]
        public async Task GetAsync_ThrowsTimeoutException_FallsBackToCosmos()
        {
            DateTime now = NewClock();
            ThrowingDistributedCache l2 = new ThrowingDistributedCache(() => new TimeoutException("simulated L2 timeout"));
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            int cosmosCalls = 0;
            DataEncryptionKeyProperties result = await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => { cosmosCalls++; return Task.FromResult(MakeDekProperties(id)); },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(DekId, result.Id);
            Assert.AreEqual(1, cosmosCalls, "A TimeoutException from L2 must be swallowed and the source fetcher invoked.");
        }

        [TestMethod]
        public async Task GetAsync_ThrowsOperationCanceledException_CallerTokenNotCancelled_MustNotFailTheOperation()
        {
            DateTime now = NewClock();
            ThrowingDistributedCache l2 = new ThrowingDistributedCache(() => new OperationCanceledException("internal L2 timeout, not caller's CT"));
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            int cosmosCalls = 0;
            CancellationTokenSource callerCts = new CancellationTokenSource(); // NOT cancelled

            DataEncryptionKeyProperties result = await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => { cosmosCalls++; return Task.FromResult(MakeDekProperties(id)); },
                CosmosDiagnosticsContext.Create(null),
                callerCts.Token);

            Assert.AreEqual(DekId, result.Id);
            Assert.AreEqual(
                1,
                cosmosCalls,
                "When the caller's CT is not cancelled, an OperationCanceledException originating inside the L2 implementation must not prevent the operation from completing.");
        }

        // ------------------------------------------------------------
        // Group C — Cancellation propagation
        // ------------------------------------------------------------

        [TestMethod]
        public async Task GetOrAddDekPropertiesAsync_CallerTokenAlreadyCancelled_ThrowsOperationCanceledExceptionPromptly()
        {
            DateTime now = NewClock();
            SpyDistributedCache l2 = new SpyDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            int cosmosCalls = 0;
            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => cache.GetOrAddDekPropertiesAsync(
                    DekId,
                    (id, ctx, ct) => { cosmosCalls++; return Task.FromResult(MakeDekProperties(id)); },
                    CosmosDiagnosticsContext.Create(null),
                    cts.Token));

            Assert.AreEqual(0, cosmosCalls, "A pre-cancelled token must prevent the fetcher from running.");
            Assert.AreEqual(0, l2.GetCount, "A pre-cancelled token must prevent the L2 GetAsync from running.");
        }

        [TestMethod]
        public async Task SetDekProperties_BackgroundL2Write_DecoupledFromCallerCancellation()
        {
            DateTime now = NewClock();
            SpyDistributedCache l2 = new SpyDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            using CancellationTokenSource callerCts = new CancellationTokenSource();

            cache.SetDekProperties(DekId, MakeDekProperties(DekId));

            // Caller's lifecycle ends / is cancelled — the background write must still finish.
            callerCts.Cancel();

            await cache.WhenAllPendingWritesAsync();

            Assert.IsTrue(l2.ContainsKey(DefaultCacheKey), "Background L2 write must complete independently of caller cancellation.");
            Assert.AreEqual(1, l2.SetCount, "Exactly one background SetAsync must have executed.");
        }

        // ------------------------------------------------------------
        // Group D — SetAsync contract compliance
        // ------------------------------------------------------------

        [TestMethod]
        public async Task SetAsync_IsCalledWithNonNullDistributedCacheEntryOptions()
        {
            DateTime now = NewClock();
            SpyDistributedCache l2 = new SpyDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            cache.SetDekProperties(DekId, MakeDekProperties(DekId));
            await cache.WhenAllPendingWritesAsync();

            Assert.AreEqual(1, l2.SetCount);
            Assert.IsTrue(l2.TryGetLastSetOptions(DefaultCacheKey, out DistributedCacheEntryOptions options));
            Assert.IsNotNull(options, "SetAsync must receive a non-null DistributedCacheEntryOptions.");
        }

        [TestMethod]
        public async Task SetAsync_AbsoluteExpirationIsSetToAFutureInstant()
        {
            DateTime now = NewClock();
            SpyDistributedCache l2 = new SpyDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            cache.SetDekProperties(DekId, MakeDekProperties(DekId));
            await cache.WhenAllPendingWritesAsync();

            Assert.IsTrue(l2.TryGetLastSetOptions(DefaultCacheKey, out DistributedCacheEntryOptions options));
            Assert.IsNotNull(options);
            Assert.IsTrue(options.AbsoluteExpiration.HasValue, "AbsoluteExpiration must be set so peers can determine freshness.");
            Assert.IsTrue(
                options.AbsoluteExpiration.Value.UtcDateTime > now,
                $"AbsoluteExpiration ({options.AbsoluteExpiration.Value.UtcDateTime:o}) must be strictly after 'now' ({now:o}).");
            Assert.AreNotEqual(DateTimeOffset.MinValue, options.AbsoluteExpiration.Value, "AbsoluteExpiration must not be DateTimeOffset.MinValue.");
        }

        // ------------------------------------------------------------
        // Group E — Null distributedCache (feature disabled)
        // ------------------------------------------------------------

        [TestMethod]
        public async Task NullDistributedCache_AllOperationsBehaveAsPreFeatureDekCache()
        {
            DateTime now = NewClock();
            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: DefaultTtl,
                distributedCache: null,
                utcNow: () => now);

            int cosmosCalls = 0;

            // GetOrAdd: source-only
            DataEncryptionKeyProperties fromGet = await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => { cosmosCalls++; return Task.FromResult(MakeDekProperties(id)); },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);
            Assert.AreEqual(DekId, fromGet.Id);
            Assert.AreEqual(1, cosmosCalls);

            // Set: no throw, no L2 task needed
            cache.SetDekProperties("otherDek", MakeDekProperties("otherDek"));
            await cache.WhenAllPendingWritesAsync(); // must be Task.CompletedTask (default)

            // Remove: no throw
            await cache.RemoveAsync(DekId);

            // A second GetOrAdd after Remove re-fetches from the source (memory cache was cleared,
            // no L2 to rescue from).
            await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => { cosmosCalls++; return Task.FromResult(MakeDekProperties(id)); },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);
            Assert.AreEqual(2, cosmosCalls, "With no L2, RemoveAsync must force the next call to re-fetch from the source.");
        }

        // ------------------------------------------------------------
        // Group F — Concurrency with a non-thread-safe-assumption L2
        // ------------------------------------------------------------

        [TestMethod]
        public async Task ConcurrentGetOrAdd_WithSerializingL2_CompletesWithoutDeadlockOrError()
        {
            DateTime now = NewClock();
            SerializingDistributedCache l2 = new SerializingDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            const int parallelism = 16;
            Task<DataEncryptionKeyProperties>[] tasks = new Task<DataEncryptionKeyProperties>[parallelism];
            for (int i = 0; i < parallelism; i++)
            {
                string id = "dek-" + i;
                tasks[i] = cache.GetOrAddDekPropertiesAsync(
                    id,
                    (k, ctx, ct) => Task.FromResult(MakeDekProperties(k)),
                    CosmosDiagnosticsContext.Create(null),
                    CancellationToken.None);
            }

            DataEncryptionKeyProperties[] results = await Task.WhenAll(tasks);

            Assert.AreEqual(parallelism, results.Length);
            for (int i = 0; i < parallelism; i++)
            {
                Assert.IsNotNull(results[i]);
                Assert.AreEqual("dek-" + i, results[i].Id);
            }

            Assert.IsFalse(l2.ObservedReentrancyViolation, "The serializing L2 fake must never have been entered re-entrantly by the SDK.");
        }

        // ------------------------------------------------------------
        // Group G — Refresh is never called
        // ------------------------------------------------------------

        [TestMethod]
        public async Task RefreshAsync_IsNeverCalledByAnySdkOperation()
        {
            DateTime now = NewClock();
            SpyDistributedCache l2 = new SpyDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            // Cold-miss GetOrAdd
            await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => Task.FromResult(MakeDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // Warm-hit GetOrAdd
            await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => Task.FromResult(MakeDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // SetDekProperties (fire-and-forget)
            cache.SetDekProperties("otherDek", MakeDekProperties("otherDek"));
            await cache.WhenAllPendingWritesAsync();

            // RemoveAsync
            await cache.RemoveAsync(DekId);

            Assert.AreEqual(0, l2.RefreshCount, "DekCache must never invoke IDistributedCache.RefreshAsync.");
        }

        // ------------------------------------------------------------
        // Group H — Set semantics (write frequency)
        // ------------------------------------------------------------

        [TestMethod]
        public async Task ColdMiss_WritesToL2_ExactlyOnce()
        {
            DateTime now = NewClock();
            SpyDistributedCache l2 = new SpyDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => Task.FromResult(MakeDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // Cold-path L2 hydration is fire-and-forget; await its completion before
            // observing l2.SetCount.
            await cache.WhenAllPendingWritesAsync();

            Assert.AreEqual(1, l2.SetCount, "Cold miss must perform exactly one L2 SetAsync.");
        }

        [TestMethod]
        public async Task WarmL1Hit_DoesNotWriteToL2()
        {
            DateTime now = NewClock();
            SpyDistributedCache l2 = new SpyDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            // Prime L1 + L2.
            await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => Task.FromResult(MakeDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // Cold-path L2 hydration is fire-and-forget; await its completion before
            // sampling l2.SetCount.
            await cache.WhenAllPendingWritesAsync();

            int setCountAfterFirst = l2.SetCount;
            Assert.AreEqual(1, setCountAfterFirst);

            // Second call — L1 hit, L2 must not be re-written.
            await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => Task.FromResult(MakeDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // No new L2 write is expected on a warm hit, but await the seam so the
            // observation is deterministic regardless of any prior state.
            await cache.WhenAllPendingWritesAsync();

            Assert.AreEqual(setCountAfterFirst, l2.SetCount, "A warm L1 hit must not trigger any additional L2 write.");
        }

        // ------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------

        private static DateTime NewClock() => new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static DekCache NewCache(TimeSpan ttl, IDistributedCache l2, Func<DateTime> utcNow)
        {
            return new DekCache(
                dekPropertiesTimeToLive: ttl,
                distributedCache: l2,
                utcNow: utcNow,
                cacheKeyPrefix: DefaultCachePrefix);
        }

        private static DataEncryptionKeyProperties MakeDekProperties(string id)
        {
            return new DataEncryptionKeyProperties(
                id,
                "AEAD_AES_256_CBC_HMAC_SHA256",
                new byte[] { 1, 2, 3 },
                new EncryptionKeyWrapMetadata("test", "test", "RSA-OAEP", "test"),
                DateTime.UtcNow);
        }

        // ------------------------------------------------------------
        // Test doubles
        // ------------------------------------------------------------

        /// <summary>
        /// Clock-respecting IDistributedCache spy that records invocation counts, raw writes, and
        /// the DistributedCacheEntryOptions used on the most recent SetAsync for each key. Thread-safe.
        /// </summary>
        private sealed class SpyDistributedCache : IDistributedCache
        {
            private readonly ConcurrentDictionary<string, Entry> store = new ConcurrentDictionary<string, Entry>();
            private readonly ConcurrentDictionary<string, DistributedCacheEntryOptions> lastOptionsByKey = new ConcurrentDictionary<string, DistributedCacheEntryOptions>();
            private readonly Func<DateTime> utcNow;
            private int getCount;
            private int setCount;
            private int removeCount;
            private int refreshCount;

            public SpyDistributedCache(Func<DateTime> utcNow)
            {
                this.utcNow = utcNow;
            }

            public int GetCount => this.getCount;

            public int SetCount => this.setCount;

            public int RemoveCount => this.removeCount;

            public int RefreshCount => this.refreshCount;

            public byte[] Get(string key) => this.GetAsync(key).GetAwaiter().GetResult();

            public Task<byte[]> GetAsync(string key, CancellationToken token = default)
            {
                Interlocked.Increment(ref this.getCount);
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
                Interlocked.Increment(ref this.setCount);
                this.store[key] = new Entry
                {
                    Value = value,
                    AbsoluteExpiration = options?.AbsoluteExpiration,
                };

                if (options != null)
                {
                    this.lastOptionsByKey[key] = options;
                }

                return Task.CompletedTask;
            }

            public void Remove(string key)
            {
                Interlocked.Increment(ref this.removeCount);
                this.store.TryRemove(key, out _);
            }

            public Task RemoveAsync(string key, CancellationToken token = default)
            {
                this.Remove(key);
                return Task.CompletedTask;
            }

            public void Refresh(string key) => Interlocked.Increment(ref this.refreshCount);

            public Task RefreshAsync(string key, CancellationToken token = default)
            {
                Interlocked.Increment(ref this.refreshCount);
                return Task.CompletedTask;
            }

            public bool ContainsKey(string key) => this.store.ContainsKey(key);

            public void SetRawForTest(string key, byte[] bytes)
            {
                this.store[key] = new Entry { Value = bytes, AbsoluteExpiration = null };
            }

            public bool TryGetLastSetOptions(string key, out DistributedCacheEntryOptions options)
            {
                return this.lastOptionsByKey.TryGetValue(key, out options);
            }

            private sealed class Entry
            {
                public byte[] Value { get; set; }

                public DateTimeOffset? AbsoluteExpiration { get; set; }
            }
        }

        /// <summary>
        /// IDistributedCache double whose GetAsync always throws an exception produced by the
        /// caller-supplied factory. SetAsync/RemoveAsync/RefreshAsync are no-ops.
        /// </summary>
        private sealed class ThrowingDistributedCache : IDistributedCache
        {
            private readonly Func<Exception> exceptionFactory;

            public ThrowingDistributedCache(Func<Exception> exceptionFactory)
            {
                this.exceptionFactory = exceptionFactory;
            }

            public byte[] Get(string key) => throw this.exceptionFactory();

            public Task<byte[]> GetAsync(string key, CancellationToken token = default)
                => Task.FromException<byte[]>(this.exceptionFactory());

            public void Set(string key, byte[] value, DistributedCacheEntryOptions options) { }

            public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
                => Task.CompletedTask;

            public void Remove(string key) { }

            public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;

            public void Refresh(string key) { }

            public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        }

        /// <summary>
        /// IDistributedCache that serializes all Get/Set/Remove calls through a single
        /// non-reentrant lock and detects re-entrant access (which would indicate the SDK
        /// assumes more parallelism than the contract allows).
        /// </summary>
        private sealed class SerializingDistributedCache : IDistributedCache
        {
            private readonly Dictionary<string, byte[]> store = new Dictionary<string, byte[]>();
            private readonly object gate = new object();
            private readonly Func<DateTime> utcNow;
            private int depth;
            private bool observedReentrancyViolation;

            public SerializingDistributedCache(Func<DateTime> utcNow)
            {
                this.utcNow = utcNow;
            }

            public bool ObservedReentrancyViolation => this.observedReentrancyViolation;

            public byte[] Get(string key) => this.GetAsync(key).GetAwaiter().GetResult();

            public Task<byte[]> GetAsync(string key, CancellationToken token = default)
            {
                lock (this.gate)
                {
                    this.EnterCritical();
                    try
                    {
                        _ = this.utcNow(); // tie test-double to the test clock
                        return this.store.TryGetValue(key, out byte[] bytes)
                            ? Task.FromResult(bytes)
                            : Task.FromResult<byte[]>(null);
                    }
                    finally
                    {
                        this.ExitCritical();
                    }
                }
            }

            public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
                => this.SetAsync(key, value, options).GetAwaiter().GetResult();

            public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
            {
                lock (this.gate)
                {
                    this.EnterCritical();
                    try
                    {
                        this.store[key] = value;
                        return Task.CompletedTask;
                    }
                    finally
                    {
                        this.ExitCritical();
                    }
                }
            }

            public void Remove(string key)
            {
                lock (this.gate)
                {
                    this.EnterCritical();
                    try
                    {
                        this.store.Remove(key);
                    }
                    finally
                    {
                        this.ExitCritical();
                    }
                }
            }

            public Task RemoveAsync(string key, CancellationToken token = default)
            {
                this.Remove(key);
                return Task.CompletedTask;
            }

            public void Refresh(string key) { }

            public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

            private void EnterCritical()
            {
                int d = Interlocked.Increment(ref this.depth);
                if (d > 1)
                {
                    this.observedReentrancyViolation = true;
                }
            }

            private void ExitCritical()
            {
                Interlocked.Decrement(ref this.depth);
            }
        }
    }
}
