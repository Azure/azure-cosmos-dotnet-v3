//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if AZURECORE
namespace Azure.Cosmos
#else
namespace Microsoft.Azure.Cosmos
#endif
{
    /// <summary>
    /// These are the operation types resulted in a version conflict on a resource. 
    /// </summary>
    /// <remarks>
    /// When a version conflict occurs during an async operation, retrieving the ConflictProperties instance will allow you 
    /// to determine which resource and operation cause the conflict.
    /// </remarks>
    public enum OperationKind
    {
        /// <summary>
        /// An invalid operation.
        /// </summary>
        Invalid,

        /// <summary>
        /// A create operation.
        /// </summary>
        Create,

        /// <summary>
        /// An replace operation.
        /// </summary>
        Replace,

        /// <summary>
        /// A delete operation.
        /// </summary>
        Delete
    }
}
