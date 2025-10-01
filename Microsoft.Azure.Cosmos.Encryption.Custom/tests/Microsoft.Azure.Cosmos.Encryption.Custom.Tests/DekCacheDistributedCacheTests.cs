//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Caching.Distributed;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class DekCacheDistributedCacheTests
    {
        [TestMethod]
        public async Task DekCache_WithoutDistributedCache_UsesOnlyL1Cache()
        {
            // Arrange
            DekCache cache = new DekCache(dekPropertiesTimeToLive: TimeSpan.FromMinutes(30));
            int fetchCount = 0;

            DataEncryptionKeyProperties CreateDekProperties(string id)
            {
                fetchCount++;
                return new DataEncryptionKeyProperties(
                    id,
                    "AEAD_AES_256_CBC_HMAC_SHA256",
                    new byte[] { 1, 2, 3 },
                    new EncryptionKeyWrapMetadata("test", "test", "RSA-OAEP", "test"),
                    DateTime.UtcNow);
            }

            // Act - First fetch
            DataEncryptionKeyProperties result1 = await cache.GetOrAddDekPropertiesAsync(
                "testDek",
                (id, ctx, ct) => Task.FromResult(CreateDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // Act - Second fetch (should use L1 cache)
            DataEncryptionKeyProperties result2 = await cache.GetOrAddDekPropertiesAsync(
                "testDek",
                (id, ctx, ct) => Task.FromResult(CreateDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // Assert
            Assert.AreEqual(1, fetchCount, "Should only fetch once - second call should use L1 cache");
            Assert.AreEqual(result1.Id, result2.Id);
        }

        [TestMethod]
        public async Task DekCache_WithDistributedCache_UsesL2Cache()
        {
            // Arrange
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();
            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache);

            int fetchCount = 0;

            DataEncryptionKeyProperties CreateDekProperties(string id)
            {
                fetchCount++;
                return new DataEncryptionKeyProperties(
                    id,
                    "AEAD_AES_256_CBC_HMAC_SHA256",
                    new byte[] { 1, 2, 3 },
                    new EncryptionKeyWrapMetadata("test", "test", "RSA-OAEP", "test"),
                    DateTime.UtcNow);
            }

            // Act - First fetch (will populate both L1 and L2)
            DataEncryptionKeyProperties result1 = await cache.GetOrAddDekPropertiesAsync(
                "testDek",
                (id, ctx, ct) => Task.FromResult(CreateDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // Clear L1 cache to test L2 cache
            await cache.RemoveAsync("testDek");

            // Act - Second fetch (should use L2 cache)
            DataEncryptionKeyProperties result2 = await cache.GetOrAddDekPropertiesAsync(
                "testDek",
                (id, ctx, ct) => Task.FromResult(CreateDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // Assert
            Assert.AreEqual(1, fetchCount, "Should only fetch once - second call should use L2 cache");
            Assert.AreEqual(result1.Id, result2.Id);
            Assert.IsTrue(distributedCache.ContainsKey("dek:testDek"), "L2 cache should contain the key");
        }

        [TestMethod]
        public async Task DekCache_WithProactiveRefresh_TriggersBackgroundRefresh()
        {
            // Arrange
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();
            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromSeconds(10),
                distributedCache: distributedCache,
                proactiveRefreshThreshold: TimeSpan.FromSeconds(8)); // Refresh when 2 seconds left

            int fetchCount = 0;

            DataEncryptionKeyProperties CreateDekProperties(string id)
            {
                fetchCount++;
                return new DataEncryptionKeyProperties(
                    id,
                    "AEAD_AES_256_CBC_HMAC_SHA256",
                    new byte[] { 1, 2, 3 },
                    new EncryptionKeyWrapMetadata("test", "test", "RSA-OAEP", "test"),
                    DateTime.UtcNow);
            }

            // Act - First fetch
            DataEncryptionKeyProperties result1 = await cache.GetOrAddDekPropertiesAsync(
                "testDek",
                (id, ctx, ct) => Task.FromResult(CreateDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(1, fetchCount, "Should fetch once initially");

            // Wait past the proactive refresh threshold (8 seconds)
            await Task.Delay(TimeSpan.FromSeconds(8.5));

            // Act - Second fetch (should return cached value but trigger background refresh)
            DataEncryptionKeyProperties result2 = await cache.GetOrAddDekPropertiesAsync(
                "testDek",
                (id, ctx, ct) => Task.FromResult(CreateDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // Give background refresh time to complete
            await Task.Delay(TimeSpan.FromSeconds(1));

            // Assert
            Assert.AreEqual(result1.Id, result2.Id, "Should return cached value immediately");
            Assert.AreEqual(2, fetchCount, "Should have triggered background refresh");
        }

        [TestMethod]
        public async Task DekCache_WithDistributedCacheFailure_ContinuesOperation()
        {
            // Arrange - Create a mock that throws on write but not on read
            Mock<IDistributedCache> mockCache = new Mock<IDistributedCache>();
            mockCache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[])null);
            mockCache.Setup(x => x.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Cache unavailable"));

            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: mockCache.Object);

            DataEncryptionKeyProperties CreateDekProperties(string id)
            {
                return new DataEncryptionKeyProperties(
                    id,
                    "AEAD_AES_256_CBC_HMAC_SHA256",
                    new byte[] { 1, 2, 3 },
                    new EncryptionKeyWrapMetadata("test", "test", "RSA-OAEP", "test"),
                    DateTime.UtcNow);
            }

            // Act & Assert - Should not throw even though distributed cache fails
            DataEncryptionKeyProperties result = await cache.GetOrAddDekPropertiesAsync(
                "testDek",
                (id, ctx, ct) => Task.FromResult(CreateDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual("testDek", result.Id);
        }

        /// <summary>
        /// Simple in-memory implementation of IDistributedCache for testing
        /// </summary>
        private class InMemoryDistributedCache : IDistributedCache
        {
            private readonly ConcurrentDictionary<string, CacheEntry> cache = new ConcurrentDictionary<string, CacheEntry>();

            public byte[] Get(string key)
            {
                return this.GetAsync(key).GetAwaiter().GetResult();
            }

            public Task<byte[]> GetAsync(string key, CancellationToken token = default)
            {
                if (this.cache.TryGetValue(key, out CacheEntry entry))
                {
                    if (!entry.AbsoluteExpiration.HasValue || entry.AbsoluteExpiration.Value > DateTimeOffset.UtcNow)
                    {
                        return Task.FromResult(entry.Value);
                    }

                    // Expired
                    this.cache.TryRemove(key, out _);
                }

                return Task.FromResult<byte[]>(null);
            }

            public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
            {
                this.SetAsync(key, value, options).GetAwaiter().GetResult();
            }

            public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
            {
                CacheEntry entry = new CacheEntry
                {
                    Value = value,
                    AbsoluteExpiration = options.AbsoluteExpiration,
                };

                this.cache[key] = entry;
                return Task.CompletedTask;
            }

            public void Remove(string key)
            {
                this.cache.TryRemove(key, out _);
            }

            public Task RemoveAsync(string key, CancellationToken token = default)
            {
                this.Remove(key);
                return Task.CompletedTask;
            }

            public void Refresh(string key)
            {
                // No-op for this simple implementation
            }

            public Task RefreshAsync(string key, CancellationToken token = default)
            {
                return Task.CompletedTask;
            }

            public bool ContainsKey(string key)
            {
                return this.cache.ContainsKey(key);
            }

            private class CacheEntry
            {
                public byte[] Value { get; set; }

                public DateTimeOffset? AbsoluteExpiration { get; set; }
            }
        }
    }
}
