//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using Microsoft.Azure.Cosmos.Encryption.Tests.TestHelpers;
    using Microsoft.Data.Encryption.Cryptography;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Verifies that the ProtectedDataEncryptionKey static cache round-trips values written by
    /// <see cref="ProtectedDataEncryptionKey.SetInCache"/> and evicted by
    /// <see cref="ProtectedDataEncryptionKey.RemoveFromCache"/>.
    ///
    /// This is the linchpin of the background refresh worker: if the (name, KEK, encryptedKey)
    /// cache key computed inside <c>SetInCache</c> does not match the one computed inside
    /// <c>GetOrCreate</c>, the worker silently degrades to a no-op and the hot path continues
    /// to take a cache miss under the semaphore. These tests fail loudly if any future MDE
    /// change to <see cref="KeyEncryptionKey.Equals(object)"/> or <see cref="KeyEncryptionKey.GetHashCode"/>
    /// breaks that identity.
    /// </summary>
    [TestClass]
    public class ProtectedDataEncryptionKeyCacheRoundTripTests
    {
        [TestInitialize]
        public void ResetTtl()
        {
            ProtectedDataEncryptionKey.TimeToLive = TimeSpan.FromHours(2);
        }

        [TestMethod]
        public void SetInCacheProducesGetOrCreateHit()
        {
            KeyEncryptionKey kek = new TestCryptoHelpers.DummyKeyEncryptionKey();

            // 32 bytes: DataEncryptionKey base ctor calls ValidateSize(KeySizeInBytes=32).
            // DummyProvider.UnwrapKey is an echo, so a cache MISS would build a PDEK whose
            // RootKeyBytes == encryptedKey; using distinct rootKey lets us prove HIT vs MISS.
            byte[] encryptedKey = new byte[32];
            byte[] rootKey = new byte[32];
            for (int i = 0; i < 32; i++)
            {
                encryptedKey[i] = (byte)i;
                rootKey[i] = (byte)(i + 100);
            }

            string cekName = "cek-roundtrip-" + Guid.NewGuid().ToString("N");

            ProtectedDataEncryptionKey.SetInCache(cekName, kek, encryptedKey, rootKey);

            ProtectedDataEncryptionKey result = ProtectedDataEncryptionKey.GetOrCreate(
                cekName,
                kek,
                encryptedKey);

            Assert.IsNotNull(result);
            CollectionAssert.AreEqual(
                rootKey,
                result.RootKeyBytes,
                "GetOrCreate did not return the PDEK inserted by SetInCache -- cache key mismatch.");
        }

        [TestMethod]
        public void RemoveFromCacheEvictsEntry()
        {
            KeyEncryptionKey kek = new TestCryptoHelpers.DummyKeyEncryptionKey();
            byte[] encryptedKey = new byte[32];
            byte[] rootKey = new byte[32];
            for (int i = 0; i < 32; i++)
            {
                encryptedKey[i] = (byte)(50 + i);
                rootKey[i] = (byte)(200 - i);
            }

            string cekName = "cek-evict-" + Guid.NewGuid().ToString("N");

            ProtectedDataEncryptionKey.SetInCache(cekName, kek, encryptedKey, rootKey);
            ProtectedDataEncryptionKey.RemoveFromCache(cekName, kek, encryptedKey);

            // After eviction, GetOrCreate must build a fresh PDEK by calling
            // KeyEncryptionKey.DecryptEncryptionKey(encryptedKey). The DummyProvider's UnwrapKey
            // is an echo, so the fresh PDEK's RootKeyBytes will equal `encryptedKey` -- NOT the
            // original rootKey we injected. Any other outcome means the eviction did not take.
            ProtectedDataEncryptionKey result = ProtectedDataEncryptionKey.GetOrCreate(
                cekName,
                kek,
                encryptedKey);

            Assert.IsNotNull(result);
            CollectionAssert.AreNotEqual(
                rootKey,
                result.RootKeyBytes,
                "RemoveFromCache did not evict; GetOrCreate returned the pre-eviction PDEK.");
            CollectionAssert.AreEqual(
                encryptedKey,
                result.RootKeyBytes,
                "Post-eviction GetOrCreate did not build a fresh PDEK using the KEK's DecryptEncryptionKey path.");
        }

        [TestMethod]
        public void SetInCacheOverwritesPreviousEntry()
        {
            KeyEncryptionKey kek = new TestCryptoHelpers.DummyKeyEncryptionKey();
            byte[] encryptedKey = new byte[32];
            byte[] rootKeyV1 = new byte[32];
            byte[] rootKeyV2 = new byte[32];
            for (int i = 0; i < 32; i++)
            {
                encryptedKey[i] = (byte)(i * 2);
                rootKeyV1[i] = (byte)i;
                rootKeyV2[i] = (byte)(255 - i);
            }

            string cekName = "cek-overwrite-" + Guid.NewGuid().ToString("N");

            ProtectedDataEncryptionKey.SetInCache(cekName, kek, encryptedKey, rootKeyV1);
            ProtectedDataEncryptionKey.SetInCache(cekName, kek, encryptedKey, rootKeyV2);

            ProtectedDataEncryptionKey result = ProtectedDataEncryptionKey.GetOrCreate(cekName, kek, encryptedKey);

            CollectionAssert.AreEqual(rootKeyV2, result.RootKeyBytes, "SetInCache did not replace the existing cache entry.");
        }

        [TestMethod]
        public void RemoveFromCacheRejectsInvalidArguments()
        {
            KeyEncryptionKey kek = new TestCryptoHelpers.DummyKeyEncryptionKey();

            // Validators inside the MDE shim throw various exception types (in some environments
            // the intended MicrosoftDataEncryptionException surfaces as MissingManifestResourceException
            // due to a shim resource-loading bug). We only care that invalid input is rejected.
            AssertThrowsAny(() => ProtectedDataEncryptionKey.RemoveFromCache(null, kek, new byte[] { 1 }));
            AssertThrowsAny(() => ProtectedDataEncryptionKey.RemoveFromCache("cek", null, new byte[] { 1 }));
            AssertThrowsAny(() => ProtectedDataEncryptionKey.RemoveFromCache("cek", kek, Array.Empty<byte>()));
        }

        private static void AssertThrowsAny(Action action)
        {
            try
            {
                action();
            }
            catch (Exception)
            {
                return;
            }

            Assert.Fail("Expected an exception, but none was thrown.");
        }
    }
}
