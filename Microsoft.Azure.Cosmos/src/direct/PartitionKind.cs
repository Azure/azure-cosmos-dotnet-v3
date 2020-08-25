//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// These are the partitioning types available for a partition key definition in the Azure Cosmos DB service.
    /// </summary> 
    /// <remarks>Only PartitionKind.Hash is supported at this time.</remarks>
    internal enum PartitionKind
    {
        /// <summary>
        /// The partition key definition path is hashed.
        /// </summary>
        Hash,

        /// <summary>
        /// The partition key definition path is ordered.
        /// </summary>
        Range,

        /// <summary>
        /// The partition key definition path has >1 entries, individual values are hashed and concatenated together to generate a single EPK.
        /// </summary>
        MultiHash,

    }
}