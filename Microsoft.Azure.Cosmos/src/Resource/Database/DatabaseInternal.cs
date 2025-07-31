//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;

    internal abstract class DatabaseInternal : Database
    {
        internal abstract string LinkUri { get; }

        internal abstract CosmosClientContext ClientContext { get; }

#if !COSMOS_GW_AOT
        internal abstract Task<ThroughputResponse> ReadThroughputIfExistsAsync(
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default);

        internal abstract Task<ThroughputResponse> ReplaceThroughputIfExistsAsync(
            int throughput,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        internal abstract Task<ThroughputResponse> ReplaceThroughputPropertiesIfExistsAsync(
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);
#endif

        internal abstract Task<string> GetRIDAsync(CancellationToken cancellationToken = default);

#if !COSMOS_GW_AOT
        public abstract FeedIterator GetUserQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);

        public abstract FeedIterator GetUserQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);
#endif
    }
}
