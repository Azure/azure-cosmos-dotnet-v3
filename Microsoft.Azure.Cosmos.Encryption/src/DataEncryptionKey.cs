//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    /// <summary>
    /// Abstraction for a data encryption key for use in client-side encryption.
    /// See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
    public abstract class DataEncryptionKey
    {
        /// <summary>
        /// Gets raw key bytes of the data encryption key.
        /// </summary>
        public abstract byte[] RawKey { get; }

        /// <summary>
        /// Gets Encryption algorithm to be used with this data encryption key.
        /// </summary>
        public abstract string EncryptionAlgorithm { get; }

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
        /// Generates raw data encryption key bytes suitable for use with the provided encryption algorithm.
        /// </summary>
        /// <param name="encryptionAlgorithm">Encryption algorithm the returned key is intended to be used with.</param>
        /// <returns>New instance of data encryption key.</returns>
        public static byte[] Generate(string encryptionAlgorithm)
        {
            if ((encryptionAlgorithm != CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized) && (encryptionAlgorithm != CosmosEncryptionAlgorithm.AEADAes256CbcHmacSha256Deterministic))
            {
                throw new ArgumentException($"Encryption algorithm not supported: {encryptionAlgorithm}", nameof(encryptionAlgorithm));
            }

            byte[] rawKey = new byte[32];
            SecurityUtility.GenerateRandomBytes(rawKey);
            return rawKey;
        }

        /// <summary>
        /// Creates a new instance of data encryption key given the raw key bytes
        /// suitable for use with the provided encryption algorithm.
        /// </summary>
        /// <param name="rawKey">Raw key bytes.</param>
        /// <param name="encryptionAlgorithm">Encryption algorithm the returned key is intended to be used with.</param>
        /// <returns>New instance of data encryption key.</returns>
        public static DataEncryptionKey Create(
            byte[] rawKey,
            string encryptionAlgorithm)
        {
            if (rawKey == null)
            {
                throw new ArgumentNullException(nameof(rawKey));
            }

            switch (encryptionAlgorithm)
            {
                case CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized:
                    AeadAes256CbcHmac256EncryptionKey aeKey = new AeadAes256CbcHmac256EncryptionKey(rawKey, AeadAes256CbcHmac256Algorithm.AlgorithmNameConstant);
                    return new AeadAes256CbcHmac256Algorithm(aeKey, EncryptionType.Randomized, algorithmVersion: 1);

                case CosmosEncryptionAlgorithm.AEADAes256CbcHmacSha256Deterministic:
                    AeadAes256CbcHmac256EncryptionKey aedKey = new AeadAes256CbcHmac256EncryptionKey(rawKey, AeadAes256CbcHmac256Algorithm.AlgorithmNameConstant);
                    return new AeadAes256CbcHmac256Algorithm(aedKey, EncryptionType.Deterministic, algorithmVersion: 1);

                default:
                    throw new ArgumentException($"Encryption algorithm not supported: {encryptionAlgorithm}", nameof(encryptionAlgorithm));
            }
        }
    }
}