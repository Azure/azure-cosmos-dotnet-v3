// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using Microsoft.Azure.Documents;

    internal delegate PartitionRangePageEnumerator<TPage, TState> CreatePartitionRangePageEnumerator<TPage, TState>(
        PartitionKeyRange feedRange,
        TState state)
        where TPage : Page<TState>
        where TState : State;
}
