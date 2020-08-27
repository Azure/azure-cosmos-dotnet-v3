//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{   
    internal enum ContentSerializationFormat
    {
        /// <summary>
        /// Standard JSON RFC UTF-8 text.
        /// </summary>
        JsonText,

        /// <summary>
        /// Custom binary for Cosmos DB that encodes a superset of JSON values.
        /// </summary>
        CosmosBinary,

        /// <summary>
        /// Set the serialization format to HybridRow.
        /// </summary>
        HybridRow,
    }
}
