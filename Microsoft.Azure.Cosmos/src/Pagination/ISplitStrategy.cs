// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal interface ISplitStrategy<TPage, TState>
        where TPage : Page<TState>
        where TState : State
    {
        Task HandleSplitAsync(
            FeedRangeInternal range,
            TState state,
            IQueue<PartitionRangePageAsyncEnumerator<TPage, TState>> enumerators,
            CancellationToken cancellationToken);
    }
}
