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
    /// Tests for all <see cref="CosmosDataEncryptionKeyProvider"/> constructor overloads,
    /// verifying correct initialization, argument validation, and distributed cache wiring.
    /// </summary>
    [TestClass]
    public class CosmosDataEncryptionKeyProviderConstructorTests
    {
        #region EncryptionKeyWrapProvider-only constructors

#pragma warning disable CS0618 // Type or member is obsolete

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

        [TestMethod]
        public void WrapProviderWithDistributedCache_ValidArgs_SetsProperties()
        {
            TestKeyWrapProvider wrapProvider = new TestKeyWrapProvider();
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                wrapProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache,
                distributedCacheKeyPrefix: "test-dek");

            Assert.AreSame(wrapProvider, provider.EncryptionKeyWrapProvider);
            Assert.IsNull(provider.EncryptionKeyStoreProvider);
            Assert.IsNotNull(provider.DekCache);
        }

        [TestMethod]
        public void WrapProviderWithDistributedCache_NullProvider_ThrowsArgumentNullException()
        {
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();

            ArgumentNullException ex = Assert.ThrowsException<ArgumentNullException>(() =>
                new CosmosDataEncryptionKeyProvider(
                    encryptionKeyWrapProvider: (EncryptionKeyWrapProvider)null,
                    dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                    distributedCache: distributedCache,
                    distributedCacheKeyPrefix: "test-dek"));

            Assert.AreEqual("encryptionKeyWrapProvider", ex.ParamName);
        }

        [TestMethod]
        public void WrapProviderWithDistributedCache_NullCache_CreatesSuccessfully()
        {
            TestKeyWrapProvider wrapProvider = new TestKeyWrapProvider();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                wrapProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: null);

            Assert.IsNotNull(provider.DekCache);
        }

        [TestMethod]
        public void WrapProviderWithDistributedCache_WithProactiveRefresh_CreatesSuccessfully()
        {
            TestKeyWrapProvider wrapProvider = new TestKeyWrapProvider();
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                wrapProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache,
                proactiveRefreshThreshold: TimeSpan.FromMinutes(5),
                distributedCacheKeyPrefix: "test-dek");

            Assert.IsNotNull(provider.DekCache);
        }

        [TestMethod]
        public void WrapProviderWithDistributedCache_ChainsToFullConstructor()
        {
            // The no-distributed-cache overload should produce the same DekCache behavior
            TestKeyWrapProvider wrapProvider = new TestKeyWrapProvider();

            CosmosDataEncryptionKeyProvider withoutDC = new CosmosDataEncryptionKeyProvider(
                wrapProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30));

            CosmosDataEncryptionKeyProvider withNullDC = new CosmosDataEncryptionKeyProvider(
                wrapProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: null,
                proactiveRefreshThreshold: null);

            Assert.IsNotNull(withoutDC.DekCache);
            Assert.IsNotNull(withNullDC.DekCache);
        }

#pragma warning restore CS0618 // Type or member is obsolete

        #endregion

        #region EncryptionKeyStoreProvider-only constructors

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
            Assert.IsNotNull(provider.DataEncryptionKeyContainer);
        }

        [TestMethod]
        public void StoreProviderOnly_NullProvider_ThrowsArgumentNullException()
        {
            ArgumentNullException ex = Assert.ThrowsException<ArgumentNullException>(() =>
                new CosmosDataEncryptionKeyProvider(
                    encryptionKeyStoreProvider: null,
                    dekPropertiesTimeToLive: TimeSpan.FromMinutes(30)));

            Assert.AreEqual("encryptionKeyStoreProvider", ex.ParamName);
        }

        [TestMethod]
        public void StoreProviderOnly_DefaultTTL_CreatesSuccessfully()
        {
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(storeProvider);

            Assert.IsNotNull(provider.DekCache);
        }

        [TestMethod]
        public void StoreProviderWithDistributedCache_ValidArgs_SetsProperties()
        {
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                storeProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache,
                distributedCacheKeyPrefix: "test-dek");

            Assert.AreSame(storeProvider, provider.EncryptionKeyStoreProvider);
            Assert.IsNotNull(provider.MdeKeyWrapProvider);
            Assert.IsNotNull(provider.DekCache);
        }

        [TestMethod]
        public void StoreProviderWithDistributedCache_NullProvider_ThrowsArgumentNullException()
        {
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();

            ArgumentNullException ex = Assert.ThrowsException<ArgumentNullException>(() =>
                new CosmosDataEncryptionKeyProvider(
                    encryptionKeyStoreProvider: null,
                    dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                    distributedCache: distributedCache,
                    distributedCacheKeyPrefix: "test-dek"));

            Assert.AreEqual("encryptionKeyStoreProvider", ex.ParamName);
        }

        [TestMethod]
        public void StoreProviderWithDistributedCache_NullCache_CreatesSuccessfully()
        {
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                storeProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: null);

            Assert.IsNotNull(provider.DekCache);
        }

        [TestMethod]
        public void StoreProviderWithDistributedCache_WithProactiveRefresh_CreatesSuccessfully()
        {
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                storeProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache,
                proactiveRefreshThreshold: TimeSpan.FromMinutes(5),
                distributedCacheKeyPrefix: "test-dek");

            Assert.IsNotNull(provider.DekCache);
        }

        [TestMethod]
        public void StoreProviderWithDistributedCache_ChainsToFullConstructor()
        {
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            CosmosDataEncryptionKeyProvider withoutDC = new CosmosDataEncryptionKeyProvider(
                storeProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30));

            CosmosDataEncryptionKeyProvider withNullDC = new CosmosDataEncryptionKeyProvider(
                storeProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: null,
                proactiveRefreshThreshold: null);

            Assert.IsNotNull(withoutDC.DekCache);
            Assert.IsNotNull(withNullDC.DekCache);
        }

        #endregion

        #region Dual-provider (EncryptionKeyWrapProvider + EncryptionKeyStoreProvider) constructors

#pragma warning disable CS0618 // Type or member is obsolete

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
            Assert.IsNotNull(provider.DataEncryptionKeyContainer);
        }

        [TestMethod]
        public void DualProvider_NullWrapProvider_ThrowsArgumentNullException()
        {
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            ArgumentNullException ex = Assert.ThrowsException<ArgumentNullException>(() =>
                new CosmosDataEncryptionKeyProvider(
                    encryptionKeyWrapProvider: null,
                    encryptionKeyStoreProvider: storeProvider,
                    dekPropertiesTimeToLive: TimeSpan.FromMinutes(30)));

            Assert.AreEqual("encryptionKeyWrapProvider", ex.ParamName);
        }

        [TestMethod]
        public void DualProvider_NullStoreProvider_ThrowsArgumentNullException()
        {
            TestKeyWrapProvider wrapProvider = new TestKeyWrapProvider();

            ArgumentNullException ex = Assert.ThrowsException<ArgumentNullException>(() =>
                new CosmosDataEncryptionKeyProvider(
                    wrapProvider,
                    encryptionKeyStoreProvider: null,
                    dekPropertiesTimeToLive: TimeSpan.FromMinutes(30)));

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

        [TestMethod]
        public void DualProviderWithDistributedCache_ValidArgs_SetsAllProperties()
        {
            TestKeyWrapProvider wrapProvider = new TestKeyWrapProvider();
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                wrapProvider,
                storeProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache,
                distributedCacheKeyPrefix: "test-dek");

            Assert.AreSame(wrapProvider, provider.EncryptionKeyWrapProvider);
            Assert.AreSame(storeProvider, provider.EncryptionKeyStoreProvider);
            Assert.IsNotNull(provider.MdeKeyWrapProvider);
            Assert.IsNotNull(provider.DekCache);
        }

        [TestMethod]
        public void DualProviderWithDistributedCache_NullWrapProvider_ThrowsArgumentNullException()
        {
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();

            ArgumentNullException ex = Assert.ThrowsException<ArgumentNullException>(() =>
                new CosmosDataEncryptionKeyProvider(
                    encryptionKeyWrapProvider: null,
                    encryptionKeyStoreProvider: storeProvider,
                    dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                    distributedCache: distributedCache,
                    distributedCacheKeyPrefix: "test-dek"));

            Assert.AreEqual("encryptionKeyWrapProvider", ex.ParamName);
        }

        [TestMethod]
        public void DualProviderWithDistributedCache_NullStoreProvider_ThrowsArgumentNullException()
        {
            TestKeyWrapProvider wrapProvider = new TestKeyWrapProvider();
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();

            ArgumentNullException ex = Assert.ThrowsException<ArgumentNullException>(() =>
                new CosmosDataEncryptionKeyProvider(
                    wrapProvider,
                    encryptionKeyStoreProvider: null,
                    dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                    distributedCache: distributedCache,
                    distributedCacheKeyPrefix: "test-dek"));

            Assert.AreEqual("encryptionKeyStoreProvider", ex.ParamName);
        }

        [TestMethod]
        public void DualProviderWithDistributedCache_NullCache_CreatesSuccessfully()
        {
            TestKeyWrapProvider wrapProvider = new TestKeyWrapProvider();
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                wrapProvider,
                storeProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: null);

            Assert.IsNotNull(provider.DekCache);
        }

        [TestMethod]
        public void DualProviderWithDistributedCache_WithProactiveRefresh_CreatesSuccessfully()
        {
            TestKeyWrapProvider wrapProvider = new TestKeyWrapProvider();
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                wrapProvider,
                storeProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache,
                proactiveRefreshThreshold: TimeSpan.FromMinutes(5),
                distributedCacheKeyPrefix: "test-dek");

            Assert.IsNotNull(provider.DekCache);
        }

        [TestMethod]
        public void DualProviderWithDistributedCache_ChainsToFullConstructor()
        {
            TestKeyWrapProvider wrapProvider = new TestKeyWrapProvider();
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            CosmosDataEncryptionKeyProvider withoutDC = new CosmosDataEncryptionKeyProvider(
                wrapProvider,
                storeProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30));

            CosmosDataEncryptionKeyProvider withNullDC = new CosmosDataEncryptionKeyProvider(
                wrapProvider,
                storeProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: null,
                proactiveRefreshThreshold: null);

            Assert.IsNotNull(withoutDC.DekCache);
            Assert.IsNotNull(withNullDC.DekCache);
        }

#pragma warning restore CS0618 // Type or member is obsolete

        #endregion

        #region Cross-constructor validation - proactiveRefreshThreshold

        [TestMethod]
        public void StoreProviderWithDistributedCache_NegativeRefreshThreshold_Throws()
        {
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new CosmosDataEncryptionKeyProvider(
                    storeProvider,
                    dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                    distributedCache: null,
                    proactiveRefreshThreshold: TimeSpan.FromMinutes(-5)));
        }

        [TestMethod]
        public void StoreProviderWithDistributedCache_RefreshThresholdEqualToTTL_Throws()
        {
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new CosmosDataEncryptionKeyProvider(
                    storeProvider,
                    dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                    distributedCache: null,
                    proactiveRefreshThreshold: TimeSpan.FromMinutes(30)));
        }

        [TestMethod]
        public void StoreProviderWithDistributedCache_RefreshThresholdGreaterThanTTL_Throws()
        {
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new CosmosDataEncryptionKeyProvider(
                    storeProvider,
                    dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                    distributedCache: null,
                    proactiveRefreshThreshold: TimeSpan.FromMinutes(60)));
        }

#pragma warning disable CS0618 // Type or member is obsolete

        [TestMethod]
        public void WrapProviderWithDistributedCache_NegativeRefreshThreshold_Throws()
        {
            TestKeyWrapProvider wrapProvider = new TestKeyWrapProvider();

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new CosmosDataEncryptionKeyProvider(
                    wrapProvider,
                    dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                    distributedCache: null,
                    proactiveRefreshThreshold: TimeSpan.FromMinutes(-5)));
        }

        [TestMethod]
        public void DualProviderWithDistributedCache_NegativeRefreshThreshold_Throws()
        {
            TestKeyWrapProvider wrapProvider = new TestKeyWrapProvider();
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new CosmosDataEncryptionKeyProvider(
                    wrapProvider,
                    storeProvider,
                    dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                    distributedCache: null,
                    proactiveRefreshThreshold: TimeSpan.FromMinutes(-5)));
        }

        [TestMethod]
        public void DualProviderWithDistributedCache_RefreshThresholdEqualToTTL_Throws()
        {
            TestKeyWrapProvider wrapProvider = new TestKeyWrapProvider();
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                new CosmosDataEncryptionKeyProvider(
                    wrapProvider,
                    storeProvider,
                    dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                    distributedCache: null,
                    proactiveRefreshThreshold: TimeSpan.FromMinutes(30)));
        }

#pragma warning restore CS0618 // Type or member is obsolete

        #endregion

        #region DekCache integration - distributed cache is actually wired through

        [TestMethod]
        public async Task StoreProvider_DistributedCacheIsWiredToDekCache()
        {
            // REQ: When a CosmosDataEncryptionKeyProvider is constructed with a distributed cache,
            //      the first source fetch must populate that distributed cache so that a peer
            //      process (or this process after memory-cache discard) can read it.
            // SOURCE: PR #5428 description — "cross-process/cross-instance caching of DEK properties."
            // NOTE: This is a wiring test. We use a peer DekCache sharing the same L2 to prove
            //       the populated entry is observable from a fresh instance, rather than asserting
            //       the literal cache-key shape.
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                storeProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache,
                distributedCacheKeyPrefix: "test-dek");

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

#pragma warning disable CS0618 // Type or member is obsolete

        [TestMethod]
        public async Task WrapProvider_DistributedCacheIsWiredToDekCache()
        {
            // REQ: Same wiring guarantee as StoreProvider_DistributedCacheIsWiredToDekCache,
            //      via the obsolete wrap-provider ctor overload.
            // SOURCE: PR #5428 description.
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();
            TestKeyWrapProvider wrapProvider = new TestKeyWrapProvider();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                wrapProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache,
                distributedCacheKeyPrefix: "test-dek");

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
        public async Task DualProvider_DistributedCacheIsWiredToDekCache()
        {
            // REQ: Same wiring guarantee for the dual (wrap+store) ctor overload.
            // SOURCE: PR #5428 description.
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();
            TestKeyWrapProvider wrapProvider = new TestKeyWrapProvider();
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                wrapProvider,
                storeProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache,
                distributedCacheKeyPrefix: "test-dek");

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
        public async Task WrapProvider_WithoutDistributedCache_MemoryCacheHonored()
        {
            // REQ: When no distributed cache is provided, repeated lookups for the same dekId
            //      must be served from the in-memory cache without re-invoking the fetcher.
            // SOURCE: Pre-feature DekCache behaviour; distributedCache is documented as optional.
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
            // REQ: Same as WrapProvider_WithoutDistributedCache_MemoryCacheHonored, via the dual ctor.
            // SOURCE: Pre-feature DekCache behaviour; distributedCache is documented as optional.
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

        [TestMethod]
        public async Task DualProvider_DistributedCacheFailure_ContinuesOperation()
        {
            TestKeyWrapProvider wrapProvider = new TestKeyWrapProvider();
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
                wrapProvider,
                storeProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: mockCache.Object,
                distributedCacheKeyPrefix: "test-dek");

            // Should not throw even though distributed cache fails
            DataEncryptionKeyProperties result = await provider.DekCache.GetOrAddDekPropertiesAsync(
                "dek1",
                (id, ctx, ct) => Task.FromResult(CreateDekProperties(id)),
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual("dek1", result.Id);
        }

#pragma warning restore CS0618 // Type or member is obsolete

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
#pragma warning restore CS0618 // Type or member is obsolete

        #endregion
    }
}
