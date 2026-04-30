// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace CachingKeyResolverSample.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core.Cryptography;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class CachingKeyEncryptionKeyTests
    {
        private static readonly byte[] TestEncryptedKey = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        private static readonly byte[] TestUnwrappedKey = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
        private const string TestAlgorithm = "RSA-OAEP";

        #region Fresh cache hit tests

        [TestMethod]
        public void UnwrapKey_FirstCall_DelegatesToInnerKey()
        {
            Mock<IKeyEncryptionKey> mockInner = CreateMockInnerKey();
            CachingKeyEncryptionKey sut = new CachingKeyEncryptionKey(mockInner.Object, TimeSpan.FromHours(1));

            byte[] result = sut.UnwrapKey(TestAlgorithm, TestEncryptedKey);

            CollectionAssert.AreEqual(TestUnwrappedKey, result);
            mockInner.Verify(
                k => k.UnwrapKey(TestAlgorithm, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public void UnwrapKey_SecondCall_ReturnsCached_NoInnerCall()
        {
            Mock<IKeyEncryptionKey> mockInner = CreateMockInnerKey();
            CachingKeyEncryptionKey sut = new CachingKeyEncryptionKey(mockInner.Object, TimeSpan.FromHours(1));

            sut.UnwrapKey(TestAlgorithm, TestEncryptedKey);
            byte[] result2 = sut.UnwrapKey(TestAlgorithm, TestEncryptedKey);

            CollectionAssert.AreEqual(TestUnwrappedKey, result2);
            mockInner.Verify(
                k => k.UnwrapKey(TestAlgorithm, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()),
                Times.Once); // only the first call hit AKV
        }

        [TestMethod]
        public async Task UnwrapKeyAsync_SecondCall_ReturnsCached()
        {
            Mock<IKeyEncryptionKey> mockInner = CreateMockInnerKeyAsync();
            CachingKeyEncryptionKey sut = new CachingKeyEncryptionKey(mockInner.Object, TimeSpan.FromHours(1));

            await sut.UnwrapKeyAsync(TestAlgorithm, TestEncryptedKey);
            byte[] result2 = await sut.UnwrapKeyAsync(TestAlgorithm, TestEncryptedKey);

            CollectionAssert.AreEqual(TestUnwrappedKey, result2);
            mockInner.Verify(
                k => k.UnwrapKeyAsync(TestAlgorithm, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region Stale fallback tests

        [TestMethod]
        public void UnwrapKey_AkvDown_StaleEntryExists_ReturnsStale()
        {
            // First call succeeds and caches with a very short TTL.
            Mock<IKeyEncryptionKey> mockInner = CreateMockInnerKey();
            CachingKeyEncryptionKey sut = new CachingKeyEncryptionKey(mockInner.Object, TimeSpan.FromMilliseconds(1));

            sut.UnwrapKey(TestAlgorithm, TestEncryptedKey);

            // Wait for TTL to expire.
            Thread.Sleep(50);

            // Now make inner fail (AKV is down).
            mockInner.Setup(k => k.UnwrapKey(It.IsAny<string>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException("AKV is down"));

            // Should return stale cached bytes.
            byte[] result = sut.UnwrapKey(TestAlgorithm, TestEncryptedKey);
            CollectionAssert.AreEqual(TestUnwrappedKey, result);
        }

        [TestMethod]
        public async Task UnwrapKeyAsync_AkvDown_StaleEntryExists_ReturnsStale()
        {
            Mock<IKeyEncryptionKey> mockInner = CreateMockInnerKeyAsync();
            CachingKeyEncryptionKey sut = new CachingKeyEncryptionKey(mockInner.Object, TimeSpan.FromMilliseconds(1));

            await sut.UnwrapKeyAsync(TestAlgorithm, TestEncryptedKey);
            await Task.Delay(50);

            // AKV goes down.
            mockInner.Setup(k => k.UnwrapKeyAsync(It.IsAny<string>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("AKV is down"));

            byte[] result = await sut.UnwrapKeyAsync(TestAlgorithm, TestEncryptedKey);
            CollectionAssert.AreEqual(TestUnwrappedKey, result);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void UnwrapKey_AkvDown_NoCachedEntry_Throws()
        {
            Mock<IKeyEncryptionKey> mockInner = new Mock<IKeyEncryptionKey>();
            mockInner.Setup(k => k.UnwrapKey(It.IsAny<string>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException("AKV is down"));

            CachingKeyEncryptionKey sut = new CachingKeyEncryptionKey(mockInner.Object, TimeSpan.FromHours(1));

            // No prior successful call → nothing to fall back to → must throw.
            sut.UnwrapKey(TestAlgorithm, TestEncryptedKey);
        }

        #endregion

        #region TTL expiry + refresh tests

        [TestMethod]
        public void UnwrapKey_StaleEntry_AkvUp_RefreshesCache()
        {
            byte[] newUnwrappedKey = new byte[] { 0x11, 0x22, 0x33, 0x44 };
            Mock<IKeyEncryptionKey> mockInner = CreateMockInnerKey();
            CachingKeyEncryptionKey sut = new CachingKeyEncryptionKey(mockInner.Object, TimeSpan.FromMilliseconds(1));

            sut.UnwrapKey(TestAlgorithm, TestEncryptedKey);
            Thread.Sleep(50);

            // AKV returns new key bytes (simulating key rotation).
            mockInner.Setup(k => k.UnwrapKey(It.IsAny<string>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
                .Returns(newUnwrappedKey);

            byte[] result = sut.UnwrapKey(TestAlgorithm, TestEncryptedKey);
            CollectionAssert.AreEqual(newUnwrappedKey, result);
        }

        #endregion

        #region Shared cache tests

        [TestMethod]
        public void SharedCache_MultipleKeyInstances_ShareEntries()
        {
            ConcurrentDictionary<string, CachingKeyEncryptionKey.CachedUnwrapEntry> sharedCache = new();
            Mock<IKeyEncryptionKey> mockInner1 = CreateMockInnerKey();
            Mock<IKeyEncryptionKey> mockInner2 = CreateMockInnerKey();

            CachingKeyEncryptionKey key1 = new CachingKeyEncryptionKey(mockInner1.Object, TimeSpan.FromHours(1), sharedCache);
            CachingKeyEncryptionKey key2 = new CachingKeyEncryptionKey(mockInner2.Object, TimeSpan.FromHours(1), sharedCache);

            // Key1 unwraps and populates shared cache.
            key1.UnwrapKey(TestAlgorithm, TestEncryptedKey);

            // Key2 should get cache hit — inner not called.
            byte[] result = key2.UnwrapKey(TestAlgorithm, TestEncryptedKey);
            CollectionAssert.AreEqual(TestUnwrappedKey, result);

            mockInner2.Verify(
                k => k.UnwrapKey(It.IsAny<string>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        #endregion

        #region WrapKey passthrough tests

        [TestMethod]
        public void WrapKey_AlwaysDelegatesToInner()
        {
            byte[] plainKey = new byte[] { 0xAA, 0xBB };
            byte[] wrappedResult = new byte[] { 0xCC, 0xDD };

            Mock<IKeyEncryptionKey> mockInner = new Mock<IKeyEncryptionKey>();
            mockInner.Setup(k => k.WrapKey(It.IsAny<string>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
                .Returns(wrappedResult);

            CachingKeyEncryptionKey sut = new CachingKeyEncryptionKey(mockInner.Object, TimeSpan.FromHours(1));

            byte[] result1 = sut.WrapKey(TestAlgorithm, plainKey);
            byte[] result2 = sut.WrapKey(TestAlgorithm, plainKey);

            CollectionAssert.AreEqual(wrappedResult, result1);
            mockInner.Verify(
                k => k.WrapKey(TestAlgorithm, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2)); // always goes to AKV
        }

        #endregion

        #region KeyId passthrough test

        [TestMethod]
        public void KeyId_DelegatesToInner()
        {
            const string expectedKeyId = "https://my-vault.vault.azure.net/keys/my-key/abc123";
            Mock<IKeyEncryptionKey> mockInner = new Mock<IKeyEncryptionKey>();
            mockInner.Setup(k => k.KeyId).Returns(expectedKeyId);

            CachingKeyEncryptionKey sut = new CachingKeyEncryptionKey(mockInner.Object, TimeSpan.FromHours(1));

            Assert.AreEqual(expectedKeyId, sut.KeyId);
        }

        #endregion

        #region Integration with CachingKeyResolver

        [TestMethod]
        public void CachingKeyResolver_WrapsWithCachingKeyEncryptionKey_WhenOptionEnabled()
        {
            Mock<IKeyEncryptionKeyResolver> mockResolver = new Mock<IKeyEncryptionKeyResolver>(MockBehavior.Strict);
            Mock<IKeyEncryptionKey> rawKey = new Mock<IKeyEncryptionKey>();
            rawKey.Setup(k => k.KeyId).Returns("test-key");
            mockResolver.Setup(r => r.Resolve(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(rawKey.Object);

            CachingKeyResolverOptions options = new CachingKeyResolverOptions
            {
                KeyCacheTimeToLive = TimeSpan.FromHours(1),
                RefreshTimerInterval = TimeSpan.FromHours(1),
                UnwrapKeyCacheTimeToLive = TimeSpan.FromHours(24),
            };

            using CachingKeyResolver sut = new CachingKeyResolver(mockResolver.Object, options);
            IKeyEncryptionKey result = sut.Resolve("test-key");

            Assert.IsInstanceOfType(result, typeof(CachingKeyEncryptionKey));
        }

        [TestMethod]
        public void CachingKeyResolver_DoesNotWrap_WhenUnwrapCacheDisabled()
        {
            Mock<IKeyEncryptionKeyResolver> mockResolver = new Mock<IKeyEncryptionKeyResolver>(MockBehavior.Strict);
            Mock<IKeyEncryptionKey> rawKey = new Mock<IKeyEncryptionKey>();
            rawKey.Setup(k => k.KeyId).Returns("test-key");
            mockResolver.Setup(r => r.Resolve(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(rawKey.Object);

            CachingKeyResolverOptions options = new CachingKeyResolverOptions
            {
                KeyCacheTimeToLive = TimeSpan.FromHours(1),
                RefreshTimerInterval = TimeSpan.FromHours(1),
                UnwrapKeyCacheTimeToLive = TimeSpan.Zero,
            };

            using CachingKeyResolver sut = new CachingKeyResolver(mockResolver.Object, options);
            IKeyEncryptionKey result = sut.Resolve("test-key");

            Assert.IsNotInstanceOfType(result, typeof(CachingKeyEncryptionKey));
        }

        #endregion

        #region Helpers

        private static Mock<IKeyEncryptionKey> CreateMockInnerKey()
        {
            Mock<IKeyEncryptionKey> mock = new Mock<IKeyEncryptionKey>();
            mock.Setup(k => k.KeyId).Returns("test-key");
            mock.Setup(k => k.UnwrapKey(It.IsAny<string>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
                .Returns(TestUnwrappedKey);
            return mock;
        }

        private static Mock<IKeyEncryptionKey> CreateMockInnerKeyAsync()
        {
            Mock<IKeyEncryptionKey> mock = new Mock<IKeyEncryptionKey>();
            mock.Setup(k => k.KeyId).Returns("test-key");
            mock.Setup(k => k.UnwrapKeyAsync(It.IsAny<string>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TestUnwrappedKey);
            return mock;
        }

        #endregion
    }
}
