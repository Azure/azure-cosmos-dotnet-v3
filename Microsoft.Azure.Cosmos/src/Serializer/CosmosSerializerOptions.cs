//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// This class provides a way to configure basic
    /// serializer settings.
    /// </summary>
    public struct CosmosSerializerOptions
    {
        /// <summary>
        /// Get's if the serializer should ignore null properties
        /// </summary>
        public bool IgnoreNullValues { get; set; }

        /// <summary>
        /// Get's if the serializer should ignore null properties
        /// </summary>
        public bool Indented { get; set; }

        /// <summary>
        /// The naming policy of the serializer. This is used to configure
        /// camel casing
        /// </summary>
        public CosmosNamingPolicy PropertyNamingPolicy { get; set; }
    }
}
