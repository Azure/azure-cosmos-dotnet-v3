//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;

    internal class InMemoryRawDek
    {
        public DataEncryptionKey DataEncryptionKey { get; }

        public DateTime RawDekExpiry { get; }

        public InMemoryRawDek(DataEncryptionKey dataEncryptionKey, TimeSpan clientCacheTimeToLive, DateTime? utcNow = null)
        {
            this.DataEncryptionKey = dataEncryptionKey;
            this.RawDekExpiry = (utcNow ?? DateTime.UtcNow) + clientCacheTimeToLive;
        }
    }
}
