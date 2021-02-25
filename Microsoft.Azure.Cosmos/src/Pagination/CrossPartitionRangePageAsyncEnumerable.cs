// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.AsyncEnumerable;

    internal sealed class CrossPartitionRangePageAsyncEnumerable<TPage, TState> : ITraceableAsyncEnumerable<TryCatch<CrossFeedRangePage<TPage, TState>>>
        where TPage : Page<TState>
        where TState : State
    {
        private readonly CrossFeedRangeState<TState> state;
        private readonly CreatePartitionRangePageAsyncEnumerator<TPage, TState> createPartitionRangeEnumerator;
        private readonly IComparer<PartitionRangePageAsyncEnumerator<TPage, TState>> comparer;
        private readonly IFeedRangeProvider feedRangeProvider;
        private readonly int maxConcurrency;

        public CrossPartitionRangePageAsyncEnumerable(
            IFeedRangeProvider feedRangeProvider,
            CreatePartitionRangePageAsyncEnumerator<TPage, TState> createPartitionRangeEnumerator,
            IComparer<PartitionRangePageAsyncEnumerator<TPage, TState>> comparer,
            int maxConcurrency,
            CrossFeedRangeState<TState> state = default)
        {
            this.feedRangeProvider = feedRangeProvider ?? throw new ArgumentNullException(nameof(comparer));
            this.createPartitionRangeEnumerator = createPartitionRangeEnumerator ?? throw new ArgumentNullException(nameof(createPartitionRangeEnumerator));
            this.comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
            this.state = state;
            this.maxConcurrency = maxConcurrency < 0 ? throw new ArgumentOutOfRangeException(nameof(maxConcurrency)) : maxConcurrency;
        }

        public IAsyncEnumerator<TryCatch<CrossFeedRangePage<TPage, TState>>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return this.GetAsyncEnumerator(NoOpTrace.Singleton, cancellationToken: default);
        }

        public ITraceableAsyncEnumerator<TryCatch<CrossFeedRangePage<TPage, TState>>> GetAsyncEnumerator(
            ITrace trace, 
            CancellationToken cancellationToken)
        {
            return new CrossPartitionRangePageAsyncEnumerator<TPage, TState>(
                this.feedRangeProvider,
                this.createPartitionRangeEnumerator,
                this.comparer,
                this.maxConcurrency,
                trace,
                cancellationToken,
                this.state);
        }
    }
}
