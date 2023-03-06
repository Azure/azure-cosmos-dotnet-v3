// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Defines serialization format for query response
    /// </summary>
    /// <remarks>
    /// Requests query response over the wire in specified serialization format. 
    /// </remarks>
    public enum TransportSerializationFormat
    {
        /// <summary>
        /// Plain text
        /// </summary>
        JsonText,

        /// <summary>
        /// Binary Encoding
        /// </summary>
        CosmosBinary,
    }
}
