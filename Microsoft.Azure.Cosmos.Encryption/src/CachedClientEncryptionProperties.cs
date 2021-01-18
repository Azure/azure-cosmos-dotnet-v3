// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Diagnostics;

    internal sealed class CachedClientEncryptionProperties
    {
        public ClientEncryptionKeyProperties ClientEncryptionKeyProperties { get; }

        public DateTime ClientEncryptionKeyPropertiesExpiryUtc { get; }

        public CachedClientEncryptionProperties(
            ClientEncryptionKeyProperties clientEncryptionKeyProperties,
            DateTime clientEncryptionKeyPropertiesExpiryUtc)
        {
            this.ClientEncryptionKeyProperties = clientEncryptionKeyProperties ?? throw new ArgumentNullException(nameof(clientEncryptionKeyProperties));
            this.ClientEncryptionKeyPropertiesExpiryUtc = clientEncryptionKeyPropertiesExpiryUtc;
        }
    }
}
