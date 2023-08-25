﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// These are the operation types resulted in a version conflict on a resource. 
    /// </summary>
    /// <remarks>
    /// When a version conflict occurs during an async operation, retrieving the <see cref="ConflictProperties"/> instance will allow you 
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
        Delete,

        /// <summary>
        /// This operation does not apply to Conflict.
        /// </summary>
        [ObsoleteAttribute("This item is obsolete as it does not apply to Conflict.")]
        Read
    }
}
