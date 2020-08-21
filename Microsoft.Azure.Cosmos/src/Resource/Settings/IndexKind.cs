//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// These are the indexing types available for indexing a path in the Azure Cosmos DB service.
    /// </summary>
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/index-policy"/>
    public enum IndexKind
    {
        /// <summary>
        /// The index entries are hashed to serve point look up queries.
        /// </summary>
        /// <remarks>
        /// Can be used to serve queries like: SELECT * FROM docs d WHERE d.prop = 5
        /// </remarks>
        Hash,

        /// <summary>
        /// The index entries are ordered. Range indexes are optimized for inequality predicate queries with efficient range scans.
        /// </summary>
        /// <remarks>
        /// Can be used to serve queries like: SELECT * FROM docs d WHERE d.prop > 5
        /// </remarks>
        Range,

        /// <summary>
        /// The index entries are indexed to serve spatial queries.
        /// </summary>
        /// <remarks>
        /// Can be used to serve queries like: SELECT * FROM Root r WHERE ST_DISTANCE({"type":"Point","coordinates":[71.0589,42.3601]}, r.location) $LE 10000
        /// </remarks>
        Spatial
    }
}
