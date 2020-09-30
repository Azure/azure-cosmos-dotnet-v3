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
        private readonly byte[] rawDek;
        private readonly EncryptionAlgorithm aapEncryptionAlgorithm;

        public override byte[] RawKey => this.rawDek;

        public override string EncryptionAlgorithm => CosmosEncryptionAlgorithm.AapAEAes256CbcHmacSha256Randomized;

        /// <summary>
        /// Initializes a new instance of AapEncryptionAlgorithm.
        /// </summary>
        /// <param name="dekProperties"> Data Encryption Key properties</param>
        /// <param name="rawDek"> Raw bytes of Data Encryption Key </param>
        /// <param name="encryptionType"> Encryption type </param>
        /// <param name="encryptionKeyStoreProvider"> EncryptionKeyStoreProvider for wrapping and unwrapping </param>
        public AapEncryptionAlgorithm(
            DataEncryptionKeyProperties dekProperties,
            byte[] rawDek,
            Data.AAP_PH.Cryptography.EncryptionType encryptionType,
            EncryptionKeyStoreProvider encryptionKeyStoreProvider)
        {
            this.rawDek = rawDek;

            KeyEncryptionKey masterKey = KeyEncryptionKey.GetOrCreate(
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
        /// Decrypt data using EncryptionAlgorithm
        /// </summary>
        /// <param name="cipherText">CipherText data to be decrypted</param>
        /// <returns>Returns the plaintext corresponding to the cipherText.</returns>
        public override byte[] DecryptData(byte[] cipherText)
        {
            return this.aapEncryptionAlgorithm.Decrypt(cipherText);
        }
    }
}