//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using Microsoft.Data.AAP_PH.Cryptography;

    /// <summary>
    /// This Class uses the Encryption Algorithm provided by AAP  Encryption Package
    /// </summary>
    internal class AapEncryptionAlgorithm : DataEncryptionKey
    {
        /// <summary>
        /// Data Encryption Key.
        /// </summary>
        private readonly byte[] rawDek;

        public override byte[] RawKey => this.rawDek;

        public override string EncryptionAlgorithm => CosmosEncryptionAlgorithm.AapAEAes256CbcHmacSha256Randomized;

        private readonly AapEncryptionSettings encryptionSettings;

        /// <summary>
        /// Initializes a new instance of AapEncryptionAlgorithm algorithm with a given key and encryption type and Wrap Provider.
        /// </summary>
        /// <param name="dekProperties"> Data Encryption Key Properties for the Encryption Key </param>
        /// <param name="rawDek"> Data Encryption Key </param>
        /// <param name="encryptionType"> Randomized Encryption </param>
        /// <param name="encryptionKeyStoreProvider"> AAP Key Store Provider for Wrapping and UnWrapping </param>
        internal AapEncryptionAlgorithm(
            DataEncryptionKeyProperties dekProperties,
            byte[] rawDek,
            Data.AAP_PH.Cryptography.EncryptionType encryptionType,
            EncryptionKeyStoreProvider encryptionKeyStoreProvider)
        {
            this.rawDek = rawDek;

            MasterKey masterKey = new MasterKey(
                dekProperties.EncryptionKeyWrapMetadata.Name,
                dekProperties.EncryptionKeyWrapMetadata.Value,
                encryptionKeyStoreProvider);

            EncryptionKey encryptionKey = new EncryptionKey(dekProperties.Id, masterKey, dekProperties.WrappedDataEncryptionKey);

            AapEncryptionSettings aapEncryptionSettingForKey = new AapEncryptionSettings
            {
                EncryptionKey = encryptionKey,
                MasterKey = masterKey,
            };

            this.encryptionSettings = AapEncryptionSettings.InitializeAapEncryptionAlogrithm(aapEncryptionSettingForKey, encryptionType);
        }

        /// <summary>
        /// Encryption Algorithm
        /// </summary>
        /// <param name="plainText">Plaintext data to be encrypted</param>
        /// <returns>Returns the ciphertext corresponding to the plaintext.</returns>
        public override byte[] EncryptData(byte[] plainText)
        {
            return this.encryptionSettings.Algorithm.EncryptData(plainText);
        }

        /// <summary>
        /// Decryption steps
        /// 1. Validate version byte
        /// 2. Validate Authentication tag
        /// 3. Decrypt the message
        /// </summary>
        public override byte[] DecryptData(byte[] cipherText)
        {
            return this.encryptionSettings.Algorithm.DecryptData(cipherText);
        }
    }
}