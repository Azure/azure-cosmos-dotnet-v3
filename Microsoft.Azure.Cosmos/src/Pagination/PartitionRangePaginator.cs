// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    /// <summary>
    /// Has the ability to page through a partition range.
    /// </summary>
    internal abstract class PartitionRangePaginator
    {
        public FeedRange FeedRange { get; }

        public Page CurrentPage { get; }

        public abstract State GetState();

        public abstract Task<TryCatch> TryMoveNextPageAsync(CancellationToken cancellationToken = default);
    }
}
