//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System;

namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// Flags enum representing binary encoding feature extensions
    /// supported by the client. These features are appended to the
    /// CosmosBinary format in the SupportedSerializationFormats header
    /// with '+' delimiters.
    /// Example: "CosmosBinary+Base64Strings+NumberArrays+UInt64"
    /// </summary>
    [Flags]
    internal enum SupportedSerializationFeatures : ushort
    {
        None = 0,

        /// <summary>
        /// Support for Base64-encoded strings in binary encoding.
        /// </summary>
        Base64Strings = 1 << 0,

        /// <summary>
        /// Support for typed number arrays in binary encoding.
        /// </summary>
        NumberArrays = 1 << 1,

        /// <summary>
        /// Support for unsigned 64-bit integers in binary encoding.
        /// </summary>
        UInt64 = 1 << 2,
    }
}
