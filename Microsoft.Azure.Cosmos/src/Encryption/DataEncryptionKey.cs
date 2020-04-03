//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Abstraction for a data encryption key for use in client-side encryption.
    /// See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    abstract class DataEncryptionKey
    {
        /// <summary>
        /// Raw key bytes of the data encryption key.
        /// </summary>
        public abstract byte[] RawKey { get; }

        /// <summary>
        /// Encryption algorithm to be used with this data encryption key.
        /// </summary>
        public abstract CosmosEncryptionAlgorithm EncryptionAlgorithm { get; }

        /// <summary>
        /// Encrypts the plainText with a data encryption key.
        /// </summary>
        /// <param name="plainText">Plain text value to be encrypted.</param>
        /// <returns>Encrypted value.</returns>
        public abstract byte[] EncryptData(byte[] plainText);

        /// <summary>
        /// Decrypts the cipherText with a data encryption key.
        /// </summary>
        /// <param name="cipherText">Ciphertext value to be decrypted.</param>
        /// <returns>Plain text.</returns>
        public abstract byte[] DecryptData(byte[] cipherText);

        /// <summary>
        /// Creates a new instance of data encryption key given the raw key bytes
        /// suitable for use with the provided encryption algorithm.
        /// </summary>
        /// <param name="rawKey">Raw key bytes.</param>
        /// <param name="encryptionAlgorithm">Encryption algorithm the returned key is intended to be used with.</param>
        /// <returns>New instance of data encryption key.</returns>
        public static DataEncryptionKey Create(
            byte[] rawKey,
            CosmosEncryptionAlgorithm encryptionAlgorithm)
        {
            if (rawKey == null)
            {
                throw new ArgumentNullException(nameof(rawKey));
            }

            if (encryptionAlgorithm != CosmosEncryptionAlgorithm.AE_AES_256_CBC_HMAC_SHA_256_RANDOMIZED)
            {
                throw new ArgumentException($"Encryption algorithm not supported: {encryptionAlgorithm}", nameof(encryptionAlgorithm));
            }

            AeadAes256CbcHmac256EncryptionKey aeKey = new AeadAes256CbcHmac256EncryptionKey(rawKey, AeadAes256CbcHmac256Algorithm.AlgorithmNameConstant);
            return new AeadAes256CbcHmac256Algorithm(aeKey, EncryptionType.Randomized, algorithmVersion: 1);
        }
    }
}