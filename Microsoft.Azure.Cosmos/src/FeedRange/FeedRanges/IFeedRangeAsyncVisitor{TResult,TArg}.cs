// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IFeedRangeAsyncVisitor<TResult, TArg>
    {
        public abstract Task<TResult> VisitAsync(FeedRangeLogicalPartitionKey feedRange, TArg argument, CancellationToken cancellationToken);

        public abstract Task<TResult> VisitAsync(FeedRangePhysicalPartitionKeyRange feedRange, TArg argument, CancellationToken cancellationToken);

        public abstract Task<TResult> VisitAsync(FeedRangeEpkRange feedRange, TArg argument, CancellationToken cancellationToken);
    }
}
