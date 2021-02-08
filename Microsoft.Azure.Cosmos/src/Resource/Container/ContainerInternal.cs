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
    using Microsoft.Azure.Cosmos.ChangeFeed;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.ReadFeed;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

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

        public Task<string> GetCachedRIDAsync(
            CancellationToken cancellationToken)
        {
            return this.GetCachedRIDAsync(forceRefresh: false, trace: NoOpTrace.Singleton, cancellationToken);
        }

        public abstract Task<string> GetCachedRIDAsync(
            bool forceRefresh,
            ITrace trace,
            CancellationToken cancellationToken);

        public abstract Task<Documents.PartitionKeyDefinition> GetPartitionKeyDefinitionAsync(
            CancellationToken cancellationToken);

        public abstract Task<ContainerProperties> GetCachedContainerPropertiesAsync(
            bool forceRefresh,
            ITrace trace,
            CancellationToken cancellationToken);

        public abstract Task<IReadOnlyList<IReadOnlyList<string>>> GetPartitionKeyPathTokensAsync(
            CancellationToken cancellationToken = default);

        public abstract Task<Documents.Routing.PartitionKeyInternal> GetNonePartitionKeyValueAsync(
            ITrace trace,
            CancellationToken cancellationToken);

        public abstract Task<CollectionRoutingMap> GetRoutingMapAsync(CancellationToken cancellationToken);

        public abstract Task<TryExecuteQueryResult> TryExecuteQueryAsync(
            QueryFeatures supportedQueryFeatures,
            QueryDefinition queryDefinition,
            string continuationToken,
            FeedRangeInternal feedRangeInternal,
            QueryRequestOptions requestOptions,
            CancellationToken cancellationToken = default);

        public abstract FeedIterator GetStandByFeedIterator(
            string continuationToken = default,
            int? maxItemCount = default,
            StandByFeedIteratorRequestOptions requestOptions = default);

        public abstract FeedIteratorInternal GetItemQueryStreamIteratorInternal(
            SqlQuerySpec sqlQuerySpec,
            bool isContinuationExcpected,
            string continuationToken,
            FeedRangeInternal feedRange,
            QueryRequestOptions requestOptions);

        public abstract FeedIteratorInternal GetReadFeedIterator(
            QueryDefinition queryDefinition,
            QueryRequestOptions queryRequestOptions,
            string resourceLink,
            ResourceType resourceType,
            string continuationToken,
            int pageSize);

        public abstract Task<PartitionKey> GetPartitionKeyValueFromStreamAsync(
            Stream stream,
            ITrace trace,
            CancellationToken cancellation);

        public abstract IAsyncEnumerable<TryCatch<ChangeFeedPage>> GetChangeFeedAsyncEnumerable(
            ChangeFeedCrossFeedRangeState state,
            ChangeFeedMode changeFeedMode,
            ChangeFeedRequestOptions changeFeedRequestOptions = null);

        public abstract IAsyncEnumerable<TryCatch<ReadFeedPage>> GetReadFeedAsyncEnumerable(
            ReadFeedCrossFeedRangeState state,
            QueryRequestOptions requestOptions = null);

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
            PatchItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        public abstract Task<ItemResponse<T>> PatchItemAsync<T>(
            string id,
            PartitionKey partitionKey,
            IReadOnlyList<PatchOperation> patchOperations,
            PatchItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

        public abstract Task<ResponseMessage> DeleteAllItemsByPartitionKeyStreamAsync(
               Cosmos.PartitionKey partitionKey,
               RequestOptions requestOptions = null,
               CancellationToken cancellationToken = default(CancellationToken));
#endif

#if !PREVIEW
        public abstract Task<IReadOnlyList<FeedRange>> GetFeedRangesAsync(CancellationToken cancellationToken = default);

        public abstract FeedIterator GetChangeFeedStreamIterator(
            ChangeFeedStartFrom changeFeedStartFrom,
            ChangeFeedMode changeFeedMode,
            ChangeFeedRequestOptions changeFeedRequestOptions = null);

        public abstract FeedIterator<T> GetChangeFeedIterator<T>(
            ChangeFeedStartFrom changeFeedStartFrom,
            ChangeFeedMode changeFeedMode,
            ChangeFeedRequestOptions changeFeedRequestOptions = null);

        public abstract Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            FeedRange feedRange,
            CancellationToken cancellationToken = default);

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