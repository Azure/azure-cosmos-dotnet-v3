//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Represents the read consistency strategies supported by the Azure Cosmos DB service.
    /// The requested strategy can be chosen independent of the account's default consistency level.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set, <see cref="ReadConsistencyStrategy"/> takes precedence over <see cref="ConsistencyLevel"/>
    /// for read and query operations. The strategy is honored in Direct connectivity mode.
    /// </para>
    /// <para>
    /// <see cref="GlobalStrong"/> is only valid for accounts configured with Strong consistency.
    /// </para>
    /// </remarks>
#if PREVIEW
    public
#else
    internal
#endif
    enum ReadConsistencyStrategy
    {
        /// <summary>
        /// Eventual consistency - reads from a single random secondary replica.
        /// This strategy also covers  <see cref="ConsistencyLevel.ConsistentPrefix"/> consistency level behavior,
        /// as both use the same underlying read mechanism (single replica read).
        /// </summary>
        Eventual = 1,

        /// <summary>
        /// Session consistency - monotonic reads, writes and read-your-writes guarantees within a session.
        /// Requires a session token obtained from prior write operations to ensure read-your-writes semantics.
        /// See <see cref="Headers.Session"/> for how session tokens are propagated.
        /// </summary>
        Session = 2,

        /// <summary>
        /// Quorum read with GLSN barrier - returns the latest committed version in the current read region.
        /// This is the read consistency equivalent of <see cref="ConsistencyLevel.BoundedStaleness"/>.
        /// </summary>
        LatestCommitted = 3,

        /// <summary>
        /// Quorum read with GCLSN barrier - returns the latest version across all regions.
        /// Only valid for accounts configured with Strong consistency.
        /// </summary>
        GlobalStrong = 4,

        /// <summary>
        /// This strategy is designed for single-master accounts only.
        /// In single-master accounts, each physical partition has a designated hub (primary) replica
        /// in the write region. When a read encounters a 404/1002 (ReadSessionNotAvailable), the SDK
        /// retries by routing the request to the partition-set level hub region using the
        /// <c>x-ms-cosmos-hub-region-processing-only</c> header. If the hub replica is in a different
        /// region than expected (403/3 WriteForbidden), the SDK discovers and retries on the actual hub.
        /// Returns the latest committed version from the hub (write) region, ensuring reads
        /// reflect the most recent writes regardless of which region the client is connected to.
        /// </summary>
        LastCommittedWriteRegion = 5
    }
}
