//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    internal sealed class CachedDekProperties : DataEncryptionKeyProperties
    {
        public byte[] RawDek { get; set; }

        public CachedDekProperties(DataEncryptionKeyProperties dekProperties, byte[] rawDek = null)
            : base(dekProperties.Id,
                  dekProperties.WrappedDataEncryptionKey,
                  dekProperties.KeyWrapMetadata,
                  dekProperties.ClientCacheTimeToLive)
        {
            this.RawDek = rawDek;
        }

        public CachedDekProperties(
            string id,
            byte[] wrappedDataEncryptionKey,
            KeyWrapMetadata keyWrapMetadata,
            TimeSpan clientCacheTimeToLive)
            : base(id, wrappedDataEncryptionKey, keyWrapMetadata, clientCacheTimeToLive)
        {
        }
    }
}
