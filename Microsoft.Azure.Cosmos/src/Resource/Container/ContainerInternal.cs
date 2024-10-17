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
            ITrace trace,
            CancellationToken cancellationToken = default);

        public abstract Task<Documents.Routing.PartitionKeyInternal> GetNonePartitionKeyValueAsync(
            ITrace trace,
            CancellationToken cancellationToken);

        public abstract Task<CollectionRoutingMap> GetRoutingMapAsync(CancellationToken cancellationToken);

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

        /// <summary>
        /// Patches an item in the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <remarks>
        /// By default, resource body will be returned as part of the response. User can request no content by setting <see cref="ItemRequestOptions.EnableContentResponseOnWrite"/> flag to false.
        /// </remarks>
        /// <param name="id">The Cosmos item id</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="streamPayload">Represents a stream containing the list of operations to be sequentially applied to the referred Cosmos item.</param>
        /// <param name="requestOptions">(Optional) The options for the item request.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>
        /// A <see cref="Task"/> containing a <see cref="ResponseMessage"/> which wraps a <see cref="Stream"/> containing the patched resource record.
        /// </returns>
        public abstract Task<ResponseMessage> PatchItemStreamAsync(
            string id,
            PartitionKey partitionKey,
            Stream streamPayload,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default);

#if !PREVIEW
        public abstract Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            FeedRange feedRange,
            CancellationToken cancellationToken = default);

        public abstract ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes<T>(
            string processorName,
            ChangeFeedHandler<ChangeFeedItem<T>> onChangesDelegate);

        public abstract Task<bool> IsFeedRangePartOfAsync(
            Cosmos.FeedRange x,
            Cosmos.FeedRange y,
            CancellationToken cancellationToken = default);
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

        public abstract FeedIterator GetChangeFeedStreamIteratorWithQuery(
            ChangeFeedStartFrom changeFeedStartFrom,
            ChangeFeedMode changeFeedMode,
            ChangeFeedQuerySpec changeFeedQuerySpec,
            ChangeFeedRequestOptions changeFeedRequestOptions = null);

        public abstract FeedIterator<T> GetChangeFeedIteratorWithQuery<T>(
           ChangeFeedStartFrom changeFeedStartFrom,
           ChangeFeedMode changeFeedMode,
           ChangeFeedQuerySpec changeFeedQuerySpec,
           ChangeFeedRequestOptions changeFeedRequestOptions = null);
    }
}
