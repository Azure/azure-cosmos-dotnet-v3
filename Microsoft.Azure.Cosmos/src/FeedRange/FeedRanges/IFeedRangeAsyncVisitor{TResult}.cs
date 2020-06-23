// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IFeedRangeAsyncVisitor<TResult>
    {
        public abstract Task<TResult> VisitAsync(FeedRangePartitionKey feedRange, CancellationToken cancellationToken = default);

        public abstract Task<TResult> VisitAsync(FeedRangePartitionKeyRange feedRange, CancellationToken cancellationToken = default);

        public abstract Task<TResult> VisitAsync(FeedRangeEPK feedRange, CancellationToken cancellationToken = default);
    }
}
