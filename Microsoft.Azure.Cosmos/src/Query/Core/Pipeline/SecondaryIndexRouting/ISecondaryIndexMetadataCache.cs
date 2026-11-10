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
        /// Gets secondary index metadata cached by the source resource identifier or discovers it using
        /// the supplied source container properties when no cached snapshot exists, the source membership
        /// changes, the three-minute snapshot lifetime expires, or a refresh is requested.
        /// Expiration is evaluated lazily when the snapshot is read.
        /// </summary>
        /// <returns>The cached or newly discovered secondary index metadata.</returns>
        Task<IEnumerable<ISecondaryIndexMetadata>> GetSecondaryIndexMetadataAsync(
            ContainerProperties source,
            ITrace trace,
            CancellationToken cancellationToken,
            bool forceRefresh = false);

        /// <summary>
        /// Invalidates all cached secondary index metadata for a source collection.
        /// </summary>
        void Invalidate(string sourceCollectionRid);
    }
}
