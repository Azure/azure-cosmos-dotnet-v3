//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    internal class InMemoryRawDek
    {
        public EncryptionAlgorithm AlgorithmUsingRawDek { get; }

        public DateTime ExpiryTime { get; }

        public InMemoryRawDek(EncryptionAlgorithm algorithmUsingRawDek, TimeSpan clientCacheTimeToLive)
        {
            Debug.Assert(algorithmUsingRawDek != null);
            this.AlgorithmUsingRawDek = algorithmUsingRawDek;
            DateTime current = DateTime.UtcNow;
            this.ExpiryTime = current + clientCacheTimeToLive;
        }
    }
}