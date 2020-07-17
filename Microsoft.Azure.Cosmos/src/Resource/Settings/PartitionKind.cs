//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Partitioning version.
    /// </summary> 
    public enum PartitionKind
    {
        /// <summary>
        /// Original version of hash partitioning.
        /// </summary>
        Hash = 1,

        /// <summary>
        /// Range partitioning 
        /// </summary>
        Range = 2,

        /// <summary>
        /// Enhanced version of hash partitioning - Offers partitioning over multiple paths.
        /// </summary>
        /// <remarks>This version is available in newer SDKs only.</remarks>
        MultiHash = 3,
        
    }
}
