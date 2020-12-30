//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Encryption.Cryptography;

    internal sealed class CachedEncryptionSettings
    {
        public MdeEncryptionSettings MdeEncryptionSettings { get; }

        public DateTime MdeEncryptionSettingsExpiryUtc { get; }

        public CachedEncryptionSettings(
            MdeEncryptionSettings mdeEncryptionSettings,
            DateTime mdeEncryptionSettingsExpiryUtc)
        {
            Debug.Assert(mdeEncryptionSettings != null);

            this.MdeEncryptionSettings = mdeEncryptionSettings;
            this.MdeEncryptionSettingsExpiryUtc = mdeEncryptionSettingsExpiryUtc;
        }
    }
}