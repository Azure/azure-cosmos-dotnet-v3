//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    internal sealed class InMemoryDekProperties : DataEncryptionKeyProperties
    {
        public byte[] RawDek { get; }

        public TimeSpan CacheTimeToLive { get; }

        public InMemoryDekProperties(DataEncryptionKeyProperties dekProperties, byte[] rawDek = null, TimeSpan cacheTtl = default)
            : base(dekProperties.Id,
                  dekProperties.WrappedDataEncryptionKey,
                  dekProperties.KeyWrapMetadata)
        {
            this.RawDek = rawDek;
            this.CacheTimeToLive = cacheTtl;
        }
    }
}
