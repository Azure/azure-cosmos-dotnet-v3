//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Tests.TestHelpers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Mde = Microsoft.Data.Encryption.Cryptography;

    /// <summary>
    /// Tests for the SDK-side ProtectedDataEncryptionKey shadow cache and the
    /// double-checked locking pattern in BuildProtectedDataEncryptionKeyAsync.
    ///
    /// These tests validate:
    /// 1. ProtectedDataEncryptionKeyCacheEntry expiry behavior (TTL, ratchet-down)
    /// 2. Shadow cache population and hit/miss semantics
    /// 3. Concurrency: cache hits bypass the global semaphore
    /// 4. Cache key correctness (different DEKs, key rewrap produces new key)
    /// </summary>
    [TestClass]
    public class ProtectedDataEncryptionKeyCacheTests
    {
        private TimeSpan originalTtl;

        [TestInitialize]
        public void Setup()
        {
            // Capture original TTL to restore after each test (it's a process-wide static).
            this.originalTtl = Mde.ProtectedDataEncryptionKey.TimeToLive;

            // Reset to a known value.
            Mde.ProtectedDataEncryptionKey.TimeToLive = TimeSpan.FromHours(2);

            // Clear the static shadow cache between tests to avoid cross-test pollution.
            EncryptionCosmosClient.ProtectedDataEncryptionKeyCache.Clear();
        }

        [TestCleanup]
        public void Cleanup()
        {
            Mde.ProtectedDataEncryptionKey.TimeToLive = this.originalTtl;
            EncryptionCosmosClient.ProtectedDataEncryptionKeyCache.Clear();

            // Ensure semaphore is released if a test left it held.
            if (EncryptionCosmosClient.EncryptionKeyCacheSemaphore.CurrentCount == 0)
            {
                EncryptionCosmosClient.EncryptionKeyCacheSemaphore.Release();
            }
        }

        #region ProtectedDataEncryptionKeyCacheEntry — Expiry Tests

        [TestMethod]
        public void CacheEntry_IsNotExpired_WhenWithinTTL()
        {
            Mde.ProtectedDataEncryptionKey pdek = CreateDummyPdek("key1");
            ProtectedDataEncryptionKeyCacheEntry entry = new ProtectedDataEncryptionKeyCacheEntry(pdek);

            Assert.IsFalse(entry.IsExpired, "Entry should not be expired immediately after creation.");
            Assert.AreSame(pdek, entry.ProtectedDataEncryptionKey, "Entry should hold the same PDEK reference.");
        }

        [TestMethod]
        public void CacheEntry_IsExpired_WhenTTLIsZero()
        {
            // TTL = Zero means entries expire immediately.
            Mde.ProtectedDataEncryptionKey.TimeToLive = TimeSpan.Zero;

            Mde.ProtectedDataEncryptionKey pdek = CreateDummyPdek("key1");
            ProtectedDataEncryptionKeyCacheEntry entry = new ProtectedDataEncryptionKeyCacheEntry(pdek);

            Assert.IsTrue(entry.IsExpired, "Entry should be expired immediately when TTL is zero.");
        }

        [TestMethod]
        public void CacheEntry_RespectsRatchetDown()
        {
            // Create entry with a 2-hour TTL.
            Mde.ProtectedDataEncryptionKey.TimeToLive = TimeSpan.FromHours(2);
            Mde.ProtectedDataEncryptionKey pdek = CreateDummyPdek("key1");
            ProtectedDataEncryptionKeyCacheEntry entry = new ProtectedDataEncryptionKeyCacheEntry(pdek);

            Assert.IsFalse(entry.IsExpired, "Entry should be valid under 2h TTL.");

            // Ratchet down to zero — simulates a second EncryptionCosmosClient
            // created with a shorter keyCacheTimeToLive.
            Mde.ProtectedDataEncryptionKey.TimeToLive = TimeSpan.Zero;

            Assert.IsTrue(entry.IsExpired,
                "Entry should expire immediately after TTL ratchet-down to zero, " +
                "because IsExpired reads the live static TTL.");
        }

        [TestMethod]
        public void CacheEntry_ExpiresAfterTTL()
        {
            // Use a very short TTL to test natural expiry.
            Mde.ProtectedDataEncryptionKey.TimeToLive = TimeSpan.FromMilliseconds(50);

            Mde.ProtectedDataEncryptionKey pdek = CreateDummyPdek("key1");
            ProtectedDataEncryptionKeyCacheEntry entry = new ProtectedDataEncryptionKeyCacheEntry(pdek);

            // Should be valid immediately.
            Assert.IsFalse(entry.IsExpired, "Entry should be valid immediately after creation.");

            // Wait for TTL to elapse.
            Thread.Sleep(100);

            Assert.IsTrue(entry.IsExpired, "Entry should be expired after TTL elapses.");
        }

        #endregion

        #region Shadow Cache — Population and Lookup

        [TestMethod]
        public void ShadowCache_StoreAndRetrieve()
        {
            Mde.ProtectedDataEncryptionKey pdek = CreateDummyPdek("key1");
            string cacheKey = "cek-pii/test://kek/AABBCCDD";

            EncryptionCosmosClient.ProtectedDataEncryptionKeyCache[cacheKey] =
                new ProtectedDataEncryptionKeyCacheEntry(pdek);

            Assert.IsTrue(
                EncryptionCosmosClient.ProtectedDataEncryptionKeyCache.TryGetValue(cacheKey, out ProtectedDataEncryptionKeyCacheEntry retrieved),
                "Shadow cache should contain the entry.");
            Assert.IsFalse(retrieved.IsExpired, "Retrieved entry should not be expired.");
            Assert.AreSame(pdek, retrieved.ProtectedDataEncryptionKey, "Should be the same PDEK object reference.");
        }

        [TestMethod]
        public void ShadowCache_DifferentKeys_DifferentEntries()
        {
            Mde.ProtectedDataEncryptionKey pdek1 = CreateDummyPdek("key1");
            Mde.ProtectedDataEncryptionKey pdek2 = CreateDummyPdek("key2");

            string cacheKey1 = "cek-pii/test://kek/AABBCCDD";
            string cacheKey2 = "cek-financial/test://kek/11223344";

            EncryptionCosmosClient.ProtectedDataEncryptionKeyCache[cacheKey1] =
                new ProtectedDataEncryptionKeyCacheEntry(pdek1);
            EncryptionCosmosClient.ProtectedDataEncryptionKeyCache[cacheKey2] =
                new ProtectedDataEncryptionKeyCacheEntry(pdek2);

            Assert.AreEqual(2, EncryptionCosmosClient.ProtectedDataEncryptionKeyCache.Count,
                "Shadow cache should hold entries for both DEKs.");

            EncryptionCosmosClient.ProtectedDataEncryptionKeyCache.TryGetValue(cacheKey1, out ProtectedDataEncryptionKeyCacheEntry entry1);
            EncryptionCosmosClient.ProtectedDataEncryptionKeyCache.TryGetValue(cacheKey2, out ProtectedDataEncryptionKeyCacheEntry entry2);

            Assert.AreSame(pdek1, entry1.ProtectedDataEncryptionKey);
            Assert.AreSame(pdek2, entry2.ProtectedDataEncryptionKey);
        }

        [TestMethod]
        public void ShadowCache_KeyRewrap_ProducesNewCacheKey()
        {
            // Simulate key rewrap: same DEK name + KEK, but different wrapped bytes.
            Mde.ProtectedDataEncryptionKey pdek = CreateDummyPdek("key1");

            string cacheKeyBefore = "cek-pii/test://kek/AABBCCDD";       // original wrapped key hex
            string cacheKeyAfter = "cek-pii/test://kek/EEFF0011";        // rewrapped key hex

            EncryptionCosmosClient.ProtectedDataEncryptionKeyCache[cacheKeyBefore] =
                new ProtectedDataEncryptionKeyCacheEntry(pdek);

            // After rewrap, the new cache key should be a miss.
            Assert.IsFalse(
                EncryptionCosmosClient.ProtectedDataEncryptionKeyCache.ContainsKey(cacheKeyAfter),
                "Rewrapped DEK should produce a cache miss (different wrappedKeyHex).");

            // Old entry still exists (will be overwritten or expire naturally).
            Assert.IsTrue(
                EncryptionCosmosClient.ProtectedDataEncryptionKeyCache.ContainsKey(cacheKeyBefore),
                "Old entry should still exist until overwritten or expired.");
        }

        [TestMethod]
        public void ShadowCache_ExpiredEntry_TreatedAsMiss()
        {
            Mde.ProtectedDataEncryptionKey.TimeToLive = TimeSpan.FromMilliseconds(50);

            Mde.ProtectedDataEncryptionKey pdek = CreateDummyPdek("key1");
            string cacheKey = "cek-pii/test://kek/AABBCCDD";

            EncryptionCosmosClient.ProtectedDataEncryptionKeyCache[cacheKey] =
                new ProtectedDataEncryptionKeyCacheEntry(pdek);

            // Wait for expiry.
            Thread.Sleep(100);

            // Entry exists in dictionary but IsExpired should be true.
            Assert.IsTrue(
                EncryptionCosmosClient.ProtectedDataEncryptionKeyCache.TryGetValue(cacheKey, out ProtectedDataEncryptionKeyCacheEntry entry),
                "Entry should still be in the dictionary.");
            Assert.IsTrue(entry.IsExpired,
                "Entry should be expired, causing the fast-path to treat it as a miss.");
        }

        #endregion

        #region Concurrency — Shadow Cache Bypasses Semaphore

        [TestMethod]
        public async Task ShadowCacheHit_DoesNotRequireSemaphore()
        {
            // Pre-populate the shadow cache.
            Mde.ProtectedDataEncryptionKey pdek = CreateDummyPdek("key1");
            string cacheKey = "cek-pii/test://kek/AABBCCDD";
            EncryptionCosmosClient.ProtectedDataEncryptionKeyCache[cacheKey] =
                new ProtectedDataEncryptionKeyCacheEntry(pdek);

            // Hold the semaphore — simulates another thread doing a Key Vault call.
            await EncryptionCosmosClient.EncryptionKeyCacheSemaphore.WaitAsync();

            try
            {
                // Even with the semaphore held, shadow cache lookups should succeed
                // because the fast path doesn't touch the semaphore.
                int hitCount = 0;
                Stopwatch sw = Stopwatch.StartNew();

                Task[] tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
                {
                    if (EncryptionCosmosClient.ProtectedDataEncryptionKeyCache.TryGetValue(cacheKey, out ProtectedDataEncryptionKeyCacheEntry entry)
                        && !entry.IsExpired)
                    {
                        Interlocked.Increment(ref hitCount);
                    }
                })).ToArray();

                await Task.WhenAll(tasks);
                sw.Stop();

                Assert.AreEqual(100, hitCount,
                    "All 100 concurrent lookups should hit the shadow cache.");
                Assert.IsTrue(sw.ElapsedMilliseconds < 1000,
                    $"Shadow cache lookups should complete quickly even with semaphore held. Actual: {sw.ElapsedMilliseconds}ms");
            }
            finally
            {
                EncryptionCosmosClient.EncryptionKeyCacheSemaphore.Release();
            }
        }

        [TestMethod]
        public async Task ShadowCacheMiss_WaitsOnSemaphore()
        {
            // Shadow cache is empty — a miss.
            // Hold the semaphore to simulate contention.
            await EncryptionCosmosClient.EncryptionKeyCacheSemaphore.WaitAsync();

            string cacheKey = "cek-pii/test://kek/AABBCCDD";
            bool missDetected = false;

            // Verify the fast path correctly detects a miss.
            if (!EncryptionCosmosClient.ProtectedDataEncryptionKeyCache.TryGetValue(cacheKey, out _))
            {
                missDetected = true;
            }

            Assert.IsTrue(missDetected, "Empty shadow cache should report a miss.");

            // Verify the semaphore is unavailable (count == 0).
            Assert.AreEqual(0, EncryptionCosmosClient.EncryptionKeyCacheSemaphore.CurrentCount,
                "Semaphore should be held, so the slow path would need to wait.");

            EncryptionCosmosClient.EncryptionKeyCacheSemaphore.Release();
        }

        [TestMethod]
        public async Task DoubleCheckPattern_SecondThreadGetsPopulatedEntry()
        {
            // Simulate the double-check pattern:
            // Thread 1 acquires semaphore, populates shadow cache, releases.
            // Thread 2 acquires semaphore, re-checks shadow cache → HIT.

            Mde.ProtectedDataEncryptionKey pdek = CreateDummyPdek("key1");
            string cacheKey = "cek-pii/test://kek/AABBCCDD";

            // Thread 1: populate cache inside semaphore.
            await EncryptionCosmosClient.EncryptionKeyCacheSemaphore.WaitAsync();
            EncryptionCosmosClient.ProtectedDataEncryptionKeyCache[cacheKey] =
                new ProtectedDataEncryptionKeyCacheEntry(pdek);
            EncryptionCosmosClient.EncryptionKeyCacheSemaphore.Release();

            // Thread 2: acquires semaphore, double-checks shadow cache.
            await EncryptionCosmosClient.EncryptionKeyCacheSemaphore.WaitAsync();
            try
            {
                bool hit = EncryptionCosmosClient.ProtectedDataEncryptionKeyCache.TryGetValue(
                    cacheKey, out ProtectedDataEncryptionKeyCacheEntry entry)
                    && !entry.IsExpired;

                Assert.IsTrue(hit, "Double-check inside semaphore should find the entry Thread 1 populated.");
                Assert.AreSame(pdek, entry.ProtectedDataEncryptionKey);
            }
            finally
            {
                EncryptionCosmosClient.EncryptionKeyCacheSemaphore.Release();
            }
        }

        [TestMethod]
        public async Task CancellationToken_RespectedOnSemaphoreWait()
        {
            // Hold the semaphore.
            await EncryptionCosmosClient.EncryptionKeyCacheSemaphore.WaitAsync();

            try
            {
                // A thread that misses the shadow cache and tries to acquire the semaphore
                // should respect CancellationToken.
                using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

                await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () =>
                {
                    await EncryptionCosmosClient.EncryptionKeyCacheSemaphore
                        .WaitAsync(-1, cts.Token)
                        .ConfigureAwait(false);
                }, "WaitAsync should throw OperationCanceledException when the token is canceled.");
            }
            finally
            {
                EncryptionCosmosClient.EncryptionKeyCacheSemaphore.Release();
            }
        }

        #endregion

        #region Stress Test — Contention Elimination

        [TestMethod]
        public async Task StressTest_ConcurrentReadsWithCacheHit_NoContention()
        {
            // Simulate high-throughput scenario: 500 concurrent reads, all hitting shadow cache.
            Mde.ProtectedDataEncryptionKey pdek = CreateDummyPdek("key1");
            string cacheKey = "cek-pii/test://kek/AABBCCDD";
            EncryptionCosmosClient.ProtectedDataEncryptionKeyCache[cacheKey] =
                new ProtectedDataEncryptionKeyCacheEntry(pdek);

            ConcurrentBag<long> latencies = new ConcurrentBag<long>();

            Task[] tasks = Enumerable.Range(0, 500).Select(_ => Task.Run(() =>
            {
                Stopwatch sw = Stopwatch.StartNew();

                for (int i = 0; i < 100; i++)
                {
                    if (EncryptionCosmosClient.ProtectedDataEncryptionKeyCache.TryGetValue(
                        cacheKey, out ProtectedDataEncryptionKeyCacheEntry entry)
                        && !entry.IsExpired)
                    {
                        // Fast path — no semaphore.
                        Mde.ProtectedDataEncryptionKey unused = entry.ProtectedDataEncryptionKey;
                        Assert.IsNotNull(unused);
                    }
                }

                sw.Stop();
                latencies.Add(sw.ElapsedMilliseconds);
            })).ToArray();

            await Task.WhenAll(tasks);

            long p99 = latencies.OrderBy(l => l).ElementAt((int)(latencies.Count * 0.99));
            double avg = latencies.Average();

            Assert.IsTrue(avg < 50,
                $"Average latency for 500x100 shadow cache lookups should be under 50ms. Actual avg: {avg}ms");
            Assert.IsTrue(p99 < 200,
                $"P99 latency should be under 200ms. Actual P99: {p99}ms");
        }

        [TestMethod]
        public async Task StressTest_CacheMissFollowedByHits_OnlyFirstCallSlow()
        {
            // Simulate real-world: first thread misses, populates cache, all others hit.
            string cacheKey = "cek-pii/test://kek/AABBCCDD";
            int keyVaultCalls = 0;

            Task[] tasks = Enumerable.Range(0, 50).Select(i => Task.Run(async () =>
            {
                // Fast path check.
                if (EncryptionCosmosClient.ProtectedDataEncryptionKeyCache.TryGetValue(
                    cacheKey, out ProtectedDataEncryptionKeyCacheEntry entry)
                    && !entry.IsExpired)
                {
                    return; // Hit — no semaphore needed.
                }

                // Slow path: acquire semaphore.
                await EncryptionCosmosClient.EncryptionKeyCacheSemaphore
                    .WaitAsync(-1, CancellationToken.None)
                    .ConfigureAwait(false);
                try
                {
                    // Double-check.
                    if (EncryptionCosmosClient.ProtectedDataEncryptionKeyCache.TryGetValue(
                        cacheKey, out entry)
                        && !entry.IsExpired)
                    {
                        return; // Another thread populated it.
                    }

                    // Simulate Key Vault call (only the first thread should reach here).
                    Interlocked.Increment(ref keyVaultCalls);
                    await Task.Delay(100); // Simulate 100ms Key Vault latency.

                    Mde.ProtectedDataEncryptionKey pdek = CreateDummyPdek("key1");
                    EncryptionCosmosClient.ProtectedDataEncryptionKeyCache[cacheKey] =
                        new ProtectedDataEncryptionKeyCacheEntry(pdek);
                }
                finally
                {
                    EncryptionCosmosClient.EncryptionKeyCacheSemaphore.Release();
                }
            })).ToArray();

            await Task.WhenAll(tasks);

            Assert.AreEqual(1, keyVaultCalls,
                $"Only one thread should have called Key Vault (cache stampede prevention). Actual: {keyVaultCalls}");
            Assert.IsTrue(
                EncryptionCosmosClient.ProtectedDataEncryptionKeyCache.ContainsKey(cacheKey),
                "Shadow cache should be populated after the test.");
        }

        #endregion

        #region Memory — Cache Size

        [TestMethod]
        public void ShadowCache_HoldsMinimalEntries()
        {
            // Simulate a realistic setup with 3 DEKs.
            for (int i = 0; i < 3; i++)
            {
                Mde.ProtectedDataEncryptionKey pdek = CreateDummyPdek($"key{i}");
                string cacheKey = $"cek-{i}/test://kek/{i:X8}";
                EncryptionCosmosClient.ProtectedDataEncryptionKeyCache[cacheKey] =
                    new ProtectedDataEncryptionKeyCacheEntry(pdek);
            }

            Assert.AreEqual(3, EncryptionCosmosClient.ProtectedDataEncryptionKeyCache.Count,
                "Shadow cache should hold exactly one entry per DEK, not per document or per property.");
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Creates a real <see cref="Mde.ProtectedDataEncryptionKey"/> using the dummy KEK
        /// from TestCryptoHelpers. The DummyKeyEncryptionKey's provider does passthrough
        /// wrap/unwrap (returns the same bytes), so no Key Vault is needed.
        /// </summary>
        private static Mde.ProtectedDataEncryptionKey CreateDummyPdek(string name)
        {
            TestCryptoHelpers.DummyKeyEncryptionKey kek = new TestCryptoHelpers.DummyKeyEncryptionKey();
            return new Mde.ProtectedDataEncryptionKey(name, kek);
        }

        #endregion
    }
}
