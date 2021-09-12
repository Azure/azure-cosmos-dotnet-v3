// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class BufferedPartitionRangePageAsyncEnumerator<TPage, TState> : PartitionRangePageAsyncEnumerator<TPage, TState>, IPrefetcher
        where TPage : Page<TState>
        where TState : State
    {
        private readonly PartitionRangePageAsyncEnumerator<TPage, TState> enumerator;
        private TryCatch<TPage>? bufferedPage;

        public BufferedPartitionRangePageAsyncEnumerator(PartitionRangePageAsyncEnumerator<TPage, TState> enumerator, CancellationToken cancellationToken)
            : base(enumerator.FeedRangeState, cancellationToken)
        {
            this.enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
        }

        public override ValueTask DisposeAsync() => this.enumerator.DisposeAsync();

        protected override async Task<TryCatch<TPage>> GetNextPageAsync(ITrace trace, CancellationToken cancellationToken)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            await this.PrefetchAsync(trace, cancellationToken);

            // Serve from the buffered page first.
            TryCatch<TPage> returnValue = this.bufferedPage.Value;
            this.bufferedPage = null;
            return returnValue;
        }

        public async ValueTask PrefetchAsync(ITrace trace, CancellationToken cancellationToken)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            using (ITrace prefetchTrace = trace.StartChild("Prefetch", TraceComponent.Pagination, TraceLevel.Info))
            {
                if (this.bufferedPage.HasValue)
                {
                    return;
                }

                await this.enumerator.MoveNextAsync(prefetchTrace);
                this.bufferedPage = this.enumerator.Current;
            }
        }

        public override void SetCancellationToken(CancellationToken cancellationToken)
        {
            base.SetCancellationToken(cancellationToken);
            this.enumerator.SetCancellationToken(cancellationToken);
        }
    }
}
