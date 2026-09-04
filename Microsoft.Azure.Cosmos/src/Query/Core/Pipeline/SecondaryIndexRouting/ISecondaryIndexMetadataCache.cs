//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.SecondaryIndexRouting
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// Caches normalized secondary index metadata by source collection resource identifier.
    /// </summary>
    internal interface ISecondaryIndexMetadataCache
    {
        /// <summary>
        /// Gets cached secondary index metadata or discovers it when no cached snapshot exists.
        /// </summary>
        /// <returns>The cached or newly discovered secondary index metadata.</returns>
        Task<IReadOnlyList<ISecondaryIndexMetadata>> TryGetSecondaryIndexMetadataAsync(
            string sourceCollectionRid,
            ITrace trace,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates all cached secondary index metadata for a source collection.
        /// </summary>
        void Invalidate(string sourceCollectionRid);
    }
}
