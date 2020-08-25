//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;

    /// <summary> 
    /// Specifies the operations on which a trigger should be executed in the Azure Cosmos DB service.
    /// </summary>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    enum TriggerOperation : short
    {
        /// <summary>
        /// Specifies all operations.
        /// </summary>
        All     = 0x0,

        /// <summary>
        /// Specifies create operations only.
        /// </summary>
        Create  = 0x1,

        /// <summary>
        /// Specifies update operations only.
        /// </summary>
        Update  = 0x2,

        /// <summary>
        /// Specifies delete operations only.
        /// </summary>
        Delete  = 0x3,

        /// <summary>
        /// Specifies replace operations only.
        /// </summary>
        Replace = 0x4
    }
}
