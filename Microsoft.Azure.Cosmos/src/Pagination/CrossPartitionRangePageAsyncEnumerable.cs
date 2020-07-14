// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal sealed class CrossPartitionRangePageAsyncEnumerable<TPage, TState> : IAsyncEnumerable<TryCatch<CrossPartitionPage<TPage, TState>>>
        where TPage : Page<TState>
        where TState : State
    {
        private readonly CrossPartitionState<TState> state;
        private readonly CreatePartitionRangePageAsyncEnumerator<TPage, TState> createPartitionRangeEnumerator;
        private readonly IComparer<PartitionRangePageAsyncEnumerator<TPage, TState>> comparer;
        private readonly IFeedRangeProvider feedRangeProvider;

        public CrossPartitionRangePageAsyncEnumerable(
            IFeedRangeProvider feedRangeProvider,
            CreatePartitionRangePageAsyncEnumerator<TPage, TState> createPartitionRangeEnumerator,
            IComparer<PartitionRangePageAsyncEnumerator<TPage, TState>> comparer,
            CrossPartitionState<TState> state = default)
        {
            this.feedRangeProvider = feedRangeProvider ?? throw new ArgumentNullException(nameof(comparer));
            this.createPartitionRangeEnumerator = createPartitionRangeEnumerator ?? throw new ArgumentNullException(nameof(createPartitionRangeEnumerator));
            this.comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
            this.state = state;
        }

        public IAsyncEnumerator<TryCatch<CrossPartitionPage<TPage, TState>>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return new CrossPartitionRangePageAsyncEnumerator<TPage, TState>(
                this.feedRangeProvider,
                this.createPartitionRangeEnumerator,
                this.comparer,
                this.state);
        }
    }
}
