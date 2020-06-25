// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Routing;

    /// <summary>
    /// Has the ability to page through a partition range.
    /// </summary>
    internal abstract class PartitionRangePageEnumerator : IAsyncEnumerator<TryCatch<Page>>
    {
        private bool hasStarted;

        public FeedRange Range { get; }

        public TryCatch<Page> Current { get; private set; }

        public State State { get; private set; }

        public async ValueTask<bool> MoveNextAsync()
        {
            if (this.hasStarted && (this.State == default))
            {
                return false;
            }

            (TryCatch<Page> page, State state) = await this.GetNextPageAsync(this.State);
            this.State = state;
            this.Current = page;

            return true;
        }

        public abstract Task<(TryCatch<Page>, State)> GetNextPageAsync(State state, CancellationToken cancellationToken = default);

        public abstract ValueTask DisposeAsync();
    }
}
