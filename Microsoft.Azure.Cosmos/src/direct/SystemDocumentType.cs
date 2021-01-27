//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    internal enum SystemDocumentType
    {
        /// <summary>
        /// Set the SystemDocumentType to PartitionKey
        /// Partitioned SystemDocument
        /// </summary>
        PartitionKey,

        /// <summary>
        /// Set the SystemDocumentType as MaterializedViewLeaseDocument
        /// Partitioned SystemDocument
        /// </summary>
        MaterializedViewLeaseDocument,
        
        /// <summary>
        /// Set the SystemDocumentType as MaterializedViewBuilderOwnershipDocument
        /// NonPartitionedSystemDocument
        /// </summary>
        MaterializedViewBuilderOwnershipDocument,
        
        /// <summary>
        /// Set the SystemDocumentType as MaterializedViewLeaseStoreInitDocument
        /// PartitionedSystemDocument
        /// </summary>
        MaterializedViewLeaseStoreInitDocument,
    }
}
