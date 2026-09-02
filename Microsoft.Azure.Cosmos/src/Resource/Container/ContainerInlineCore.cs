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
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;
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
                operationName: nameof(ReadContainerAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Read,
                requestOptions: requestOptions,
                task: (trace) => base.ReadContainerAsync(trace, requestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReadContainer, (response) => new OpenTelemetryResponse<ContainerProperties>(response)));
        }

        public override Task<ResponseMessage> ReadContainerStreamAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReadContainerStreamAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Read,
                requestOptions: requestOptions,
                task: (trace) => base.ReadContainerStreamAsync(trace, requestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReadContainer, (response) => new OpenTelemetryResponse(response)));
        }

        public override Task<ContainerResponse> ReplaceContainerAsync(
            ContainerProperties containerProperties,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReplaceContainerAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Replace,
                requestOptions: requestOptions,
                task: (trace) => base.ReplaceContainerAsync(containerProperties, trace, requestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReplaceContainer, (response) => new OpenTelemetryResponse<ContainerProperties>(response)));
        }

        public override Task<ResponseMessage> ReplaceContainerStreamAsync(
            ContainerProperties containerProperties,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReplaceContainerStreamAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Replace,
                requestOptions: requestOptions,
                task: (trace) => base.ReplaceContainerStreamAsync(containerProperties, trace, requestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReplaceContainer, (response) => new OpenTelemetryResponse(response)));
        }

        public override Task<ContainerResponse> DeleteContainerAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(DeleteContainerAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Delete,
                requestOptions: requestOptions,
                task: (trace) => base.DeleteContainerAsync(trace, requestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.DeleteContainer, (response) => new OpenTelemetryResponse<ContainerProperties>(response)));
        }

        public override Task<ResponseMessage> DeleteContainerStreamAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(DeleteContainerStreamAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Delete,
                requestOptions: requestOptions,
                task: (trace) => base.DeleteContainerStreamAsync(trace, requestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.DeleteContainer, (response) => new OpenTelemetryResponse(response)));
        }

        public override Task<int?> ReadThroughputAsync(CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReadThroughputAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Read,
                requestOptions: null,
                task: (trace) => base.ReadThroughputAsync(trace, cancellationToken));
        }

        public override Task<ThroughputResponse> ReadThroughputAsync(
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReadThroughputAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Read,
                requestOptions: requestOptions,
                task: (trace) => base.ReadThroughputAsync(requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReadThroughput, (response) => new OpenTelemetryResponse<ThroughputProperties>(response)));
        }

        public override Task<ThroughputResponse> ReplaceThroughputAsync(
            int throughput,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReplaceThroughputAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Replace,
                requestOptions: requestOptions,
                task: (trace) => base.ReplaceThroughputAsync(throughput, trace, requestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReplaceThroughput, (response) => new OpenTelemetryResponse<ThroughputProperties>(response)));
        }

        public override Task<ThroughputResponse> ReplaceThroughputAsync(
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReplaceThroughputAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Replace,
                requestOptions: requestOptions,
                task: (trace) => base.ReplaceThroughputAsync(throughputProperties, trace, requestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReplaceThroughput, (response) => new OpenTelemetryResponse<ThroughputProperties>(response)));
        }

        public override Task<ThroughputResponse> ReadThroughputIfExistsAsync(RequestOptions requestOptions, CancellationToken cancellationToken)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReadThroughputIfExistsAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Read,
                requestOptions: requestOptions,
                task: (trace) => base.ReadThroughputIfExistsAsync(requestOptions, trace, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReadThroughputIfExists, (response) => new OpenTelemetryResponse<ThroughputProperties>(response)));
        }

        public override Task<ThroughputResponse> ReplaceThroughputIfExistsAsync(ThroughputProperties throughput, RequestOptions requestOptions, CancellationToken cancellationToken)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReplaceThroughputIfExistsAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Replace,
                requestOptions: requestOptions,
                task: (trace) => base.ReplaceThroughputIfExistsAsync(throughput, trace, requestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReplaceThroughputIfExists, (response) => new OpenTelemetryResponse<ThroughputProperties>(response)));
        }

        public override Task<ResponseMessage> CreateItemStreamAsync(
            Stream streamPayload,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            Task<ResponseMessage> func(ITrace trace)
            {
                return base.CreateItemStreamAsync(
                    streamPayload,
                    partitionKey,
                    trace,
                    requestOptions,
                    cancellationToken);
            }

            return this.ClientContext.OperationHelperAsync<ResponseMessage>(
                operationName: nameof(CreateItemStreamAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Create,
                requestOptions: requestOptions,
                task: func,
                openTelemetry: new (OpenTelemetryConstants.Operations.CreateItem, (response) => new OpenTelemetryResponse(response)),
                resourceType: Documents.ResourceType.Document);
        }

        public override Task<ItemResponse<T>> CreateItemAsync<T>(T item,
            PartitionKey? partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(CreateItemAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Create,
                requestOptions: requestOptions,
                task: (trace) => base.CreateItemAsync<T>(item, trace, partitionKey, requestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.CreateItem, (response) => new OpenTelemetryResponse<T>(response)),
                resourceType: Documents.ResourceType.Document);
        }

        public override Task<ResponseMessage> ReadItemStreamAsync(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReadItemStreamAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Read,
                requestOptions: requestOptions,
                task: (trace) => base.ReadItemStreamAsync(id, partitionKey, trace, requestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReadItem, (response) => new OpenTelemetryResponse(response)),
                resourceType: Documents.ResourceType.Document);
        }

        public override Task<ItemResponse<T>> ReadItemAsync<T>(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReadItemAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Read,
                requestOptions: requestOptions,
                task: (trace) => base.ReadItemAsync<T>(id, partitionKey, trace, requestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReadItem, (response) => new OpenTelemetryResponse<T>(response)),
                resourceType: Documents.ResourceType.Document);
        }

        public override Task<ResponseMessage> UpsertItemStreamAsync(
            Stream streamPayload,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(UpsertItemStreamAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Upsert,
                requestOptions: requestOptions,
                task: (trace) => base.UpsertItemStreamAsync(streamPayload, partitionKey, trace, requestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.UpsertItem, (response) => new OpenTelemetryResponse(response)),
                resourceType: Documents.ResourceType.Document);
        }

        public override Task<ItemResponse<T>> UpsertItemAsync<T>(
            T item,
            PartitionKey? partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(UpsertItemAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Upsert,
                requestOptions: requestOptions,
                task: (trace) => base.UpsertItemAsync<T>(item, trace, partitionKey, requestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.UpsertItem, (response) => new OpenTelemetryResponse<T>(response)),
                resourceType: Documents.ResourceType.Document);
        }

        public override Task<ResponseMessage> ReplaceItemStreamAsync(
            Stream streamPayload,
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReplaceItemStreamAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Replace,
                requestOptions: requestOptions,
                task: (trace) => base.ReplaceItemStreamAsync(streamPayload, id, partitionKey, trace, requestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReplaceItem, (response) => new OpenTelemetryResponse(response)),
                resourceType: Documents.ResourceType.Document);
        }

        public override Task<ItemResponse<T>> ReplaceItemAsync<T>(
            T item,
            string id,
            PartitionKey? partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReplaceItemAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Replace,
                requestOptions: requestOptions,
                task: (trace) => base.ReplaceItemAsync<T>(item, id, trace, partitionKey, requestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReplaceItem, (response) => new OpenTelemetryResponse<T>(response)),
                resourceType: Documents.ResourceType.Document);
        }

        public override Task<ResponseMessage> DeleteItemStreamAsync(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(DeleteItemStreamAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Delete,
                requestOptions: requestOptions,
                task: (trace) => base.DeleteItemStreamAsync(id, partitionKey, trace, requestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.DeleteItem, (response) => new OpenTelemetryResponse(response)),
                resourceType: Documents.ResourceType.Document);
        }

        public override Task<ItemResponse<T>> DeleteItemAsync<T>(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(DeleteItemAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Delete,
                requestOptions: requestOptions,
                task: (trace) => base.DeleteItemAsync<T>(id, partitionKey, trace, requestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.DeleteItem, (response) => new OpenTelemetryResponse<T>(response)),
                resourceType: Documents.ResourceType.Document);
        }

        public override Task<ResponseMessage> PatchItemStreamAsync(
                string id,
                PartitionKey partitionKey,
                IReadOnlyList<PatchOperation> patchOperations,
                PatchItemRequestOptions requestOptions = null,
                CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(PatchItemStreamAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Patch,
                requestOptions: requestOptions,
                task: (trace) => base.PatchItemStreamAsync(id, partitionKey, patchOperations, trace, requestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.PatchItem, (response) => new OpenTelemetryResponse(response)),
                resourceType: Documents.ResourceType.Document);
        }

        public override Task<ResponseMessage> PatchItemStreamAsync(
                string id,
                PartitionKey partitionKey,
                Stream streamPayload,
                ItemRequestOptions requestOptions = null,
                CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(PatchItemStreamAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Patch,
                requestOptions: requestOptions,
                task: (trace) => base.PatchItemStreamAsync(id, partitionKey, streamPayload, trace, requestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.PatchItem, (response) => new OpenTelemetryResponse(response)),
                resourceType: Documents.ResourceType.Document);
        }

        public override Task<ItemResponse<T>> PatchItemAsync<T>(
                string id,
                PartitionKey partitionKey,
                IReadOnlyList<PatchOperation> patchOperations,
                PatchItemRequestOptions requestOptions = null,
                CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(PatchItemAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Patch,
                requestOptions: requestOptions,
                task: (trace) => base.PatchItemAsync<T>(id, partitionKey, patchOperations, trace, requestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.PatchItem, (response) => new OpenTelemetryResponse<T>(response)),
                resourceType: Documents.ResourceType.Document);
        }

        public override Task<ResponseMessage> ReadManyItemsStreamAsync(
            IReadOnlyList<(string id, PartitionKey partitionKey)> items,
            ReadManyRequestOptions readManyRequestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReadManyItemsStreamAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Read,
                requestOptions: readManyRequestOptions,
                task: (trace) => base.ReadManyItemsStreamAsync(items, trace, readManyRequestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReadManyItems, (response) => new OpenTelemetryResponse(responseMessage: response)));
        }

        public override Task<FeedResponse<T>> ReadManyItemsAsync<T>(
            IReadOnlyList<(string id, PartitionKey partitionKey)> items,
            ReadManyRequestOptions readManyRequestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(ReadManyItemsAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Read,
                requestOptions: readManyRequestOptions,
                task: (trace) => base.ReadManyItemsAsync<T>(items, trace, readManyRequestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.ReadManyItems, (response) => new OpenTelemetryResponse<T>(responseMessage: response)));
        }

        public override FeedIterator GetItemQueryStreamIterator(
                  QueryDefinition queryDefinition,
                  string continuationToken = null,
                  QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(base.GetItemQueryStreamIterator(
                    queryDefinition,
                    continuationToken,
                    requestOptions),
                    this.ClientContext);
        }

        public override FeedIterator<T> GetItemQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(base.GetItemQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override FeedIterator GetItemQueryStreamIterator(string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(base.GetItemQueryStreamIterator(
                queryText,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override FeedIterator<T> GetItemQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(base.GetItemQueryIterator<T>(
                queryText,
                continuationToken,
                requestOptions),
                this.ClientContext);
        }

        public override IOrderedQueryable<T> GetItemLinqQueryable<T>(bool allowSynchronousQueryExecution = false,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CosmosLinqSerializerOptions linqSerializerOptions = null)
        {
            return base.GetItemLinqQueryable<T>(
                allowSynchronousQueryExecution,
                continuationToken,
                requestOptions,
                linqSerializerOptions);
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder<T>(
            string processorName,
            ChangesHandler<T> onChangesDelegate)
        {
            return base.GetChangeFeedProcessorBuilder<T>(processorName, onChangesDelegate);
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder<T>(
            string processorName,
            ChangeFeedHandler<T> onChangesDelegate)
        {
            return base.GetChangeFeedProcessorBuilder<T>(processorName, onChangesDelegate);
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilderWithManualCheckpoint<T>(
            string processorName,
            ChangeFeedHandlerWithManualCheckpoint<T> onChangesDelegate)
        {
            return base.GetChangeFeedProcessorBuilderWithManualCheckpoint<T>(processorName, onChangesDelegate);
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder(
            string processorName,
            ChangeFeedStreamHandler onChangesDelegate)
        {
            return base.GetChangeFeedProcessorBuilder(processorName, onChangesDelegate);
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilderWithManualCheckpoint(
            string processorName,
            ChangeFeedStreamHandlerWithManualCheckpoint onChangesDelegate)
        {
            return base.GetChangeFeedProcessorBuilderWithManualCheckpoint(processorName, onChangesDelegate);
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
            // TODO: The current use of Documents.OperationType.ReadFeed is not a precise fit for this operation.
            // A more suitable or generic Documents.OperationType should be created in the future to accurately represent this action.

            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(GetFeedRangesAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.ReadFeed,
                requestOptions: null,
                task: (trace) => base.GetFeedRangesAsync(trace, cancellationToken));
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
            return new FeedIteratorInlineCore<T>(base.GetChangeFeedIterator<T>(changeFeedStartFrom, 
                                                 changeFeedMode, 
                                                 changeFeedRequestOptions),
                                                 this.ClientContext);
        }

        public override Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            FeedRange feedRange,
            CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(GetPartitionKeyRangesAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Read,
                requestOptions: null,
                task: (trace) => base.GetPartitionKeyRangesAsync(feedRange, trace, cancellationToken));
        }

        public override FeedIterator GetItemQueryStreamIterator(
            FeedRange feedRange,
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore(
                base.GetItemQueryStreamIterator(feedRange, queryDefinition, continuationToken, requestOptions),
                this.ClientContext);
        }

        public override FeedIterator<T> GetItemQueryIterator<T>(
            FeedRange feedRange,
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorInlineCore<T>(
                base.GetItemQueryIterator<T>(feedRange, queryDefinition, continuationToken, requestOptions),
                this.ClientContext);
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

        public override Task<ResponseMessage> DeleteAllItemsByPartitionKeyStreamAsync(
          Cosmos.PartitionKey partitionKey,
          RequestOptions requestOptions = null,
          CancellationToken cancellationToken = default)
        {
            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(DeleteAllItemsByPartitionKeyStreamAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.Delete,
                requestOptions: requestOptions,
                task: (trace) => base.DeleteAllItemsByPartitionKeyStreamAsync(partitionKey, trace, requestOptions, cancellationToken),
                openTelemetry: new (OpenTelemetryConstants.Operations.DeleteAllItemsByPartitionKey, (response) => new OpenTelemetryResponse(response)));
        }

        public override Task<bool> IsFeedRangePartOfAsync(
            FeedRange x,
            FeedRange y,
            CancellationToken cancellationToken = default)
        {
            // TODO: The current use of Documents.OperationType.ReadFeed is not a precise fit for this operation.
            // A more suitable or generic Documents.OperationType should be created in the future to accurately represent this action.

            return this.ClientContext.OperationHelperAsync(
                operationName: nameof(IsFeedRangePartOfAsync),
                containerName: this.Id,
                databaseName: this.Database.Id,
                operationType: Documents.OperationType.ReadFeed,
                requestOptions: null,
                task: (trace) => base.IsFeedRangePartOfAsync(
                    x,
                    y,
                    cancellationToken: cancellationToken));
        }
    }
}