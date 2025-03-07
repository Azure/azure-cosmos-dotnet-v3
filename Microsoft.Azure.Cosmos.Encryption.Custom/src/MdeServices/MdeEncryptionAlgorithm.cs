//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using Microsoft.Data.Encryption.Cryptography;

    /// <summary>
    /// Encryption Algorithm provided by MDE Encryption Package.
    /// </summary>
    internal sealed class MdeEncryptionAlgorithm : DataEncryptionKey
    {
        private const byte Version = 1;

        private readonly AeadAes256CbcHmac256EncryptionAlgorithm mdeAeadAes256CbcHmac256EncryptionAlgorithm;

        // unused for MDE Algorithm.
        public override byte[] RawKey { get; }

        public override string EncryptionAlgorithm => CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized;

        /// <summary>
        /// Initializes a new instance of MdeEncryptionAlgorithm.
        /// Uses <see cref="AeadAes256CbcHmac256EncryptionAlgorithm"/> which implements authenticated encryption algorithm with associated data as described
        /// <see href="http://tools.ietf.org/html/draft-mcgrew-aead-aes-cbc-hmac-sha2-05">here</see> .
        /// More specifically this implements AEAD_AES_256_CBC_HMAC_SHA256 algorithm.
        /// </summary>
        public MdeEncryptionAlgorithm(
            DataEncryptionKeyProperties dekProperties,
            Data.Encryption.Cryptography.EncryptionType encryptionType,
            EncryptionKeyStoreProvider encryptionKeyStoreProvider,
            TimeSpan? cacheTimeToLive,
            bool withRawKey = false)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(dekProperties);
            ArgumentNullException.ThrowIfNull(encryptionKeyStoreProvider);
#else
            if (dekProperties == null)
            {
                throw new ArgumentNullException(nameof(dekProperties));
            }

            if (encryptionKeyStoreProvider == null)
            {
                throw new ArgumentNullException(nameof(encryptionKeyStoreProvider));
            }
#endif

            KeyEncryptionKey keyEncryptionKey = KeyEncryptionKey.GetOrCreate(
                dekProperties.EncryptionKeyWrapMetadata.Name,
                dekProperties.EncryptionKeyWrapMetadata.Value,
                encryptionKeyStoreProvider);

            if (!withRawKey)
            {
                ProtectedDataEncryptionKey protectedDataEncryptionKey = cacheTimeToLive.HasValue && cacheTimeToLive.Value == TimeSpan.Zero
                    ? new ProtectedDataEncryptionKey(
                        dekProperties.Id,
                        keyEncryptionKey,
                        dekProperties.WrappedDataEncryptionKey)
                    : ProtectedDataEncryptionKey.GetOrCreate(
                        dekProperties.Id,
                        keyEncryptionKey,
                        dekProperties.WrappedDataEncryptionKey);
                this.mdeAeadAes256CbcHmac256EncryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(
                    protectedDataEncryptionKey,
                    encryptionType,
                    Version);
            }
            else
            {
                byte[] rawKey = keyEncryptionKey.DecryptEncryptionKey(dekProperties.WrappedDataEncryptionKey);
                PlaintextDataEncryptionKey plaintextDataEncryptionKey = cacheTimeToLive.HasValue && (cacheTimeToLive.Value == TimeSpan.Zero)
                    ? new PlaintextDataEncryptionKey(
                            dekProperties.Id,
                            rawKey)
                    : PlaintextDataEncryptionKey.GetOrCreate(
                           dekProperties.Id,
                           rawKey);
                this.RawKey = rawKey;
                this.mdeAeadAes256CbcHmac256EncryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(
                    plaintextDataEncryptionKey,
                    encryptionType,
                    Version);
            }
        }

        /// <summary>
        /// Initializes a new instance of MdeEncryptionAlgorithm.
        /// Uses <see cref="AeadAes256CbcHmac256EncryptionAlgorithm"/> which implements authenticated encryption algorithm with associated data as described
        /// <see href="http://tools.ietf.org/html/draft-mcgrew-aead-aes-cbc-hmac-sha2-05">here</see> .
        /// More specifically this implements AEAD_AES_256_CBC_HMAC_SHA256 algorithm.
        /// </summary>
        public MdeEncryptionAlgorithm(
            byte[] rawkey,
            Data.Encryption.Cryptography.DataEncryptionKey dataEncryptionKey,
            Data.Encryption.Cryptography.EncryptionType encryptionType)
        {
            this.RawKey = rawkey;
            this.mdeAeadAes256CbcHmac256EncryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(
                dataEncryptionKey,
                encryptionType,
                Version);
        }

        /// <summary>
        /// Encrypt data using EncryptionAlgorithm
        /// </summary>
        /// <param name="plainText">Plaintext data to be encrypted</param>
        /// <returns>Returns the ciphertext corresponding to the plaintext.</returns>
        public override byte[] EncryptData(byte[] plainText)
        {
            return this.mdeAeadAes256CbcHmac256EncryptionAlgorithm.Encrypt(plainText);
        }

        /// <summary>
        /// Decrypt data using EncryptionAlgorithm
        /// </summary>
        /// <param name="cipherText">CipherText data to be decrypted</param>
        /// <returns>Returns the plaintext corresponding to the cipherText.</returns>
        public override byte[] DecryptData(byte[] cipherText)
        {
            return this.mdeAeadAes256CbcHmac256EncryptionAlgorithm.Decrypt(cipherText);
        }

        public override int EncryptData(byte[] plainText, int plainTextOffset, int plainTextLength, byte[] output, int outputOffset)
        {
            return this.mdeAeadAes256CbcHmac256EncryptionAlgorithm.Encrypt(plainText, plainTextOffset, plainTextLength, output, outputOffset);
        }

        public override int DecryptData(byte[] cipherText, int cipherTextOffset, int cipherTextLength, byte[] output, int outputOffset)
        {
            return this.mdeAeadAes256CbcHmac256EncryptionAlgorithm.Decrypt(cipherText, cipherTextOffset, cipherTextLength, output, outputOffset);
        }

        public override int GetEncryptByteCount(int plainTextLength)
        {
            return this.mdeAeadAes256CbcHmac256EncryptionAlgorithm.GetEncryptByteCount(plainTextLength);
        }

        public override int GetDecryptByteCount(int cipherTextLength)
        {
            return this.mdeAeadAes256CbcHmac256EncryptionAlgorithm.GetDecryptByteCount(cipherTextLength);
        }
    }
}