// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    /// <summary>
    /// Has the ability to page through a partition range.
    /// </summary>
    internal abstract class PartitionRangePageEnumerator : IAsyncEnumerator<TryCatch<Page>>
    {
        private bool hasStarted;

        protected PartitionRangePageEnumerator(FeedRange range, State state = null)
        {
            this.Range = range;
            this.State = state;
        }

        public FeedRange Range { get; }

        public TryCatch<Page> Current { get; private set; }

        public State State { get; private set; }

        public bool HasMoreResults => !this.hasStarted || (this.State != default);

        public async ValueTask<bool> MoveNextAsync()
        {
            if (!this.HasMoreResults)
            {
                return false;
            }

            this.hasStarted = true;

            this.Current = await this.GetNextPageAsync();
            if (this.Current.Succeeded)
            {
                this.State = this.Current.Result.State;
            }

            return true;
        }

        public abstract Task<TryCatch<Page>> GetNextPageAsync(CancellationToken cancellationToken = default);

        public abstract ValueTask DisposeAsync();
    }
}
