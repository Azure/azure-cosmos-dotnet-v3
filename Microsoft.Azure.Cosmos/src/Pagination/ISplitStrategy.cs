// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;

    internal interface ISplitStrategy<TPage, TState>
        where TPage : Page<TState>
        where TState : State
    {
        Task HandleSplitAsync(
            PartitionRangePageAsyncEnumerator<TPage, TState> currentEnumerator,
            IQueue<PartitionRangePageAsyncEnumerator<TPage, TState>> enumerators,
            ITrace trace,
            CancellationToken cancellationToken);
    }
}
