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
    /// Discovers Global Secondary Index metadata for a source collection.
    /// </summary>
    internal interface ISecondaryIndexMetadataProvider
    {
        /// <summary>
        /// Gets normalized secondary index metadata for a source collection.
        /// </summary>
        /// <returns>The secondary indexes associated with the source collection.</returns>
        Task<IReadOnlyList<ISecondaryIndexMetadata>> GetSecondaryIndexMetadataAsync(
            string sourceCollectionRid,
            ITrace trace,
            CancellationToken cancellationToken = default);
    }
}
