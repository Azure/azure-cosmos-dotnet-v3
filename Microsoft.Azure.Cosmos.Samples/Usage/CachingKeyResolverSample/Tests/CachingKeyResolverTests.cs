// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace CachingKeyResolverSample.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core.Cryptography;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class CachingKeyResolverTests
    {
        private const string TestKeyId = "https://my-vault.vault.azure.net/keys/my-key/abc123";

        private static Mock<IKeyEncryptionKeyResolver> CreateMockResolver()
        {
            Mock<IKeyEncryptionKeyResolver> mock = new Mock<IKeyEncryptionKeyResolver>(MockBehavior.Strict);
            Mock<IKeyEncryptionKey> mockKey = new Mock<IKeyEncryptionKey>();
            mockKey.Setup(k => k.KeyId).Returns(TestKeyId);

            mock.Setup(r => r.Resolve(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(mockKey.Object);

            mock.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockKey.Object);

            return mock;
        }

        [TestMethod]
        public void CacheHitReturnsImmediately()
        {
            Mock<IKeyEncryptionKeyResolver> mockResolver = CreateMockResolver();
            CachingKeyResolverOptions options = new CachingKeyResolverOptions
            {
                KeyCacheTimeToLive = TimeSpan.FromHours(1),
                RefreshTimerInterval = TimeSpan.FromHours(1), // prevent timer interference
                UnwrapKeyCacheTimeToLive = TimeSpan.Zero, // disable unwrap wrapping for reference equality test
            };

            using CachingKeyResolver sut = new CachingKeyResolver(mockResolver.Object, options);

            // First call: cache miss — inner resolver is invoked.
            IKeyEncryptionKey result1 = sut.Resolve(TestKeyId);
            // Second call: cache hit — inner resolver should NOT be invoked again.
            IKeyEncryptionKey result2 = sut.Resolve(TestKeyId);

            Assert.AreSame(result1, result2);
            mockResolver.Verify(r => r.Resolve(TestKeyId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public void CacheMissFallsThrough()
        {
            Mock<IKeyEncryptionKeyResolver> mockResolver = CreateMockResolver();
            CachingKeyResolverOptions options = new CachingKeyResolverOptions
            {
                KeyCacheTimeToLive = TimeSpan.FromHours(1),
                RefreshTimerInterval = TimeSpan.FromHours(1),
            };

            using CachingKeyResolver sut = new CachingKeyResolver(mockResolver.Object, options);

            IKeyEncryptionKey result = sut.Resolve(TestKeyId);

            Assert.IsNotNull(result);
            mockResolver.Verify(r => r.Resolve(TestKeyId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public void ExpiredEntryIsReResolved()
        {
            Mock<IKeyEncryptionKeyResolver> mockResolver = new Mock<IKeyEncryptionKeyResolver>(MockBehavior.Strict);
            Mock<IKeyEncryptionKey> key1 = new Mock<IKeyEncryptionKey>();
            Mock<IKeyEncryptionKey> key2 = new Mock<IKeyEncryptionKey>();

            int callCount = 0;
            mockResolver.Setup(r => r.Resolve(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    Interlocked.Increment(ref callCount);
                    return callCount == 1 ? key1.Object : key2.Object;
                });

            // Also setup async for background refresh timer 
            mockResolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => key2.Object);

            CachingKeyResolverOptions options = new CachingKeyResolverOptions
            {
                KeyCacheTimeToLive = TimeSpan.FromMilliseconds(200),
                ProactiveRefreshThreshold = TimeSpan.Zero, // disable proactive refresh
                RefreshTimerInterval = TimeSpan.FromHours(1), // prevent timer interference
                UnwrapKeyCacheTimeToLive = TimeSpan.Zero, // disable wrapping for AreSame assertions
            };

            using CachingKeyResolver sut = new CachingKeyResolver(mockResolver.Object, options);

            IKeyEncryptionKey first = sut.Resolve(TestKeyId);
            Assert.AreSame(key1.Object, first);

            // Wait for entry to expire.
            Thread.Sleep(300);

            IKeyEncryptionKey second = sut.Resolve(TestKeyId);
            Assert.AreSame(key2.Object, second);
            Assert.AreEqual(2, callCount);
        }

        [TestMethod]
        public void ConcurrentResolveCallsAreSafe()
        {
            Mock<IKeyEncryptionKeyResolver> mockResolver = CreateMockResolver();
            CachingKeyResolverOptions options = new CachingKeyResolverOptions
            {
                KeyCacheTimeToLive = TimeSpan.FromHours(1),
                RefreshTimerInterval = TimeSpan.FromHours(1),
                UnwrapKeyCacheTimeToLive = TimeSpan.Zero, // disable unwrap wrapping for reference equality
            };

            using CachingKeyResolver sut = new CachingKeyResolver(mockResolver.Object, options);

            ConcurrentBag<IKeyEncryptionKey> results = new ConcurrentBag<IKeyEncryptionKey>();
            List<Task> tasks = new List<Task>();

            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    IKeyEncryptionKey key = sut.Resolve(TestKeyId);
                    results.Add(key);
                }));
            }

            Task.WaitAll(tasks.ToArray());

            Assert.AreEqual(100, results.Count);
            // All should return the same cached instance (after the first resolution).
            Assert.IsTrue(results.All(k => k == results.First()));
        }

        [TestMethod]
        public async Task BackgroundRefreshUpdatesCache()
        {
            Mock<IKeyEncryptionKeyResolver> mockResolver = new Mock<IKeyEncryptionKeyResolver>(MockBehavior.Strict);
            Mock<IKeyEncryptionKey> originalKey = new Mock<IKeyEncryptionKey>();
            Mock<IKeyEncryptionKey> refreshedKey = new Mock<IKeyEncryptionKey>();

            int resolveAsyncCallCount = 0;

            mockResolver.Setup(r => r.Resolve(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(originalKey.Object);

            mockResolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    Interlocked.Increment(ref resolveAsyncCallCount);
                    return refreshedKey.Object;
                });

            CachingKeyResolverOptions options = new CachingKeyResolverOptions
            {
                KeyCacheTimeToLive = TimeSpan.FromMilliseconds(500),
                ProactiveRefreshThreshold = TimeSpan.FromMilliseconds(400), // refresh when < 400ms left
                RefreshTimerInterval = TimeSpan.FromMilliseconds(100), // check every 100ms
                UnwrapKeyCacheTimeToLive = TimeSpan.Zero, // disable wrapping for AreSame assertions
            };

            using CachingKeyResolver sut = new CachingKeyResolver(mockResolver.Object, options);

            // Seed the cache.
            IKeyEncryptionKey first = sut.Resolve(TestKeyId);
            Assert.AreSame(originalKey.Object, first);

            // Wait for proactive refresh: the entry expires at ~500ms,
            // proactive threshold is 400ms, so refresh should fire at ~100ms.
            // Timer checks every 100ms.
            await Task.Delay(600);

            // The cache should now contain the refreshed key.
            IKeyEncryptionKey after = sut.Resolve(TestKeyId);
            Assert.AreSame(refreshedKey.Object, after);
            Assert.IsTrue(resolveAsyncCallCount >= 1, $"Expected at least 1 async resolve call, got {resolveAsyncCallCount}");
        }

        [TestMethod]
        public async Task BackgroundRefreshFailureDoesNotEvict()
        {
            Mock<IKeyEncryptionKeyResolver> mockResolver = new Mock<IKeyEncryptionKeyResolver>(MockBehavior.Strict);
            Mock<IKeyEncryptionKey> originalKey = new Mock<IKeyEncryptionKey>();

            mockResolver.Setup(r => r.Resolve(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(originalKey.Object);

            // Async resolve (used by background refresh) throws.
            mockResolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Simulated AKV failure"));

            CachingKeyResolverOptions options = new CachingKeyResolverOptions
            {
                KeyCacheTimeToLive = TimeSpan.FromMilliseconds(500),
                ProactiveRefreshThreshold = TimeSpan.FromMilliseconds(400),
                RefreshTimerInterval = TimeSpan.FromMilliseconds(100),
                UnwrapKeyCacheTimeToLive = TimeSpan.Zero, // disable wrapping for AreSame assertions
            };

            using CachingKeyResolver sut = new CachingKeyResolver(mockResolver.Object, options);

            // Seed the cache.
            IKeyEncryptionKey first = sut.Resolve(TestKeyId);
            Assert.AreSame(originalKey.Object, first);

            // Wait for background refresh attempt (which will fail).
            await Task.Delay(400);

            // The original entry should still be in cache (not evicted).
            IKeyEncryptionKey afterFailedRefresh = sut.Resolve(TestKeyId);
            Assert.AreSame(originalKey.Object, afterFailedRefresh);
        }

        [TestMethod]
        public void DisposeStopsTimer()
        {
            Mock<IKeyEncryptionKeyResolver> mockResolver = CreateMockResolver();
            CachingKeyResolverOptions options = new CachingKeyResolverOptions
            {
                KeyCacheTimeToLive = TimeSpan.FromHours(1),
                RefreshTimerInterval = TimeSpan.FromMilliseconds(50),
            };

            CachingKeyResolver sut = new CachingKeyResolver(mockResolver.Object, options);

            // Seed cache.
            sut.Resolve(TestKeyId);

            // Dispose.
            sut.Dispose();

            // After dispose, inner resolver should not receive any new calls.
            int callsBefore = mockResolver.Invocations.Count;
            Thread.Sleep(200); // Wait several timer intervals.
            int callsAfter = mockResolver.Invocations.Count;

            Assert.AreEqual(callsBefore, callsAfter, "No new resolve calls should happen after Dispose.");
        }

        [TestMethod]
        public void ResolveAfterDisposeThrows()
        {
            Mock<IKeyEncryptionKeyResolver> mockResolver = CreateMockResolver();
            CachingKeyResolverOptions options = new CachingKeyResolverOptions
            {
                KeyCacheTimeToLive = TimeSpan.FromHours(1),
                RefreshTimerInterval = TimeSpan.FromHours(1),
            };

            CachingKeyResolver sut = new CachingKeyResolver(mockResolver.Object, options);
            sut.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(() => sut.Resolve(TestKeyId));
        }

        [TestMethod]
        public async Task ResolveAsyncAfterDisposeThrows()
        {
            Mock<IKeyEncryptionKeyResolver> mockResolver = CreateMockResolver();
            CachingKeyResolverOptions options = new CachingKeyResolverOptions
            {
                KeyCacheTimeToLive = TimeSpan.FromHours(1),
                RefreshTimerInterval = TimeSpan.FromHours(1),
            };

            CachingKeyResolver sut = new CachingKeyResolver(mockResolver.Object, options);
            sut.Dispose();

            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(
                () => sut.ResolveAsync(TestKeyId));
        }

        [TestMethod]
        public async Task RefreshDeduplication()
        {
            Mock<IKeyEncryptionKeyResolver> mockResolver = new Mock<IKeyEncryptionKeyResolver>(MockBehavior.Strict);
            Mock<IKeyEncryptionKey> key = new Mock<IKeyEncryptionKey>();

            int asyncResolveCount = 0;

            mockResolver.Setup(r => r.Resolve(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(key.Object);

            // Async resolve is slow, simulating AKV latency.
            mockResolver.Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(async (string id, CancellationToken ct) =>
                {
                    Interlocked.Increment(ref asyncResolveCount);
                    await Task.Delay(300, ct);
                    return key.Object;
                });

            CachingKeyResolverOptions options = new CachingKeyResolverOptions
            {
                KeyCacheTimeToLive = TimeSpan.FromMilliseconds(400),
                ProactiveRefreshThreshold = TimeSpan.FromMilliseconds(350), // Almost always eligible
                RefreshTimerInterval = TimeSpan.FromMilliseconds(50), // Fire rapidly
            };

            using CachingKeyResolver sut = new CachingKeyResolver(mockResolver.Object, options);

            // Seed cache.
            sut.Resolve(TestKeyId);

            // Wait for multiple timer ticks to fire while refresh is in flight.
            // The resolve is 300ms, timer is 50ms, so multiple ticks will fire
            // during one refresh. Deduplication should prevent multiple concurrent resolves.
            await Task.Delay(800);

            // We expect one initial sync Resolve + a small number of async refresh calls.
            // Without deduplication, we'd see many more async calls.
            // With deduplication, the number of async calls should be low (1-3 depending on timing).
            Assert.IsTrue(
                asyncResolveCount <= 4,
                $"Expected at most 4 async resolve calls due to deduplication, got {asyncResolveCount}");
        }
    }
}
