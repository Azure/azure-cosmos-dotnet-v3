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

    internal sealed class PartitionRangePageAsyncEnumerable<TPage, TState> : IAsyncEnumerable<TryCatch<TPage>>
        where TPage : Page<TState>
        where TState : State
    {
        private readonly FeedRangeState<TState> feedRangeState;
        private readonly CreatePartitionRangePageAsyncEnumerator<TPage, TState> createPartitionRangeEnumerator;
        private readonly ITrace trace;

        public PartitionRangePageAsyncEnumerable(
            FeedRangeState<TState> feedRangeState,
            CreatePartitionRangePageAsyncEnumerator<TPage, TState> createPartitionRangeEnumerator,
            ITrace trace)
        {
            this.feedRangeState = feedRangeState;
            this.createPartitionRangeEnumerator = createPartitionRangeEnumerator ?? throw new ArgumentNullException(nameof(createPartitionRangeEnumerator));
            this.trace = trace ?? throw new ArgumentNullException(nameof(trace));
        }

        public IAsyncEnumerator<TryCatch<TPage>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return new TracingAsyncEnumerator<TryCatch<TPage>>(this.createPartitionRangeEnumerator(this.feedRangeState), this.trace, cancellationToken);
        }
    }
}