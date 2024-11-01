//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;

    /// <summary>
    /// Specifies options for configuring the behavior of JSON writing.
    /// </summary>
    [FlagsAttribute]
#if INTERNAL
    public
#else
    internal
#endif
    enum JsonWriteOptions
    {
        None = 0,
        EnableNumberArrays = 1 << 0,
        EnableUInt64 = 1 << 1,
    }
}
