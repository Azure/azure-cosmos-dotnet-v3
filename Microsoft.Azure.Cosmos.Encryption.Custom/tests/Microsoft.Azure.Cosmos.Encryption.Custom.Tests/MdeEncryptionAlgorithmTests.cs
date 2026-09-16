//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class MdeEncryptionAlgorithmTests
    {
        private readonly TestEncryptionKeyStoreProvider encryptionKeyStoreProvider = new ();
        private readonly DataEncryptionKeyProperties dekProperties = CreateDekProperties();

        [TestMethod]
        public async Task CreateAsync_WithRawKeyFalse_NullTtl_EncryptDecrypt()
        {
            MdeEncryptionAlgorithm algorithm = await MdeEncryptionAlgorithm.CreateAsync(
                this.dekProperties,
                Data.Encryption.Cryptography.EncryptionType.Randomized,
                this.encryptionKeyStoreProvider,
                cacheTimeToLive: null,
                withRawKey: false,
                cancellationToken: default);

            ValidateEncryptDecryptRoundTrip(algorithm);

            Assert.IsNull(algorithm.RawKey);
            Assert.AreEqual(1, this.encryptionKeyStoreProvider.UnwrapCalls);
        }

        [TestMethod]
        public async Task CreateAsync_WithRawKeyFalse_ZeroTtl_EncryptDecrypt()
        {
            MdeEncryptionAlgorithm algorithm = await MdeEncryptionAlgorithm.CreateAsync(
                this.dekProperties,
                Data.Encryption.Cryptography.EncryptionType.Randomized,
                this.encryptionKeyStoreProvider,
                cacheTimeToLive: TimeSpan.Zero,
                withRawKey: false,
                cancellationToken: default);

            ValidateEncryptDecryptRoundTrip(algorithm);

            Assert.IsNull(algorithm.RawKey);
            Assert.AreEqual(1, this.encryptionKeyStoreProvider.UnwrapCalls);
        }

        [TestMethod]
        public async Task CreateAsync_WithRawKeyTrue_NullTtl_RawKeyExposed()
        {
            MdeEncryptionAlgorithm algorithm = await MdeEncryptionAlgorithm.CreateAsync(
                this.dekProperties,
                Data.Encryption.Cryptography.EncryptionType.Randomized,
                this.encryptionKeyStoreProvider,
                cacheTimeToLive: null,
                withRawKey: true,
                cancellationToken: default);

            ValidateEncryptDecryptRoundTrip(algorithm);

            Assert.IsNotNull(algorithm.RawKey);
            Assert.AreEqual(this.encryptionKeyStoreProvider.DerivedRawKey.SequenceEqual(algorithm.RawKey), true);
            Assert.AreEqual(1, this.encryptionKeyStoreProvider.UnwrapCalls);
        }

        [TestMethod]
        public async Task CreateAsync_WithRawKeyTrue_ZeroTtl_RawKeyExposed()
        {
            MdeEncryptionAlgorithm algorithm = await MdeEncryptionAlgorithm.CreateAsync(
                this.dekProperties,
                Data.Encryption.Cryptography.EncryptionType.Randomized,
                this.encryptionKeyStoreProvider,
                cacheTimeToLive: TimeSpan.Zero,
                withRawKey: true,
                cancellationToken: default);

            ValidateEncryptDecryptRoundTrip(algorithm);

            Assert.IsNotNull(algorithm.RawKey);
            Assert.IsTrue(this.encryptionKeyStoreProvider.DerivedRawKey.SequenceEqual(algorithm.RawKey));
            Assert.AreEqual(1, this.encryptionKeyStoreProvider.UnwrapCalls);
        }

        [TestMethod]
        public async Task CreateAsync_NullDekProperties_Throws()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
                await MdeEncryptionAlgorithm.CreateAsync(
                    dekProperties: null,
                    encryptionType: Data.Encryption.Cryptography.EncryptionType.Randomized,
                    encryptionKeyStoreProvider: this.encryptionKeyStoreProvider,
                    cacheTimeToLive: null,
                    withRawKey: false,
                    cancellationToken: default));
        }

        [TestMethod]
        public async Task CreateAsync_NullProvider_Throws()
        {
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
                await MdeEncryptionAlgorithm.CreateAsync(
                    dekProperties: this.dekProperties,
                    encryptionType: Data.Encryption.Cryptography.EncryptionType.Randomized,
                    encryptionKeyStoreProvider: null,
                    cacheTimeToLive: null,
                    withRawKey: false,
                    cancellationToken: default));
        }

        private static void ValidateEncryptDecryptRoundTrip(MdeEncryptionAlgorithm algorithm)
        {
            byte[] plaintext = new byte[] { 1, 2, 3, 4, 5 };
            ValidateEncryptDecryptAlloc(algorithm, plaintext);
            ValidateEncryptDecryptNonAlloc(algorithm, plaintext);
        }

        private static void ValidateEncryptDecryptAlloc(MdeEncryptionAlgorithm algorithm, byte[] plaintext)
        {
            byte[] ciphertext = algorithm.EncryptData(plaintext);
            byte[] decrypted = algorithm.DecryptData(ciphertext);
            CollectionAssert.AreNotEqual(plaintext, ciphertext);
            CollectionAssert.AreEqual(plaintext, decrypted);
        }

        private static void ValidateEncryptDecryptNonAlloc(MdeEncryptionAlgorithm algorithm, byte[] plaintext)
        {
            int encryptSize = algorithm.GetEncryptByteCount(plaintext.Length);
            byte[] ciphertextBuffer = new byte[encryptSize];
            algorithm.EncryptData(plaintext, 0, plaintext.Length, ciphertextBuffer, 0);

            int decryptSize = algorithm.GetDecryptByteCount(ciphertextBuffer.Length);
            byte[] plaintextBuffer = new byte[decryptSize];
            algorithm.DecryptData(ciphertextBuffer, 0, ciphertextBuffer.Length, plaintextBuffer, 0);
            
            CollectionAssert.AreEqual(plaintext, plaintextBuffer.AsSpan(0, plaintext.Length).ToArray());
        }

        private static DataEncryptionKeyProperties CreateDekProperties()
        {
            return new DataEncryptionKeyProperties(
                id: "dek1",
                encryptionAlgorithm: CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                wrappedDataEncryptionKey: Enumerable.Range(0, 32).Select(i => (byte)i).ToArray(),
                encryptionKeyWrapMetadata: new EncryptionKeyWrapMetadata("name", "value"),
                createdTime: DateTime.UtcNow);
        }
    }
}
