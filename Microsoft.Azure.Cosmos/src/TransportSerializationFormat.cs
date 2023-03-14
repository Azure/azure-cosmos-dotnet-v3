// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Specifies the serialization format for query response.
    /// </summary>
    public enum TransportSerializationFormat
    {
        /// <summary>
        /// Indicates binary format to be used for transport serialization.
        /// </summary>
        Binary,

        /// <summary>
        /// Indicates text format to be used for transport serialization.
        /// </summary>
        Text, 
    }
}
