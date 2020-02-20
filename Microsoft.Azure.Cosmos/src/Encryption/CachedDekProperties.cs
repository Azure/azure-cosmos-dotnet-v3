//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;

    internal sealed class CachedDekProperties
    {
        public string DatabaseId { get; }

        public DataEncryptionKeyProperties ServerProperties { get;  }

        public DateTime ServerPropertiesExpiryUtc { get; }

        public CachedDekProperties(
            string databaseId,
            DataEncryptionKeyProperties serverProperties,
            DateTime serverPropertiesExpiryUtc)
        {
            Debug.Assert(databaseId != null);
            Debug.Assert(serverProperties != null);

            this.DatabaseId = databaseId;
            this.ServerProperties = serverProperties;
            this.ServerPropertiesExpiryUtc = serverPropertiesExpiryUtc;
        }
    }
}
