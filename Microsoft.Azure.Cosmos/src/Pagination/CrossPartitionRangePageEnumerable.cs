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
        private readonly State state;
        private readonly CreatePartitionRangePageEnumerator createPartitionRangeEnumerator;
        private readonly IComparer<PartitionRangePageEnumerator> comparer;
        private readonly IFeedRangeProvider feedRangeProvider;

        public CrossPartitionRangePageEnumerable(
            IFeedRangeProvider feedRangeProvider,
            CreatePartitionRangePageEnumerator createPartitionRangeEnumerator,
            IComparer<PartitionRangePageEnumerator> comparer,
            State state = default)
        {
            this.feedRangeProvider = feedRangeProvider ?? throw new ArgumentNullException(nameof(comparer));
            this.createPartitionRangeEnumerator = createPartitionRangeEnumerator ?? throw new ArgumentNullException(nameof(createPartitionRangeEnumerator));
            this.comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
            this.state = state;
        }

        public IAsyncEnumerator<TryCatch<Page>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return new CrossPartitionRangePageEnumerator(
                this.feedRangeProvider,
                this.createPartitionRangeEnumerator,
                this.comparer,
                this.state);
        }
    }
}
