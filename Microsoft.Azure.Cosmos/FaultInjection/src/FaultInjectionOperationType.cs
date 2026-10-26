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
        /// Metadata operations for containers.
        /// </summary>
        MetadataContainer,

        /// <summary>
        /// Metadata operations for databases.
        /// </summary>
        MetadataDatabaseAccount,

        /// <summary>
        /// Metadata operations for partition key ranges.
        /// </summary>
        MetadataPartitionKeyRange,

        /// <summary>
        /// Metadata operations for addresses.
        /// </summary>
        MetadataRefreshAddresses,

        /// <summary>
        /// Metadata operations for query plan.
        /// </summary>
        MetadataQueryPlan,

        /// <summary>
        /// All <c>Head</c> requests targeting the <c>Collection</c> resource
        /// (maps to <see cref="Microsoft.Azure.Documents.OperationType.Head"/>
        /// + <see cref="Microsoft.Azure.Documents.ResourceType.Collection"/>).
        /// <para>
        /// The SDK issues this operation for several distinct purposes — most
        /// notably the consistency-barrier <c>Head</c> the read path performs
        /// under Strong / Bounded-Staleness to verify <c>GlobalCommittedLSN</c>
        /// on a replica, but also container-existence checks and some
        /// metadata bootstrap calls. Rules that target this enum value will
        /// match every Head/Collection request — narrow the match further
        /// with <c>WithConnectionType(Direct)</c> or
        /// <c>WithRegion(...)</c> when only the barrier path is desired.
        /// </para>
        /// </summary>
        MetadataHeadCollection,

        /// <summary>
        /// All operation types. Default value.
        /// </summary>
        All,
    }
}
