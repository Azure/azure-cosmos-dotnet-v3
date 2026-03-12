//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System;
    using System.Collections.Concurrent;
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
                distributedCache: distributedCache);

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
                    distributedCache: distributedCache));

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
                proactiveRefreshThreshold: TimeSpan.FromMinutes(5));

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
                distributedCache: distributedCache);

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
                    distributedCache: distributedCache));

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
                proactiveRefreshThreshold: TimeSpan.FromMinutes(5));

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
                distributedCache: distributedCache);

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
                    distributedCache: distributedCache));

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
                    distributedCache: distributedCache));

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
                proactiveRefreshThreshold: TimeSpan.FromMinutes(5));

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
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                storeProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache);

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
            Assert.IsTrue(distributedCache.ContainsKey("dek:dek1"), "Distributed cache should be populated");

            // Clear memory cache, re-fetch should use distributed cache
            await provider.DekCache.DekPropertiesCache.RemoveAsync("dek1");

            DataEncryptionKeyProperties props2 = await provider.DekCache.GetOrAddDekPropertiesAsync(
                "dek1",
                (id, ctx, ct) =>
                {
                    fetchCount++;
                    return Task.FromResult(CreateDekProperties(id));
                },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(1, fetchCount, "Should have used distributed cache, not fetched again");
            Assert.AreEqual(props.Id, props2.Id);
        }

#pragma warning disable CS0618 // Type or member is obsolete

        [TestMethod]
        public async Task WrapProvider_DistributedCacheIsWiredToDekCache()
        {
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();
            TestKeyWrapProvider wrapProvider = new TestKeyWrapProvider();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                wrapProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache);

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
            Assert.IsTrue(distributedCache.ContainsKey("dek:dek1"), "Distributed cache should be populated");

            // Clear memory cache, re-fetch should use distributed cache
            await provider.DekCache.DekPropertiesCache.RemoveAsync("dek1");

            DataEncryptionKeyProperties props2 = await provider.DekCache.GetOrAddDekPropertiesAsync(
                "dek1",
                (id, ctx, ct) =>
                {
                    fetchCount++;
                    return Task.FromResult(CreateDekProperties(id));
                },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(1, fetchCount, "Should have used distributed cache, not fetched again");
            Assert.AreEqual(props.Id, props2.Id);
        }

        [TestMethod]
        public async Task DualProvider_DistributedCacheIsWiredToDekCache()
        {
            InMemoryDistributedCache distributedCache = new InMemoryDistributedCache();
            TestKeyWrapProvider wrapProvider = new TestKeyWrapProvider();
            TestEncryptionKeyStoreProvider storeProvider = new TestEncryptionKeyStoreProvider();

            CosmosDataEncryptionKeyProvider provider = new CosmosDataEncryptionKeyProvider(
                wrapProvider,
                storeProvider,
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: distributedCache);

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
            Assert.IsTrue(distributedCache.ContainsKey("dek:dek1"), "Distributed cache should be populated");

            // Clear memory cache, re-fetch should use distributed cache
            await provider.DekCache.DekPropertiesCache.RemoveAsync("dek1");

            DataEncryptionKeyProperties props2 = await provider.DekCache.GetOrAddDekPropertiesAsync(
                "dek1",
                (id, ctx, ct) =>
                {
                    fetchCount++;
                    return Task.FromResult(CreateDekProperties(id));
                },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(1, fetchCount, "Should have used distributed cache, not fetched again");
            Assert.AreEqual(props.Id, props2.Id);
        }

        [TestMethod]
        public async Task WrapProvider_WithoutDistributedCache_DoesNotPopulateDistributedCache()
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

            // Second fetch from same provider should use memory cache
            await provider.DekCache.GetOrAddDekPropertiesAsync(
                "dek1",
                (id, ctx, ct) =>
                {
                    fetchCount++;
                    return Task.FromResult(CreateDekProperties(id));
                },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(1, fetchCount, "Should have used memory cache");
        }

        [TestMethod]
        public async Task DualProvider_WithoutDistributedCache_DoesNotPopulateDistributedCache()
        {
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

            // Second fetch from same provider should use memory cache
            await provider.DekCache.GetOrAddDekPropertiesAsync(
                "dek1",
                (id, ctx, ct) =>
                {
                    fetchCount++;
                    return Task.FromResult(CreateDekProperties(id));
                },
                CosmosDiagnosticsContext.Create(null),
                CancellationToken.None);

            Assert.AreEqual(1, fetchCount, "Should have used memory cache");
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
                distributedCache: mockCache.Object);

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

        /// <summary>
        /// Simple in-memory implementation of IDistributedCache for testing.
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
                this.cache[key] = new CacheEntry
                {
                    Value = value,
                    AbsoluteExpiration = options.AbsoluteExpiration,
                };
                return Task.CompletedTask;
            }

            public void Remove(string key) => this.cache.TryRemove(key, out _);

            public Task RemoveAsync(string key, CancellationToken token = default)
            {
                this.Remove(key);
                return Task.CompletedTask;
            }

            public void Refresh(string key) { }

            public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

            public bool ContainsKey(string key) => this.cache.ContainsKey(key);

            private class CacheEntry
            {
                public byte[] Value { get; set; }

                public DateTimeOffset? AbsoluteExpiration { get; set; }
            }
        }

        #endregion
    }
}
