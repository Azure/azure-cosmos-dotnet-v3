//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Routing;

    internal abstract class ContainerInternal : Container
    {
        public abstract string LinkUri { get; }

        public abstract CosmosClientContext ClientContext { get; }

        public abstract BatchAsyncContainerExecutor BatchExecutor { get; }

        public abstract Task<ThroughputResponse> ReadThroughputIfExistsAsync(
           RequestOptions requestOptions,
           CancellationToken cancellationToken);

        public abstract Task<ThroughputResponse> ReplaceThroughputIfExistsAsync(
            ThroughputProperties throughput,
            RequestOptions requestOptions,
            CancellationToken cancellationToken);

        public abstract Task<string> GetRIDAsync(CancellationToken cancellationToken);

        public abstract Task<Documents.PartitionKeyDefinition> GetPartitionKeyDefinitionAsync(
            CancellationToken cancellationToken);

        public abstract Task<ContainerProperties> GetCachedContainerPropertiesAsync(
            CancellationToken cancellationToken = default(CancellationToken));

        public abstract Task<IReadOnlyList<IReadOnlyList<string>>> GetPartitionKeyPathTokensAsync(
            CancellationToken cancellationToken = default(CancellationToken));

        public abstract Task<Documents.Routing.PartitionKeyInternal> GetNonePartitionKeyValueAsync(
            CancellationToken cancellationToken);

        public abstract Task<CollectionRoutingMap> GetRoutingMapAsync(CancellationToken cancellationToken);

        public abstract Task<TryExecuteQueryResult> TryExecuteQueryAsync(
            QueryFeatures supportedQueryFeatures,
            QueryDefinition queryDefinition,
            string continuationToken,
            FeedRangeInternal feedRangeInternal,
            QueryRequestOptions requestOptions,
            CancellationToken cancellationToken = default);

        internal abstract FeedIterator GetStandByFeedIterator(
            ChangeFeedRequestOptions requestOptions = default);

        public abstract FeedIteratorInternal GetItemQueryStreamIteratorInternal(
            SqlQuerySpec sqlQuerySpec,
            bool isContinuationExcpected,
            string continuationToken,
            FeedRangeInternal feedRange,
            QueryRequestOptions requestOptions);

        public abstract Task<PartitionKey> GetPartitionKeyValueFromStreamAsync(
            Stream stream,
            CancellationToken cancellation);

        public abstract Task<IEnumerable<string>> GetChangeFeedTokensAsync(
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Throw an exception if the partition key is null or empty string
        /// </summary>
        public static void ValidatePartitionKey(object partitionKey, RequestOptions requestOptions)
        {
            if (partitionKey != null)
            {
                return;
            }

            if (requestOptions != null && requestOptions.IsEffectivePartitionKeyRouting)
            {
                return;
            }

            throw new ArgumentNullException(nameof(partitionKey));
        }

#if !INTERNAL
        public abstract Task<ResponseMessage> PatchItemStreamAsync(
            string id,
            PartitionKey partitionKey,
            IReadOnlyList<PatchOperation> patchOperations,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));

        public abstract Task<ItemResponse<T>> PatchItemAsync<T>(
            string id,
            PartitionKey partitionKey,
            IReadOnlyList<PatchOperation> patchOperations,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken));
#endif

#if !PREVIEW

        public abstract Task<IReadOnlyList<FeedRange>> GetFeedRangesAsync(CancellationToken cancellationToken = default(CancellationToken));

        public abstract FeedIterator GetChangeFeedStreamIterator(
            ChangeFeedRequestOptions changeFeedRequestOptions = null);

        public abstract FeedIterator<T> GetChangeFeedIterator<T>(
            ChangeFeedRequestOptions changeFeedRequestOptions = null);

        public abstract Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            FeedRange feedRange,
            CancellationToken cancellationToken = default(CancellationToken));

        public abstract FeedIterator GetItemQueryStreamIterator(
            FeedRange feedRange,
            QueryDefinition queryDefinition,
            string continuationToken,
            QueryRequestOptions requestOptions = null);

        public abstract FeedIterator<T> GetItemQueryIterator<T>(
            FeedRange feedRange,
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null);
#endif

        public abstract class TryExecuteQueryResult
        {
        }

        public sealed class FailedToGetQueryPlanResult : TryExecuteQueryResult
        {
            public FailedToGetQueryPlanResult(Exception exception)
            {
                this.Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            }

            public Exception Exception { get; }
        }

        public sealed class QueryPlanNotSupportedResult : TryExecuteQueryResult
        {
            public QueryPlanNotSupportedResult(PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
            {
                this.QueryPlan = partitionedQueryExecutionInfo ?? throw new ArgumentNullException(nameof(partitionedQueryExecutionInfo));
            }

            public PartitionedQueryExecutionInfo QueryPlan { get; }
        }

        public sealed class QueryPlanIsSupportedResult : TryExecuteQueryResult
        {
            public QueryPlanIsSupportedResult(QueryIterator queryIterator)
            {
                this.QueryIterator = queryIterator ?? throw new ArgumentNullException(nameof(queryIterator));
            }

            public QueryIterator QueryIterator { get; }
        }
    }
}