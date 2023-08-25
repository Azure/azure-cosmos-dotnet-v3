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

        /// <summary>
        /// Initializes a new instance of the <see cref="PreblittedBinaryJsonScope"/> struct.
        /// </summary>
        /// <param name="bytes">Preblitted binary json bytes.</param>
        public PreblittedBinaryJsonScope(ReadOnlyMemory<byte> bytes)
        {
            this.Bytes = bytes;
        }
    }
}