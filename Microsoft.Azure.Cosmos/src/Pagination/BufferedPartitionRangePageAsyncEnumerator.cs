// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal sealed class BufferedPartitionRangePageAsyncEnumerator<TPage, TState> : PartitionRangePageAsyncEnumerator<TPage, TState>, IBufferable
        where TPage : Page<TState>
        where TState : State
    {
        private readonly PartitionRangePageAsyncEnumerator<TPage, TState> enumerator;
        private readonly SemaphoreSlim mutex;
        private TryCatch<TPage>? bufferedPage;

        public BufferedPartitionRangePageAsyncEnumerator(PartitionRangePageAsyncEnumerator<TPage, TState> enumerator)
            : base(enumerator.Range, enumerator.State)
        {
            this.enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
            this.mutex = new SemaphoreSlim(initialCount: 1, maxCount: 1);
        }

        public override ValueTask DisposeAsync() => this.enumerator.DisposeAsync();

        protected override async Task<TryCatch<TPage>> GetNextPageAsync(CancellationToken cancellationToken)
        {
            try
            {
                await this.mutex.WaitAsync(cancellationToken);
                if (this.bufferedPage.HasValue)
                {
                    // Serve from the buffered page first.
                    TryCatch<TPage> returnValue = this.bufferedPage.Value;
                    this.bufferedPage = null;
                    return returnValue;
                }

                await this.enumerator.MoveNextAsync();
                return this.enumerator.Current;
            }
            finally
            {
                this.mutex.Release();
            }
        }

        public async ValueTask BufferAsync()
        {
            try
            {
                await this.mutex.WaitAsync();
                if (this.bufferedPage.HasValue)
                {
                    return;
                }

                await this.enumerator.MoveNextAsync();
                this.bufferedPage = this.enumerator.Current;
            }
            finally
            {
                this.mutex.Release();
            }
        }
    }
}
