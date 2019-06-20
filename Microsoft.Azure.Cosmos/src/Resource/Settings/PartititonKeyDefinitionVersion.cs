//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Partitioning version.
    /// </summary> 
    public enum PartitionKeyDefinitionVersion
    {
        /// <summary>
        /// Original version of hash partitioning.
        /// </summary>
        V1 = 1,

        /// <summary>
        /// Enhanced version of hash partitioning - offers better distribution of long partition keys and uses less storage.
        /// </summary>
        /// <remarks>This version should be used for any practical purpose, but it is available in newer SDKs only.</remarks>
        V2 = 2,
    }
}
