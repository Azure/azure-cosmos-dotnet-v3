// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Documents;

    internal interface IMonadicFeedRangeProvider
    {
        Task<TryCatch<List<PartitionKeyRange>>> MonadicGetChildRangeAsync(
            PartitionKeyRange partitionKeyRange,
            CancellationToken cancellationToken);

        Task<TryCatch<List<PartitionKeyRange>>> MonadicGetFeedRangesAsync(
            CancellationToken cancellationToken);
    }
}
