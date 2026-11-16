//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    internal enum ReadMode
    {
        Primary, // Test hook
        Strong,
        BoundedStaleness,
        Any
    }
}
