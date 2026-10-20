//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// Partitioning version.
    /// </summary> 
#if COSMOSCLIENT
    internal
#else
    public
#endif
     enum PartitionKeyDefinitionVersion
    {
        /// <summary>
        /// Hash partitioning scheme optimized for partition keys that are up to 100 bytes size.
        /// </summary>
        V1 = 1,

        /// <summary>
        /// Enhanced version of hash partitioning scheme, which supports partition key up to 2KB size. This is the  
        /// preferred hash function for partition keys with high cardinality. 
        /// </summary>
        /// <remarks>Collections created with V2 version can only be accessed from version 1.18 and above.</remarks>
        V2 = 2,
    }
}
