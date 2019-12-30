//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    internal class CachedDekProperties
    {
        public string DatabaseId { get; }

        public DataEncryptionKeyProperties ServerProperties { get;  }

        public DateTime ServerPropertiesExpiry { get; }

        public CachedDekProperties(string databaseId, DataEncryptionKeyProperties serverProperties, DateTime serverPropertiesExpiry)
        {
            this.DatabaseId = databaseId;
            this.ServerProperties = serverProperties;
            this.ServerPropertiesExpiry = serverPropertiesExpiry;
        }
    }
}
