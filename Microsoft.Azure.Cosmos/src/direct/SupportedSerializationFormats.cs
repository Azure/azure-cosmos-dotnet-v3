//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System;

namespace Microsoft.Azure.Documents
{
    [Flags]
    internal enum SupportedSerializationFormats
    {
        None = 0,

        /// <summary>
        /// Standard JSON RFC UTF-8 text.
        /// </summary>
        JsonText = 1 << 0,

        /// <summary>
        /// Custom binary for Cosmos DB that encodes a superset of JSON values.
        /// </summary>
        CosmosBinary = 1 << 1,

        /// <summary>
        /// HybridRow format.
        /// </summary>
        HybridRow = 1 << 2,
    }
}
