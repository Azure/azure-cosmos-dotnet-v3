//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    /// <summary> 
    /// Specifies the supported indexing modes in the Azure Cosmos DB service.
    /// </summary>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    enum IndexingMode
    {
        /// <summary>
        /// Index is updated synchronously with a create, update or delete operation.
        /// </summary>
        /// <remarks>
        /// With consistent indexing, query consistency is the same as the default consistency level for the database account. 
        /// The index is always kept up to date with the data.
        /// 
        /// The default IndexingMode is Consistent.
        /// </remarks>
        Consistent,

        /// <summary>
        /// Index is updated asynchronously with respect to a create, update or delete operation.
        /// </summary>
        /// <remarks>
        /// With lazy indexing, queries are eventually consistent. 
        /// The index is updated when the collection is operating below full throughput capacity (Request units per second). 
        /// 
        /// Write operations will consume fewer request units (RequestCharge) at the time of write.
        /// </remarks>
        Lazy,

        /// <summary>
        /// No index is provided.
        /// </summary>
        /// <remarks>
        /// Setting IndexingMode to "None" drops the index. Use this if you don't want to maintain the index for a document collection, to save the storage cost or improve the write throughput. Your queries will degenerate to scans of the entire collection.
        /// </remarks>
        None
    }
}
