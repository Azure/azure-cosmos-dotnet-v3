// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements
{
#nullable enable

    /// <summary>
    /// An enum that describes the kind of number represented by a cosmos number
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    enum CosmosNumberType
    {
        /// <summary>
        /// A Json number where integers and floating points are interchangeably used.
        /// </summary>
        Number64 = 0,

        /// <summary>
        /// A single byte signed integer type.
        /// </summary>
        Int8,

        /// <summary>
        /// A 2 byte signed integer type.
        /// </summary>
        Int16,

        /// <summary>
        /// A 4 byte signed integer type.
        /// </summary>
        Int32,

        /// <summary>
        /// An 8 byte signed integer type.
        /// </summary>
        Int64,

        /// <summary>
        /// A 4 byte unsigned integer type.
        /// </summary>
        UInt32,

        /// <summary>
        /// A 4 byte floating point type.
        /// </summary>
        Float32,

        /// <summary>
        /// An 8 byte floating point type.
        /// </summary>
        Float64,
    }
}