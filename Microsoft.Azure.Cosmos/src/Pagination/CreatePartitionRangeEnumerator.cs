// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    internal delegate PartitionRangePageEnumerator<TPage, TState> CreatePartitionRangePageEnumerator<TPage, TState>(FeedRange feedRange, TState state)
        where TPage : Page<TState>
        where TState : State;
}
