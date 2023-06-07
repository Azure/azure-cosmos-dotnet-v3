//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    /// <summary>
    /// Operation Types fault injection can be applied to.
    /// </summary>
    public enum FaultInjectionOperationType
    {
        /// <summary>
        /// Read items.
        /// </summary>
        READ_ITEM,

        /// <summary>
        /// Query items.
        /// </summary>
        QUERY_ITEM, 

        /// <summary>
        /// Create items.
        /// </summary>
        CREATE_ITEM,

        /// <summary>
        /// Upsert items.
        /// </summary>
        UPSERT_ITEM,

        /// <summary>
        /// Replace items.
        /// </summary>
        REPLACE_ITEM,

        /// <summary>
        /// Delete items.
        /// </summary>
        DELETE_ITEM,

        /// <summary>
        /// Patch items.
        /// </summary>  
        PATCH_ITEM
    }
}
