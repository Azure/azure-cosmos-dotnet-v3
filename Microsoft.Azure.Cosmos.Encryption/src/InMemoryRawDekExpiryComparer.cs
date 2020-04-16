//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System.Collections.Generic;

    internal sealed class InMemoryRawDekExpiryComparer : IComparer<InMemoryRawDek>
    {
        public int Compare(InMemoryRawDek dek1, InMemoryRawDek dek2)
        {
            return dek1.RawDekExpiry.CompareTo(dek2.RawDekExpiry);
        }
    }
}