//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryClient
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;

    internal interface ICosmosDistributedQueryClient
    {
        Task<TryCatch<QueryPage>> MonadicQueryAsync(
            PartitionKey? partitionKey,
            FeedRangeInternal feedRange,
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            QueryExecutionOptions queryPaginationOptions,
            ITrace trace,
            CancellationToken cancellationToken);
    }
}