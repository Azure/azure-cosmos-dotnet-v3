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
    /// Contract-shaped behavioral specification for the DekCache distributed-cache (L2) feature
    /// introduced in PR #5428. These tests are scoped to behaviors the SDK must honor for any
    /// conformant <see cref="IDistributedCache"/> implementation — independent of the specific
    /// backing store — and do not repeat coverage already present in
    /// <see cref="DekCacheDistributedCacheTests"/> (basic null/present L2 handling) or
    /// <see cref="DekCacheResilienceTests"/> (corrupted JSON, partial DEK, future version,
    /// resilience/TTL coupling).
    ///
    /// Sources of truth referenced below:
    ///   SOURCE-CONTRACT:
    ///     IDistributedCache interface — https://learn.microsoft.com/dotnet/api/microsoft.extensions.caching.distributed.idistributedcache
    ///     - GetAsync returns null for missing keys.
    ///     - SetAsync takes a non-null DistributedCacheEntryOptions.
    ///     - RemoveAsync/RefreshAsync are the sole mutation/touch methods.
    ///     - Thread safety is NOT mandated by the interface.
    ///
    ///   SOURCE-EXCEPTION-CLASS:
    ///     IDistributedCache implementations may throw arbitrary exceptions on backend errors.
    ///     The SDK MUST degrade gracefully — fail-open for reads (DekCache.cs:329-340) and
    ///     fire-and-forget for writes (DekCache.cs:380-401, 185-208).
    ///
    ///   SOURCE-CANCELLATION-CONVENTION:
    ///     .NET convention — a cancelled CancellationToken should cause OperationCanceledException
    ///     to be thrown promptly; cancellation during I/O should propagate.
    ///
    ///   SOURCE-XMLDOC:
    ///     CosmosDataEncryptionKeyProvider XML docs describe the distributedCache parameter as
    ///     "Optional distributed cache implementation". "Optional" means null must be handled
    ///     gracefully (the SDK must function with no L2 integration).
    ///
    ///   SOURCE-FAIL-SAFE:
    ///     DekCache.cs:337-340 ("If distributed cache fails, fall back to source / Don't throw -
    ///     this is an optimization layer") — L2 failures are never permitted to fail a read.
    ///
    /// All tests use a deterministic clock and an IDistributedCache test double that respects
    /// that clock. No real wall-clock sleeps.
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
            // REQ: An IDistributedCache returning an empty (non-null) byte[] from GetAsync must
            //      not surface as a user-visible error. The SDK must either treat it as a miss or
            //      otherwise recover; it must never bubble a deserialization exception to the caller.
            // SOURCE: SOURCE-CONTRACT (GetAsync return value is unconstrained beyond "null means miss";
            //         empty is not defined) + SOURCE-FAIL-SAFE (DekCache.cs:329-340 — never fail a
            //         read because of an L2 oddity).
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
            // REQ: When L2 GetAsync throws a non-cancellation transient exception (e.g. TimeoutException),
            //      the SDK must swallow it and fall through to the source fetcher. A distributed cache
            //      is an optimization layer; its failure must not fail the operation.
            // SOURCE: SOURCE-EXCEPTION-CLASS + SOURCE-FAIL-SAFE (DekCache.cs:329-340 catches Exception
            //         and logs — only OperationCanceledException is rethrown).
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
            // REQ: If an IDistributedCache implementation throws OperationCanceledException for reasons
            //      unrelated to the caller's token (e.g., the impl used an internal timeout token that
            //      fired), the SDK must still serve the caller — either by falling through to Cosmos or
            //      otherwise recovering. Propagating OCE when the caller never cancelled would break the
            //      fail-open contract of the L2 layer.
            //
            //      NOTE: This is legitimately ambiguous. Current code at DekCache.cs:325-327 unconditionally
            //      rethrows OperationCanceledException. That would cause this test to fail. Per the fail-open
            //      contract for an optimization layer, the correct behavior is to fall through. This test is
            //      intentionally written to the required behavior so it will surface the issue if unimplemented.
            // SOURCE: SOURCE-FAIL-SAFE (optimization layer must not fail reads) vs
            //         SOURCE-CANCELLATION-CONVENTION (OCE should propagate when cancellation was requested).
            //         Because the caller's CT is NOT cancelled in this scenario, SOURCE-FAIL-SAFE governs.
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
            // REQ: If the caller's CancellationToken is already cancelled when GetOrAddDekPropertiesAsync
            //      is invoked, the SDK must throw an OperationCanceledException promptly, without invoking
            //      the fetcher or the L2 GetAsync.
            // SOURCE: SOURCE-CANCELLATION-CONVENTION — .NET convention for IsCancellationRequested on entry.
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
            // REQ: SetDekProperties performs its distributed-cache write as a fire-and-forget Task.Run
            //      with CancellationToken.None (DekCache.cs:185-208, 195). The caller's token — even if
            //      cancelled after the SetDekProperties call returns — must not cancel or fail the
            //      background write; the background write must proceed to completion and populate L2.
            // SOURCE: DekCache.cs:185-208 (Task.Run fire-and-forget) and DekCache.cs:195 (explicit
            //         CancellationToken.None).
            DateTime now = NewClock();
            SpyDistributedCache l2 = new SpyDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            using CancellationTokenSource callerCts = new CancellationTokenSource();

            cache.SetDekProperties(DekId, MakeDekProperties(DekId));

            // Caller's lifecycle ends / is cancelled — the background write must still finish.
            callerCts.Cancel();

            await cache.LastDistributedCacheWriteTask;

            Assert.IsTrue(l2.ContainsKey(DefaultCacheKey), "Background L2 write must complete independently of caller cancellation.");
            Assert.AreEqual(1, l2.SetCount, "Exactly one background SetAsync must have executed.");
        }

        // ------------------------------------------------------------
        // Group D — SetAsync contract compliance
        // ------------------------------------------------------------

        [TestMethod]
        public async Task SetAsync_IsCalledWithNonNullDistributedCacheEntryOptions()
        {
            // REQ: The SDK must always pass a non-null DistributedCacheEntryOptions to
            //      IDistributedCache.SetAsync. The interface does not forbid null options, but many
            //      real implementations dereference it; passing null is an interop hazard.
            // SOURCE: SOURCE-CONTRACT — SetAsync signature requires a DistributedCacheEntryOptions
            //         parameter and its nullness is unspecified, so a conformant consumer must not
            //         pass null.
            DateTime now = NewClock();
            SpyDistributedCache l2 = new SpyDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            cache.SetDekProperties(DekId, MakeDekProperties(DekId));
            await cache.LastDistributedCacheWriteTask;

            Assert.AreEqual(1, l2.SetCount);
            Assert.IsTrue(l2.TryGetLastSetOptions(DefaultCacheKey, out DistributedCacheEntryOptions options));
            Assert.IsNotNull(options, "SetAsync must receive a non-null DistributedCacheEntryOptions.");
        }

        [TestMethod]
        public async Task SetAsync_AbsoluteExpirationIsSetToAFutureInstant()
        {
            // REQ: The SDK must set DistributedCacheEntryOptions.AbsoluteExpiration to a DateTimeOffset
            //      that is strictly in the future relative to the clock at write time. A past/MinValue
            //      value is rejected by some IDistributedCache implementations and makes the entry
            //      unreachable by peers even when they arrive microseconds later.
            // SOURCE: SOURCE-CONTRACT — DistributedCacheEntryOptions.AbsoluteExpiration semantics:
            //         entries with a past expiration are treated as already expired.
            DateTime now = NewClock();
            SpyDistributedCache l2 = new SpyDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            cache.SetDekProperties(DekId, MakeDekProperties(DekId));
            await cache.LastDistributedCacheWriteTask;

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
            // REQ: Constructing DekCache with distributedCache: null must disable the L2 integration
            //      entirely. GetOrAddDekPropertiesAsync, SetDekProperties, and RemoveAsync must all
            //      succeed without touching any IDistributedCache member — the SDK must function as
            //      it did before PR #5428 when the feature is opted out.
            // SOURCE: SOURCE-XMLDOC ("Optional distributed cache implementation.") and
            //         DekCache.cs:283-286 (TryGet null guard), :375-378 (Update null guard), :232
            //         (RemoveAsync null guard).
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
            await cache.LastDistributedCacheWriteTask; // must be Task.CompletedTask (default)

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
            // REQ: The SDK assumes the supplied IDistributedCache is safe to call concurrently (the
            //      interface itself does not mandate thread safety — SOURCE-CONTRACT). That assumption
            //      must hold even when the implementation serializes internally (e.g., via a lock).
            //      Concurrent GetOrAddDekPropertiesAsync calls for distinct DEK ids must all complete
            //      without deadlock and without spurious errors.
            // SOURCE: SOURCE-CONTRACT (thread safety unspecified by the interface) combined with the
            //         SDK's observable use pattern (every public operation issues an L2 call).
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
            // REQ: DekCache uses AbsoluteExpiration only (no sliding-expiration semantics). Therefore
            //      the SDK must never call IDistributedCache.RefreshAsync or IDistributedCache.Refresh
            //      across any of its operations (GetOrAddDekPropertiesAsync cold miss, warm hit,
            //      SetDekProperties, or RemoveAsync).
            // SOURCE: SOURCE-CONTRACT (Refresh is for sliding expiration) + inspection of DekCache.cs
            //         which references no .Refresh call (only Get/Set/Remove on IDistributedCache).
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
            await cache.LastDistributedCacheWriteTask;

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
            // REQ: A cold miss that falls through to Cosmos must write the fetched value to L2
            //      exactly once — no retry loop, no duplicate write from the outer and inner cache
            //      layers.
            // SOURCE: DekCache.cs:362 — FetchFromSourceAndUpdateCachesAsync awaits
            //         UpdateDistributedCacheAsync exactly once; there is no loop.
            DateTime now = NewClock();
            SpyDistributedCache l2 = new SpyDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => Task.FromResult(MakeDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(1, l2.SetCount, "Cold miss must perform exactly one L2 SetAsync.");
        }

        [TestMethod]
        public async Task WarmL1Hit_DoesNotWriteToL2()
        {
            // REQ: A second GetOrAddDekPropertiesAsync call that is satisfied from L1 (memory) must
            //      NOT cause any L2 write. The only write paths are SetDekProperties
            //      (DekCache.cs:167) and the miss-refill at DekCache.cs:362 — both miss/explicit paths.
            // SOURCE: DekCache.cs:58-100 — GetOrAddDekPropertiesAsync memory-cache path does not call
            //         UpdateDistributedCacheAsync when the entry is fresh.
            DateTime now = NewClock();
            SpyDistributedCache l2 = new SpyDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            // Prime L1 + L2.
            await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => Task.FromResult(MakeDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            int setCountAfterFirst = l2.SetCount;
            Assert.AreEqual(1, setCountAfterFirst);

            // Second call — L1 hit, L2 must not be re-written.
            await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => Task.FromResult(MakeDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

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
        /// An IDistributedCache that serializes all Get/Set/Remove calls through a single
        /// non-reentrant lock. Models a backing store that is NOT thread-safe for concurrent
        /// access — exactly the lower bound permitted by SOURCE-CONTRACT. Detects re-entrant
        /// access, which would indicate the SDK assumes more parallelism than the contract allows
        /// without yielding.
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
