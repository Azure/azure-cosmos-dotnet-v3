// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal sealed class PartitionRangePageEnumerable<TPage, TState> : IAsyncEnumerable<TryCatch<TPage>>
        where TPage : Page<TState>
        where TState : State
    {
        private readonly FeedRange range;
        private readonly TState state;
        private readonly CreatePartitionRangePageEnumerator<TPage, TState> createPartitionRangeEnumerator;

        public PartitionRangePageEnumerable(
            FeedRange range,
            TState state,
            CreatePartitionRangePageEnumerator<TPage, TState> createPartitionRangeEnumerator)
        {
            this.range = range ?? throw new ArgumentNullException(nameof(range));
            this.state = state;
            this.createPartitionRangeEnumerator = createPartitionRangeEnumerator ?? throw new ArgumentNullException(nameof(createPartitionRangeEnumerator));
        }

        public IAsyncEnumerator<TryCatch<TPage>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return this.createPartitionRangeEnumerator(this.range, this.state);
        }
    }
}
