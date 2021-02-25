// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.AsyncEnumerable;

    /// <summary>
    /// Has the ability to page through a partition range.
    /// </summary>
    internal abstract class PartitionRangePageAsyncEnumerator<TPage, TState> : ITraceableAsyncEnumerator<TryCatch<TPage>>
        where TPage : Page<TState>
        where TState : State
    {
        private readonly ITrace trace;
        private CancellationToken cancellationToken;

        protected PartitionRangePageAsyncEnumerator(
            FeedRangeState<TState> feedRangeState,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            this.FeedRangeState = feedRangeState;
            this.trace = trace ?? throw new ArgumentNullException(nameof(trace));
            this.cancellationToken = cancellationToken;
        }

        public FeedRangeState<TState> FeedRangeState { get; private set; }

        public TryCatch<TPage> Current { get; private set; }

        public bool HasStarted { get; private set; }

        private bool HasMoreResults => !this.HasStarted || (this.FeedRangeState.State != default);

        public ValueTask<bool> MoveNextAsync()
        {
            return this.MoveNextAsync(this.trace);
        }

        public async ValueTask<bool> MoveNextAsync(ITrace trace)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            using (ITrace childTrace = trace.StartChild(name: $"{this.FeedRangeState.FeedRange} move next", TraceComponent.Pagination, TraceLevel.Info))
            {
                if (!this.HasMoreResults)
                {
                    return false;
                }

                this.Current = await this.GetNextPageAsync(trace: childTrace, cancellationToken: this.cancellationToken);
                if (this.Current.Succeeded)
                {
                    this.FeedRangeState = new FeedRangeState<TState>(this.FeedRangeState.FeedRange, this.Current.Result.State);
                    this.HasStarted = true;
                }

                return true;
            }
        }

        protected abstract Task<TryCatch<TPage>> GetNextPageAsync(ITrace trace, CancellationToken cancellationToken);

        public abstract ValueTask DisposeAsync();

        public void SetCancellationToken(CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
        }
    }
}
