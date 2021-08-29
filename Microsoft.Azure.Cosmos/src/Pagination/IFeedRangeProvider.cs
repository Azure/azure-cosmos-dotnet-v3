// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;

    internal interface IFeedRangeProvider : IMonadicFeedRangeProvider
    {
        Task<List<FeedRangeEpk>> GetChildRangeAsync(
            FeedRangeInternal feedRange,
            ITrace trace,
            CancellationToken cancellationToken);

        Task<List<FeedRangeEpk>> GetFeedRangesAsync(
            ITrace trace,
            CancellationToken cancellationToken);

        /// <summary>
        /// Get Archival range for given range.
        /// If given range is too wide and does not map to single PKRange, this would return null.
        /// </summary>
        /// <param name="feedRange">Typically, this would be the range that gone through split.</param>
        /// <param name="trace">The trace.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task<List<FeedRangeArchivalPartition>> GetArchivalRangesAsync(
            FeedRangeInternal feedRange,
            ITrace trace,
            CancellationToken cancellationToken);

        Task RefreshProviderAsync(
            ITrace trace, 
            CancellationToken cancellationToken);
    }
}
