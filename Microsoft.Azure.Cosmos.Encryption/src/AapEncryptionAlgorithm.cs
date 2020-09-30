//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using Microsoft.Data.AAP_PH.Cryptography;

    /// <summary>
    /// Encryption Algorithm provided by AAP Encryption Package
    /// </summary>
    internal sealed class AapEncryptionAlgorithm : DataEncryptionKey
    {
        /// <summary>
        /// Data Encryption Key.
        /// </summary>
        private readonly byte[] rawDek;
        private readonly Data.AAP_PH.Cryptography.EncryptionAlgorithm aapEncryptionAlgorithm;

        public override byte[] RawKey => this.rawDek;

        public override string EncryptionAlgorithm => CosmosEncryptionAlgorithm.AapAEAes256CbcHmacSha256Randomized;

        /// <summary>
        /// Initializes a new instance of AapEncryptionAlgorithm.
        /// </summary>
        /// <param name="dekProperties"> Data Encryption Key Properties for the Encryption Key </param>
        /// <param name="rawDek"> Data Encryption Key </param>
        /// <param name="encryptionType"> Encryption type </param>
        /// <param name="encryptionKeyStoreProvider"> AAP Key Store Provider for Wrapping and UnWrapping </param>
        public AapEncryptionAlgorithm(
            DataEncryptionKeyProperties dekProperties,
            byte[] rawDek,
            Data.AAP_PH.Cryptography.EncryptionType encryptionType,
            EncryptionKeyStoreProvider encryptionKeyStoreProvider)
        {
            this.rawDek = rawDek;

            KeyEncryptionKey masterKey = new KeyEncryptionKey(
                dekProperties.EncryptionKeyWrapMetadata.Name,
                dekProperties.EncryptionKeyWrapMetadata.Value,
                encryptionKeyStoreProvider);

            Data.AAP_PH.Cryptography.DataEncryptionKey encryptionKey = Data.AAP_PH.Cryptography.DataEncryptionKey.GetOrCreate(
                dekProperties.Id,
                masterKey,
                dekProperties.WrappedDataEncryptionKey);

            this.aapEncryptionAlgorithm = Data.AAP_PH.Cryptography.EncryptionAlgorithm.GetOrCreate(
                encryptionKey,
                encryptionType);
        }

        /// <summary>
        /// Encrypt data using EncryptionAlgorithm
        /// </summary>
        /// <param name="plainText">Plaintext data to be encrypted</param>
        /// <returns>Returns the ciphertext corresponding to the plaintext.</returns>
        public override byte[] EncryptData(byte[] plainText)
        {
            return this.aapEncryptionAlgorithm.Encrypt(plainText);
        }

        /// <summary>
        /// Decryption steps
        /// 1. Validate version byte
        /// 2. Validate Authentication tag
        /// 3. Decrypt the message
        /// </summary>
        /// <param name="cipherText">CipherText data to be decrypted</param>
        /// <returns>Returns the plaintext corresponding to the cipherText.</returns>
        public override byte[] DecryptData(byte[] cipherText)
        {
            return this.aapEncryptionAlgorithm.Decrypt(cipherText);
        }
    }
}