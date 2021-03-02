// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IFeedRangeAsyncVisitor<TResult>
    {
        public abstract Task<TResult> VisitAsync(FeedRangeLogicalPartitionKey feedRange, CancellationToken cancellationToken = default);

        public abstract Task<TResult> VisitAsync(FeedRangePhysicalPartitionKeyRange feedRange, CancellationToken cancellationToken = default);

        public abstract Task<TResult> VisitAsync(FeedRangeEpkRange feedRange, CancellationToken cancellationToken = default);
    }
}
