// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IFeedRangeProvider
    {
        public Task<IEnumerable<FeedRange>> GetChildRangeAsync(
            FeedRange feedRange,
            CancellationToken cancellationToken = default);

        public Task<IEnumerable<FeedRange>> GetFeedRangesAsync(CancellationToken cancellationToken = default);
    }
}
