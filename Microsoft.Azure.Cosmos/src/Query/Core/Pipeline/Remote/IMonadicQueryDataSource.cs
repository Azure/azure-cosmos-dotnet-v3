// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Remote
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal interface IMonadicQueryDataSource
    {
        Task<TryCatch<QueryPage>> MonadicQueryAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            Cosmos.PartitionKey partitionKey,
            int pageSize,
            CancellationToken cancellationToken);

        Task<TryCatch<QueryPage>> MonadicQueryAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            int partitionKeyRangeId,
            int pageSize,
            CancellationToken cancellationToken);

        Task<TryCatch<QueryPage>> MonadicQueryAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            FeedRangeInternal feedRange,
            int pageSize,
            CancellationToken cancellationToken);
    }
}
