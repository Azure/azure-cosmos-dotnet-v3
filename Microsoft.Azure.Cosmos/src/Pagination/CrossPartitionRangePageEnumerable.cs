// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal sealed class CrossPartitionRangePageEnumerable : IAsyncEnumerable<TryCatch<Page>>
    {
        private readonly IEnumerable<(FeedRange, State)> rangeAndStates;
        private readonly Func<FeedRange, State, PartitionRangePageEnumerator> createPartitionRangePaginator;
        private readonly IComparer<PartitionRangePageEnumerator> comparer;
        private readonly FeedRangeProvider feedRangeProvider;

        public CrossPartitionRangePageEnumerable(
            IEnumerable<(FeedRange, State)> rangeAndStates,
            Func<FeedRange, State, PartitionRangePageEnumerator> createPartitionRangePaginator,
            IComparer<PartitionRangePageEnumerator> comparer,
            FeedRangeProvider feedRangeProvider)
        {
            this.rangeAndStates = rangeAndStates ?? throw new ArgumentNullException(nameof(rangeAndStates));
            this.createPartitionRangePaginator = createPartitionRangePaginator ?? throw new ArgumentNullException(nameof(createPartitionRangePaginator));
            this.comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
            this.feedRangeProvider = feedRangeProvider ?? throw new ArgumentNullException(nameof(comparer));
        }

        public IAsyncEnumerator<TryCatch<Page>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<PartitionRangePageEnumerator> enumerators = new List<PartitionRangePageEnumerator>(this.rangeAndStates.Count());
            foreach ((FeedRange range, State state) in this.rangeAndStates)
            {
                PartitionRangePageEnumerator enumerator = this.createPartitionRangePaginator(range, state);
                enumerators.Add(enumerator);
            }

            return new CrossPartitionRangePageEnumerator(
                this.feedRangeProvider,
                this.createPartitionRangePaginator,
                enumerators,
                this.comparer);
        }
    }
}
