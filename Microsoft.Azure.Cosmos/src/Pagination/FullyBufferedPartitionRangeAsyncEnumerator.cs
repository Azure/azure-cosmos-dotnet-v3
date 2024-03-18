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
        private int bufferedItemCount;
        private Exception exception;

        private bool hasPrefetched;

        public override Exception BufferedException => this.exception;

        public override int BufferedItemCount => this.bufferedItemCount;

        public FullyBufferedPartitionRangeAsyncEnumerator(PartitionRangePageAsyncEnumerator<TPage, TState> enumerator)
            : this(enumerator, null)
        {
        }

        public FullyBufferedPartitionRangeAsyncEnumerator(PartitionRangePageAsyncEnumerator<TPage, TState> enumerator, IReadOnlyList<TPage> bufferedPages)
            : base(enumerator.FeedRangeState)
        {
            this.enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
            this.bufferedPages = new List<TPage>();

            if (bufferedPages != null)
            {
                this.bufferedPages.AddRange(bufferedPages);
            }
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

            if (this.hasPrefetched)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            using (ITrace prefetchTrace = trace.StartChild("Prefetch", TraceComponent.Pagination, TraceLevel.Info))
            {
                while (await this.enumerator.MoveNextAsync(prefetchTrace, cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    TryCatch<TPage> current = this.enumerator.Current;
                    if (current.Succeeded)
                    {
                        this.bufferedPages.Add(current.Result);
                        this.bufferedItemCount += current.Result.ItemCount;
                    }
                    else
                    {
                        this.exception = current.Exception;
                        break;
                    }
                }
            }

            this.hasPrefetched = true;
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
                await this.enumerator.MoveNextAsync(trace, cancellationToken);
                result = this.enumerator.Current;
            }

            ++this.currentIndex;
            return result;
        }
    }
}
