//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#if INTERNAL
namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Partition kind.
    /// </summary> 
    public enum PartitionKind
    {
        /// <summary>
        /// Original and default Partition Kind. 
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
#endif