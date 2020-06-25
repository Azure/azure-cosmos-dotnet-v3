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
    internal abstract class PartitionRangePaginator : IAsyncEnumerator<TryCatch<Page>>
    {
        public PartitionKeyHashRange Range { get; }

        public TryCatch<Page> Current { get; protected set; }

        public abstract ValueTask DisposeAsync();

        public abstract State GetState();

        public abstract ValueTask<bool> MoveNextAsync();
    }
}
