// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal interface IFeedRangeProvider
    {
        public Task<IEnumerable<PartitionKeyRange>> GetChildRangeAsync(
            PartitionKeyRange partitionKeyRange,
            CancellationToken cancellationToken);

        public Task<IEnumerable<PartitionKeyRange>> GetFeedRangesAsync(CancellationToken cancellationToken);
    }
}
