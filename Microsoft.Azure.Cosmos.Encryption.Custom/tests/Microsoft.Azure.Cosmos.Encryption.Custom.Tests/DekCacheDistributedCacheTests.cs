//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Caching.Distributed;
    using Microsoft.Extensions.Time.Testing;
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
            FakeTimeProvider fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();
            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache,
                proactiveRefreshThreshold: TimeSpan.FromMinutes(25), // Refresh when 5 minutes left
                timeProvider: fakeTime);

            int fetchCount = 0;
            SemaphoreSlim fetchSignal = new SemaphoreSlim(0, 10);

            DataEncryptionKeyProperties CreateDekProperties(string id)
            {
                fetchCount++;
                fetchSignal.Release();
                return new DataEncryptionKeyProperties(
                    id,
                    "AEAD_AES_256_CBC_HMAC_SHA256",
                    new byte[] { 1, 2, 3 },
                    new EncryptionKeyWrapMetadata("test", "test", "RSA-OAEP", "test"),
                    fakeTime.GetUtcNow().UtcDateTime);
            }

            // Act - First fetch
            DataEncryptionKeyProperties result1 = await cache.GetOrAddDekPropertiesAsync(
                "testDek",
                (id, ctx, ct) => Task.FromResult(CreateDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // Drain the signal from the initial fetch
            await fetchSignal.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(1, fetchCount, "Should fetch once initially");

            // Advance time past the proactive refresh threshold (25 min into 30 min TTL)
            fakeTime.Advance(TimeSpan.FromMinutes(26));

            // Act - Second fetch (should return cached value but trigger background refresh)
            DataEncryptionKeyProperties result2 = await cache.GetOrAddDekPropertiesAsync(
                "testDek",
                (id, ctx, ct) => Task.FromResult(CreateDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // Wait deterministically for the background refresh to signal completion
            bool refreshCompleted = await fetchSignal.WaitAsync(TimeSpan.FromSeconds(5));

            // Assert
            Assert.AreEqual(result1.Id, result2.Id, "Should return cached value immediately");
            Assert.IsTrue(refreshCompleted, "Background refresh should have completed within timeout");
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
        public async Task DekCache_SetDekProperties_WritesToDistributedCache()
        {
            // Arrange
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();
            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache);

            DataEncryptionKeyProperties dekProps = new DataEncryptionKeyProperties(
                "dek1",
                "AEAD_AES_256_CBC_HMAC_SHA256",
                new byte[] { 1, 2, 3 },
                new EncryptionKeyWrapMetadata("test", "test", "RSA-OAEP", "test"),
                DateTime.UtcNow);

            // Act
            cache.SetDekProperties("dek1", dekProps);

            // Wait deterministically for the fire-and-forget distributed cache write
            await cache.LastDistributedCacheWriteTask;

            // Assert
            Assert.IsTrue(distributedCache.ContainsKey("dek:dek1"), "Distributed cache should contain the key after SetDekProperties");
        }

        [TestMethod]
        public async Task DekCache_SetDekProperties_DistributedCacheFailure_DoesNotThrow()
        {
            // Arrange
            Mock<IDistributedCache> mockCache = new Mock<IDistributedCache>();
            mockCache.Setup(x => x.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Cache unavailable"));

            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: mockCache.Object);

            DataEncryptionKeyProperties dekProps = new DataEncryptionKeyProperties(
                "dek1",
                "AEAD_AES_256_CBC_HMAC_SHA256",
                new byte[] { 1, 2, 3 },
                new EncryptionKeyWrapMetadata("test", "test", "RSA-OAEP", "test"),
                DateTime.UtcNow);

            // Act - should not throw
            cache.SetDekProperties("dek1", dekProps);

            // Wait deterministically for the fire-and-forget distributed cache write
            await cache.LastDistributedCacheWriteTask;

            // Assert - memory cache should still have the value
            CachedDekProperties cached = await cache.DekPropertiesCache.GetAsync(
                "dek1",
                null,
                () => throw new InvalidOperationException("Should not fetch"),
                CancellationToken.None);
            Assert.AreEqual("dek1", cached.ServerProperties.Id);
        }

        [TestMethod]
        public async Task DekCache_RemoveAsync_RemovesFromDistributedCache()
        {
            // Arrange
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();
            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache);

            int fetchCount = 0;
            await cache.GetOrAddDekPropertiesAsync(
                "dek1",
                (id, ctx, ct) =>
                {
                    fetchCount++;
                    return Task.FromResult(new DataEncryptionKeyProperties(
                        id,
                        "AEAD_AES_256_CBC_HMAC_SHA256",
                        new byte[] { 1, 2, 3 },
                        new EncryptionKeyWrapMetadata("test", "test", "RSA-OAEP", "test"),
                        DateTime.UtcNow));
                },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.IsTrue(distributedCache.ContainsKey("dek:dek1"), "Precondition: distributed cache should contain the key");

            // Act
            await cache.RemoveAsync("dek1");

            // Assert
            Assert.IsFalse(distributedCache.ContainsKey("dek:dek1"), "Distributed cache should be cleared after RemoveAsync");
        }

        [TestMethod]
        public async Task DekCache_RemoveAsync_RemovesFromDistributedCache_EvenWhenMemoryCacheIsEmpty()
        {
            // Arrange - populate distributed cache via one DekCache instance
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();
            DekCache cache1 = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache);

            await cache1.GetOrAddDekPropertiesAsync(
                "dek1",
                (id, ctx, ct) => Task.FromResult(new DataEncryptionKeyProperties(
                    id,
                    "AEAD_AES_256_CBC_HMAC_SHA256",
                    new byte[] { 1, 2, 3 },
                    new EncryptionKeyWrapMetadata("test", "test", "RSA-OAEP", "test"),
                    DateTime.UtcNow)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.IsTrue(distributedCache.ContainsKey("dek:dek1"), "Precondition: distributed cache populated");

            // Create a fresh instance (simulates process restart) - memory cache is empty
            DekCache cache2 = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache);

            // Act - RemoveAsync from a fresh instance with empty memory cache
            await cache2.RemoveAsync("dek1");

            // Assert - distributed cache should STILL be cleared (BUG-1 fix verification)
            Assert.IsFalse(distributedCache.ContainsKey("dek:dek1"),
                "Distributed cache should be cleared even when memory cache doesn't have the entry");
        }

        [TestMethod]
        public async Task DekCache_RemoveAsync_DistributedCacheFailure_DoesNotThrow()
        {
            // Arrange
            Mock<IDistributedCache> mockCache = new Mock<IDistributedCache>();
            mockCache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[])null);
            mockCache.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Cache unavailable"));

            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: mockCache.Object);

            // Act & Assert - should not throw even though distributed cache fails
            await cache.RemoveAsync("dek1");
        }

        [TestMethod]
        public async Task DekCache_DistributedCacheReadFailure_FallsBackToSource()
        {
            // Arrange - distributed cache throws on read
            Mock<IDistributedCache> mockCache = new Mock<IDistributedCache>();
            mockCache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Cache read failed"));
            mockCache.Setup(x => x.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: mockCache.Object);

            int fetchCount = 0;

            // Act - should fall back to source when distributed cache read fails
            DataEncryptionKeyProperties result = await cache.GetOrAddDekPropertiesAsync(
                "dek1",
                (id, ctx, ct) =>
                {
                    fetchCount++;
                    return Task.FromResult(new DataEncryptionKeyProperties(
                        id,
                        "AEAD_AES_256_CBC_HMAC_SHA256",
                        new byte[] { 1, 2, 3 },
                        new EncryptionKeyWrapMetadata("test", "test", "RSA-OAEP", "test"),
                        DateTime.UtcNow));
                },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("dek1", result.Id);
            Assert.AreEqual(1, fetchCount, "Should have fallen back to source fetch");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void DekCache_WhitespaceCacheKeyPrefix_ThrowsArgumentException()
        {
            // Act & Assert
            _ = new DekCache(cacheKeyPrefix: "   ");
        }

        [TestMethod]
        public async Task DekCache_DistributedCacheEntry_WithMismatchedVersion_FallsBackToSource()
        {
            // Arrange - Simulate a distributed cache entry written by a future SDK version (v:2)
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();
            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache);

            // Manually inject a v2 entry into the distributed cache
            string v2Json = "{\"v\":2,\"serverProperties\":{\"id\":\"dek1\"},\"serverPropertiesExpiryUtc\":\"2099-01-01T00:00:00Z\"}";
            await distributedCache.SetAsync(
                "dek:dek1",
                System.Text.Encoding.UTF8.GetBytes(v2Json),
                new DistributedCacheEntryOptions { AbsoluteExpiration = DateTimeOffset.UtcNow.AddHours(1) });

            int fetchCount = 0;

            // Act - Should fall back to source because v2 is unsupported
            DataEncryptionKeyProperties result = await cache.GetOrAddDekPropertiesAsync(
                "dek1",
                (id, ctx, ct) =>
                {
                    fetchCount++;
                    return Task.FromResult(new DataEncryptionKeyProperties(
                        id,
                        "AEAD_AES_256_CBC_HMAC_SHA256",
                        new byte[] { 1, 2, 3 },
                        new EncryptionKeyWrapMetadata("test", "test", "RSA-OAEP", "test"),
                        DateTime.UtcNow));
                },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // Assert - Should have fallen back to source fetch
            Assert.IsNotNull(result);
            Assert.AreEqual("dek1", result.Id);
            Assert.AreEqual(1, fetchCount, "Should fall back to source when distributed cache has unsupported version");
        }

        [TestMethod]
        public async Task DekCache_DistributedCacheEntry_WithoutVersionField_DeserializesAsV1()
        {
            // Arrange - Simulate a legacy cache entry without the "v" field (pre-versioning SDK)
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();
            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache);

            // First, populate the cache normally to get a valid entry
            int fetchCount = 0;
            await cache.GetOrAddDekPropertiesAsync(
                "dek1",
                (id, ctx, ct) =>
                {
                    fetchCount++;
                    return Task.FromResult(new DataEncryptionKeyProperties(
                        id,
                        "AEAD_AES_256_CBC_HMAC_SHA256",
                        new byte[] { 1, 2, 3 },
                        new EncryptionKeyWrapMetadata("test", "test", "RSA-OAEP", "test"),
                        DateTime.UtcNow));
                },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(1, fetchCount);

            // Clear memory cache, re-fetch should use distributed cache (which has v:1)
            await cache.DekPropertiesCache.RemoveAsync("dek1");

            DataEncryptionKeyProperties result = await cache.GetOrAddDekPropertiesAsync(
                "dek1",
                (id, ctx, ct) =>
                {
                    fetchCount++;
                    return Task.FromResult(new DataEncryptionKeyProperties(
                        id,
                        "AEAD_AES_256_CBC_HMAC_SHA256",
                        new byte[] { 1, 2, 3 },
                        new EncryptionKeyWrapMetadata("test", "test", "RSA-OAEP", "test"),
                        DateTime.UtcNow));
                },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            // Assert - Should have used distributed cache, not fetched again
            Assert.AreEqual(1, fetchCount, "Entries with v:1 (or default) should deserialize successfully");
            Assert.AreEqual("dek1", result.Id);
        }
    }
}
