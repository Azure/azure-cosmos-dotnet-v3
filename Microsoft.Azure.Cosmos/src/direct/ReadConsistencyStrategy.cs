//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;

    /// <summary>
    /// Represents the read consistency strategies supported by the Azure Cosmos DB service.
    /// The requested strategy can be chosen independent of the account's default consistency level.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    enum ReadConsistencyStrategy
    {
        /// <summary>
        /// Eventual consistency - reads from a single random secondary replica.
        /// This strategy also covers ConsistentPrefix consistency level behavior,
        /// as both use the same underlying read mechanism (single replica read).
        /// </summary>
        Eventual = 1,

        /// <summary>Session consistency - monotonic reads, writes and read-your-writes guarantees within a session.</summary>
        Session = 2,

        /// <summary>Quorum read with GLSN barrier - returns latest committed version in current region.</summary>
        LatestCommitted = 3,

        /// <summary>Quorum read with GCLSN barrier - returns latest version across all regions. Only for Strong accounts.</summary>
        GlobalStrong = 4
    }

}