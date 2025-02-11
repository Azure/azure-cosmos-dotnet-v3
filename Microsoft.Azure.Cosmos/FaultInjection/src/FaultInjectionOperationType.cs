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
        ReadItem,

        /// <summary>
        /// Query items.
        /// </summary>
        QueryItem, 

        /// <summary>
        /// Create items.
        /// </summary>
        CreateItem,

        /// <summary>
        /// Upsert items.
        /// </summary>
        UpsertItem,

        /// <summary>
        /// Replace items.
        /// </summary>
        ReplaceItem,

        /// <summary>
        /// Delete items.
        /// </summary>
        DeleteItem,

        /// <summary>
        /// Patch items.
        /// </summary>  
        PatchItem,

        /// <summary>
        /// Batch operations.
        /// </summary>
        Batch,

        /// <summary>
        /// Read Feed operations for ChangeFeed.
        /// </summary>
        ReadFeed,

        /// <summary>
        /// All operation types. Default value.
        /// </summary>
        All,
    }
}
