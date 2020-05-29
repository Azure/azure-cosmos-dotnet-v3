//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal abstract class FeedIteratorBase<T>
    {
        public abstract bool HasMoreResults { get; }

        internal abstract RequestOptions RequestOptions { get; }

        public abstract CosmosElement GetCosmosElementContinuationToken();

        internal abstract Task<FeedResponse<T>> ReadNextAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken);
    }
}