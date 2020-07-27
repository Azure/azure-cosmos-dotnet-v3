//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Diagnostics;

    internal sealed class CachedDekProperties
    {
        public DataEncryptionKeyProperties ServerProperties { get; }

        public DateTime ServerPropertiesExpiryUtc { get; }

        public CachedDekProperties(
            DataEncryptionKeyProperties serverProperties,
            DateTime serverPropertiesExpiryUtc)
        {
            Debug.Assert(serverProperties != null);

            this.ServerProperties = serverProperties;
            this.ServerPropertiesExpiryUtc = serverPropertiesExpiryUtc;
        }
    }
}
