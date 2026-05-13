//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Caching.Distributed;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    /// <summary>
    /// Scenario-level resilience tests extending the L1 × L2 × Cosmos baseline covered by
    /// <c>DekCacheResilienceTests</c>: coalescing, cancellation propagation, clock edges,
    /// proactive-refresh nuances, and resilience composition rules.
    /// </summary>
    [TestClass]
    public class DekCacheResilienceScenariosTests
    {
        private const string DekId = "testDek";
        private const string DefaultCachePrefix = "dek";
        private const string DefaultCacheKey = DefaultCachePrefix + ":v1:" + DekId;

        private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(30);

        // =======================================================================
        // A. Concurrency / coalescing
        // =======================================================================

        [TestMethod]
        public async Task ColdL1_TwoConcurrentCallers_CoalesceIntoSingleCosmosFetch()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            int cosmosCalls = 0;
            TaskCompletionSource<bool> gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            Func<string, CosmosDiagnosticsContext, CancellationToken, Task<DataEncryptionKeyProperties>> fetcher =
                async (id, ctx, ct) =>
                {
                    Interlocked.Increment(ref cosmosCalls);
                    await gate.Task;
                    return MakeDekProperties(id);
                };

            Task<DataEncryptionKeyProperties> t1 = cache.GetOrAddDekPropertiesAsync(
                DekId, fetcher, CosmosDiagnosticsContext.Create(null), CancellationToken.None);
            Task<DataEncryptionKeyProperties> t2 = cache.GetOrAddDekPropertiesAsync(
                DekId, fetcher, CosmosDiagnosticsContext.Create(null), CancellationToken.None);

            // Both tasks are now queued on the same in-flight AsyncLazy.
            gate.SetResult(true);

            DataEncryptionKeyProperties r1 = await t1;
            DataEncryptionKeyProperties r2 = await t2;

            Assert.AreEqual(1, cosmosCalls,
                "AsyncCache must coalesce concurrent callers on the same key into a single fetcher invocation.");
            Assert.AreSame(r1, r2,
                "Both coalesced callers must observe the same DEK properties instance.");
        }

        // NOTE: distributedCache is intentionally null — with an L2 populated by the warmup,
        //       racers would be served from L2 and the fetcher would not be invoked at all,
        //       which is correct but not the coalescing behaviour under test here.
        [TestMethod]
        public async Task ExpiredL1_NConcurrentCallers_CosmosInvokedAtMostOnce()
        {
            const int N = 16;

            DateTime now = NewClock();
            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: DefaultTtl,
                distributedCache: null,
                utcNow: () => now);

            // Warm L1 (fetcher #1).
            await cache.GetOrAddDekPropertiesAsync(
                DekId, HealthyFetcher, CosmosDiagnosticsContext.Create(null), CancellationToken.None);

            // Move past L1 TTL so the next calls take the refresh branch.
            now = now.AddMinutes(31);

            int refreshCalls = 0;
            TaskCompletionSource<bool> gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Func<string, CosmosDiagnosticsContext, CancellationToken, Task<DataEncryptionKeyProperties>> fetcher =
                async (id, ctx, ct) =>
                {
                    Interlocked.Increment(ref refreshCalls);
                    await gate.Task;
                    return MakeDekProperties(id);
                };

            Task<DataEncryptionKeyProperties>[] racers = Enumerable.Range(0, N).Select(_ =>
                cache.GetOrAddDekPropertiesAsync(
                    DekId, fetcher, CosmosDiagnosticsContext.Create(null), CancellationToken.None)).ToArray();

            // Give racers a chance to all queue against the same refreshed AsyncLazy.
            await Task.Yield();
            gate.SetResult(true);

            await Task.WhenAll(racers);

            Assert.AreEqual(1, refreshCalls,
                $"Expected at most one Cosmos refresh across {N} concurrent callers; observed {refreshCalls}.");
        }

        // =======================================================================
        // B. Cancellation propagation
        // =======================================================================

        [TestMethod]
        public async Task CancellationToken_AlreadyCancelled_ThrowsOperationCanceledException()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            int cosmosCalls = 0;
            int l2Reads = 0;

            l2.OnGet = (key, ct) => Interlocked.Increment(ref l2Reads);

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(() =>
                cache.GetOrAddDekPropertiesAsync(
                    DekId,
                    (id, ctx, ct) => { Interlocked.Increment(ref cosmosCalls); return Task.FromResult(MakeDekProperties(id)); },
                    CosmosDiagnosticsContext.Create(null),
                    cts.Token));

            Assert.AreEqual(0, cosmosCalls, "No fetcher invocation must occur when the CT is already cancelled.");
            Assert.AreEqual(0, l2Reads, "No L2 read must occur when the CT is already cancelled.");
        }

        [TestMethod]
        public async Task CancellationDuringL2Read_PropagatesOperationCanceledException()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            l2.HangOnGet = true;

            using CancellationTokenSource cts = new CancellationTokenSource();

            Task<DataEncryptionKeyProperties> call = cache.GetOrAddDekPropertiesAsync(
                DekId, HealthyFetcher, CosmosDiagnosticsContext.Create(null), cts.Token);

            // Give the operation a moment to reach the L2 read.
            await Task.Delay(20);
            cts.Cancel();

            Exception caughtA = null;
            try { await call; } catch (Exception ex) { caughtA = ex; }
            Assert.IsInstanceOfType(caughtA, typeof(OperationCanceledException),
                $"Expected OperationCanceledException (or subclass) on CT-cancelled L2 read; observed {caughtA?.GetType().Name ?? "<none>"}.");
        }

        [TestMethod]
        public async Task CancellationDuringCosmosFetch_PropagatesAndDoesNotPoisonL1()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            using CancellationTokenSource cts = new CancellationTokenSource();
            TaskCompletionSource<bool> fetcherEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            Task<DataEncryptionKeyProperties> call = cache.GetOrAddDekPropertiesAsync(
                DekId,
                async (id, ctx, ct) =>
                {
                    fetcherEntered.TrySetResult(true);
                    await Task.Delay(Timeout.Infinite, ct);
                    return MakeDekProperties(id);
                },
                CosmosDiagnosticsContext.Create(null),
                cts.Token);

            await fetcherEntered.Task;
            cts.Cancel();

            Exception caughtB = null;
            try { await call; } catch (Exception ex) { caughtB = ex; }
            Assert.IsInstanceOfType(caughtB, typeof(OperationCanceledException),
                $"Expected OperationCanceledException (or subclass) on CT-cancelled Cosmos fetch; observed {caughtB?.GetType().Name ?? "<none>"}.");

            // L1 must not have been poisoned. A clean retry with a healthy fetcher must succeed.
            DataEncryptionKeyProperties retry = await cache.GetOrAddDekPropertiesAsync(
                DekId, HealthyFetcher, CosmosDiagnosticsContext.Create(null), CancellationToken.None);

            Assert.AreEqual(DekId, retry.Id, "After a cancelled fetch, a clean retry must succeed without residual cancellation state.");
        }

        // =======================================================================
        // C. Clock edge cases
        // =======================================================================

        // NOTE: L2 is intentionally null because this test targets the L1-expiry boundary,
        //       not L2-rescue. With L2 populated, the refresh would be served from L2 and
        //       the Cosmos counter would not move, which is correct resilience behaviour but
        //       not the boundary condition under test here.
        [TestMethod]
        public async Task ClockExactlyAtExpiry_TreatedAsExpiredAndRefetches()
        {
            DateTime now = NewClock();
            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: DefaultTtl,
                distributedCache: null,
                utcNow: () => now);

            int cosmosCalls = 0;
            Func<string, CosmosDiagnosticsContext, CancellationToken, Task<DataEncryptionKeyProperties>> fetcher =
                (id, ctx, ct) => { Interlocked.Increment(ref cosmosCalls); return Task.FromResult(MakeDekProperties(id)); };

            await cache.GetOrAddDekPropertiesAsync(
                DekId, fetcher, CosmosDiagnosticsContext.Create(null), CancellationToken.None);
            Assert.AreEqual(1, cosmosCalls, "Setup: warm Cosmos fetch.");

            // Advance to EXACTLY the expiry moment.
            now = now + DefaultTtl;

            await cache.GetOrAddDekPropertiesAsync(
                DekId, fetcher, CosmosDiagnosticsContext.Create(null), CancellationToken.None);

            Assert.AreEqual(2, cosmosCalls,
                "At now == expiry the entry must be treated as expired (ServerPropertiesExpiryUtc <= utcNow()), forcing a refresh.");
        }

        [TestMethod]
        public async Task ClockGoingBackwardAfterWarmup_DoesNotCorruptCache()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            int cosmosCalls = 0;
            Func<string, CosmosDiagnosticsContext, CancellationToken, Task<DataEncryptionKeyProperties>> fetcher =
                (id, ctx, ct) => { Interlocked.Increment(ref cosmosCalls); return Task.FromResult(MakeDekProperties(id)); };

            await cache.GetOrAddDekPropertiesAsync(
                DekId, fetcher, CosmosDiagnosticsContext.Create(null), CancellationToken.None);
            Assert.AreEqual(1, cosmosCalls, "Setup: warm Cosmos fetch.");

            // Clock jumps backward 5 seconds (NTP correction).
            now = now.AddSeconds(-5);

            DataEncryptionKeyProperties next = await cache.GetOrAddDekPropertiesAsync(
                DekId, fetcher, CosmosDiagnosticsContext.Create(null), CancellationToken.None);

            Assert.AreEqual(DekId, next.Id);
            Assert.AreEqual(1, cosmosCalls, "A backward clock jump must not force an unnecessary refresh.");
        }

        // =======================================================================
        // D. Proactive refresh nuances
        // =======================================================================

        [TestMethod]
        public async Task ProactiveRefresh_DoesNotBlockSynchronousCaller()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: DefaultTtl,
                distributedCache: l2,
                refreshBeforeExpiry: TimeSpan.FromMinutes(5),
                utcNow: () => now,
                cacheKeyPrefix: DefaultCachePrefix);

            // Warm.
            await cache.GetOrAddDekPropertiesAsync(
                DekId, HealthyFetcher, CosmosDiagnosticsContext.Create(null), CancellationToken.None);

            // Enter the refresh window (<5 min remaining on a 30 min TTL).
            now = now.AddMinutes(26);

            // Background refresh fetcher blocks on a gate that the test never sets.
            TaskCompletionSource<DataEncryptionKeyProperties> neverCompletes =
                new TaskCompletionSource<DataEncryptionKeyProperties>(TaskCreationOptions.RunContinuationsAsynchronously);

            Task<DataEncryptionKeyProperties> call = cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => neverCompletes.Task,
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // The synchronous caller must return the cached value even though the bg fetcher is blocked.
            DataEncryptionKeyProperties result = await call.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.AreEqual(DekId, result.Id);

            // Cleanup: observe the hanging task so the test runner does not complain.
            neverCompletes.TrySetCanceled();
        }

        [TestMethod]
        public async Task ProactiveRefresh_NCallsInWindow_AtMostOneRefreshInflight()
        {
            const int N = 8;

            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: DefaultTtl,
                distributedCache: l2,
                refreshBeforeExpiry: TimeSpan.FromMinutes(5),
                utcNow: () => now,
                cacheKeyPrefix: DefaultCachePrefix);

            await cache.GetOrAddDekPropertiesAsync(
                DekId, HealthyFetcher, CosmosDiagnosticsContext.Create(null), CancellationToken.None);

            now = now.AddMinutes(26);

            int refreshCalls = 0;
            TaskCompletionSource<bool> gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Func<string, CosmosDiagnosticsContext, CancellationToken, Task<DataEncryptionKeyProperties>> fetcher =
                async (id, ctx, ct) =>
                {
                    Interlocked.Increment(ref refreshCalls);
                    await gate.Task;
                    return MakeDekProperties(id);
                };

            // Fire N synchronous calls in close succession. Each returns the cached value.
            for (int i = 0; i < N; i++)
            {
                await cache.GetOrAddDekPropertiesAsync(
                    DekId, fetcher, CosmosDiagnosticsContext.Create(null), CancellationToken.None);
            }

            // Give background scheduling a chance to settle; fetcher is still blocked on the gate.
            await Task.Delay(50);

            Assert.AreEqual(1, refreshCalls,
                $"Expected at most one in-flight proactive refresh across {N} calls in the refresh window; observed {refreshCalls}.");

            gate.SetResult(true);
        }

        [TestMethod]
        public async Task ProactiveRefresh_AfterCompletion_SubsequentReadObservesRefreshedValue()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: DefaultTtl,
                distributedCache: l2,
                refreshBeforeExpiry: TimeSpan.FromMinutes(5),
                utcNow: () => now,
                cacheKeyPrefix: DefaultCachePrefix);

            // Warm with wrappedKey v1.
            await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => Task.FromResult(MakeDekProperties(id, wrappedKey: new byte[] { 0x01 })),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            now = now.AddMinutes(26);

            SemaphoreSlim refreshDone = new SemaphoreSlim(0, 1);
            Func<string, CosmosDiagnosticsContext, CancellationToken, Task<DataEncryptionKeyProperties>> refreshFetcher =
                (id, ctx, ct) =>
                {
                    DataEncryptionKeyProperties v2 = MakeDekProperties(id, wrappedKey: new byte[] { 0x02 });
                    refreshDone.Release();
                    return Task.FromResult(v2);
                };

            // First call triggers the background refresh and returns the still-fresh v1.
            await cache.GetOrAddDekPropertiesAsync(
                DekId, refreshFetcher, CosmosDiagnosticsContext.Create(null), CancellationToken.None);

            bool completed = await refreshDone.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(completed, "Background refresh must complete within a reasonable timeout.");

            // Give the bg task a chance to store the refreshed value before we re-read.
            await Task.Delay(50);

            DataEncryptionKeyProperties next = await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => Task.FromResult(MakeDekProperties(id, wrappedKey: new byte[] { 0xFF })),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            CollectionAssert.AreEqual(
                new byte[] { 0x02 },
                next.WrappedDataEncryptionKey,
                "A subsequent read after a successful proactive refresh must observe the refreshed wrappedKey.");
        }

        // =======================================================================
        // E. Negative-fetcher scenarios
        // =======================================================================

        [TestMethod]
        public async Task FetcherReturnsDifferentId_RejectedToPreventCrossPollution()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            Func<string, CosmosDiagnosticsContext, CancellationToken, Task<DataEncryptionKeyProperties>> misbehaving =
                (id, ctx, ct) => Task.FromResult(MakeDekProperties("a-completely-different-dek"));

            Exception caught = null;
            DataEncryptionKeyProperties result = null;
            try
            {
                result = await cache.GetOrAddDekPropertiesAsync(
                    DekId, misbehaving, CosmosDiagnosticsContext.Create(null), CancellationToken.None);
            }
            catch (Exception ex)
            {
                caught = ex;
            }

            Assert.IsTrue(
                caught != null || (result != null && result.Id == DekId),
                $"The cache must either reject a mismatched-Id fetcher result with an exception " +
                $"or normalize the Id. Observed: result.Id='{result?.Id ?? "<null>"}' exception='{caught?.GetType().Name ?? "<none>"}'.");
        }

        // =======================================================================
        // F. Resilience composition
        // =======================================================================

        [TestMethod]
        public async Task ColdL1_L2ReadThrows_CosmosAvailable_FallsThroughToCosmos()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            l2.ThrowOnGet = new InvalidOperationException("simulated L2 outage");

            int cosmosCalls = 0;
            DataEncryptionKeyProperties result = await cache.GetOrAddDekPropertiesAsync(
                DekId,
                (id, ctx, ct) => { Interlocked.Increment(ref cosmosCalls); return Task.FromResult(MakeDekProperties(id)); },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(DekId, result.Id);
            Assert.AreEqual(1, cosmosCalls,
                "A throwing L2 read must not abort the request; the cache must fall through to Cosmos.");
        }

        [TestMethod]
        public async Task ColdL1_L2Hangs_CancellationTokenShortDeadline_DoesNotHangIndefinitely()
        {
            DateTime now = NewClock();
            ClockControlledDistributedCache l2 = new ClockControlledDistributedCache(() => now);
            DekCache cache = NewCache(DefaultTtl, l2, () => now);

            l2.HangOnGet = true;

            using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

            Task<DataEncryptionKeyProperties> call = cache.GetOrAddDekPropertiesAsync(
                DekId, HealthyFetcher, CosmosDiagnosticsContext.Create(null), cts.Token);

            // The test's own deadline (2s) is the hang-detector. The request under test must
            // complete within it one way or another: via OperationCanceledException (short CT
            // was honoured) or via a Cosmos fallback that produced a result.
            Task first = await Task.WhenAny(call, Task.Delay(TimeSpan.FromSeconds(2)));

            if (first != call)
            {
                Assert.Fail("Request did not complete within 2s; L2 hang was not mitigated by caller-provided CancellationToken or a fallback.");
            }

            // If call completed without exception, the cache fell through to Cosmos — that
            // is also acceptable. If it threw, it must be a cancellation exception.
            if (call.IsFaulted)
            {
                Assert.IsInstanceOfType(call.Exception?.GetBaseException(), typeof(OperationCanceledException),
                    $"Expected OperationCanceledException on hang+CT-deadline; observed {call.Exception?.GetBaseException().GetType().Name}.");
            }
        }

        // =======================================================================
        // Helpers
        // =======================================================================

        private static DateTime NewClock()
        {
            return new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        private static DekCache NewCache(TimeSpan ttl, IDistributedCache l2, Func<DateTime> utcNow)
        {
            return new DekCache(
                dekPropertiesTimeToLive: ttl,
                distributedCache: l2,
                utcNow: utcNow,
                cacheKeyPrefix: DefaultCachePrefix);
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

        /// <summary>
        /// IDistributedCache test double whose freshness evaluation honours an injected
        /// clock (avoiding the DateTimeOffset.UtcNow skew that <c>InMemoryDistributedCache</c>
        /// inflicts on clock-sensitive tests) and which exposes deterministic fault and
        /// hang hooks on the read path.
        /// </summary>
        private sealed class ClockControlledDistributedCache : IDistributedCache
        {
            private readonly ConcurrentDictionary<string, Entry> store = new ConcurrentDictionary<string, Entry>();
            private readonly Func<DateTime> utcNow;

            public ClockControlledDistributedCache(Func<DateTime> utcNow)
            {
                this.utcNow = utcNow;
            }

            public Exception ThrowOnGet { get; set; }

            public bool HangOnGet { get; set; }

            public Action<string, CancellationToken> OnGet { get; set; }

            public byte[] Get(string key) => this.GetAsync(key).GetAwaiter().GetResult();

            public async Task<byte[]> GetAsync(string key, CancellationToken token = default)
            {
                this.OnGet?.Invoke(key, token);

                if (this.ThrowOnGet != null)
                {
                    throw this.ThrowOnGet;
                }

                if (this.HangOnGet)
                {
                    // Co-operative hang: respects the CT, observably pending otherwise.
                    TaskCompletionSource<byte[]> tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                    using (token.Register(() => tcs.TrySetCanceled(token)))
                    {
                        return await tcs.Task;
                    }
                }

                if (this.store.TryGetValue(key, out Entry entry))
                {
                    if (entry.AbsoluteExpiration.HasValue
                        && entry.AbsoluteExpiration.Value.UtcDateTime <= this.utcNow())
                    {
                        this.store.TryRemove(key, out _);
                        return null;
                    }

                    return entry.Value;
                }

                return null;
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

            private sealed class Entry
            {
                public byte[] Value { get; set; }

                public DateTimeOffset? AbsoluteExpiration { get; set; }
            }
        }
    }

    // GAPS — scenarios deliberately not tested here because the contract is unspecified:
    //   1. Fetcher returning null: DekCache passes the result straight to CachedDekProperties
    //      whose ctor Debug.Asserts non-null, but Debug.Assert is not a specification.
    //   2. Concurrent SetDekProperties / GetOrAddDekPropertiesAsync ordering for the in-memory
    //      cache (eventual consistency is documented for L2 only).
    //   3. Forced-refresh branch is exercised by DekCacheResilienceTests.ExpiredL1_* and not
    //      duplicated here.
    //   4. Boundary case where utcNow() exactly equals the refresh threshold (code uses ">=" so
    //      the boundary is included, but no external doc pins this).
}
