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

    internal sealed class FullyBufferedPartitionRangeAsyncEnumerator<TPage, TState> : BufferedPartitionRangePageAsyncEnumeratorBase<TPage, TState>
        where TPage : Page<TState>
        where TState : State
    {
        private readonly PartitionRangePageAsyncEnumerator<TPage, TState> enumerator;
        private readonly List<TPage> bufferedPages;
        private int currentIndex;
        private Exception exception;

        private bool HasPrefetched => (this.exception != null) || (this.bufferedPages.Count > 0);

        public FullyBufferedPartitionRangeAsyncEnumerator(PartitionRangePageAsyncEnumerator<TPage, TState> enumerator, CancellationToken cancellationToken)
            : base(enumerator.FeedRangeState, cancellationToken)
        {
            this.enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
            this.bufferedPages = new List<TPage>();
        }

        public override ValueTask DisposeAsync()
        {
            return this.enumerator.DisposeAsync();
        }

        public override async ValueTask PrefetchAsync(ITrace trace, CancellationToken cancellationToken)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            if (this.HasPrefetched)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            using (ITrace prefetchTrace = trace.StartChild("Prefetch", TraceComponent.Pagination, TraceLevel.Info))
            {
                while (await this.enumerator.MoveNextAsync(prefetchTrace))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    TryCatch<TPage> current = this.enumerator.Current;
                    if (current.Succeeded)
                    {
                        this.bufferedPages.Add(current.Result);
                    }
                    else
                    {
                        this.exception = current.Exception;
                        break;
                    }
                }
            }
        }

        protected override async Task<TryCatch<TPage>> GetNextPageAsync(ITrace trace, CancellationToken cancellationToken)
        {
            TryCatch<TPage> result;
            if (this.currentIndex < this.bufferedPages.Count)
            {
                result = TryCatch<TPage>.FromResult(this.bufferedPages[this.currentIndex]);
            }
            else if (this.currentIndex == this.bufferedPages.Count && this.exception != null)
            {
                result = TryCatch<TPage>.FromException(this.exception);
            }
            else
            {
                await this.enumerator.MoveNextAsync(trace);
                result = this.enumerator.Current;
            }

            ++this.currentIndex;
            return result;
        }

        public override void SetCancellationToken(CancellationToken cancellationToken)
        {
            base.SetCancellationToken(cancellationToken);
            this.enumerator.SetCancellationToken(cancellationToken);
        }
    }
}
