// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    internal enum TypeMarker : byte
    {
        Null = 1, // not used
        String = 2,
        Double = 3,
        Long = 4,
        Boolean = 5,
        Array = 6,
        Object = 7,
    }
}
