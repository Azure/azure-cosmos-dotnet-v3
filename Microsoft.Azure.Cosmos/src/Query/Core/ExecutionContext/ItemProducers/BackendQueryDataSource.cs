// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Documents;

    internal sealed class BackendQueryDataSource : IQueryDataSource
    {
        private readonly CosmosQueryContext cosmosQueryContext;

        public BackendQueryDataSource(CosmosQueryContext cosmosQueryContext)
        {
            this.cosmosQueryContext = cosmosQueryContext ?? throw new ArgumentNullException(nameof(cosmosQueryContext));
        }

        public Task<TryCatch<QueryPage>> ExecuteQueryAsync(
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            int partitionKeyRangeId,
            int pageSize,
            CancellationToken cancellationToken) => this.cosmosQueryContext.ExecuteQueryAsync(
                querySpecForInit: sqlQuerySpec,
                continuationToken: continuationToken,
                partitionKeyRange: new PartitionKeyRangeIdentity(
                    this.cosmosQueryContext.ContainerResourceId,
                    partitionKeyRangeId.ToString()),
                isContinuationExpected: this.cosmosQueryContext.IsContinuationExpected,
                pageSize: pageSize,
                cancellationToken: cancellationToken);
    }
}
