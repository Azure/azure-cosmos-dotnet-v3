//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
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
        public static async Task<MdeEncryptionAlgorithm> CreateAsync(
            DataEncryptionKeyProperties dekProperties,
            Data.Encryption.Cryptography.EncryptionType encryptionType,
            EncryptionKeyStoreProvider encryptionKeyStoreProvider,
            TimeSpan? cacheTimeToLive,
            bool withRawKey,
            CancellationToken cancellationToken)
        {
            if (dekProperties == null)
            {
                throw new ArgumentNullException(nameof(dekProperties));
            }

            if (encryptionKeyStoreProvider == null)
            {
                throw new ArgumentNullException(nameof(encryptionKeyStoreProvider));
            }

            KeyEncryptionKey keyEncryptionKey = KeyEncryptionKey.GetOrCreate(
                dekProperties.EncryptionKeyWrapMetadata.Name,
                dekProperties.EncryptionKeyWrapMetadata.Value,
                encryptionKeyStoreProvider);

            AeadAes256CbcHmac256EncryptionAlgorithm aeadAes256CbcHmac256EncryptionAlgorithm;
            byte[] rawKey = null;

            if (!withRawKey)
            {
                ProtectedDataEncryptionKey protectedDataEncryptionKey = cacheTimeToLive.HasValue && cacheTimeToLive.Value == TimeSpan.Zero
                    ? await ProtectedDataEncryptionKey.CreateAsync(
                        dekProperties.Id,
                        keyEncryptionKey,
                        dekProperties.WrappedDataEncryptionKey,
                        cancellationToken)
                    : await ProtectedDataEncryptionKey.GetOrCreateAsync(
                        dekProperties.Id,
                        keyEncryptionKey,
                        dekProperties.WrappedDataEncryptionKey,
                        cancellationToken);
                aeadAes256CbcHmac256EncryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(
                    protectedDataEncryptionKey,
                    encryptionType,
                    Version);
            }
            else
            {
                rawKey = await keyEncryptionKey.DecryptEncryptionKeyAsync(dekProperties.WrappedDataEncryptionKey, cancellationToken).ConfigureAwait(false);
                PlaintextDataEncryptionKey plaintextDataEncryptionKey = cacheTimeToLive.HasValue && (cacheTimeToLive.Value == TimeSpan.Zero)
                    ? new PlaintextDataEncryptionKey(
                            dekProperties.Id,
                            rawKey)
                    : PlaintextDataEncryptionKey.GetOrCreate(
                           dekProperties.Id,
                           rawKey);
                aeadAes256CbcHmac256EncryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(
                    plaintextDataEncryptionKey,
                    encryptionType,
                    Version);
            }

            return new MdeEncryptionAlgorithm(aeadAes256CbcHmac256EncryptionAlgorithm, rawKey);
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

        private MdeEncryptionAlgorithm(AeadAes256CbcHmac256EncryptionAlgorithm aeadAes256CbcHmac256EncryptionAlgorithm, byte[] rawKey)
        {
            this.mdeAeadAes256CbcHmac256EncryptionAlgorithm = aeadAes256CbcHmac256EncryptionAlgorithm ?? throw new ArgumentNullException(nameof(aeadAes256CbcHmac256EncryptionAlgorithm));
            this.RawKey = rawKey;
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