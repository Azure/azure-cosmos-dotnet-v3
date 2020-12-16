// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;

    /// <summary>
    /// Holds a blitted binary JSON blob.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    readonly struct PreblittedBinaryJsonScope
    {
        /// <summary>
        /// Blitted bytes.
        /// </summary>
        public readonly ReadOnlyMemory<byte> Bytes;

        public PreblittedBinaryJsonScope(ReadOnlyMemory<byte> bytes)
        {
            this.Bytes = bytes;
        }
    }
}