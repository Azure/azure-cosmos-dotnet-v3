//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;

    internal class InMemoryRawDek
    {
        public DataEncryptionKey DataEncryptionKey { get; }

        public DateTime RawDekExpiry { get; }

        public InMemoryRawDek(DataEncryptionKey dataEncryptionKey, TimeSpan clientCacheTimeToLive)
        {
            this.DataEncryptionKey = dataEncryptionKey;
            this.RawDekExpiry = DateTime.UtcNow + clientCacheTimeToLive;
        }
    }
}