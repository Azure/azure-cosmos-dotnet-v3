//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    internal enum ItemType
    {
        NoValue = 0x0,
        Null = 0x1,
        Bool = 0x2,
        Number = 0x4,
        String = 0x5
    }
}
