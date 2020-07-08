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
        public Task<IEnumerable<FeedRangeInternal>> GetChildRangeAsync(FeedRangeInternal feedRange, CancellationToken cancellationToken);

        public Task<IEnumerable<FeedRangeInternal>> GetFeedRangesAsync(CancellationToken cancellationToken);

        public Task<FeedRangePartitionKeyRange> ToPhysicalPartitionKeyRangeAsync(FeedRangeInternal feedRange, CancellationToken cancellationToken);

        public Task<FeedRangeEpk> ToEffectivePartitionKeyRangeAsync(FeedRangeInternal feedRange, CancellationToken cancellationToken);
    }
}
