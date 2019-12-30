//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    internal class InMemoryRawDek
    {
        public byte[] RawDek { get; }

        public EncryptionAlgorithm AlgorithmUsingRawDek { get; }

        public DateTime RawDekExpiry { get; }

        public InMemoryRawDek(byte[] rawDek, EncryptionAlgorithm algorithmUsingRawDek, TimeSpan clientCacheTimeToLive)
        {
            this.RawDek = rawDek;
            this.AlgorithmUsingRawDek = algorithmUsingRawDek;
            this.RawDekExpiry = DateTime.UtcNow + clientCacheTimeToLive;
        }
    }
}
