//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Internal
{
    using System;

    /// <summary> 
    /// These are the access permissions for creating or replacing a <see cref="Microsoft.Azure.Documents.Permission" /> resource in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// A Permission resource associates an access permission of a user on a particular resource.
    /// </remarks>
    [Flags]
    internal enum PermissionMode : byte
    {
        /// <summary>
        /// Read permission mode will provide the user with Read only access to a resource.
        /// </summary>
        Read = 0x1,

        /// <summary>
        /// All permission mode will provide the user with full access(read, insert, replace and delete) to a resource.
        /// </summary>
        All = 0x2
    }
}
