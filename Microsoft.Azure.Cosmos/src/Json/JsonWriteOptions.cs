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
        /// <summary>
        /// No special options are applied. Uses default behavior for JSON writing.
        /// </summary>
        None = 0,

        /// <summary>
        /// Enables the writing of uniform number arrays and arrays containing uniform number arrays.
        /// </summary>
        EnableNumberArrays = 1 << 0,

        /// <summary>
        /// Enables support for writing 64-bit unsigned integers (UInt64).
        /// </summary>
        EnableUInt64 = 1 << 1,
    }
}
