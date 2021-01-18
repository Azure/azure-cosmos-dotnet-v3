//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Diagnostics;

    internal sealed class CachedEncryptionSettings
    {
        public EncryptionSettings EncryptionSettings { get; }

        public DateTime EncryptionSettingsExpiryUtc { get; }

        public CachedEncryptionSettings(
            EncryptionSettings encryptionSettings,
            DateTime encryptionSettingsExpiryUtc)
        {
            this.EncryptionSettings = encryptionSettings ?? throw new ArgumentNullException(nameof(encryptionSettings));
            this.EncryptionSettingsExpiryUtc = encryptionSettingsExpiryUtc;
        }
    }
}