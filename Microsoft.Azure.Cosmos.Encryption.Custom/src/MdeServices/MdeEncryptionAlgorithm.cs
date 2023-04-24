//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using Microsoft.Data.Encryption.Cryptography;

    /// <summary>
    /// Encryption Algorithm provided by MDE Encryption Package
    /// </summary>
    internal sealed class MdeEncryptionAlgorithm : DataEncryptionKey
    {
        private readonly AeadAes256CbcHmac256EncryptionAlgorithm mdeAeadAes256CbcHmac256EncryptionAlgorithm;

        private readonly byte[] unwrapKey;

        // unused for MDE Algorithm.
        public override byte[] RawKey { get; }

        public override string EncryptionAlgorithm => CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized;

        /// <summary>
        /// Initializes a new instance of MdeEncryptionAlgorithm.
        /// Uses <see cref="AeadAes256CbcHmac256EncryptionAlgorithm"/> which implements authenticated encryption algorithm with associated data as described
        /// <see href="http://tools.ietf.org/html/draft-mcgrew-aead-aes-cbc-hmac-sha2-05">here</see> .
        /// More specifically this implements AEAD_AES_256_CBC_HMAC_SHA256 algorithm.
        /// </summary>
        /// <param name="dekProperties"> Data Encryption Key properties</param>
        /// <param name="encryptionType"> Encryption type </param>
        /// <param name="encryptionKeyStoreProvider"> EncryptionKeyStoreProvider for wrapping and unwrapping </param>
        public MdeEncryptionAlgorithm(
            DataEncryptionKeyProperties dekProperties,
            Data.Encryption.Cryptography.EncryptionType encryptionType,
            EncryptionKeyStoreProvider encryptionKeyStoreProvider,
            TimeSpan? cacheTimeToLive,
            bool withRawKey=false)
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

            if (!withRawKey)
            {
                ProtectedDataEncryptionKey protectedDataEncryptionKey;
                if (cacheTimeToLive.HasValue)
                {
                    // no caching
                    if (cacheTimeToLive.Value == TimeSpan.Zero)
                    {
                        protectedDataEncryptionKey = new ProtectedDataEncryptionKey(
                            dekProperties.Id,
                            keyEncryptionKey,
                            dekProperties.WrappedDataEncryptionKey);
                    }
                    else
                    {
                        protectedDataEncryptionKey = ProtectedDataEncryptionKey.GetOrCreate(
                           dekProperties.Id,
                           keyEncryptionKey,
                           dekProperties.WrappedDataEncryptionKey);
                    }
                }
                else
                {
                    protectedDataEncryptionKey = ProtectedDataEncryptionKey.GetOrCreate(
                           dekProperties.Id,
                           keyEncryptionKey,
                           dekProperties.WrappedDataEncryptionKey);
                }

                this.mdeAeadAes256CbcHmac256EncryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(
                    protectedDataEncryptionKey,
                    encryptionType);
            }
            else
            {
                byte[] rawKey = keyEncryptionKey.DecryptEncryptionKey(dekProperties.WrappedDataEncryptionKey);
                PlaintextDataEncryptionKey plaintextDataEncryptionKey;
                if (cacheTimeToLive.HasValue)
                {
                    // no caching
                    if (cacheTimeToLive.Value == TimeSpan.Zero)
                    {
                        plaintextDataEncryptionKey = new PlaintextDataEncryptionKey(
                            dekProperties.Id,
                            rawKey);
                    }
                    else
                    {
                        plaintextDataEncryptionKey = PlaintextDataEncryptionKey.GetOrCreate(
                           dekProperties.Id,
                           rawKey);
                    }
                }
                else
                {
                    plaintextDataEncryptionKey = PlaintextDataEncryptionKey.GetOrCreate(
                           dekProperties.Id,
                           rawKey);
                }

                this.RawKey = rawKey;
                this.mdeAeadAes256CbcHmac256EncryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(
                    plaintextDataEncryptionKey,
                    encryptionType);

            }


        }

        /// <summary>
        /// Initializes a new instance of MdeEncryptionAlgorithm.
        /// Uses <see cref="AeadAes256CbcHmac256EncryptionAlgorithm"/> which implements authenticated encryption algorithm with associated data as described
        /// <see href="http://tools.ietf.org/html/draft-mcgrew-aead-aes-cbc-hmac-sha2-05">here</see> .
        /// More specifically this implements AEAD_AES_256_CBC_HMAC_SHA256 algorithm.
        /// </summary>
        /// <param name="dataEncryptionKey"> Data Encryption Key </param>
        /// <param name="encryptionType"> Encryption type </param>
        public MdeEncryptionAlgorithm(
            byte[] rawkey,
            Data.Encryption.Cryptography.DataEncryptionKey dataEncryptionKey,
            Data.Encryption.Cryptography.EncryptionType encryptionType)
        {
            this.RawKey = rawkey;
            this.mdeAeadAes256CbcHmac256EncryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(
                dataEncryptionKey,
                encryptionType);
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
    }
}