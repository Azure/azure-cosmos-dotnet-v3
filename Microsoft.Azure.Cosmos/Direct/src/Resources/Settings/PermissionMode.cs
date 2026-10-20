//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;

    /// <summary> 
    /// These are the access permissions for creating or replacing a <see cref="Permission" /> resource in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// A Permission resource associates an access permission of a user on a particular resource.
    /// </remarks>
    [Flags]
#if COSMOSCLIENT
    internal
#else
    public
#endif
    enum PermissionMode : byte
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
