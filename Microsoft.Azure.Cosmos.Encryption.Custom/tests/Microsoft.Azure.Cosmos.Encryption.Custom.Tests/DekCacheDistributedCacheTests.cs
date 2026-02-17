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
        public async Task DekCache_WithoutDistributedCache_UsesOnlyMemoryCache()
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

            // Act - Second fetch (should use memory cache)
            DataEncryptionKeyProperties result2 = await cache.GetOrAddDekPropertiesAsync(
                "testDek",
                (id, ctx, ct) => Task.FromResult(CreateDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // Assert
            Assert.AreEqual(1, fetchCount, "Should only fetch once - second call should use memory cache");
            Assert.AreEqual(result1.Id, result2.Id);
        }

        [TestMethod]
        public async Task DekCache_WithDistributedCache_UsesDistributedCache()
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

            // Act - First fetch (will populate both memory cache and distributed cache)
            DataEncryptionKeyProperties result1 = await cache.GetOrAddDekPropertiesAsync(
                "testDek",
                (id, ctx, ct) => Task.FromResult(CreateDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // Clear only memory cache to test distributed cache
            // Use internal property to clear memory cache without affecting distributed cache
            await cache.DekPropertiesCache.RemoveAsync("testDek");

            // Act - Second fetch (should use distributed cache)
            DataEncryptionKeyProperties result2 = await cache.GetOrAddDekPropertiesAsync(
                "testDek",
                (id, ctx, ct) => Task.FromResult(CreateDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // Assert
            Assert.AreEqual(1, fetchCount, "Should only fetch once - second call should use distributed cache");
            Assert.AreEqual(result1.Id, result2.Id);
            Assert.IsTrue(distributedCache.ContainsKey("dek:testDek"), "Distributed cache should contain the key");
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

        [TestMethod]
        public void DekCache_ProactiveRefreshThreshold_NegativeValue_ThrowsArgumentOutOfRangeException()
        {
            // Act & Assert
            ArgumentOutOfRangeException ex = Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            {
                DekCache cache = new DekCache(
                    dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                    distributedCache: null,
                    proactiveRefreshThreshold: TimeSpan.FromMinutes(-5));
            });

            Assert.AreEqual("proactiveRefreshThreshold", ex.ParamName);
        }

        [TestMethod]
        public void DekCache_ProactiveRefreshThreshold_EqualToTTL_ThrowsArgumentException()
        {
            // Act & Assert
            ArgumentOutOfRangeException ex = Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            {
                DekCache cache = new DekCache(
                    dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                    distributedCache: null,
                    proactiveRefreshThreshold: TimeSpan.FromMinutes(30));
            });

            Assert.AreEqual("proactiveRefreshThreshold", ex.ParamName);
        }

        [TestMethod]
        public void DekCache_ProactiveRefreshThreshold_GreaterThanTTL_ThrowsArgumentException()
        {
            // Act & Assert
            ArgumentOutOfRangeException ex = Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            {
                DekCache cache = new DekCache(
                    dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                    distributedCache: null,
                    proactiveRefreshThreshold: TimeSpan.FromMinutes(60));
            });

            Assert.AreEqual("proactiveRefreshThreshold", ex.ParamName);
        }

        [TestMethod]
        public void DekCache_ProactiveRefreshThreshold_ValidValue_DoesNotThrow()
        {
            // Act & Assert - Should not throw
            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: null,
                proactiveRefreshThreshold: TimeSpan.FromMinutes(25));

            Assert.IsNotNull(cache);
        }

        [TestMethod]
        public void DekCache_ProactiveRefreshThreshold_Null_DoesNotThrow()
        {
            // Act & Assert - Should not throw
            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: null,
                proactiveRefreshThreshold: null);

            Assert.IsNotNull(cache);
        }

        [TestMethod]
        public void DekCache_ProactiveRefreshThreshold_WithDefaultTTL_ValidatesCorrectly()
        {
            // Default TTL is 120 minutes, so 119 minutes should be valid, 120+ should throw

            // Valid case
            DekCache validCache = new DekCache(
                dekPropertiesTimeToLive: null, // Uses default 120 minutes
                distributedCache: null,
                proactiveRefreshThreshold: TimeSpan.FromMinutes(119));

            Assert.IsNotNull(validCache);

            // Invalid case - equal to default TTL
            ArgumentOutOfRangeException ex = Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            {
                DekCache cache = new DekCache(
                    dekPropertiesTimeToLive: null, // Uses default 120 minutes
                    distributedCache: null,
                    proactiveRefreshThreshold: TimeSpan.FromMinutes(120));
            });

            Assert.AreEqual("proactiveRefreshThreshold", ex.ParamName);
        }

        [TestMethod]
        public async Task DekCache_DefaultCacheKeyPrefix_UsesDekPrefix()
        {
            // Arrange
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();
            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache);

            static DataEncryptionKeyProperties CreateDekProperties(string id)
            {
                return new DataEncryptionKeyProperties(
                    id,
                    "AEAD_AES_256_CBC_HMAC_SHA256",
                    new byte[] { 1, 2, 3 },
                    new EncryptionKeyWrapMetadata("test", "test", "RSA-OAEP", "test"),
                    DateTime.UtcNow);
            }

            // Act
            await cache.GetOrAddDekPropertiesAsync(
                "testDek",
                (id, ctx, ct) => Task.FromResult(CreateDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // Assert
            Assert.IsTrue(distributedCache.ContainsKey("dek:testDek"), "Should use default 'dek' prefix");
        }

        [TestMethod]
        public async Task DekCache_CustomCacheKeyPrefix_UsesCustomPrefix()
        {
            // Arrange
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();
            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache,
                cacheKeyPrefix: "tenant1-dek");

            static DataEncryptionKeyProperties CreateDekProperties(string id)
            {
                return new DataEncryptionKeyProperties(
                    id,
                    "AEAD_AES_256_CBC_HMAC_SHA256",
                    new byte[] { 1, 2, 3 },
                    new EncryptionKeyWrapMetadata("test", "test", "RSA-OAEP", "test"),
                    DateTime.UtcNow);
            }

            // Act
            await cache.GetOrAddDekPropertiesAsync(
                "testDek",
                (id, ctx, ct) => Task.FromResult(CreateDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // Assert
            Assert.IsTrue(distributedCache.ContainsKey("tenant1-dek:testDek"), "Should use custom 'tenant1-dek' prefix");
            Assert.IsFalse(distributedCache.ContainsKey("dek:testDek"), "Should NOT use default 'dek' prefix");
        }

        [TestMethod]
        public async Task DekCache_MultiTenantScenario_IsolatesCacheEntriesByPrefix()
        {
            // Arrange - Shared distributed cache with two tenants
            InMemoryDistributedCache sharedDistributedCache = new InMemoryDistributedCache();

            DekCache tenant1Cache = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: sharedDistributedCache,
                cacheKeyPrefix: "tenant1");

            DekCache tenant2Cache = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: sharedDistributedCache,
                cacheKeyPrefix: "tenant2");

            int tenant1FetchCount = 0;
            int tenant2FetchCount = 0;

            DataEncryptionKeyProperties CreateTenant1DekProperties(string id)
            {
                tenant1FetchCount++;
                return new DataEncryptionKeyProperties(
                    "tenant1-" + id, // Prefix DEK ID with tenant
                    "AEAD_AES_256_CBC_HMAC_SHA256",
                    new byte[] { 1, 1, 1 }, // Tenant 1 key
                    new EncryptionKeyWrapMetadata("test", "test", "RSA-OAEP", "tenant1-kek"),
                    DateTime.UtcNow);
            }

            DataEncryptionKeyProperties CreateTenant2DekProperties(string id)
            {
                tenant2FetchCount++;
                return new DataEncryptionKeyProperties(
                    "tenant2-" + id, // Prefix DEK ID with tenant
                    "AEAD_AES_256_CBC_HMAC_SHA256",
                    new byte[] { 2, 2, 2 }, // Tenant 2 key (different)
                    new EncryptionKeyWrapMetadata("test", "test", "RSA-OAEP", "tenant2-kek"),
                    DateTime.UtcNow);
            }

            // Act - Both tenants use same DEK ID "shared-dek-id"
            DataEncryptionKeyProperties tenant1Result = await tenant1Cache.GetOrAddDekPropertiesAsync(
                "shared-dek-id",
                (id, ctx, ct) => Task.FromResult(CreateTenant1DekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            DataEncryptionKeyProperties tenant2Result = await tenant2Cache.GetOrAddDekPropertiesAsync(
                "shared-dek-id",
                (id, ctx, ct) => Task.FromResult(CreateTenant2DekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // Assert - Both entries should exist with different prefixes
            Assert.IsTrue(sharedDistributedCache.ContainsKey("tenant1:shared-dek-id"), "Tenant 1 entry should exist");
            Assert.IsTrue(sharedDistributedCache.ContainsKey("tenant2:shared-dek-id"), "Tenant 2 entry should exist");
            Assert.AreEqual(1, tenant1FetchCount, "Tenant 1 should fetch once");
            Assert.AreEqual(1, tenant2FetchCount, "Tenant 2 should fetch once");

            // Verify different DEK IDs showing proper isolation
            Assert.AreEqual("tenant1-shared-dek-id", tenant1Result.Id);
            Assert.AreEqual("tenant2-shared-dek-id", tenant2Result.Id);

            // Verify different KEK names
            Assert.AreEqual("tenant1-kek", tenant1Result.EncryptionKeyWrapMetadata.Name);
            Assert.AreEqual("tenant2-kek", tenant2Result.EncryptionKeyWrapMetadata.Name);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void DekCache_NullCacheKeyPrefix_ThrowsArgumentException()
        {
            // Act & Assert
            _ = new DekCache(cacheKeyPrefix: null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void DekCache_EmptyCacheKeyPrefix_ThrowsArgumentException()
        {
            // Act & Assert
            _ = new DekCache(cacheKeyPrefix: string.Empty);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void DekCache_WhitespaceCacheKeyPrefix_ThrowsArgumentException()
        {
            // Act & Assert
            _ = new DekCache(cacheKeyPrefix: "   ");
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
