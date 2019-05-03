//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System;
namespace Microsoft.Azure.Cosmos.Internal
{
    /// <summary>
    /// These are the operation types resulted in a version conflict on a resource. 
    /// </summary>
    /// <remarks>
    /// When a version conflict occurs during an async operation, retrieving the <see cref="Microsoft.Azure.Documents.Conflict"/> instance will allow you 
    /// to determine which resource and operation caause the conflict.
    /// </remarks>
    internal enum OperationKind
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
        Delete,

        /// <summary>
        /// This operation does not apply to Conflict.
        /// </summary>
        [ObsoleteAttribute("This item is obsolete as it does not apply to Conflict.")]
        Read
    }
}
