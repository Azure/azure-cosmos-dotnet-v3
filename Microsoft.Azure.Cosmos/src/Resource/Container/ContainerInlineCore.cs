//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;

    // This class acts as a wrapper for environments that use SynchronizationContext.
    internal sealed partial class ContainerInlineCore : ContainerCore
    {
        internal ContainerInlineCore(
            CosmosClientContext clientContext,
            DatabaseInternal database,
            string containerId,
            CosmosQueryClient cosmosQueryClient = null)
            : base(clientContext,
                database,
                containerId,
                cosmosQueryClient)
        {
        }

        public override Task<ContainerResponse> ReadContainerAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => base.ReadContainerAsync(requestOptions, cancellationToken));
        }

        public override Task<ResponseMessage> ReadContainerStreamAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => base.ReadContainerStreamAsync(requestOptions, cancellationToken));
        }

        public override Task<ContainerResponse> ReplaceContainerAsync(
            ContainerProperties containerProperties,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => base.ReplaceContainerAsync(containerProperties, requestOptions, cancellationToken));
        }

        public override Task<ResponseMessage> ReplaceContainerStreamAsync(
            ContainerProperties containerProperties,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => base.ReplaceContainerStreamAsync(containerProperties, requestOptions, cancellationToken));
        }

        public override Task<ContainerResponse> DeleteContainerAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => base.DeleteContainerAsync(requestOptions, cancellationToken));
        }

        public override Task<ResponseMessage> DeleteContainerStreamAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => base.DeleteContainerStreamAsync(requestOptions, cancellationToken));
        }

        public override Task<int?> ReadThroughputAsync(CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => base.ReadThroughputAsync(cancellationToken));
        }

        public override Task<ThroughputResponse> ReadThroughputAsync(
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => base.ReadThroughputAsync(requestOptions, cancellationToken));
        }

        public override Task<ThroughputResponse> ReplaceThroughputAsync(
            int throughput,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => base.ReplaceThroughputAsync(throughput, requestOptions, cancellationToken));
        }

#if PREVIEW
        public override Task<ThroughputResponse> ReplaceThroughputAsync(ThroughputProperties throughputProperties, RequestOptions requestOptions = null, CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => base.ReplaceThroughputAsync(throughputProperties, requestOptions, cancellationToken));
        }
#endif

        public override Task<ResponseMessage> CreateItemStreamAsync(
            Stream streamPayload,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => base.CreateItemStreamAsync(streamPayload, partitionKey, requestOptions, cancellationToken));
        }

        public override Task<ItemResponse<T>> CreateItemAsync<T>(T item,
            PartitionKey? partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => base.CreateItemAsync<T>(item, partitionKey, requestOptions, cancellationToken));
        }

        public override Task<ResponseMessage> ReadItemStreamAsync(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => base.ReadItemStreamAsync(id, partitionKey, requestOptions, cancellationToken));
        }

        public override Task<ItemResponse<T>> ReadItemAsync<T>(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => base.ReadItemAsync<T>(id, partitionKey, requestOptions, cancellationToken));
        }

        public override Task<ResponseMessage> UpsertItemStreamAsync(
            Stream streamPayload,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => base.UpsertItemStreamAsync(streamPayload, partitionKey, requestOptions, cancellationToken));
        }

        public override Task<ItemResponse<T>> UpsertItemAsync<T>(
            T item,
            PartitionKey? partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => base.UpsertItemAsync<T>(item, partitionKey, requestOptions, cancellationToken));
        }

        public override Task<ResponseMessage> ReplaceItemStreamAsync(
            Stream streamPayload,
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => base.ReplaceItemStreamAsync(streamPayload, id, partitionKey, requestOptions, cancellationToken));
        }

        public override Task<ItemResponse<T>> ReplaceItemAsync<T>(
            T item,
            string id,
            PartitionKey? partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => base.ReplaceItemAsync<T>(item, id, partitionKey, requestOptions, cancellationToken));
        }

        public override Task<ResponseMessage> DeleteItemStreamAsync(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => base.DeleteItemStreamAsync(id, partitionKey, requestOptions, cancellationToken));
        }

        public override Task<ItemResponse<T>> DeleteItemAsync<T>(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return TaskHelper.RunInlineIfNeededAsync(() => base.DeleteItemAsync<T>(id, partitionKey, requestOptions, cancellationToken));
        }

        public override FeedIterator GetItemQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(base.GetItemQueryStreamIterator(
                    queryDefinition,
                    continuationToken,
                    requestOptions));
        }

        public override FeedIterator<T> GetItemQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(base.GetItemQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator GetItemQueryStreamIterator(string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(base.GetItemQueryStreamIterator(
                queryText,
                continuationToken,
                requestOptions));
        }

        public override FeedIterator<T> GetItemQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(base.GetItemQueryIterator<T>(
                queryText,
                continuationToken,
                requestOptions));
        }

        public override IOrderedQueryable<T> GetItemLinqQueryable<T>(bool allowSynchronousQueryExecution = false,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return base.GetItemLinqQueryable<T>(
                allowSynchronousQueryExecution,
                continuationToken,
                requestOptions);
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder<T>(
            string processorName,
            ChangesHandler<T> onChangesDelegate)
        {
            return base.GetChangeFeedProcessorBuilder<T>(processorName, onChangesDelegate);
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedEstimatorBuilder(string processorName,
            ChangesEstimationHandler estimationDelegate,
            TimeSpan? estimationPeriod = null)
        {
            return base.GetChangeFeedEstimatorBuilder(processorName, estimationDelegate, estimationPeriod);
        }

        public override TransactionalBatch CreateTransactionalBatch(PartitionKey partitionKey)
        {
            return base.CreateTransactionalBatch(partitionKey);
        }

#if PREVIEW
        public override Task<IReadOnlyList<FeedToken>> GetFeedTokensAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return TaskHelper.RunInlineIfNeededAsync(() => base.GetFeedTokensAsync(cancellationToken));
        }

        public override FeedIterator GetChangeFeedStreamIterator(ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            return base.GetChangeFeedStreamIterator(changeFeedRequestOptions);
        }

        public override FeedIterator GetChangeFeedStreamIterator(
            FeedToken feedToken,
            ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            return base.GetChangeFeedStreamIterator(feedToken, changeFeedRequestOptions);
        }

        public override FeedIterator GetChangeFeedStreamIterator(
            PartitionKey partitionKey,
            ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            return base.GetChangeFeedStreamIterator(partitionKey, changeFeedRequestOptions);
        }

        public override FeedIterator<T> GetChangeFeedIterator<T>(ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            return base.GetChangeFeedIterator<T>(changeFeedRequestOptions);
        }

        public override FeedIterator<T> GetChangeFeedIterator<T>(
            FeedToken feedToken,
            ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            return base.GetChangeFeedIterator<T>(feedToken, changeFeedRequestOptions);
        }

        public override FeedIterator<T> GetChangeFeedIterator<T>(
            PartitionKey partitionKey,
            ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            return base.GetChangeFeedIterator<T>(partitionKey, changeFeedRequestOptions);
        }

        public override Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            FeedToken feedToken,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return TaskHelper.RunInlineIfNeededAsync(() => base.GetPartitionKeyRangesAsync(feedToken, cancellationToken));
        }

        public override FeedIterator GetItemQueryStreamIterator(
            FeedToken feedToken,
            string queryText = null,
            QueryRequestOptions requestOptions = null)
        {
            return base.GetItemQueryStreamIterator(feedToken, queryText, requestOptions);
        }

        public override FeedIterator<T> GetItemQueryIterator<T>(
            FeedToken feedToken,
            string queryText = null,
            QueryRequestOptions requestOptions = null)
        {
            return base.GetItemQueryIterator<T>(feedToken, queryText, requestOptions);
        }

        public override FeedIterator GetItemQueryStreamIterator(
            FeedToken feedToken,
            QueryDefinition queryDefinition,
            QueryRequestOptions requestOptions = null)
        {
            return base.GetItemQueryStreamIterator(feedToken, queryDefinition, requestOptions);
        }

        public override FeedIterator<T> GetItemQueryIterator<T>(
            FeedToken feedToken,
            QueryDefinition queryDefinition,
            QueryRequestOptions requestOptions = null)
        {
            return base.GetItemQueryIterator<T>(feedToken, queryDefinition, requestOptions);
        }
#endif
    }
}
