//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;

    internal sealed class InMemoryRawDekComparer : IComparer<InMemoryRawDek>
    {
        public int Compare(InMemoryRawDek dek1, InMemoryRawDek dek2)
        {
            return dek1.ExpiryTime.CompareTo(dek2.ExpiryTime);
        }
    }
}