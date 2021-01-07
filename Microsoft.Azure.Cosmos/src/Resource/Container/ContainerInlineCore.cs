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
    using Microsoft.Azure.Cosmos.ChangeFeed;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.ReadFeed;
    using Microsoft.Azure.Cosmos.Tracing;

    // This class acts as a wrapper for environments that use SynchronizationContext.
    internal sealed class ContainerInlineCore : ContainerCore
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
            return this.ClientContext.OperationHelperAsync(
                nameof(ReadContainerAsync),
                requestOptions,
                (diagnostics, trace) => base.ReadContainerAsync(diagnostics, trace, requestOptions, cancellationToken));
        }

        public override Task<ResponseMessage> ReadContainerStreamAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReadContainerStreamAsync),
                requestOptions,
                (diagnostics, trace) => base.ReadContainerStreamAsync(diagnostics, trace, requestOptions, cancellationToken));
        }

        public override Task<ContainerResponse> ReplaceContainerAsync(
            ContainerProperties containerProperties,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReplaceContainerAsync),
                requestOptions,
                (diagnostics, trace) => base.ReplaceContainerAsync(diagnostics, containerProperties, trace, requestOptions, cancellationToken));
        }

        public override Task<ResponseMessage> ReplaceContainerStreamAsync(
            ContainerProperties containerProperties,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReplaceContainerStreamAsync),
                requestOptions,
                (diagnostics, trace) => base.ReplaceContainerStreamAsync(diagnostics, containerProperties, trace, requestOptions, cancellationToken));
        }

        public override Task<ContainerResponse> DeleteContainerAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(DeleteContainerAsync),
                requestOptions,
                (diagnostics, trace) => base.DeleteContainerAsync(diagnostics, trace, requestOptions, cancellationToken));
        }

        public override Task<ResponseMessage> DeleteContainerStreamAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(DeleteContainerStreamAsync),
                requestOptions,
                (diagnostics, trace) => base.DeleteContainerStreamAsync(diagnostics, trace, requestOptions, cancellationToken));
        }

        public override Task<int?> ReadThroughputAsync(CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReadThroughputAsync),
                null,
                (diagnostics, trace) => base.ReadThroughputAsync(diagnostics, trace, cancellationToken));
        }

        public override Task<ThroughputResponse> ReadThroughputAsync(
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReadThroughputAsync),
                requestOptions,
                (diagnostics, trace) => base.ReadThroughputAsync(diagnostics, requestOptions, trace, cancellationToken));
        }

        public override Task<ThroughputResponse> ReplaceThroughputAsync(
            int throughput,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReplaceThroughputAsync),
                requestOptions,
                (diagnostics, trace) => base.ReplaceThroughputAsync(diagnostics, throughput, trace, requestOptions, cancellationToken));
        }

        public override Task<ThroughputResponse> ReplaceThroughputAsync(
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReplaceThroughputAsync),
                requestOptions,
                (diagnostics, trace) => base.ReplaceThroughputAsync(diagnostics, throughputProperties, trace, requestOptions, cancellationToken));
        }

        public override Task<ThroughputResponse> ReadThroughputIfExistsAsync(RequestOptions requestOptions, CancellationToken cancellationToken)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReadThroughputIfExistsAsync),
                requestOptions,
                (diagnostics, trace) => base.ReadThroughputIfExistsAsync(diagnostics, requestOptions, trace, cancellationToken));
        }

        public override Task<ThroughputResponse> ReplaceThroughputIfExistsAsync(ThroughputProperties throughput, RequestOptions requestOptions, CancellationToken cancellationToken)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReplaceThroughputIfExistsAsync),
                requestOptions,
                (diagnostics, trace) => base.ReplaceThroughputIfExistsAsync(diagnostics, throughput, trace, requestOptions, cancellationToken));
        }

        public override Task<ResponseMessage> CreateItemStreamAsync(
            Stream streamPayload,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            Task<ResponseMessage> func(CosmosDiagnosticsContext diagnostics, ITrace trace)
            {
                return base.CreateItemStreamAsync(
                    diagnostics,
                    streamPayload,
                    partitionKey,
                    trace,
                    requestOptions,
                    cancellationToken);
            }

            return this.ClientContext.OperationHelperAsync<ResponseMessage>(
                nameof(CreateItemStreamAsync),
                requestOptions,
                func);
        }

        public override Task<ItemResponse<T>> CreateItemAsync<T>(T item,
            PartitionKey? partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(CreateItemAsync),
                requestOptions,
                (diagnostics, trace) => base.CreateItemAsync<T>(diagnostics, item, trace, partitionKey, requestOptions, cancellationToken));
        }

        public override Task<ResponseMessage> ReadItemStreamAsync(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReadItemStreamAsync),
                requestOptions,
                (diagnostics, trace) => base.ReadItemStreamAsync(diagnostics, id, partitionKey, trace, requestOptions, cancellationToken));
        }

        public override Task<ItemResponse<T>> ReadItemAsync<T>(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReadItemAsync),
                requestOptions,
                (diagnostics, trace) => base.ReadItemAsync<T>(diagnostics, id, partitionKey, trace, requestOptions, cancellationToken));
        }

        public override Task<ResponseMessage> UpsertItemStreamAsync(
            Stream streamPayload,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(UpsertItemStreamAsync),
                requestOptions,
                (diagnostics, trace) => base.UpsertItemStreamAsync(diagnostics, streamPayload, partitionKey, trace, requestOptions, cancellationToken));
        }

        public override Task<ItemResponse<T>> UpsertItemAsync<T>(
            T item,
            PartitionKey? partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(UpsertItemAsync),
                requestOptions,
                (diagnostics, trace) => base.UpsertItemAsync<T>(diagnostics, item, trace, partitionKey, requestOptions, cancellationToken));
        }

        public override Task<ResponseMessage> ReplaceItemStreamAsync(
            Stream streamPayload,
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReplaceItemStreamAsync),
                requestOptions,
                (diagnostics, trace) => base.ReplaceItemStreamAsync(diagnostics, streamPayload, id, partitionKey, trace, requestOptions, cancellationToken));
        }

        public override Task<ItemResponse<T>> ReplaceItemAsync<T>(
            T item,
            string id,
            PartitionKey? partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(ReplaceItemAsync),
                requestOptions,
                (diagnostics, trace) => base.ReplaceItemAsync<T>(diagnostics, item, id, trace, partitionKey, requestOptions, cancellationToken));
        }

        public override Task<ResponseMessage> DeleteItemStreamAsync(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(DeleteItemStreamAsync),
                requestOptions,
                (diagnostics, trace) => base.DeleteItemStreamAsync(diagnostics, id, partitionKey, trace, requestOptions, cancellationToken));
        }

        public override Task<ItemResponse<T>> DeleteItemAsync<T>(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(DeleteItemAsync),
                requestOptions,
                (diagnostics, trace) => base.DeleteItemAsync<T>(diagnostics, id, partitionKey, trace, requestOptions, cancellationToken));
        }

        public override Task<ResponseMessage> PatchItemStreamAsync(
                string id,
                PartitionKey partitionKey,
                IReadOnlyList<PatchOperation> patchOperations,
                ItemRequestOptions requestOptions = null,
                CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(PatchItemStreamAsync),
                requestOptions,
                (diagnostics, trace) => base.PatchItemStreamAsync(diagnostics, id, partitionKey, patchOperations, trace, requestOptions, cancellationToken));
        }

        public override Task<ItemResponse<T>> PatchItemAsync<T>(
                string id,
                PartitionKey partitionKey,
                IReadOnlyList<PatchOperation> patchOperations,
                ItemRequestOptions requestOptions = null,
                CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(PatchItemAsync),
                requestOptions,
                (diagnostics, trace) => base.PatchItemAsync<T>(diagnostics, id, partitionKey, patchOperations, trace, requestOptions, cancellationToken));
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

        public override ChangeFeedEstimator GetChangeFeedEstimator(
            string processorName,
            Container leaseContainer)
        {
            return base.GetChangeFeedEstimator(processorName, leaseContainer);
        }

        public override TransactionalBatch CreateTransactionalBatch(PartitionKey partitionKey)
        {
            return base.CreateTransactionalBatch(partitionKey);
        }

        public override Task<IReadOnlyList<FeedRange>> GetFeedRangesAsync(CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(GetFeedRangesAsync),
                null,
                (diagnostics, trace) => base.GetFeedRangesAsync(diagnostics, trace, cancellationToken));
        }

        public override FeedIterator GetChangeFeedStreamIterator(
            ChangeFeedStartFrom changeFeedStartFrom,
            ChangeFeedMode changeFeedMode,
            ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            return base.GetChangeFeedStreamIterator(changeFeedStartFrom, changeFeedMode, changeFeedRequestOptions);
        }

        public override FeedIterator<T> GetChangeFeedIterator<T>(
            ChangeFeedStartFrom changeFeedStartFrom,
            ChangeFeedMode changeFeedMode,
            ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            return base.GetChangeFeedIterator<T>(changeFeedStartFrom, changeFeedMode, changeFeedRequestOptions);
        }

        public override Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            FeedRange feedRange,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                nameof(GetPartitionKeyRangesAsync),
                null,
                (diagnostics, trace) => base.GetPartitionKeyRangesAsync(feedRange, cancellationToken));
        }

        public override FeedIterator GetItemQueryStreamIterator(
            FeedRange feedRange,
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return base.GetItemQueryStreamIterator(feedRange, queryDefinition, continuationToken, requestOptions);
        }

        public override FeedIterator<T> GetItemQueryIterator<T>(
            FeedRange feedRange,
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return base.GetItemQueryIterator<T>(feedRange, queryDefinition, continuationToken, requestOptions);
        }

        public override FeedIteratorInternal GetReadFeedIterator(QueryDefinition queryDefinition, QueryRequestOptions queryRequestOptions, string resourceLink, Documents.ResourceType resourceType, string continuationToken, int pageSize)
        {
            return base.GetReadFeedIterator(queryDefinition, queryRequestOptions, resourceLink, resourceType, continuationToken, pageSize);
        }

        public override IAsyncEnumerable<TryCatch<ChangeFeedPage>> GetChangeFeedAsyncEnumerable(
            ChangeFeedCrossFeedRangeState state,
            ChangeFeedMode changeFeedMode,
            ChangeFeedRequestOptions changeFeedRequestOptions = default)
        {
            return base.GetChangeFeedAsyncEnumerable(state, changeFeedMode, changeFeedRequestOptions);
        }

        public override IAsyncEnumerable<TryCatch<ReadFeedPage>> GetReadFeedAsyncEnumerable(
            ReadFeedCrossFeedRangeState state,
            QueryRequestOptions requestOptions = null)
        {
            return base.GetReadFeedAsyncEnumerable(state, requestOptions);
        }
    }
}