//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// Routing map provider provides list of effective partition key ranges for a collection.
    /// </summary>
    internal interface IRoutingMapProvider
    {
        /// <summary>
        /// Returns list of effective partition key ranges for a collection.
        /// </summary>
        /// <param name="collectionResourceId">Collection for which to retrieve routing map.</param>
        /// <param name="range">This method will return all ranges which overlap this range.</param>
        /// <param name="trace">The trace.</param>
        /// <param name="forceRefresh">Whether forcefully refreshing the routing map is necessary</param>
        /// <returns>List of effective partition key ranges for a collection or null if collection doesn't exist.</returns>
        Task<IReadOnlyList<PartitionKeyRange>> TryGetOverlappingRangesAsync(string collectionResourceId, Range<string> range, ITrace trace, bool forceRefresh = false);

        Task<PartitionKeyRange> TryGetPartitionKeyRangeByIdAsync(string collectionResourceId, string partitionKeyRangeId, ITrace trace, bool forceRefresh = false);
    }
}
