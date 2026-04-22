//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System;
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
        public async Task DekCache_WithDistributedCache_SurvivesMemoryCacheRestart()
        {
            // REQ: When the in-process memory cache is discarded (process restart / cold start)
            //      but a peer-populated entry is present in the distributed cache, the next
            //      lookup must serve from the distributed cache and avoid a source fetch.
            // SOURCE: SOURCE-PR-INTENT (cross-process/cross-instance caching) and DekCache.cs:260-272.
            //
            // This exercises the COLD-MISS path (empty L1 dictionary). The parallel scenario of
            // "L1 has an expired entry" is a different code path and is covered by
            // DekCacheResilienceTests.ExpiredL1_L2HasFreshEntry_CosmosFails_ServesFromL2.
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();
            DekCache peerA = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache,
                cacheKeyPrefix: "test-dek");

            int peerAFetchCount = 0;

            DataEncryptionKeyProperties CreateDekProperties(string id)
            {
                peerAFetchCount++;
                return new DataEncryptionKeyProperties(
                    id,
                    "AEAD_AES_256_CBC_HMAC_SHA256",
                    new byte[] { 1, 2, 3 },
                    new EncryptionKeyWrapMetadata("test", "test", "RSA-OAEP", "test"),
                    DateTime.UtcNow);
            }

            // Peer A populates L2 through a normal cold-miss fetch.
            DataEncryptionKeyProperties peerAResult = await peerA.GetOrAddDekPropertiesAsync(
                "testDek",
                (id, ctx, ct) => Task.FromResult(CreateDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);
            Assert.AreEqual(1, peerAFetchCount);
            await peerA.LastDistributedCacheWriteTask;

            // Peer B is a brand-new DekCache instance sharing the same L2 (simulates a
            // different process / a process that just restarted and has an empty L1).
            DekCache peerB = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache,
                cacheKeyPrefix: "test-dek");

            int peerBFetchCount = 0;
            DataEncryptionKeyProperties peerBResult = await peerB.GetOrAddDekPropertiesAsync(
                "testDek",
                (id, ctx, ct) =>
                {
                    peerBFetchCount++;
                    return Task.FromResult(CreateDekProperties(id));
                },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(0, peerBFetchCount, "Peer B must read L2 on cold L1, not invoke its fetcher.");
            Assert.AreEqual(peerAResult.Id, peerBResult.Id);
        }

        [TestMethod]
        public async Task DekCache_WithProactiveRefresh_TriggersBackgroundRefresh()
        {
            // Arrange
            DateTime fakeNow = DateTime.UtcNow;
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();
            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache,
                refreshBeforeExpiry: TimeSpan.FromMinutes(25), // Refresh when 5 minutes left
                utcNow: () => fakeNow,
                cacheKeyPrefix: "test-dek");

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
                    fakeNow);
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
            fakeNow = fakeNow.AddMinutes(26);

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
                distributedCache: mockCache.Object,
                cacheKeyPrefix: "test-dek");

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
                    refreshBeforeExpiry: TimeSpan.FromMinutes(-5));
            });

            Assert.AreEqual("refreshBeforeExpiry", ex.ParamName);
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
                    refreshBeforeExpiry: TimeSpan.FromMinutes(30));
            });

            Assert.AreEqual("refreshBeforeExpiry", ex.ParamName);
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
                    refreshBeforeExpiry: TimeSpan.FromMinutes(60));
            });

            Assert.AreEqual("refreshBeforeExpiry", ex.ParamName);
        }

        [TestMethod]
        public void DekCache_ProactiveRefreshThreshold_ValidValue_DoesNotThrow()
        {
            // Act & Assert - Should not throw
            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: null,
                refreshBeforeExpiry: TimeSpan.FromMinutes(25));

            Assert.IsNotNull(cache);
        }

        [TestMethod]
        public void DekCache_ProactiveRefreshThreshold_Null_DoesNotThrow()
        {
            // Act & Assert - Should not throw
            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: null,
                refreshBeforeExpiry: null);

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
                refreshBeforeExpiry: TimeSpan.FromMinutes(119));

            Assert.IsNotNull(validCache);

            // Invalid case - equal to default TTL
            ArgumentOutOfRangeException ex = Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
            {
                DekCache cache = new DekCache(
                    dekPropertiesTimeToLive: null, // Uses default 120 minutes
                    distributedCache: null,
                    refreshBeforeExpiry: TimeSpan.FromMinutes(120));
            });

            Assert.AreEqual("refreshBeforeExpiry", ex.ParamName);
        }

        // NOTE: The former DekCache_DefaultCacheKeyPrefix_UsesDekPrefix test has been deleted.
        // It hard-coded the literal "dek:testDek" cache-key shape, which codifies the current
        // collision-prone default (M6). The requirement — that two providers constructed with
        // defaults do not read each other's entries — is covered by
        // DekCacheResilienceTests.TwoProvidersWithDefaults_DoNotCollideOnSameDekId.

        [TestMethod]
        public async Task DekCache_CustomCacheKeyPrefix_KeysAreScopedToPrefix()
        {
            // REQ: A custom prefix must scope the L2 keyspace — another cache with a different
            //      (default) prefix must NOT see entries written by this cache for the same dekId.
            // SOURCE: CosmosDataEncryptionKeyProvider XML doc on distributedCacheKeyPrefix
            //         ("avoid collisions when multiple providers share the same cache instance").
            // NOTE: This asserts behavioural isolation, not the literal cache-key shape, so it
            //       continues to hold if the cache-key format evolves (e.g., container-scoping).
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();
            DekCache customPrefixCache = new DekCache(
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

            await customPrefixCache.GetOrAddDekPropertiesAsync(
                "testDek",
                (id, ctx, ct) => Task.FromResult(CreateDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);
            await customPrefixCache.LastDistributedCacheWriteTask;

            // A cache sharing the same L2 but using the DEFAULT prefix must not see the entry.
            DekCache defaultPrefixCache = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache,
                cacheKeyPrefix: "test-dek");

            int defaultFetchCount = 0;
            await defaultPrefixCache.GetOrAddDekPropertiesAsync(
                "testDek",
                (id, ctx, ct) =>
                {
                    defaultFetchCount++;
                    return Task.FromResult(CreateDekProperties(id));
                },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(
                1,
                defaultFetchCount,
                "A cache with the default prefix must not see entries written under a custom prefix.");
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

            // REQ: Distinct explicit prefixes must isolate entries — each tenant's read path
            //      invokes its own fetcher and produces its own result. This test asserts
            //      behavioural isolation (no cross-pollination of keys / KEK metadata),
            //      not the literal cache-key shape.
            // SOURCE: CosmosDataEncryptionKeyProvider XML doc on distributedCacheKeyPrefix
            //         ("avoid collisions when multiple providers share the same cache instance").
            Assert.AreEqual(1, tenant1FetchCount, "Tenant 1 should fetch once");
            Assert.AreEqual(1, tenant2FetchCount, "Tenant 2 should fetch once");

            // Each tenant received its own DEK — no cross-pollination.
            Assert.AreEqual("tenant1-shared-dek-id", tenant1Result.Id);
            Assert.AreEqual("tenant2-shared-dek-id", tenant2Result.Id);
            Assert.AreEqual("tenant1-kek", tenant1Result.EncryptionKeyWrapMetadata.Name);
            Assert.AreEqual("tenant2-kek", tenant2Result.EncryptionKeyWrapMetadata.Name);

            // A third fresh provider with the same "tenant1" prefix must see tenant1's entry,
            // proving the keyspace is genuinely partitioned per prefix.
            DekCache tenant1Peer = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: sharedDistributedCache,
                cacheKeyPrefix: "tenant1");
            int peerFetchCount = 0;
            DataEncryptionKeyProperties peerResult = await tenant1Peer.GetOrAddDekPropertiesAsync(
                "shared-dek-id",
                (id, ctx, ct) =>
                {
                    peerFetchCount++;
                    return Task.FromResult(CreateTenant1DekProperties(id));
                },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);
            Assert.AreEqual(0, peerFetchCount, "A peer sharing tenant1's prefix must read tenant1's entry from L2.");
            Assert.AreEqual("tenant1-kek", peerResult.EncryptionKeyWrapMetadata.Name);
        }

        [TestMethod]
        public void DekCache_NullCacheKeyPrefix_WithoutDistributedCache_IsAccepted()
        {
            // A null prefix is meaningless when no distributed cache is configured; the ctor
            // accepts it without error. The prefix is only REQUIRED when a distributed cache
            // is provided (see DekCache_NullCacheKeyPrefix_WithDistributedCache_Throws).
            _ = new DekCache(cacheKeyPrefix: null);
        }

        [TestMethod]
        public void DekCache_NullCacheKeyPrefix_WithDistributedCache_Throws()
        {
            // REQ: When the distributed cache is configured, a cacheKeyPrefix is REQUIRED so
            //      that multiple providers sharing one cache cannot silently collide on
            //      identical DEK ids. Null is rejected via ArgumentNullException; whitespace is
            //      rejected via ArgumentException. We assert the base ArgumentException to
            //      tolerate either derived type while still pinning the parameter name.
            ArgumentException ex = Assert.ThrowsException<ArgumentNullException>(
                () => new DekCache(
                    distributedCache: new InMemoryDistributedCache(),
                    cacheKeyPrefix: null));
            Assert.AreEqual("cacheKeyPrefix", ex.ParamName);
        }

        [TestMethod]
        public void DekCache_EmptyCacheKeyPrefix_Throws()
        {
            // Empty / whitespace is rejected regardless of distributed-cache presence so the
            // argument shape remains predictable.
            Assert.ThrowsException<ArgumentException>(() => new DekCache(cacheKeyPrefix: string.Empty));
        }

        [TestMethod]
        public async Task DekCache_SetDekProperties_WritesToDistributedCache()
        {
            // Arrange
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();
            DekCache cache = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache,
                cacheKeyPrefix: "test-dek");

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
            Assert.IsTrue(distributedCache.ContainsKey("test-dek:v1:dek1"), "Distributed cache should contain the key after SetDekProperties");
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
                distributedCache: mockCache.Object,
                cacheKeyPrefix: "test-dek");

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
                distributedCache: distributedCache,
                cacheKeyPrefix: "test-dek");

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

            Assert.IsTrue(distributedCache.ContainsKey("test-dek:v1:dek1"), "Precondition: distributed cache should contain the key");

            // Act
            await cache.RemoveAsync("dek1");

            // Assert
            Assert.IsFalse(distributedCache.ContainsKey("test-dek:v1:dek1"), "Distributed cache should be cleared after RemoveAsync");
        }

        [TestMethod]
        public async Task DekCache_RemoveAsync_RemovesFromDistributedCache_EvenWhenMemoryCacheIsEmpty()
        {
            // Arrange - populate distributed cache via one DekCache instance
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();
            DekCache cache1 = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache,
                cacheKeyPrefix: "test-dek");

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

            Assert.IsTrue(distributedCache.ContainsKey("test-dek:v1:dek1"), "Precondition: distributed cache populated");

            // Create a fresh instance (simulates process restart) - memory cache is empty
            DekCache cache2 = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache,
                cacheKeyPrefix: "test-dek");

            // Act - RemoveAsync from a fresh instance with empty memory cache
            await cache2.RemoveAsync("dek1");

            // Assert - distributed cache should STILL be cleared (BUG-1 fix verification)
            Assert.IsFalse(distributedCache.ContainsKey("test-dek:v1:dek1"),
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
                distributedCache: mockCache.Object,
                cacheKeyPrefix: "test-dek");

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
                distributedCache: mockCache.Object,
                cacheKeyPrefix: "test-dek");

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
        public void DekCache_WhitespaceCacheKeyPrefix_Throws()
        {
            // A whitespace-only prefix is treated as not provided and rejected; this prevents
            // a caller from believing they supplied a discriminator when they effectively did not.
            Assert.ThrowsException<ArgumentException>(() => new DekCache(cacheKeyPrefix: "   "));
        }

        // NOTE: DekCache_DistributedCacheEntry_WithMismatchedVersion_FallsBackToSource has been
        // deleted. It asserted that a v1 SDK reading a v2 entry falls back to source, but did NOT
        // assert that the v2 entry survives untouched in L2. The current implementation silently
        // overwrites the v2 entry with a v1 write after the fallback fetch (M4 downgrade).
        // The required invariant is covered by
        // DekCacheResilienceTests.L2ContainsFutureVersion_DoesNotOverwriteOnFallbackFetch.
        //
        // NOTE: DekCache_DistributedCacheEntry_WithoutVersionField_DeserializesAsV1 has been
        // deleted. Its setup populated L2 through the normal write path (which always emits
        // "v":1) and never actually injected a payload missing the "v" field. The requirement
        // — that a payload without "v" is treated as v1 — is verified by
        // DekCacheInteropTests.L2PayloadMissingVersionField_IsTreatedAsV1_AndServed, which
        // actually writes a JSON blob without the "v" key.
    }
}
