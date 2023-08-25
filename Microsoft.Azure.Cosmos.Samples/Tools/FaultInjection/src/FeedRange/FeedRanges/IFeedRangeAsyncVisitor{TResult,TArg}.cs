// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IFeedRangeAsyncVisitor<TResult, TArg>
    {
        public abstract Task<TResult> VisitAsync(FeedRangePartitionKey feedRange, TArg argument, CancellationToken cancellationToken);

        public abstract Task<TResult> VisitAsync(FeedRangePartitionKeyRange feedRange, TArg argument, CancellationToken cancellationToken);

        public abstract Task<TResult> VisitAsync(FeedRangeEpk feedRange, TArg argument, CancellationToken cancellationToken);
    }
}
