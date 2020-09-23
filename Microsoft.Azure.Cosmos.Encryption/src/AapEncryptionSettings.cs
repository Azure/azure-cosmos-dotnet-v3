//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using Microsoft.Data.AAP_PH.Cryptography;

    internal class AapEncryptionSettings
    {
        internal MasterKey MasterKey { get; set; }

        internal EncryptionKey EncryptionKey { get; set; }

        internal Data.AAP_PH.Cryptography.EncryptionType EncryptionType { get; set; }

        internal EncryptionAlgorithm Algorithm { get; set; }

        internal static AapEncryptionSettings InitializeAapEncryptionAlogrithm(
            AapEncryptionSettings aapEncryptionSettingForKey,
            Data.AAP_PH.Cryptography.EncryptionType encryptionType)
        {
            return new AapEncryptionSettings()
            {
                EncryptionKey = aapEncryptionSettingForKey.EncryptionKey,
                EncryptionType = encryptionType,

                Algorithm = EncryptionAlgorithm.GetOrCreate(
                    aapEncryptionSettingForKey.EncryptionKey,
                    aapEncryptionSettingForKey.EncryptionType),
            };
        }
    }
}