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
    /// Scenario-level resilience tests that extend the L1 x L2 x Cosmos baseline matrix
    /// covered by <c>DekCacheResilienceTests</c>. The scenarios here focus on coalescing,
    /// cancellation propagation, clock edges, proactive-refresh nuances, and resilience
    /// composition rules that the PR #5428 feature must observe beyond its happy path.
    ///
    /// Every test carries a REQ/SOURCE header citing the named source of truth it
    /// encodes. Tests that describe required-but-not-yet-implemented behavior are allowed
    /// to fail; no test codifies today's buggy behavior as-correct.
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

        // REQ: On a cold cache, two concurrent GetOrAdd calls for the same dekId
        //      must coalesce: Cosmos is invoked at most once, and both callers
        //      observe the same DEK properties instance.
        // SOURCE-ASYNCCACHE: Mirrored/AsyncCache.cs:85-140 documents that "If another
        //                    initialization function is already running, new initialization
        //                    function will not be started. The result will be result of
        //                    currently running initialization function."
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

        // REQ: When L1 has an expired entry, L2 is empty, and N callers race the refresh
        //      path, the fetcher (Cosmos) must be invoked at most once. A resilience cache
        //      must not amplify upstream load at the TTL boundary.
        // SOURCE-ASYNCCACHE: Mirrored/AsyncCache.cs:128-131 — compare-and-swap on
        //                    AddOrUpdate guarantees a single forceRefresh generator
        //                    wins even with N concurrent callers.
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

        // REQ: A CancellationToken that is already cancelled at the time of the call
        //      must cause the operation to throw OperationCanceledException rather
        //      than proceeding to any cache read or fetcher invocation.
        // SOURCE-ASYNCCACHE: Mirrored/AsyncCache.cs:92 — "cancellationToken.ThrowIfCancellationRequested();"
        //                    at the entry of GetAsync.
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

        // REQ: When the distributed cache blocks on a read and the caller's
        //      CancellationToken is cancelled mid-read, the cancellation must be
        //      propagated to the caller as OperationCanceledException. The cache
        //      must not silently swallow cancellation as a generic L2 "miss".
        // SOURCE-DEKCACHE: src/DekCache.cs:325-327 — a dedicated
        //                  catch (OperationCanceledException) { throw; } clause
        //                  separates cancellation from generic L2-failure swallowing.
        // SOURCE-CONTRACT: IDistributedCache.GetAsync(key, CancellationToken) — the
        //                  CT parameter must be observed per the published contract.
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

        // REQ: Cancellation during a Cosmos fetch must propagate to the caller
        //      and must not poison L1 — a subsequent call with a fresh token
        //      must be able to retry cleanly.
        // SOURCE-ASYNCCACHE: Mirrored/AsyncCache.cs:112-122 — the cache-hit branch
        //                    "Don't check Task if there's an exception or it's been
        //                    canceled" guarantees cancelled lazies do not return stale
        //                    values; subsequent calls re-enter the fetcher path.
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

        // REQ: When the wall clock reaches exactly ServerPropertiesExpiryUtc, the
        //      entry must be treated as expired consistently across the read-side
        //      (distributed cache validation) and the write-side (L1 expiry check).
        // SOURCE-DEKCACHE: src/DekCache.cs:75 uses "expiryUtc <= utcNow()" to mark
        //                  L1 expired. Line 310 uses "expiryUtc > utcNow()" to mark
        //                  an L2 entry valid. Both sides therefore say "expired" when
        //                  now == expiry.
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

        // REQ: A small backward adjustment of the wall clock (e.g. NTP correction)
        //      must not corrupt the cache. A valid cached entry must remain valid
        //      and no spurious refresh is required.
        // SOURCE-DEKCACHE: src/DekCache.cs:75/:310 — expiry is a simple comparison
        //                  of ServerPropertiesExpiryUtc against the current clock.
        //                  Going backward only makes the entry "more valid", not less.
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

        // REQ: Entering the proactive-refresh window must NOT block the synchronous
        //      caller. The call must return the still-fresh cached value even when
        //      the background refresh has not yet completed.
        // SOURCE-XMLDOC: src/DekCache.cs:91-92 "Trigger background refresh without
        //                blocking caller. Use CancellationToken.None since this runs
        //                independently of the caller's request lifecycle."
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

        // REQ: Within the proactive-refresh window, N synchronous calls must
        //      trigger AT MOST ONE in-flight background refresh. N calls must not
        //      produce N concurrent Cosmos refreshes.
        // SOURCE-ASYNCCACHE: Mirrored/AsyncCache.cs:220-230 — BackgroundRefreshNonBlocking
        //                    short-circuits when a value is still being generated:
        //                    "if a value is currently being generated, we do nothing".
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

        // REQ: After a successful proactive refresh, a subsequent synchronous read
        //      must observe the refreshed value.
        // SOURCE-PR: PR #5428 description — "Adds proactive refresh capability" — the
        //            observable consequence is that the cached value moves forward to
        //            the refreshed one.
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

        // REQ: The fetcher must not be allowed to cross-pollute cache entries by
        //      returning a DEK whose Id differs from the requested dekId. The
        //      distributed cache key is derived from the dekId
        //      (src/DekCache.cs:415-418, "{cacheKeyPrefix}:{dekId}"), so storing
        //      mismatched properties under that key would produce a cache entry
        //      whose payload.Id disagrees with its key — a correctness hazard when
        //      peers read that entry and trust the stored Id.
        // SOURCE-DEKCACHE: src/DekCache.cs:415-418 establishes key == dekId. The
        //                  integrity invariant "payload.Id == dekId" follows directly
        //                  from that key derivation.
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

        // REQ: On a cold L1 miss, if the distributed cache throws on read, the
        //      operation must fall through to Cosmos and return the fetched value.
        //      Distributed cache is an optimization layer; its failures must not be
        //      surfaced to callers.
        // SOURCE-DEKCACHE: src/DekCache.cs:329-340 — "If distributed cache fails,
        //                  fall back to source. Don't throw - this is an optimization
        //                  layer" (with the explicit OperationCanceledException
        //                  re-throw carve-out immediately above).
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

        // REQ: If the distributed cache hangs indefinitely on read, the operation
        //      must not hang indefinitely when the caller supplies a cancellable
        //      token with a deadline. The operation must either honour the
        //      cancellation or complete via fallback — it must NOT deadlock.
        // SOURCE-CONTRACT: IDistributedCache.GetAsync(key, CancellationToken) — the
        //                  CT parameter must be honoured per the published contract.
        // SOURCE-DEKCACHE: src/DekCache.cs:298-300 — the CT is forwarded to
        //                  distributedCache.GetAsync, so cooperating caches must
        //                  observe cancellation.
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

    // GAPS:
    //
    // The following scenarios were considered but could not be grounded in a named source
    // of truth from the sources listed in the prompt; they are documented here so a future
    // owner can either pin the semantics (in XMLDOC, in the PR description, or in a design
    // doc) or add tests once the behavior is decided.
    //
    // 1. Fetcher returning null (E.1): Neither DekCache XML doc, the PR description, nor
    //    the IDistributedCache contract specifies whether a null DataEncryptionKeyProperties
    //    from the fetcher should throw or be treated as a cache miss. DekCache.cs:356 passes
    //    the fetcher result straight into CachedDekProperties whose ctor Debug.Asserts
    //    non-null — but Debug.Assert is not a specification. GAP until semantics are pinned.
    //
    // 2. Concurrent SetDekProperties + GetOrAddDekPropertiesAsync race (A.3): DekCache.cs:138-165
    //    documents eventual consistency for *distributed-cache* writes but does not specify
    //    whether a racing reader sees the pre- or post-Set memory-cache value deterministically.
    //    Asserting a specific ordering would be testing implementation timing rather than a
    //    stated contract. GAP until "last-write-wins" is pinned for the memory cache under
    //    contention.
    //
    // 3. Expiry-path L2 rescue on Cosmos outage beyond what DekCacheResilienceTests already
    //    covers — specifically the *ordering* of "consult L2 first, then Cosmos" on the
    //    forceRefresh branch (src/DekCache.cs:80-85) is stated in the PR narrative but the
    //    code path calls FetchFromSourceAndUpdateCachesAsync directly, bypassing L2. This is
    //    already expressed by DekCacheResilienceTests.ExpiredL1_* and is not duplicated here.
    //
    // 4. Proactive-refresh window lower bound — behavior when refreshBeforeExpiry is
    //    exactly equal to remaining TTL (i.e. utcNow() == refreshThreshold). DekCache.cs:412
    //    uses ">=" so the boundary is included, but no external doc pins this; skipped
    //    pending a source.
}
