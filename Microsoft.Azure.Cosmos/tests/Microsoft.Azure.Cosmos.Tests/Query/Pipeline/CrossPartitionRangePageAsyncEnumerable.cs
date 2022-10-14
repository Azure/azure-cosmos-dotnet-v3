// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class CrossPartitionRangePageAsyncEnumerable<TPage, TState> : IAsyncEnumerable<TryCatch<CrossFeedRangePage<TPage, TState>>>
        where TPage : Page<TState>
        where TState : State
    {
        private readonly CrossFeedRangeState<TState> state;
        private readonly CreatePartitionRangePageAsyncEnumerator<TPage, TState> createPartitionRangeEnumerator;
        private readonly IComparer<PartitionRangePageAsyncEnumerator<TPage, TState>> comparer;
        private readonly IFeedRangeProvider feedRangeProvider;
        private readonly int maxConcurrency;
        private readonly PrefetchPolicy prefetchPolicy;
        private readonly ITrace trace;

        public CrossPartitionRangePageAsyncEnumerable(
            IFeedRangeProvider feedRangeProvider,
            CreatePartitionRangePageAsyncEnumerator<TPage, TState> createPartitionRangeEnumerator,
            IComparer<PartitionRangePageAsyncEnumerator<TPage, TState>> comparer,
            int maxConcurrency,
            PrefetchPolicy prefetchPolicy,
            ITrace trace,
            CrossFeedRangeState<TState> state = default)
        {
            this.feedRangeProvider = feedRangeProvider ?? throw new ArgumentNullException(nameof(comparer));
            this.createPartitionRangeEnumerator = createPartitionRangeEnumerator ?? throw new ArgumentNullException(nameof(createPartitionRangeEnumerator));
            this.comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
            this.state = state;
            this.maxConcurrency = maxConcurrency < 0 ? throw new ArgumentOutOfRangeException(nameof(maxConcurrency)) : maxConcurrency;
            this.prefetchPolicy = prefetchPolicy;
            this.trace = trace ?? throw new ArgumentNullException(nameof(trace));
        }

        public IAsyncEnumerator<TryCatch<CrossFeedRangePage<TPage, TState>>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return new TracingAsyncEnumerator<TryCatch<CrossFeedRangePage<TPage, TState>>>(
                new CrossPartitionRangePageAsyncEnumerator<TPage, TState>(
                    this.feedRangeProvider,
                    this.createPartitionRangeEnumerator,
                    this.comparer,
                    this.maxConcurrency,
                    this.prefetchPolicy,
                    cancellationToken,
                    this.state),
                this.trace);
        }
    }
}
