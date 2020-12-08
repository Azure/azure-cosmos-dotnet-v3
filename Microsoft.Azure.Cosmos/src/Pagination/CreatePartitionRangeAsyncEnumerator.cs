// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    internal delegate PartitionRangePageAsyncEnumerator<TPage, TState> CreatePartitionRangePageAsyncEnumerator<TPage, TState>(
        FeedRangeInternal feedRange,
        TState state)
            where TPage : Page<TState>
            where TState : State;
}
