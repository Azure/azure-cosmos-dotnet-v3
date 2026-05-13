//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Tests;
    using Microsoft.Extensions.Caching.Distributed;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Tests for the public <see cref="CosmosDataEncryptionKeyProvider"/> constructor surface:
    /// the three legacy ctors (WrapProvider, StoreProvider, dual) plus the canonical
    /// <see cref="DekCacheOptions"/> overload.
    /// </summary>
    [TestClass]
    public class CosmosDataEncryptionKeyProviderConstructorTests
    {
        #region EncryptionKeyWrapProvider-only constructor

#pragma warning disable CS0618 // EncryptionKeyWrapProvider ctor is itself obsolete; we still test it for back-compat.

        [TestMethod]
        public void WrapProviderOnly_ValidArgs_SetsProperties()
        {
            TestKeyWrapProvider wrapProvider = new TestKeyWrapProvider();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                wrapProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30));

            Assert.AreSame(wrapProvider, provider.EncryptionKeyWrapProvider);
            Assert.IsNull(provider.EncryptionKeyStoreProvider);
            Assert.IsNotNull(provider.DekCache);
            Assert.IsNotNull(provider.DataEncryptionKeyContainer);
        }

        [TestMethod]
        public void WrapProviderOnly_NullProvider_ThrowsArgumentNullException()
        {
            ArgumentNullException ex = Assert.ThrowsException<ArgumentNullException>(() =>
                new CosmosDataEncryptionKeyProvider(
                    encryptionKeyWrapProvider: (EncryptionKeyWrapProvider)null));

            Assert.AreEqual("encryptionKeyWrapProvider", ex.ParamName);
        }

        [TestMethod]
        public void WrapProviderOnly_DefaultTTL_CreatesSuccessfully()
        {
            TestKeyWrapProvider wrapProvider = new TestKeyWrapProvider();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(wrapProvider);

            Assert.IsNotNull(provider.DekCache);
        }

#pragma warning restore CS0618

        #endregion

        #region EncryptionKeyStoreProvider-only constructor

        [TestMethod]
        public void StoreProviderOnly_ValidArgs_SetsProperties()
        {
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                storeProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30));

            Assert.AreSame(storeProvider, provider.EncryptionKeyStoreProvider);
            Assert.IsNotNull(provider.MdeKeyWrapProvider);
            Assert.IsNotNull(provider.DekCache);
        }

        [TestMethod]
        public void StoreProviderOnly_NullProvider_ThrowsArgumentNullException()
        {
            ArgumentNullException ex = Assert.ThrowsException<ArgumentNullException>(() =>
                new CosmosDataEncryptionKeyProvider(encryptionKeyStoreProvider: null));

            Assert.AreEqual("encryptionKeyStoreProvider", ex.ParamName);
        }

        [TestMethod]
        public void StoreProviderOnly_DefaultTTL_CreatesSuccessfully()
        {
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(storeProvider);

            Assert.IsNotNull(provider.DekCache);
        }

        #endregion

        #region Dual-provider (EncryptionKeyWrapProvider + EncryptionKeyStoreProvider) constructor

#pragma warning disable CS0618 // Dual-provider ctor is itself obsolete; we still test it for back-compat.

        [TestMethod]
        public void DualProvider_ValidArgs_SetsBothProviders()
        {
            TestKeyWrapProvider wrapProvider = new TestKeyWrapProvider();
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                wrapProvider,
                storeProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30));

            Assert.AreSame(wrapProvider, provider.EncryptionKeyWrapProvider);
            Assert.AreSame(storeProvider, provider.EncryptionKeyStoreProvider);
            Assert.IsNotNull(provider.MdeKeyWrapProvider);
            Assert.IsNotNull(provider.DekCache);
        }

        [TestMethod]
        public void DualProvider_NullWrapProvider_ThrowsArgumentNullException()
        {
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            ArgumentNullException ex = Assert.ThrowsException<ArgumentNullException>(() =>
                new CosmosDataEncryptionKeyProvider(
                    encryptionKeyWrapProvider: (EncryptionKeyWrapProvider)null,
                    encryptionKeyStoreProvider: storeProvider));

            Assert.AreEqual("encryptionKeyWrapProvider", ex.ParamName);
        }

        [TestMethod]
        public void DualProvider_NullStoreProvider_ThrowsArgumentNullException()
        {
            TestKeyWrapProvider wrapProvider = new TestKeyWrapProvider();

            ArgumentNullException ex = Assert.ThrowsException<ArgumentNullException>(() =>
                new CosmosDataEncryptionKeyProvider(
                    encryptionKeyWrapProvider: wrapProvider,
                    encryptionKeyStoreProvider: null));

            Assert.AreEqual("encryptionKeyStoreProvider", ex.ParamName);
        }

        [TestMethod]
        public void DualProvider_DefaultTTL_CreatesSuccessfully()
        {
            TestKeyWrapProvider wrapProvider = new TestKeyWrapProvider();
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                wrapProvider,
                storeProvider);

            Assert.IsNotNull(provider.DekCache);
        }

#pragma warning restore CS0618

        #endregion

        #region DekCacheOptions overload (canonical distributed-cache configuration)

        [TestMethod]
        public void StoreProviderWithOptions_NullOptions_UsesDefaults()
        {
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                storeProvider,
                dekCacheOptions: null);

            Assert.AreSame(storeProvider, provider.EncryptionKeyStoreProvider);
            Assert.IsNotNull(provider.DekCache);
        }

        [TestMethod]
        public void StoreProviderWithOptions_FullOptions_WiresEverythingThrough()
        {
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                storeProvider,
                new DekCacheOptions
                {
                    DekPropertiesTimeToLive = TimeSpan.FromMinutes(30),
                    DistributedCache = distributedCache,
                    RefreshBeforeExpiry = TimeSpan.FromMinutes(5),
                    DistributedCacheKeyPrefix = "options-bag-test",
                    DistributedCacheEntryLifetime = TimeSpan.FromMinutes(120),
                });

            Assert.AreSame(storeProvider, provider.EncryptionKeyStoreProvider);
            Assert.IsNotNull(provider.DekCache);
        }

        [TestMethod]
        public void StoreProviderWithOptions_NullProvider_ThrowsArgumentNullException()
        {
            ArgumentNullException ex = Assert.ThrowsException<ArgumentNullException>(() =>
                new CosmosDataEncryptionKeyProvider(
                    encryptionKeyStoreProvider: null,
                    dekCacheOptions: new DekCacheOptions()));

            Assert.AreEqual("encryptionKeyStoreProvider", ex.ParamName);
        }

        [TestMethod]
        public void StoreProviderWithOptions_DistributedCacheWithoutPrefix_PropagatesValidationFromDekCache()
        {
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();

            // Validation lives in DekCache: distributedCache without a prefix is rejected so
            // multiple providers sharing one cache cannot silently collide.
            ArgumentException ex = Assert.ThrowsException<ArgumentNullException>(() =>
                new CosmosDataEncryptionKeyProvider(
                    storeProvider,
                    new DekCacheOptions
                    {
                        DistributedCache = distributedCache,
                        DistributedCacheKeyPrefix = null,
                    }));

            Assert.AreEqual("cacheKeyPrefix", ex.ParamName);
        }

        [TestMethod]
        public void StoreProviderWithOptions_NoDistributedCache_AcceptsNullPrefix()
        {
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            // Without a distributed cache the prefix is ignored; passing null must be accepted.
            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                storeProvider,
                new DekCacheOptions
                {
                    DistributedCache = null,
                    DistributedCacheKeyPrefix = null,
                });

            Assert.IsNotNull(provider.DekCache);
        }

        [TestMethod]
        public void StoreProviderWithOptions_NoDistributedCache_RejectsNonNullPrefix()
        {
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            // Supplying a prefix without a distributed cache is misconfiguration: the prefix
            // would be dead state. DekCache rejects with a clear error.
            ArgumentException ex = Assert.ThrowsException<ArgumentException>(() =>
                new CosmosDataEncryptionKeyProvider(
                    storeProvider,
                    new DekCacheOptions
                    {
                        DistributedCache = null,
                        DistributedCacheKeyPrefix = "tenant",
                    }));

            Assert.AreEqual("cacheKeyPrefix", ex.ParamName);
        }

        #endregion

        #region DekCache integration — distributed cache is actually wired through

        [TestMethod]
        public async Task StoreProvider_DistributedCacheIsWiredToDekCache()
        {
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                storeProvider,
                new DekCacheOptions
                {
                    DekPropertiesTimeToLive = TimeSpan.FromMinutes(30),
                    DistributedCache = distributedCache,
                    DistributedCacheKeyPrefix = "test-dek",
                });

            int fetchCount = 0;
            DataEncryptionKeyProperties props = await provider.DekCache.GetOrAddDekPropertiesAsync(
                "dek1",
                (id, ctx, ct) =>
                {
                    fetchCount++;
                    return Task.FromResult(CreateDekProperties(id));
                },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(1, fetchCount);
            await provider.DekCache.LastDistributedCacheWriteTask;

            // A peer DekCache sharing the same L2 (simulating a second process / cold L1) must
            // read the populated entry without invoking its fetcher.
            DekCache peerCache = new DekCache(
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache,
                cacheKeyPrefix: "test-dek");
            int peerFetchCount = 0;
            DataEncryptionKeyProperties peerProps = await peerCache.GetOrAddDekPropertiesAsync(
                "dek1",
                (id, ctx, ct) =>
                {
                    peerFetchCount++;
                    return Task.FromResult(CreateDekProperties(id));
                },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(0, peerFetchCount, "Peer cache must read L2 without invoking its fetcher.");
            Assert.AreEqual(props.Id, peerProps.Id);
        }

        [TestMethod]
        public async Task StoreProvider_DistributedCacheFailure_ContinuesOperation()
        {
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            Mock<IDistributedCache> mockCache = new Mock<IDistributedCache>();
            mockCache.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[])null);
            mockCache.Setup(x => x.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<DistributedCacheEntryOptions>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Cache unavailable"));

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                storeProvider,
                new DekCacheOptions
                {
                    DekPropertiesTimeToLive = TimeSpan.FromMinutes(30),
                    DistributedCache = mockCache.Object,
                    DistributedCacheKeyPrefix = "test-dek",
                });

            // Should not throw even though distributed cache fails.
            DataEncryptionKeyProperties result = await provider.DekCache.GetOrAddDekPropertiesAsync(
                "dek1",
                (id, ctx, ct) => Task.FromResult(CreateDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual("dek1", result.Id);
        }

#pragma warning disable CS0618 // Wrap-provider ctor is obsolete; we still smoke-test memory-cache behaviour for back-compat.

        [TestMethod]
        public async Task WrapProvider_WithoutDistributedCache_MemoryCacheHonored()
        {
            TestKeyWrapProvider wrapProvider = new TestKeyWrapProvider();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                wrapProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30));

            int fetchCount = 0;
            await provider.DekCache.GetOrAddDekPropertiesAsync(
                "dek1",
                (id, ctx, ct) =>
                {
                    fetchCount++;
                    return Task.FromResult(CreateDekProperties(id));
                },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);
            Assert.AreEqual(1, fetchCount);

            await provider.DekCache.GetOrAddDekPropertiesAsync(
                "dek1",
                (id, ctx, ct) =>
                {
                    fetchCount++;
                    return Task.FromResult(CreateDekProperties(id));
                },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);
            Assert.AreEqual(1, fetchCount, "Memory cache must serve the second call.");
        }

        [TestMethod]
        public async Task DualProvider_WithoutDistributedCache_MemoryCacheHonored()
        {
            // Same as WrapProvider_WithoutDistributedCache_MemoryCacheHonored, via the dual ctor.
            TestKeyWrapProvider wrapProvider = new TestKeyWrapProvider();
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                wrapProvider,
                storeProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30));

            int fetchCount = 0;
            await provider.DekCache.GetOrAddDekPropertiesAsync(
                "dek1",
                (id, ctx, ct) =>
                {
                    fetchCount++;
                    return Task.FromResult(CreateDekProperties(id));
                },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);
            Assert.AreEqual(1, fetchCount);

            await provider.DekCache.GetOrAddDekPropertiesAsync(
                "dek1",
                (id, ctx, ct) =>
                {
                    fetchCount++;
                    return Task.FromResult(CreateDekProperties(id));
                },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);
            Assert.AreEqual(1, fetchCount, "Memory cache must serve the second call.");
        }

#pragma warning restore CS0618

        #endregion

        #region Helpers

        private static DataEncryptionKeyProperties CreateDekProperties(string id)
        {
            return new DataEncryptionKeyProperties(
                id,
                "AEAD_AES_256_CBC_HMAC_SHA256",
                new byte[] { 1, 2, 3 },
                new EncryptionKeyWrapMetadata("test", "test", "RSA-OAEP", "test"),
                DateTime.UtcNow);
        }

        /// <summary>
        /// Minimal test implementation of the obsolete EncryptionKeyWrapProvider.
        /// </summary>
#pragma warning disable CS0618 // Type or member is obsolete
        private class TestKeyWrapProvider : EncryptionKeyWrapProvider
        {
            public override Task<EncryptionKeyUnwrapResult> UnwrapKeyAsync(
                byte[] wrappedKey,
                EncryptionKeyWrapMetadata metadata,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(new EncryptionKeyUnwrapResult(wrappedKey, TimeSpan.FromMinutes(60)));
            }

            public override Task<EncryptionKeyWrapResult> WrapKeyAsync(
                byte[] key,
                EncryptionKeyWrapMetadata metadata,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(new EncryptionKeyWrapResult(key, metadata));
            }
        }
#pragma warning restore CS0618

        #endregion
    }
}
