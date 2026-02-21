//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core.Cryptography;
    using Microsoft.Data.Encryption.Cryptography;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for <see cref="EncryptionKeyStoreProviderImpl"/>, covering:
    ///   - Prefetch async cache (PrefetchUnwrapKeyAsync)
    ///   - UnwrapKey fast-path from prefetch cache
    ///   - Sync fallback when prefetch cache is cold
    ///   - Proactive background refresh near TTL expiry
    ///   - Concurrent decryption (many threads hitting UnwrapKey)
    ///   - Cache expiry behavior
    ///   - Cancellation token propagation
    ///
    /// All tests use an in-memory <see cref="InMemoryKeyResolver"/> that tracks call
    /// counts and optionally injects latency, so no real Key Vault calls are made.
    /// </summary>
    [TestClass]
    public class EncryptionKeyStoreProviderImplTests
    {
        private const string TestKeyId = "test-key-1";
        private const string TestProviderName = "TEST_PROVIDER";

        // A well-known plaintext key and its "encrypted" form (trivial shift cipher).
        private static readonly byte[] PlainKey = new byte[] { 10, 20, 30, 40, 50 };
        private static readonly byte[] EncryptedKey = PlainKey.Select(b => (byte)(b + 1)).ToArray();

        #region PrefetchUnwrapKeyAsync Tests

        [TestMethod]
        public async Task PrefetchUnwrapKeyAsync_WarmsCacheForSubsequentUnwrapKey()
        {
            // Arrange
            InMemoryKeyResolver resolver = new InMemoryKeyResolver();
            resolver.RegisterKey(TestKeyId, shift: 1);
            EncryptionKeyStoreProviderImpl provider = new CachingEncryptionKeyStoreProviderImpl(resolver, TestProviderName);

            // Act — prefetch the key asynchronously
            await provider.PrefetchUnwrapKeyAsync(TestKeyId, EncryptedKey, CancellationToken.None);

            // Assert — UnwrapKey should hit the prefetch cache, NOT call Resolve again.
            // Reset counters after prefetch so we can assert on the sync path.
            int resolveCountAfterPrefetch = resolver.ResolveAsyncCallCount;
            byte[] result = provider.UnwrapKey(TestKeyId, KeyEncryptionKeyAlgorithm.RSA_OAEP, EncryptedKey);

            CollectionAssert.AreEqual(PlainKey, result, "Unwrapped key should match original plaintext.");
            Assert.AreEqual(resolveCountAfterPrefetch, resolver.ResolveAsyncCallCount,
                "ResolveAsync should NOT be called again — UnwrapKey should use the prefetch cache.");
            Assert.AreEqual(0, resolver.ResolveSyncCallCount,
                "Sync Resolve should not be called when prefetch cache is warm.");
        }

        [TestMethod]
        public async Task PrefetchUnwrapKeyAsync_SkipsRefreshWhenCacheIsWarm()
        {
            // Arrange
            InMemoryKeyResolver resolver = new InMemoryKeyResolver();
            resolver.RegisterKey(TestKeyId, shift: 1);
            EncryptionKeyStoreProviderImpl provider = new CachingEncryptionKeyStoreProviderImpl(resolver, TestProviderName);

            // Act — prefetch twice in quick succession
            await provider.PrefetchUnwrapKeyAsync(TestKeyId, EncryptedKey, CancellationToken.None);
            int countAfterFirst = resolver.ResolveAsyncCallCount;

            await provider.PrefetchUnwrapKeyAsync(TestKeyId, EncryptedKey, CancellationToken.None);
            int countAfterSecond = resolver.ResolveAsyncCallCount;

            // Assert — second call should be a no-op (cache is well within TTL)
            Assert.AreEqual(countAfterFirst, countAfterSecond,
                "Second PrefetchUnwrapKeyAsync should skip refresh when the cache is still warm.");
        }

        [TestMethod]
        public async Task PrefetchUnwrapKeyAsync_PropagatesCancellation()
        {
            // Arrange
            InMemoryKeyResolver resolver = new InMemoryKeyResolver();
            resolver.RegisterKey(TestKeyId, shift: 1);
            resolver.ResolveAsyncDelay = TimeSpan.FromSeconds(10); // simulate slow resolve
            EncryptionKeyStoreProviderImpl provider = new CachingEncryptionKeyStoreProviderImpl(resolver, TestProviderName);

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel(); // pre-cancel

            // Act & Assert
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => provider.PrefetchUnwrapKeyAsync(TestKeyId, EncryptedKey, cts.Token));
        }

        [TestMethod]
        public async Task PrefetchUnwrapKeyAsync_UsesResolveAsyncNotResolve()
        {
            // Arrange
            InMemoryKeyResolver resolver = new InMemoryKeyResolver();
            resolver.RegisterKey(TestKeyId, shift: 1);
            EncryptionKeyStoreProviderImpl provider = new CachingEncryptionKeyStoreProviderImpl(resolver, TestProviderName);

            // Act
            await provider.PrefetchUnwrapKeyAsync(TestKeyId, EncryptedKey, CancellationToken.None);

            // Assert — the async path should call ResolveAsync, not Resolve
            Assert.AreEqual(1, resolver.ResolveAsyncCallCount, "PrefetchUnwrapKeyAsync should call ResolveAsync.");
            Assert.AreEqual(0, resolver.ResolveSyncCallCount, "PrefetchUnwrapKeyAsync should NOT call sync Resolve.");
        }

        [TestMethod]
        public async Task PrefetchUnwrapKeyAsync_IndependentKeysAreCachedSeparately()
        {
            // Arrange
            InMemoryKeyResolver resolver = new InMemoryKeyResolver();
            resolver.RegisterKey("key-A", shift: 1);
            resolver.RegisterKey("key-B", shift: 2);

            byte[] encKeyA = PlainKey.Select(b => (byte)(b + 1)).ToArray();
            byte[] encKeyB = PlainKey.Select(b => (byte)(b + 2)).ToArray();

            EncryptionKeyStoreProviderImpl provider = new CachingEncryptionKeyStoreProviderImpl(resolver, TestProviderName);

            // Act
            await provider.PrefetchUnwrapKeyAsync("key-A", encKeyA, CancellationToken.None);
            await provider.PrefetchUnwrapKeyAsync("key-B", encKeyB, CancellationToken.None);

            byte[] resultA = provider.UnwrapKey("key-A", KeyEncryptionKeyAlgorithm.RSA_OAEP, encKeyA);
            byte[] resultB = provider.UnwrapKey("key-B", KeyEncryptionKeyAlgorithm.RSA_OAEP, encKeyB);

            // Assert
            CollectionAssert.AreEqual(PlainKey, resultA, "Key A unwrap should return original plaintext.");
            CollectionAssert.AreEqual(PlainKey, resultB, "Key B unwrap should return original plaintext.");
            Assert.AreEqual(2, resolver.ResolveAsyncCallCount, "Each key should trigger exactly one ResolveAsync.");
        }

        #endregion

        #region UnwrapKey Sync Fallback Tests

        [TestMethod]
        public void UnwrapKey_FallsBackToSyncWhenPrefetchCacheIsCold()
        {
            // Arrange — no prefetch call; cache is empty
            InMemoryKeyResolver resolver = new InMemoryKeyResolver();
            resolver.RegisterKey(TestKeyId, shift: 1);
            EncryptionKeyStoreProviderImpl provider = new CachingEncryptionKeyStoreProviderImpl(resolver, TestProviderName);

            // Act
            byte[] result = provider.UnwrapKey(TestKeyId, KeyEncryptionKeyAlgorithm.RSA_OAEP, EncryptedKey);

            // Assert
            CollectionAssert.AreEqual(PlainKey, result, "Sync fallback should still unwrap correctly.");
            Assert.AreEqual(1, resolver.ResolveSyncCallCount,
                "Cold cache should trigger sync Resolve fallback.");
            Assert.AreEqual(0, resolver.ResolveAsyncCallCount,
                "Sync fallback should not call ResolveAsync.");
        }

        [TestMethod]
        public void UnwrapKey_SyncFallbackPopulatesPrefetchCache()
        {
            // Arrange — cold cache, sync fallback will populate it
            InMemoryKeyResolver resolver = new InMemoryKeyResolver();
            resolver.RegisterKey(TestKeyId, shift: 1);
            EncryptionKeyStoreProviderImpl provider = new CachingEncryptionKeyStoreProviderImpl(resolver, TestProviderName);

            // Act — first call goes through sync path
            provider.UnwrapKey(TestKeyId, KeyEncryptionKeyAlgorithm.RSA_OAEP, EncryptedKey);
            int syncCountAfterFirst = resolver.ResolveSyncCallCount;

            // Second call should hit prefetch cache (populated by first call's sync path)
            byte[] result = provider.UnwrapKey(TestKeyId, KeyEncryptionKeyAlgorithm.RSA_OAEP, EncryptedKey);

            // Assert
            CollectionAssert.AreEqual(PlainKey, result);
            Assert.AreEqual(syncCountAfterFirst, resolver.ResolveSyncCallCount,
                "Second UnwrapKey should hit the prefetch cache, not call Resolve again.");
        }

        [TestMethod]
        public void UnwrapKey_ReturnsCorrectResultForDifferentKeys()
        {
            // Arrange
            InMemoryKeyResolver resolver = new InMemoryKeyResolver();
            resolver.RegisterKey("key1", shift: 3);
            resolver.RegisterKey("key2", shift: 7);

            byte[] plain = new byte[] { 100, 110, 120 };
            byte[] enc1 = plain.Select(b => (byte)(b + 3)).ToArray();
            byte[] enc2 = plain.Select(b => (byte)(b + 7)).ToArray();

            EncryptionKeyStoreProviderImpl provider = new CachingEncryptionKeyStoreProviderImpl(resolver, TestProviderName);

            // Act
            byte[] result1 = provider.UnwrapKey("key1", KeyEncryptionKeyAlgorithm.RSA_OAEP, enc1);
            byte[] result2 = provider.UnwrapKey("key2", KeyEncryptionKeyAlgorithm.RSA_OAEP, enc2);

            // Assert
            CollectionAssert.AreEqual(plain, result1);
            CollectionAssert.AreEqual(plain, result2);
        }

        #endregion

        #region Concurrency Tests

        [TestMethod]
        public async Task ConcurrentUnwrapKey_WithPrefetchCache_NoContention()
        {
            // Arrange — prefetch the key, then hammer UnwrapKey from many threads
            InMemoryKeyResolver resolver = new InMemoryKeyResolver();
            resolver.RegisterKey(TestKeyId, shift: 1);
            EncryptionKeyStoreProviderImpl provider = new CachingEncryptionKeyStoreProviderImpl(resolver, TestProviderName);

            await provider.PrefetchUnwrapKeyAsync(TestKeyId, EncryptedKey, CancellationToken.None);
            int resolveCountAfterPrefetch = resolver.ResolveAsyncCallCount;

            int concurrency = 50;
            ConcurrentBag<byte[]> results = new ConcurrentBag<byte[]>();
            List<Task> tasks = new List<Task>();

            // Act — simulate concurrent feed iterator decryption
            for (int i = 0; i < concurrency; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    byte[] result = provider.UnwrapKey(TestKeyId, KeyEncryptionKeyAlgorithm.RSA_OAEP, EncryptedKey);
                    results.Add(result);
                }));
            }

            await Task.WhenAll(tasks);

            // Assert — all results correct, no additional Resolve calls
            Assert.AreEqual(concurrency, results.Count);
            foreach (byte[] result in results)
            {
                CollectionAssert.AreEqual(PlainKey, result, "Every concurrent unwrap should return correct bytes.");
            }

            Assert.AreEqual(resolveCountAfterPrefetch, resolver.ResolveAsyncCallCount,
                "No additional ResolveAsync calls should happen — all served from prefetch cache.");
            Assert.AreEqual(0, resolver.ResolveSyncCallCount,
                "No sync Resolve calls should happen when prefetch cache is warm.");
        }

        [TestMethod]
        public async Task ConcurrentUnwrapKey_ColdCache_AllSucceed()
        {
            // Arrange — no prefetch, all threads hit the sync fallback
            InMemoryKeyResolver resolver = new InMemoryKeyResolver();
            resolver.RegisterKey(TestKeyId, shift: 1);
            EncryptionKeyStoreProviderImpl provider = new CachingEncryptionKeyStoreProviderImpl(resolver, TestProviderName);

            int concurrency = 20;
            ConcurrentBag<byte[]> results = new ConcurrentBag<byte[]>();
            List<Task> tasks = new List<Task>();

            // Act
            for (int i = 0; i < concurrency; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    byte[] result = provider.UnwrapKey(TestKeyId, KeyEncryptionKeyAlgorithm.RSA_OAEP, EncryptedKey);
                    results.Add(result);
                }));
            }

            await Task.WhenAll(tasks);

            // Assert — all succeed despite contention on the sync fallback
            Assert.AreEqual(concurrency, results.Count);
            foreach (byte[] result in results)
            {
                CollectionAssert.AreEqual(PlainKey, result);
            }
        }

        [TestMethod]
        public async Task ConcurrentPrefetchAndUnwrap_MixedPattern()
        {
            // Simulate the real pattern: some threads prefetch while others are already unwrapping.
            InMemoryKeyResolver resolver = new InMemoryKeyResolver();
            resolver.RegisterKey(TestKeyId, shift: 1);
            EncryptionKeyStoreProviderImpl provider = new CachingEncryptionKeyStoreProviderImpl(resolver, TestProviderName);

            int totalOps = 40;
            ConcurrentBag<byte[]> results = new ConcurrentBag<byte[]>();
            List<Task> tasks = new List<Task>();

            for (int i = 0; i < totalOps; i++)
            {
                if (i % 5 == 0)
                {
                    // Every 5th thread does a prefetch first
                    tasks.Add(Task.Run(async () =>
                    {
                        await provider.PrefetchUnwrapKeyAsync(TestKeyId, EncryptedKey, CancellationToken.None);
                        byte[] result = provider.UnwrapKey(TestKeyId, KeyEncryptionKeyAlgorithm.RSA_OAEP, EncryptedKey);
                        results.Add(result);
                    }));
                }
                else
                {
                    // Other threads just call UnwrapKey directly
                    tasks.Add(Task.Run(() =>
                    {
                        byte[] result = provider.UnwrapKey(TestKeyId, KeyEncryptionKeyAlgorithm.RSA_OAEP, EncryptedKey);
                        results.Add(result);
                    }));
                }
            }

            await Task.WhenAll(tasks);

            Assert.AreEqual(totalOps, results.Count);
            foreach (byte[] result in results)
            {
                CollectionAssert.AreEqual(PlainKey, result, "All concurrent operations should return correct bytes.");
            }
        }

        [TestMethod]
        public async Task ConcurrentUnwrapKey_MultipleKeys_NoInterference()
        {
            // Verify that concurrent operations on different keys don't interfere.
            InMemoryKeyResolver resolver = new InMemoryKeyResolver();
            resolver.RegisterKey("keyA", shift: 1);
            resolver.RegisterKey("keyB", shift: 2);
            resolver.RegisterKey("keyC", shift: 3);

            byte[] encA = PlainKey.Select(b => (byte)(b + 1)).ToArray();
            byte[] encB = PlainKey.Select(b => (byte)(b + 2)).ToArray();
            byte[] encC = PlainKey.Select(b => (byte)(b + 3)).ToArray();

            EncryptionKeyStoreProviderImpl provider = new CachingEncryptionKeyStoreProviderImpl(resolver, TestProviderName);

            // Prefetch all keys
            await provider.PrefetchUnwrapKeyAsync("keyA", encA, CancellationToken.None);
            await provider.PrefetchUnwrapKeyAsync("keyB", encB, CancellationToken.None);
            await provider.PrefetchUnwrapKeyAsync("keyC", encC, CancellationToken.None);

            int concurrencyPerKey = 20;
            ConcurrentBag<(string Key, byte[] Result)> results = new ConcurrentBag<(string, byte[])>();
            List<Task> tasks = new List<Task>();

            foreach ((string keyId, byte[] enc) in new[] { ("keyA", encA), ("keyB", encB), ("keyC", encC) })
            {
                for (int i = 0; i < concurrencyPerKey; i++)
                {
                    string capturedKeyId = keyId;
                    byte[] capturedEnc = enc;
                    tasks.Add(Task.Run(() =>
                    {
                        byte[] result = provider.UnwrapKey(capturedKeyId, KeyEncryptionKeyAlgorithm.RSA_OAEP, capturedEnc);
                        results.Add((capturedKeyId, result));
                    }));
                }
            }

            await Task.WhenAll(tasks);

            Assert.AreEqual(concurrencyPerKey * 3, results.Count);
            foreach ((string key, byte[] result) in results)
            {
                CollectionAssert.AreEqual(PlainKey, result, $"Key '{key}' should unwrap to original plaintext.");
            }
        }

        #endregion

        #region WrapKey Tests

        [TestMethod]
        public void WrapKey_PassesThroughToResolver()
        {
            // Arrange
            InMemoryKeyResolver resolver = new InMemoryKeyResolver();
            resolver.RegisterKey(TestKeyId, shift: 1);
            EncryptionKeyStoreProviderImpl provider = new CachingEncryptionKeyStoreProviderImpl(resolver, TestProviderName);

            // Act
            byte[] wrapped = provider.WrapKey(TestKeyId, KeyEncryptionKeyAlgorithm.RSA_OAEP, PlainKey);

            // Assert
            CollectionAssert.AreEqual(EncryptedKey, wrapped);
        }

        #endregion

        #region Prefetch + Latency Simulation Tests

        [TestMethod]
        public async Task PrefetchUnwrapKeyAsync_WithSlowResolver_UnwrapKeyIsFast()
        {
            // Arrange — resolver has 200ms latency
            InMemoryKeyResolver resolver = new InMemoryKeyResolver();
            resolver.RegisterKey(TestKeyId, shift: 1);
            resolver.ResolveAsyncDelay = TimeSpan.FromMilliseconds(200);
            EncryptionKeyStoreProviderImpl provider = new CachingEncryptionKeyStoreProviderImpl(resolver, TestProviderName);

            // Act — prefetch absorbs the latency
            await provider.PrefetchUnwrapKeyAsync(TestKeyId, EncryptedKey, CancellationToken.None);

            // Time the sync UnwrapKey — it should be near-instant from cache
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            byte[] result = provider.UnwrapKey(TestKeyId, KeyEncryptionKeyAlgorithm.RSA_OAEP, EncryptedKey);
            sw.Stop();

            // Assert
            CollectionAssert.AreEqual(PlainKey, result);
            Assert.IsTrue(sw.ElapsedMilliseconds < 50,
                $"UnwrapKey from prefetch cache should be near-instant but took {sw.ElapsedMilliseconds}ms.");
        }

        [TestMethod]
        public async Task ConcurrentUnwrapKey_WithSlowResolver_PrefetchEliminatesContention()
        {
            // Simulate the real scenario: slow Key Vault, high concurrency.
            // With prefetch, all threads should return instantly.
            InMemoryKeyResolver resolver = new InMemoryKeyResolver();
            resolver.RegisterKey(TestKeyId, shift: 1);
            resolver.ResolveAsyncDelay = TimeSpan.FromMilliseconds(500);
            EncryptionKeyStoreProviderImpl provider = new CachingEncryptionKeyStoreProviderImpl(resolver, TestProviderName);

            // Prefetch outside the hot path (absorbs the 500ms)
            await provider.PrefetchUnwrapKeyAsync(TestKeyId, EncryptedKey, CancellationToken.None);

            int concurrency = 100;
            ConcurrentBag<long> timings = new ConcurrentBag<long>();
            List<Task> tasks = new List<Task>();

            for (int i = 0; i < concurrency; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                    byte[] result = provider.UnwrapKey(TestKeyId, KeyEncryptionKeyAlgorithm.RSA_OAEP, EncryptedKey);
                    sw.Stop();
                    timings.Add(sw.ElapsedMilliseconds);
                }));
            }

            await Task.WhenAll(tasks);

            long maxTiming = timings.Max();
            Assert.IsTrue(maxTiming < 100,
                $"With warm prefetch cache, worst-case UnwrapKey should be <100ms, was {maxTiming}ms.");
            Assert.AreEqual(0, resolver.ResolveSyncCallCount,
                "No sync Resolve calls when prefetch cache is warm.");
        }

        #endregion

        #region Edge Case Tests

        [TestMethod]
        public void UnwrapKey_ThrowsOnInvalidAlgorithm()
        {
            InMemoryKeyResolver resolver = new InMemoryKeyResolver();
            resolver.RegisterKey(TestKeyId, shift: 1);
            EncryptionKeyStoreProviderImpl provider = new EncryptionKeyStoreProviderImpl(resolver, TestProviderName);

            Assert.ThrowsException<InvalidOperationException>(
                () => provider.UnwrapKey(TestKeyId, (KeyEncryptionKeyAlgorithm)999, EncryptedKey));
        }

        [TestMethod]
        public void Sign_ThrowsNotSupported()
        {
            InMemoryKeyResolver resolver = new InMemoryKeyResolver();
            EncryptionKeyStoreProviderImpl provider = new EncryptionKeyStoreProviderImpl(resolver, TestProviderName);

            Assert.ThrowsException<NotSupportedException>(
                () => provider.Sign("key", true));
        }

        [TestMethod]
        public void Verify_ThrowsNotSupported()
        {
            InMemoryKeyResolver resolver = new InMemoryKeyResolver();
            EncryptionKeyStoreProviderImpl provider = new EncryptionKeyStoreProviderImpl(resolver, TestProviderName);

            Assert.ThrowsException<NotSupportedException>(
                () => provider.Verify("key", true, new byte[] { 1 }));
        }

        [TestMethod]
        public void ProviderName_ReturnsConfiguredName()
        {
            InMemoryKeyResolver resolver = new InMemoryKeyResolver();
            EncryptionKeyStoreProviderImpl provider = new EncryptionKeyStoreProviderImpl(resolver, TestProviderName);

            Assert.AreEqual(TestProviderName, provider.ProviderName);
        }

        [TestMethod]
        public void DataEncryptionKeyCacheTimeToLive_IsZeroByDefault()
        {
            InMemoryKeyResolver resolver = new InMemoryKeyResolver();
            EncryptionKeyStoreProviderImpl provider = new EncryptionKeyStoreProviderImpl(resolver, TestProviderName);

            Assert.AreEqual(TimeSpan.Zero, provider.DataEncryptionKeyCacheTimeToLive);
        }

        #endregion

        #region Base Class No-Op Tests

        [TestMethod]
        public async Task BaseClass_PrefetchUnwrapKeyAsync_IsNoOp()
        {
            // Arrange — use the base class directly (no caching)
            InMemoryKeyResolver resolver = new InMemoryKeyResolver();
            resolver.RegisterKey(TestKeyId, shift: 1);
            EncryptionKeyStoreProviderImpl provider = new EncryptionKeyStoreProviderImpl(resolver, TestProviderName);

            // Act — prefetch should be a no-op on the base class
            await provider.PrefetchUnwrapKeyAsync(TestKeyId, EncryptedKey, CancellationToken.None);

            // Assert — no resolver calls made (base class returns Task.CompletedTask)
            Assert.AreEqual(0, resolver.ResolveAsyncCallCount,
                "Base class PrefetchUnwrapKeyAsync should be a no-op.");

            // UnwrapKey should use the sync Resolve path
            byte[] result = provider.UnwrapKey(TestKeyId, KeyEncryptionKeyAlgorithm.RSA_OAEP, EncryptedKey);
            CollectionAssert.AreEqual(PlainKey, result);
            Assert.AreEqual(1, resolver.ResolveSyncCallCount,
                "Base class UnwrapKey should use the sync Resolve path.");
        }

        [TestMethod]
        public void BaseClass_Cleanup_IsNoOp()
        {
            // Arrange
            InMemoryKeyResolver resolver = new InMemoryKeyResolver();
            EncryptionKeyStoreProviderImpl provider = new EncryptionKeyStoreProviderImpl(resolver, TestProviderName);

            // Act & Assert — should not throw
            provider.Cleanup();
            provider.Cleanup(); // double-call safe
        }

        #endregion

        #region Cleanup / Lifecycle Tests

        [TestMethod]
        public void Cleanup_CanBeCalledMultipleTimes()
        {
            // Arrange
            InMemoryKeyResolver resolver = new InMemoryKeyResolver();
            CachingEncryptionKeyStoreProviderImpl provider = new CachingEncryptionKeyStoreProviderImpl(resolver, TestProviderName);

            // Act & Assert — should not throw on double-cleanup
            provider.Cleanup();
            provider.Cleanup();
        }

        [TestMethod]
        public async Task Cleanup_CancelsInFlightBackgroundRefresh()
        {
            // Arrange — use a very slow resolver so the background refresh is still in-flight when we clean up
            InMemoryKeyResolver resolver = new InMemoryKeyResolver();
            resolver.RegisterKey(TestKeyId, shift: 1);
            resolver.ResolveAsyncDelay = TimeSpan.FromSeconds(30); // very slow

            CachingEncryptionKeyStoreProviderImpl provider = new CachingEncryptionKeyStoreProviderImpl(resolver, TestProviderName);

            // Pre-warm with a fast resolve first (bypass the slow delay for initial population)
            TimeSpan originalDelay = resolver.ResolveAsyncDelay;
            resolver.ResolveAsyncDelay = TimeSpan.Zero;
            await provider.PrefetchUnwrapKeyAsync(TestKeyId, EncryptedKey, CancellationToken.None);
            resolver.ResolveAsyncDelay = originalDelay;

            // Act — cleanup should return quickly, not block on the 30s resolve
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            provider.Cleanup();
            sw.Stop();

            Assert.IsTrue(sw.ElapsedMilliseconds < 1000,
                $"Cleanup should complete promptly (not block on background I/O), took {sw.ElapsedMilliseconds}ms.");
        }

        [TestMethod]
        public async Task Cleanup_ClearsPrefetchCache()
        {
            // Arrange
            InMemoryKeyResolver resolver = new InMemoryKeyResolver();
            resolver.RegisterKey(TestKeyId, shift: 1);
            CachingEncryptionKeyStoreProviderImpl provider = new CachingEncryptionKeyStoreProviderImpl(resolver, TestProviderName);

            await provider.PrefetchUnwrapKeyAsync(TestKeyId, EncryptedKey, CancellationToken.None);

            // Verify cache is warm
            byte[] warmResult = provider.UnwrapKey(TestKeyId, KeyEncryptionKeyAlgorithm.RSA_OAEP, EncryptedKey);
            CollectionAssert.AreEqual(PlainKey, warmResult);
            int resolveCountBeforeCleanup = resolver.ResolveAsyncCallCount + resolver.ResolveSyncCallCount;

            // Act
            provider.Cleanup();

            // After cleanup, UnwrapKey should go through the sync fallback
            // (prefetch cache was cleared).
            byte[] postCleanupResult = provider.UnwrapKey(TestKeyId, KeyEncryptionKeyAlgorithm.RSA_OAEP, EncryptedKey);
            CollectionAssert.AreEqual(PlainKey, postCleanupResult);

            int resolveCountAfterCleanup = resolver.ResolveAsyncCallCount + resolver.ResolveSyncCallCount;
            Assert.IsTrue(resolveCountAfterCleanup > resolveCountBeforeCleanup,
                "After Cleanup clears prefetch cache, UnwrapKey should require a new Resolve call.");
        }

        #endregion

        #region InMemoryKeyResolver — test double

        /// <summary>
        /// An in-memory implementation of <see cref="IKeyEncryptionKeyResolver"/> for unit testing.
        /// Each registered key uses a simple shift cipher (add/subtract a byte offset).
        /// Tracks call counts for assertions and supports configurable latency.
        /// </summary>
        internal sealed class InMemoryKeyResolver : IKeyEncryptionKeyResolver
        {
            private readonly Dictionary<string, int> keyShifts = new Dictionary<string, int>();

            private int resolveAsyncCallCount;
            private int resolveSyncCallCount;

            /// <summary>
            /// Optional delay injected into ResolveAsync to simulate Key Vault latency.
            /// </summary>
            public TimeSpan ResolveAsyncDelay { get; set; } = TimeSpan.Zero;

            /// <summary>
            /// Optional delay injected into sync Resolve to simulate Key Vault latency.
            /// </summary>
            public TimeSpan ResolveSyncDelay { get; set; } = TimeSpan.Zero;

            public int ResolveAsyncCallCount => this.resolveAsyncCallCount;

            public int ResolveSyncCallCount => this.resolveSyncCallCount;

            public void RegisterKey(string keyId, int shift)
            {
                this.keyShifts[keyId] = shift;
            }

            public IKeyEncryptionKey Resolve(string keyId, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Interlocked.Increment(ref this.resolveSyncCallCount);

                if (this.ResolveSyncDelay > TimeSpan.Zero)
                {
                    Thread.Sleep(this.ResolveSyncDelay);
                }

                return this.CreateKey(keyId);
            }

            public async Task<IKeyEncryptionKey> ResolveAsync(string keyId, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Interlocked.Increment(ref this.resolveAsyncCallCount);

                if (this.ResolveAsyncDelay > TimeSpan.Zero)
                {
                    await Task.Delay(this.ResolveAsyncDelay, cancellationToken);
                }

                return this.CreateKey(keyId);
            }

            private IKeyEncryptionKey CreateKey(string keyId)
            {
                if (!this.keyShifts.TryGetValue(keyId, out int shift))
                {
                    throw new InvalidOperationException($"Key '{keyId}' not registered in InMemoryKeyResolver.");
                }

                return new InMemoryKeyEncryptionKey(keyId, shift);
            }
        }

        /// <summary>
        /// A trivial in-memory <see cref="IKeyEncryptionKey"/> using a byte-shift cipher.
        /// </summary>
        internal sealed class InMemoryKeyEncryptionKey : IKeyEncryptionKey
        {
            private readonly int shift;

            public InMemoryKeyEncryptionKey(string keyId, int shift)
            {
                this.KeyId = keyId;
                this.shift = shift;
            }

            public string KeyId { get; }

            public byte[] WrapKey(string algorithm, ReadOnlyMemory<byte> key, CancellationToken cancellationToken = default)
            {
                return key.ToArray().Select(b => (byte)(b + this.shift)).ToArray();
            }

            public Task<byte[]> WrapKeyAsync(string algorithm, ReadOnlyMemory<byte> key, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(this.WrapKey(algorithm, key, cancellationToken));
            }

            public byte[] UnwrapKey(string algorithm, ReadOnlyMemory<byte> encryptedKey, CancellationToken cancellationToken = default)
            {
                return encryptedKey.ToArray().Select(b => (byte)(b - this.shift)).ToArray();
            }

            public Task<byte[]> UnwrapKeyAsync(string algorithm, ReadOnlyMemory<byte> encryptedKey, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(this.UnwrapKey(algorithm, encryptedKey, cancellationToken));
            }
        }

        #endregion
    }
}
