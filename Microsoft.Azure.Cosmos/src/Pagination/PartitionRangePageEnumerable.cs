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
        private readonly CreatePartitionRangePageEnumerator createPartitionRangeEnumerator;

        public PartitionRangePageEnumerable(
            FeedRange range,
            State state,
            CreatePartitionRangePageEnumerator createPartitionRangeEnumerator)
        {
            this.range = range ?? throw new ArgumentNullException(nameof(range));
            this.state = state;
            this.createPartitionRangeEnumerator = createPartitionRangeEnumerator ?? throw new ArgumentNullException(nameof(createPartitionRangeEnumerator));
        }

        public IAsyncEnumerator<TryCatch<Page>> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return this.createPartitionRangeEnumerator(this.range, this.state);
        }
    }
}
