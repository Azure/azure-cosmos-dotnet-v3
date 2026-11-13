//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// Specifies the supported byok status in the Azure Cosmos DB service.
    /// </summary> 
internal enum ByokStatus
    {
        /// <summary>
        /// Represents byok is not enabled on collection.
        /// </summary>
        None,

        /// <summary>
        /// Represents byok is enabled on collection.
        /// </summary>
        Active
    }
}