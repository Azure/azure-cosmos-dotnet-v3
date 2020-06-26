// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal sealed class PartitionRangePageEnumerable : IAsyncEnumerable<TryCatch<Page>>
    {
        private readonly FeedRange range;
        private readonly State state;
        private readonly Func<FeedRange, State, PartitionRangePageEnumerator> createPartitionRangePaginator;

        public PartitionRangePageEnumerable(
            FeedRange range,
            State state,
            Func<FeedRange, State, PartitionRangePageEnumerator> createPartitionRangePaginator)
        {
            this.range = range ?? throw new ArgumentNullException(nameof(range));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.createPartitionRangePaginator = createPartitionRangePaginator ?? throw new ArgumentNullException(nameof(createPartitionRangePaginator));
        }

        public IAsyncEnumerator<TryCatch<Page>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return this.createPartitionRangePaginator(this.range, this.state);
        }
    }
}
