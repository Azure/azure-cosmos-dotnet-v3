//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    /// <summary>
    /// API for JSON processing
    /// </summary>
    public enum JsonProcessor
    {
        /// <summary>
        /// Newtonsoft.Json
        /// </summary>
        Newtonsoft,

#if NET8_0_OR_GREATER
        /// <summary>
        /// Ut8JsonReader/Writer
        /// </summary>
        /// <remarks>Available with .NET8.0 package only.</remarks>
        Stream,
#endif
    }
}