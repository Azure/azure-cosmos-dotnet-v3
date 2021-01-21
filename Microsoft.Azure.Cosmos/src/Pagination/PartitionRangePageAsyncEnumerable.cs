// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal sealed class PartitionRangePageAsyncEnumerable<TPage, TState> : IAsyncEnumerable<TryCatch<TPage>>
        where TPage : Page<TState>
        where TState : State
    {
        private readonly FeedRangeState<TState> feedRangeState;
        private readonly CreatePartitionRangePageAsyncEnumerator<TPage, TState> createPartitionRangeEnumerator;

        public PartitionRangePageAsyncEnumerable(
            FeedRangeState<TState> feedRangeState,
            CreatePartitionRangePageAsyncEnumerator<TPage, TState> createPartitionRangeEnumerator)
        {
            this.feedRangeState = feedRangeState;
            this.createPartitionRangeEnumerator = createPartitionRangeEnumerator ?? throw new ArgumentNullException(nameof(createPartitionRangeEnumerator));
        }

        public IAsyncEnumerator<TryCatch<TPage>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return this.createPartitionRangeEnumerator(this.feedRangeState);
        }
    }
}
