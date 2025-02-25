// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;

    internal abstract class BufferedPartitionRangePageAsyncEnumeratorBase<TPage, TState> : PartitionRangePageAsyncEnumerator<TPage, TState>, IPrefetcher
        where TPage : Page<TState>
        where TState : State
    {
        protected BufferedPartitionRangePageAsyncEnumeratorBase(FeedRangeState<TState> feedRangeState)
            : base(feedRangeState)
        {
        }

        public abstract Exception BufferedException { get; }

        public abstract int BufferedItemCount { get; }

        public abstract ValueTask PrefetchAsync(ITrace trace, CancellationToken cancellationToken);
    }
}
