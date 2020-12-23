//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using Microsoft.Data.Encryption.Cryptography;

    internal class MdeEncryptionSettings
    {
        public string ClientEncryptionKeyId { get; set; }

        public DateTime MdeEncryptionSettingsExpiry { get; set; }

        public Data.Encryption.Cryptography.DataEncryptionKey DataEncryptionKey { get; set; }

        public AeadAes256CbcHmac256EncryptionAlgorithm AeadAes256CbcHmac256EncryptionAlgorithm { get; set; }

        public Data.Encryption.Cryptography.EncryptionType EncryptionType { get; set; }

        public MdeEncryptionSettings()
        {
        }

        internal static MdeEncryptionSettings Create(
            MdeEncryptionSettings settingsForKey,
            Data.Encryption.Cryptography.EncryptionType encryptionType)
        {
            return new MdeEncryptionSettings()
            {
                ClientEncryptionKeyId = settingsForKey.ClientEncryptionKeyId,
                DataEncryptionKey = settingsForKey.DataEncryptionKey,
                EncryptionType = encryptionType,
                MdeEncryptionSettingsExpiry = DateTime.UtcNow + TimeSpan.FromMinutes(30),
                AeadAes256CbcHmac256EncryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(
                settingsForKey.DataEncryptionKey,
                encryptionType),
            };
        }
    }
}