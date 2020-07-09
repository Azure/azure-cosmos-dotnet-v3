// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal sealed class CrossPartitionRangePageEnumerable<TPage, TState> : IAsyncEnumerable<TryCatch<CrossPartitionPage<TPage, TState>>>
        where TPage : Page<TState>
        where TState : State
    {
        private readonly CrossPartitionState<TState> state;
        private readonly CreatePartitionRangePageEnumerator<TPage, TState> createPartitionRangeEnumerator;
        private readonly IComparer<PartitionRangePageEnumerator<TPage, TState>> comparer;
        private readonly IFeedRangeProvider feedRangeProvider;

        public CrossPartitionRangePageEnumerable(
            IFeedRangeProvider feedRangeProvider,
            CreatePartitionRangePageEnumerator<TPage, TState> createPartitionRangeEnumerator,
            IComparer<PartitionRangePageEnumerator<TPage, TState>> comparer,
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

            return new CrossPartitionRangePageEnumerator<TPage, TState>(
                this.feedRangeProvider,
                this.createPartitionRangeEnumerator,
                this.comparer,
                forceEpkRange: false,
                this.state);
        }
    }
}
