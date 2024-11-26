//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.ReadFeed;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Serializer;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Used to perform operations on items. There are two different types of operations.
    /// 1. The object operations where it serializes and deserializes the item on request/response
    /// 2. The stream response which takes a Stream containing a JSON serialized object and returns a response containing a Stream
    /// </summary>
    internal abstract partial class ContainerCore : ContainerInternal
    {
        /// <summary>
        /// Cache the full URI segment without the last resource id.
        /// This allows only a single con-cat operation instead of building the full URI string each time.
        /// </summary>
        private string cachedUriSegmentWithoutId { get; }

        private readonly CosmosQueryClient queryClient;

        public async Task<ResponseMessage> CreateItemStreamAsync(
            Stream streamPayload,
            PartitionKey partitionKey,
            ITrace trace,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return await this.ProcessItemStreamAsync(
                partitionKey: partitionKey,
                itemId: null,
                streamPayload: streamPayload,
                operationType: OperationType.Create,
                requestOptions: requestOptions,
                trace: trace,
                targetResponseSerializationFormat: JsonSerializationFormat.Text,
                cancellationToken: cancellationToken);
        }

        public async Task<ItemResponse<T>> CreateItemAsync<T>(
            T item,
            ITrace trace,
            PartitionKey? partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            ResponseMessage response = await this.ExtractPartitionKeyAndProcessItemStreamAsync(
                partitionKey: partitionKey,
                itemId: null,
                item: item,
                operationType: OperationType.Create,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateItemResponse<T>(response);
        }

        public async Task<ResponseMessage> ReadItemStreamAsync(
            string id,
            PartitionKey partitionKey,
            ITrace trace,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return await this.ProcessItemStreamAsync(
                partitionKey: partitionKey,
                itemId: id,
                streamPayload: null,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                trace: trace,
                targetResponseSerializationFormat: JsonSerializationFormat.Text,
                cancellationToken: cancellationToken);
        }

        public async Task<ItemResponse<T>> ReadItemAsync<T>(
            string id,
            PartitionKey partitionKey,
            ITrace trace,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            ResponseMessage response = await this.ProcessItemStreamAsync(
                partitionKey: partitionKey,
                itemId: id,
                streamPayload: null,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                trace: trace,
                targetResponseSerializationFormat: default,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateItemResponse<T>(response);
        }

        public async Task<ResponseMessage> UpsertItemStreamAsync(
            Stream streamPayload,
            PartitionKey partitionKey,
            ITrace trace,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return await this.ProcessItemStreamAsync(
                partitionKey: partitionKey,
                itemId: null,
                streamPayload: streamPayload,
                operationType: OperationType.Upsert,
                requestOptions: requestOptions,
                trace: trace,
                targetResponseSerializationFormat: JsonSerializationFormat.Text,
                cancellationToken: cancellationToken);
        }

        public async Task<ItemResponse<T>> UpsertItemAsync<T>(
            T item,
            ITrace trace,
            PartitionKey? partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            ResponseMessage response = await this.ExtractPartitionKeyAndProcessItemStreamAsync(
                partitionKey: partitionKey,
                itemId: null,
                item: item,
                operationType: OperationType.Upsert,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateItemResponse<T>(response);
        }

        public async Task<ResponseMessage> ReplaceItemStreamAsync(
            Stream streamPayload,
            string id,
            PartitionKey partitionKey,
            ITrace trace,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return await this.ProcessItemStreamAsync(
                partitionKey: partitionKey,
                itemId: id,
                streamPayload: streamPayload,
                operationType: OperationType.Replace,
                requestOptions: requestOptions,
                trace: trace,
                targetResponseSerializationFormat: JsonSerializationFormat.Text,
                cancellationToken: cancellationToken);
        }

        public async Task<ItemResponse<T>> ReplaceItemAsync<T>(
            T item,
            string id,
            ITrace trace,
            PartitionKey? partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            ResponseMessage response = await this.ExtractPartitionKeyAndProcessItemStreamAsync(
               partitionKey: partitionKey,
               itemId: id,
               item: item,
               operationType: OperationType.Replace,
               requestOptions: requestOptions,
               trace: trace,
               cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateItemResponse<T>(response);
        }

        public async Task<ResponseMessage> DeleteItemStreamAsync(
            string id,
            PartitionKey partitionKey,
            ITrace trace,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return await this.ProcessItemStreamAsync(
                partitionKey: partitionKey,
                itemId: id,
                streamPayload: null,
                operationType: OperationType.Delete,
                requestOptions: requestOptions,
                trace: trace,
                targetResponseSerializationFormat: JsonSerializationFormat.Text,
                cancellationToken: cancellationToken);
        }

        public async Task<ItemResponse<T>> DeleteItemAsync<T>(
            string id,
            PartitionKey partitionKey,
            ITrace trace,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            ResponseMessage response = await this.ProcessItemStreamAsync(
                partitionKey: partitionKey,
                itemId: id,
                streamPayload: null,
                operationType: OperationType.Delete,
                requestOptions: requestOptions,
                trace: trace,
                targetResponseSerializationFormat: default,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateItemResponse<T>(response);
        }

        public override FeedIterator GetItemQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetItemQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public override FeedIterator GetItemQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return this.GetItemQueryStreamIteratorInternal(
                sqlQuerySpec: queryDefinition?.ToSqlQuerySpec(),
                isContinuationExcpected: true,
                continuationToken: continuationToken,
                feedRange: null,
                requestOptions: requestOptions);
        }

        public async Task<ResponseMessage> ReadManyItemsStreamAsync(
            IReadOnlyList<(string id, PartitionKey partitionKey)> items,
            ITrace trace,
            ReadManyRequestOptions readManyRequestOptions = null,
            CancellationToken cancellationToken = default)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            PartitionKeyDefinition partitionKeyDefinition;
            try
            {
                partitionKeyDefinition = await this.GetPartitionKeyDefinitionAsync();
            }
            catch (CosmosException ex)
            {
                return ex.ToCosmosResponseMessage(request: null);
            }

            ReadManyHelper readManyHelper = new ReadManyQueryHelper(partitionKeyDefinition,
                                                                    this);

            return await readManyHelper.ExecuteReadManyRequestAsync(items,
                                                                    readManyRequestOptions,
                                                                    trace,
                                                                    cancellationToken);
        }

        public async Task<FeedResponse<T>> ReadManyItemsAsync<T>(
            IReadOnlyList<(string id, PartitionKey partitionKey)> items,
            ITrace trace,
            ReadManyRequestOptions readManyRequestOptions = null,
            CancellationToken cancellationToken = default)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            ReadManyHelper readManyHelper = new ReadManyQueryHelper(await this.GetPartitionKeyDefinitionAsync(),
                                                                    this);

            return await readManyHelper.ExecuteReadManyRequestAsync<T>(items,
                                                                    readManyRequestOptions,
                                                                    trace,
                                                                    cancellationToken);
        }

        public override FeedIterator<T> GetItemQueryIterator<T>(
           string queryText = null,
           string continuationToken = null,
           QueryRequestOptions requestOptions = null)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetItemQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public override FeedIterator<T> GetItemQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            requestOptions ??= new QueryRequestOptions();

            if (requestOptions.IsEffectivePartitionKeyRouting)
            {
                requestOptions.PartitionKey = null;
            }

            if (!(this.GetItemQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions) is FeedIteratorInternal feedIterator))
            {
                throw new InvalidOperationException($"Expected a FeedIteratorInternal.");
            }

            return new FeedIteratorCore<T>(
                feedIterator: feedIterator,
                responseCreator: this.ClientContext.ResponseFactory.CreateQueryFeedUserTypeResponse<T>);
        }

        public override IOrderedQueryable<T> GetItemLinqQueryable<T>(
            bool allowSynchronousQueryExecution = false,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null,
            CosmosLinqSerializerOptions linqSerializerOptions = null)
        {
            requestOptions ??= new QueryRequestOptions();

            if (this.ClientContext.ClientOptions != null)
            {
                linqSerializerOptions ??= new CosmosLinqSerializerOptions
                {
                    PropertyNamingPolicy = this.ClientContext.ClientOptions.SerializerOptions?.PropertyNamingPolicy ?? CosmosPropertyNamingPolicy.Default             
                };
            }

            CosmosLinqSerializerOptionsInternal linqSerializerOptionsInternal = CosmosLinqSerializerOptionsInternal.Create(linqSerializerOptions, this.ClientContext.ClientOptions.Serializer);

            return new CosmosLinqQuery<T>(
                this,
                this.ClientContext.ResponseFactory,
                (CosmosQueryClientCore)this.queryClient,
                continuationToken,
                requestOptions,
                allowSynchronousQueryExecution,
                linqSerializerOptionsInternal);
        }

        public override FeedIterator<T> GetItemQueryIterator<T>(
            FeedRange feedRange,
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            requestOptions ??= new QueryRequestOptions();

            if (!(this.GetItemQueryStreamIterator(
                feedRange,
                queryDefinition,
                continuationToken,
                requestOptions) is FeedIteratorInternal feedIterator))
            {
                throw new InvalidOperationException($"Expected a FeedIteratorInternal.");
            }

            return new FeedIteratorCore<T>(
                feedIterator: feedIterator,
                responseCreator: this.ClientContext.ResponseFactory.CreateQueryFeedUserTypeResponse<T>);
        }

        public override FeedIterator GetItemQueryStreamIterator(
            FeedRange feedRange,
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            FeedRangeInternal feedRangeInternal = feedRange as FeedRangeInternal;
            return this.GetItemQueryStreamIteratorInternal(
                sqlQuerySpec: queryDefinition?.ToSqlQuerySpec(),
                isContinuationExcpected: true,
                continuationToken: continuationToken,
                feedRange: feedRangeInternal,
                requestOptions: requestOptions);
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder<T>(
            string processorName,
            ChangesHandler<T> onChangesDelegate)
        {
            if (processorName == null)
            {
                throw new ArgumentNullException(nameof(processorName));
            }

            if (onChangesDelegate == null)
            {
                throw new ArgumentNullException(nameof(onChangesDelegate));
            }

            ChangeFeedObserverFactory observerFactory = new CheckpointerObserverFactory(
                new ChangeFeedObserverFactoryCore<T>(onChangesDelegate, this.ClientContext.SerializerCore),
                withManualCheckpointing: false);
            return this.GetChangeFeedProcessorBuilderPrivate(processorName,
                observerFactory, ChangeFeedMode.LatestVersion);
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder<T>(
            string processorName,
            ChangeFeedHandler<T> onChangesDelegate)
        {
            if (processorName == null)
            {
                throw new ArgumentNullException(nameof(processorName));
            }

            if (onChangesDelegate == null)
            {
                throw new ArgumentNullException(nameof(onChangesDelegate));
            }

            ChangeFeedObserverFactory observerFactory = new CheckpointerObserverFactory(
                new ChangeFeedObserverFactoryCore<T>(onChangesDelegate, this.ClientContext.SerializerCore),
                withManualCheckpointing: false);
            return this.GetChangeFeedProcessorBuilderPrivate(processorName,
                observerFactory, ChangeFeedMode.LatestVersion);
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilderWithManualCheckpoint<T>(
            string processorName,
            ChangeFeedHandlerWithManualCheckpoint<T> onChangesDelegate)
        {
            if (processorName == null)
            {
                throw new ArgumentNullException(nameof(processorName));
            }

            if (onChangesDelegate == null)
            {
                throw new ArgumentNullException(nameof(onChangesDelegate));
            }

            ChangeFeedObserverFactory observerFactory = new CheckpointerObserverFactory(
                new ChangeFeedObserverFactoryCore<T>(onChangesDelegate, this.ClientContext.SerializerCore),
                withManualCheckpointing: true);
            return this.GetChangeFeedProcessorBuilderPrivate(processorName,
                observerFactory, ChangeFeedMode.LatestVersion);
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilder(
            string processorName,
            ChangeFeedStreamHandler onChangesDelegate)
        {
            if (processorName == null)
            {
                throw new ArgumentNullException(nameof(processorName));
            }

            if (onChangesDelegate == null)
            {
                throw new ArgumentNullException(nameof(onChangesDelegate));
            }

            ChangeFeedObserverFactory observerFactory = new CheckpointerObserverFactory(
                new ChangeFeedObserverFactoryCore(onChangesDelegate),
                withManualCheckpointing: false);
            return this.GetChangeFeedProcessorBuilderPrivate(processorName,
                observerFactory, ChangeFeedMode.LatestVersion);
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilderWithManualCheckpoint(
            string processorName,
            ChangeFeedStreamHandlerWithManualCheckpoint onChangesDelegate)
        {
            if (processorName == null)
            {
                throw new ArgumentNullException(nameof(processorName));
            }

            if (onChangesDelegate == null)
            {
                throw new ArgumentNullException(nameof(onChangesDelegate));
            }

            ChangeFeedObserverFactory observerFactory = new CheckpointerObserverFactory(
                new ChangeFeedObserverFactoryCore(onChangesDelegate),
                withManualCheckpointing: true);
            return this.GetChangeFeedProcessorBuilderPrivate(processorName,
                observerFactory,
                ChangeFeedMode.LatestVersion);
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedEstimatorBuilder(
            string processorName,
            ChangesEstimationHandler estimationDelegate,
            TimeSpan? estimationPeriod = null)
        {
            if (processorName == null)
            {
                throw new ArgumentNullException(nameof(processorName));
            }

            if (estimationDelegate == null)
            {
                throw new ArgumentNullException(nameof(estimationDelegate));
            }

            ChangeFeedEstimatorRunner changeFeedEstimatorCore = new ChangeFeedEstimatorRunner(estimationDelegate, estimationPeriod);
            return new ChangeFeedProcessorBuilder(
                processorName: processorName,
                container: this,
                changeFeedProcessor: changeFeedEstimatorCore,
                applyBuilderConfiguration: changeFeedEstimatorCore.ApplyBuildConfiguration);
        }

        public override ChangeFeedEstimator GetChangeFeedEstimator(
            string processorName,
            Container leaseContainer)
        {
            if (processorName == null)
            {
                throw new ArgumentNullException(nameof(processorName));
            }

            if (leaseContainer == null)
            {
                throw new ArgumentNullException(nameof(leaseContainer));
            }

            return new ChangeFeedEstimatorCore(
                processorName: processorName,
                monitoredContainer: this,
                leaseContainer: (ContainerInternal)leaseContainer,
                documentServiceLeaseContainer: default);
        }

        public override TransactionalBatch CreateTransactionalBatch(PartitionKey partitionKey)
        {
            return new BatchCore(this, partitionKey);
        }

        public override IAsyncEnumerable<TryCatch<ChangeFeed.ChangeFeedPage>> GetChangeFeedAsyncEnumerable(
            ChangeFeedCrossFeedRangeState state,
            ChangeFeedMode changeFeedMode,
            ChangeFeedRequestOptions changeFeedRequestOptions = default)
        {
            NetworkAttachedDocumentContainer networkAttachedDocumentContainer = new NetworkAttachedDocumentContainer(
                this,
                this.queryClient,
                Guid.NewGuid(),
                changeFeedRequestOptions: changeFeedRequestOptions);
            DocumentContainer documentContainer = new DocumentContainer(networkAttachedDocumentContainer);

            Dictionary<string, string> additionalHeaders;
            
            if ((changeFeedRequestOptions?.Properties != null) && changeFeedRequestOptions.Properties.Any())
            {
                Dictionary<string, object> additionalNonStringHeaders = new Dictionary<string, object>();
                additionalHeaders = new Dictionary<string, string>();
                foreach (KeyValuePair<string, object> keyValuePair in changeFeedRequestOptions.Properties)
                {
                    if (keyValuePair.Value is string stringValue)
                    {
                        additionalHeaders[keyValuePair.Key] = stringValue;
                    }
                    else
                    {
                        additionalNonStringHeaders[keyValuePair.Key] = keyValuePair.Value;
                    }
                }

                changeFeedRequestOptions.Properties = additionalNonStringHeaders;
            }
            else
            {
                additionalHeaders = null;
            }

            ChangeFeedExecutionOptions changeFeedPaginationOptions = new ChangeFeedExecutionOptions(
                changeFeedMode,
                changeFeedRequestOptions?.PageSizeHint,
                changeFeedRequestOptions?.JsonSerializationFormatOptions?.JsonSerializationFormat,
                additionalHeaders);

            return new ChangeFeedCrossFeedRangeAsyncEnumerable(
                documentContainer,
                state,
                changeFeedPaginationOptions,
                changeFeedRequestOptions?.JsonSerializationFormatOptions);
        }

        public override FeedIterator GetStandByFeedIterator(
            string continuationToken = null,
            int? maxItemCount = null,
            StandByFeedIteratorRequestOptions requestOptions = null)
        {
            StandByFeedIteratorRequestOptions cosmosQueryRequestOptions = requestOptions ?? new StandByFeedIteratorRequestOptions();

            return new StandByFeedIteratorCore(
                clientContext: this.ClientContext,
                continuationToken: continuationToken,
                maxItemCount: maxItemCount,
                container: this,
                options: cosmosQueryRequestOptions);
        }

        /// <summary>
        /// Helper method to create a stream feed iterator.
        /// It decides if it is a query or read feed and create
        /// the correct instance.
        /// </summary>
        public override FeedIteratorInternal GetItemQueryStreamIteratorInternal(
            SqlQuerySpec sqlQuerySpec,
            bool isContinuationExcpected,
            string continuationToken,
            FeedRangeInternal feedRange,
            QueryRequestOptions requestOptions)
        {
            requestOptions ??= new QueryRequestOptions();

            if (requestOptions.IsEffectivePartitionKeyRouting)
            {
                if (feedRange != null)
                {
                    throw new ArgumentException(nameof(feedRange), ClientResources.FeedToken_EffectivePartitionKeyRouting);
                }

                requestOptions.PartitionKey = null;
            }

            if (sqlQuerySpec == null)
            {
                NetworkAttachedDocumentContainer networkAttachedDocumentContainer = new NetworkAttachedDocumentContainer(
                    this,
                    this.queryClient,
                    Guid.NewGuid(),
                    requestOptions);

                DocumentContainer documentContainer = new DocumentContainer(networkAttachedDocumentContainer);

                ReadFeedExecutionOptions.PaginationDirection? direction = null;
                if ((requestOptions.Properties != null) && requestOptions.Properties.TryGetValue(HttpConstants.HttpHeaders.EnumerationDirection, out object enumerationDirection))
                {
                    direction = (byte)enumerationDirection == (byte)RntbdConstants.RntdbEnumerationDirection.Reverse ? ReadFeedExecutionOptions.PaginationDirection.Reverse : ReadFeedExecutionOptions.PaginationDirection.Forward;
                }

                ReadFeedExecutionOptions readFeedPaginationOptions = new ReadFeedExecutionOptions(
                    direction,
                    pageSizeHint: requestOptions.MaxItemCount ?? int.MaxValue);

                return new ReadFeedIteratorCore(
                    documentContainer,
                    continuationToken,
                    readFeedPaginationOptions,
                    requestOptions,
                    this,
                    cancellationToken: default);
            }

            return QueryIterator.Create(
                containerCore: this,
                client: this.queryClient,
                clientContext: this.ClientContext,
                sqlQuerySpec: sqlQuerySpec,
                continuationToken: continuationToken,
                feedRangeInternal: feedRange,
                queryRequestOptions: requestOptions,
                resourceLink: this.LinkUri,
                isContinuationExpected: isContinuationExcpected,
                allowNonValueAggregateQuery: true,
                partitionedQueryExecutionInfo: null,
                resourceType: ResourceType.Document);
        }

        public override FeedIteratorInternal GetReadFeedIterator(
            QueryDefinition queryDefinition,
            QueryRequestOptions queryRequestOptions,
            string resourceLink,
            ResourceType resourceType,
            string continuationToken,
            int pageSize)
        {
            queryRequestOptions ??= new QueryRequestOptions();

            NetworkAttachedDocumentContainer networkAttachedDocumentContainer = new NetworkAttachedDocumentContainer(
                this,
                this.queryClient,
                Guid.NewGuid(),
                queryRequestOptions,
                resourceLink: resourceLink,
                resourceType: resourceType);

            DocumentContainer documentContainer = new DocumentContainer(networkAttachedDocumentContainer);

            FeedIteratorInternal feedIterator;
            if (queryDefinition != null)
            {
                feedIterator = QueryIterator.Create(
                    containerCore: this,
                    client: this.queryClient,
                    clientContext: this.ClientContext,
                    sqlQuerySpec: queryDefinition.ToSqlQuerySpec(),
                    continuationToken: continuationToken,
                    feedRangeInternal: FeedRangeEpk.FullRange,
                    queryRequestOptions: queryRequestOptions,
                    resourceLink: resourceLink,
                    isContinuationExpected: false,
                    allowNonValueAggregateQuery: true,
                    partitionedQueryExecutionInfo: null,
                    resourceType: resourceType);
            }
            else
            {
                ReadFeedExecutionOptions.PaginationDirection? direction = null;
                if ((queryRequestOptions.Properties != null) && queryRequestOptions.Properties.TryGetValue(HttpConstants.HttpHeaders.EnumerationDirection, out object enumerationDirection))
                {
                    direction = (byte)enumerationDirection == (byte)RntbdConstants.RntdbEnumerationDirection.Reverse ? ReadFeedExecutionOptions.PaginationDirection.Reverse : ReadFeedExecutionOptions.PaginationDirection.Forward;
                }

                ReadFeedExecutionOptions readFeedPaginationOptions = new ReadFeedExecutionOptions(
                    direction,
                    pageSizeHint: queryRequestOptions.MaxItemCount ?? int.MaxValue);

                feedIterator = new ReadFeedIteratorCore(
                    documentContainer: documentContainer,
                    queryRequestOptions: queryRequestOptions,
                    continuationToken: continuationToken,
                    readFeedPaginationOptions: readFeedPaginationOptions,
                    container: this,
                    cancellationToken: default);
            }

            return feedIterator;
        }

        public override IAsyncEnumerable<TryCatch<ReadFeed.ReadFeedPage>> GetReadFeedAsyncEnumerable(
            ReadFeedCrossFeedRangeState state,
            QueryRequestOptions queryRequestOptions = default)
        {
            NetworkAttachedDocumentContainer networkAttachedDocumentContainer = new NetworkAttachedDocumentContainer(
                this,
                this.queryClient,
                Guid.NewGuid(),
                queryRequestOptions);
            DocumentContainer documentContainer = new DocumentContainer(networkAttachedDocumentContainer);

            ReadFeedExecutionOptions.PaginationDirection? direction = null;
            if ((queryRequestOptions?.Properties != null) && queryRequestOptions.Properties.TryGetValue(HttpConstants.HttpHeaders.EnumerationDirection, out object enumerationDirection))
            {
                direction = (byte)enumerationDirection == (byte)RntbdConstants.RntdbEnumerationDirection.Reverse ? ReadFeedExecutionOptions.PaginationDirection.Reverse : ReadFeedExecutionOptions.PaginationDirection.Forward;
            }

            ReadFeedExecutionOptions readFeedPaginationOptions = new ReadFeedExecutionOptions(
                direction,
                pageSizeHint: queryRequestOptions?.MaxItemCount);

            return new ReadFeedCrossFeedRangeAsyncEnumerable(
                documentContainer,
                state,
                readFeedPaginationOptions);
        }

        // Extracted partition key might be invalid as CollectionCache might be stale.
        // Stale collection cache is refreshed through PartitionKeyMismatchRetryPolicy
        // and partition-key is extracted again. 
        private async Task<ResponseMessage> ExtractPartitionKeyAndProcessItemStreamAsync<T>(
            PartitionKey? partitionKey,
            string itemId,
            T item,
            OperationType operationType,
            ItemRequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            Stream itemStream;
            using (trace.StartChild("ItemSerialize"))
            {
                itemStream = this.ClientContext.SerializerCore.ToStream<T>(item);
            }

            // User specified PK value, no need to extract it
            if (partitionKey.HasValue)
            {
                return await this.ProcessItemStreamAsync(
                        partitionKey,
                        itemId,
                        itemStream,
                        operationType,
                        requestOptions,
                        trace: trace,
                        targetResponseSerializationFormat: default,
                        cancellationToken: cancellationToken);
            }

            PartitionKeyMismatchRetryPolicy requestRetryPolicy = null;
            while (true)
            {
                partitionKey = await this.GetPartitionKeyValueFromStreamAsync(itemStream, trace, cancellationToken);

                ResponseMessage responseMessage = await this.ProcessItemStreamAsync(
                    partitionKey,
                    itemId,
                    itemStream,
                    operationType,
                    requestOptions,
                    trace: trace,
                    targetResponseSerializationFormat: default,
                    cancellationToken: cancellationToken);

                if (responseMessage.IsSuccessStatusCode)
                {
                    return responseMessage;
                }

                if (requestRetryPolicy == null)
                {
                    requestRetryPolicy = new PartitionKeyMismatchRetryPolicy(
                        await this.ClientContext.DocumentClient.GetCollectionCacheAsync(trace),
                        requestRetryPolicy);
                }

                ShouldRetryResult retryResult = await requestRetryPolicy.ShouldRetryAsync(responseMessage, cancellationToken);
                if (!retryResult.ShouldRetry)
                {
                    return responseMessage;
                }
            }
        }

        private async Task<ResponseMessage> ProcessItemStreamAsync(
            PartitionKey? partitionKey,
            string itemId,
            Stream streamPayload,
            OperationType operationType,
            ItemRequestOptions requestOptions,
            ITrace trace,
            JsonSerializationFormat? targetResponseSerializationFormat,
            CancellationToken cancellationToken)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            if (requestOptions != null && requestOptions.IsEffectivePartitionKeyRouting)
            {
                partitionKey = null;
            }

            ContainerInternal.ValidatePartitionKey(partitionKey, requestOptions);
            string resourceUri = this.GetResourceUri(requestOptions, operationType, itemId);

            // Convert Text to Binary Stream.
            if (ConfigurationManager.IsBinaryEncodingEnabled())
            {
                streamPayload = CosmosSerializationUtil.TrySerializeStreamToTargetFormat(
                    targetSerializationFormat: ContainerCore.GetTargetRequestSerializationFormat(),
                    inputStream: streamPayload == null ? null : await StreamExtension.AsClonableStreamAsync(
                        mediaStream: streamPayload,
                        allowUnsafeDataAccess: true));
            }

            ResponseMessage responseMessage = await this.ClientContext.ProcessResourceOperationStreamAsync(
                resourceUri: resourceUri,
                resourceType: ResourceType.Document,
                operationType: operationType,
                requestOptions: requestOptions,
                cosmosContainerCore: this,
                partitionKey: partitionKey,
                itemId: itemId,
                streamPayload: streamPayload,
                requestEnricher: null,
                trace: trace,
                cancellationToken: cancellationToken);

            // Convert Binary Stream to Text.
            if (targetResponseSerializationFormat.HasValue
                && (requestOptions == null || !requestOptions.EnableBinaryResponseOnPointOperations)
                && responseMessage?.Content is CloneableStream outputCloneableStream)
            {
                responseMessage.Content = CosmosSerializationUtil.TrySerializeStreamToTargetFormat(
                    targetSerializationFormat: targetResponseSerializationFormat.Value,
                    inputStream: outputCloneableStream);
            }

            return responseMessage;
        }

        public override async Task<PartitionKey> GetPartitionKeyValueFromStreamAsync(
            Stream stream,
            ITrace trace,
            CancellationToken cancellation = default)
        {
            if (!stream.CanSeek)
            {
                throw new ArgumentException("Stream needs to be seekable", nameof(stream));
            }

            using (ITrace childTrace = trace.StartChild("Get PkValue From Stream", TraceComponent.Routing, Tracing.TraceLevel.Info))
            {
                try
                {
                    stream.Position = 0;

                    if (!(stream is MemoryStream memoryStream))
                    {
                        memoryStream = new MemoryStream();
                        stream.CopyTo(memoryStream);
                    }

                    // TODO: Avoid copy 
                    IJsonNavigator jsonNavigator = JsonNavigator.Create(memoryStream.ToArray());
                    IJsonNavigatorNode jsonNavigatorNode = jsonNavigator.GetRootNode();
                    CosmosObject pathTraversal = CosmosObject.Create(jsonNavigator, jsonNavigatorNode);

                    IReadOnlyList<IReadOnlyList<string>> tokenslist = await this.GetPartitionKeyPathTokensAsync(childTrace, cancellation);
                    List<CosmosElement> cosmosElementList = new List<CosmosElement>(tokenslist.Count);

                    foreach (IReadOnlyList<string> tokenList in tokenslist)
                    {
                        if (ContainerCore.TryParseTokenListForElement(pathTraversal, tokenList, out CosmosElement element))
                        {
                            cosmosElementList.Add(element);
                        }
                        else
                        {
                            cosmosElementList.Add(null);
                        }
                    }

                    return ContainerCore.CosmosElementToPartitionKeyObject(cosmosElementList);
                }
                finally
                {
                    // MemoryStream casting leverage might change position 
                    stream.Position = 0;
                }
            }
        }

        public Task<ResponseMessage> DeleteAllItemsByPartitionKeyStreamAsync(
          Cosmos.PartitionKey partitionKey,
          ITrace trace,
          RequestOptions requestOptions = null,
          CancellationToken cancellationToken = default)
        {
            PartitionKey? resultingPartitionKey = requestOptions != null && requestOptions.IsEffectivePartitionKeyRouting ? null : (PartitionKey?)partitionKey;
            ContainerCore.ValidatePartitionKey(resultingPartitionKey, requestOptions);

            return this.ClientContext.ProcessResourceOperationStreamAsync(
                resourceUri: this.LinkUri,
                resourceType: ResourceType.PartitionKey,
                operationType: OperationType.Delete,
                requestOptions: requestOptions,
                cosmosContainerCore: this,
                partitionKey: resultingPartitionKey,
                itemId: null,
                streamPayload: null,
                requestEnricher: null,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        private static bool TryParseTokenListForElement(CosmosObject pathTraversal, IReadOnlyList<string> tokens, out CosmosElement result)
        {
            result = null;
            for (int i = 0; i < tokens.Count - 1; i++)
            {
                if (!pathTraversal.TryGetValue(tokens[i], out pathTraversal))
                {
                    return false;
                }
            }

            if (!pathTraversal.TryGetValue(tokens[tokens.Count - 1], out result))
            {
                return false;
            }

            return true;
        }

        private static PartitionKey CosmosElementToPartitionKeyObject(IReadOnlyList<CosmosElement> cosmosElementList)
        {
            PartitionKeyBuilder partitionKeyBuilder = new PartitionKeyBuilder();

            foreach (CosmosElement cosmosElement in cosmosElementList)
            {
                if (cosmosElement == null)
                {
                    partitionKeyBuilder.AddNoneType();
                }
                else
                {
                    _ = cosmosElement switch
                    {
                        CosmosString cosmosString => partitionKeyBuilder.Add(cosmosString.Value),
                        CosmosNumber cosmosNumber => partitionKeyBuilder.Add(Number64.ToDouble(cosmosNumber.Value)),
                        CosmosBoolean cosmosBoolean => partitionKeyBuilder.Add(cosmosBoolean.Value),
                        CosmosNull _ => partitionKeyBuilder.AddNullValue(),
                        _ => throw new ArgumentException(
                               string.Format(
                                   CultureInfo.InvariantCulture,
                                   RMResources.UnsupportedPartitionKeyComponentValue,
                                   cosmosElement)),
                    };
                }
            }

            return partitionKeyBuilder.Build();
        }

        private string GetResourceUri(RequestOptions requestOptions, OperationType operationType, string itemId)
        {
            if (requestOptions != null && requestOptions.TryGetResourceUri(out Uri resourceUri))
            {
                return resourceUri.OriginalString;
            }

            switch (operationType)
            {
                case OperationType.Create:
                case OperationType.Upsert:
                    return this.LinkUri;

                default:
                    return this.ContcatCachedUriWithId(itemId);
            }
        }

        /// <summary>
        /// Gets the full resource segment URI without the last id.
        /// </summary>
        /// <returns>Example: /dbs/*/colls/*/{this.pathSegment}/ </returns>
        private string GetResourceSegmentUriWithoutId()
        {
            // StringBuilder is roughly 2x faster than string.Format
            StringBuilder stringBuilder = new StringBuilder(this.LinkUri.Length +
                                                            Paths.DocumentsPathSegment.Length + 2);
            stringBuilder.Append(this.LinkUri);
            stringBuilder.Append("/");
            stringBuilder.Append(Paths.DocumentsPathSegment);
            stringBuilder.Append("/");
            return stringBuilder.ToString();
        }

        /// <summary>
        /// Gets the full resource URI using the cached resource URI segment 
        /// </summary>
        /// <param name="resourceId">The resource id</param>
        /// <returns>
        /// A document link in the format of {CachedUriSegmentWithoutId}/{0}/ with {0} being a Uri escaped version of the <paramref name="resourceId"/>
        /// </returns>
        /// <remarks>Would be used when creating an <see cref="Attachment"/>, or when replacing or deleting a item in Azure Cosmos DB.</remarks>
        /// <seealso cref="Uri.EscapeUriString"/>
        private string ContcatCachedUriWithId(string resourceId)
        {
            Debug.Assert(this.cachedUriSegmentWithoutId.EndsWith("/"));
            return this.cachedUriSegmentWithoutId + Uri.EscapeUriString(resourceId);
        }

        public async Task<ItemResponse<T>> PatchItemAsync<T>(
            string id,
            PartitionKey partitionKey,
            IReadOnlyList<PatchOperation> patchOperations,
            ITrace trace,
            PatchItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            ResponseMessage responseMessage = await this.PatchItemStreamAsync(
                id,
                partitionKey,
                patchOperations,
                trace,
                requestOptions,
                cancellationToken);

            return this.ClientContext.ResponseFactory.CreateItemResponse<T>(responseMessage);
        }

        public Task<ResponseMessage> PatchItemStreamAsync(
            string id,
            PartitionKey partitionKey,
            IReadOnlyList<PatchOperation> patchOperations,
            ITrace trace,
            PatchItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (partitionKey == null)
            {
                throw new ArgumentNullException(nameof(partitionKey));
            }

            if (patchOperations == null ||
                !patchOperations.Any())
            {
                throw new ArgumentNullException(nameof(patchOperations));
            }

            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            Stream patchOperationsStream;
            using (ITrace serializeTrace = trace.StartChild("Patch Operations Serialize"))
            {
                patchOperationsStream = this.ClientContext.SerializerCore.ToStream(new PatchSpec(patchOperations, requestOptions));
            }

            return this.ClientContext.ProcessResourceOperationStreamAsync(
                resourceUri: this.GetResourceUri(
                    requestOptions,
                    OperationType.Patch,
                    id),
                resourceType: ResourceType.Document,
                operationType: OperationType.Patch,
                requestOptions: requestOptions,
                cosmosContainerCore: this,
                partitionKey: partitionKey,
                itemId: id,
                streamPayload: patchOperationsStream,
                requestEnricher: null,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        public Task<ResponseMessage> PatchItemStreamAsync(
            string id,
            PartitionKey partitionKey,
            Stream streamPayload,
            ITrace trace,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            if (partitionKey == null)
            {
                throw new ArgumentNullException(nameof(partitionKey));
            }

            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (streamPayload == null)
            {
                throw new ArgumentNullException(nameof(streamPayload));
            }

            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            return this.ProcessItemStreamAsync(
                partitionKey: partitionKey,
                itemId: id,
                streamPayload: streamPayload,
                operationType: OperationType.Patch,
                requestOptions: requestOptions,
                trace: trace,
                targetResponseSerializationFormat: JsonSerializationFormat.Text,
                cancellationToken: cancellationToken);
        }

        public override ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes<T>(
            string processorName,
            ChangeFeedHandler<ChangeFeedItem<T>> onChangesDelegate)
        {
            if (processorName == null)
            {
                throw new ArgumentNullException(nameof(processorName));
            }

            if (onChangesDelegate == null)
            {
                throw new ArgumentNullException(nameof(onChangesDelegate));
            }

            ChangeFeedObserverFactory observerFactory = new CheckpointerObserverFactory(
                new ChangeFeedObserverFactoryCore<T>(onChangesDelegate, this.ClientContext.SerializerCore),
                withManualCheckpointing: false);
            return this.GetChangeFeedProcessorBuilderPrivate(processorName,
                observerFactory, ChangeFeedMode.AllVersionsAndDeletes);
        }

        private ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilderPrivate(
            string processorName,
            ChangeFeedObserverFactory observerFactory,
            ChangeFeedMode mode)
        {
            ChangeFeedProcessorCore changeFeedProcessor = new ChangeFeedProcessorCore(observerFactory);
            return new ChangeFeedProcessorBuilder(
                processorName: processorName,
                container: this,
                changeFeedProcessor: changeFeedProcessor,
                applyBuilderConfiguration: changeFeedProcessor.ApplyBuildConfiguration).WithChangeFeedMode(mode);
        }

        private static JsonSerializationFormat GetTargetRequestSerializationFormat()
        {
            return ConfigurationManager.IsBinaryEncodingEnabled()
                ? JsonSerializationFormat.Binary
                : JsonSerializationFormat.Text;
        }

        /// <summary>
        /// This method is useful for determining if a smaller, more granular feed range (y) is fully contained within a broader feed range (x), which is a common operation in distributed systems to manage partitioned data.
        ///
        /// - **x and y Feed Ranges**: Both `x` and `y` are representations of logical partitions or ranges within the Cosmos DB container.
        ///   - These ranges are typically used for operations such as querying or reading data within a specified range of partition key values.
        ///
        /// - **Validation and Parsing**:
        ///   - The method begins by validating that neither `x` nor `y` is null. If either is null, an `ArgumentNullException` is thrown.
        ///   - It then checks whether each feed range is of type `FeedRangeInternal`. If not, it attempts to parse the JSON representation of the feed range into the internal format (`FeedRangeInternal`).
        ///   - If the parsing fails, an `ArgumentException` is thrown, indicating that the feed range is of an unknown or unsupported format.
        ///
        /// - **Partition Key and Routing Map Setup**:
        ///   - The partition key definition for the container is retrieved asynchronously using `GetPartitionKeyDefinitionAsync`, as it is required to identify the partition structure.
        ///   - The method also retrieves the container's resource ID (`containerRId`) and the partition key range routing map from the `IRoutingMapProvider`. These are essential for determining the actual partition key ranges that correspond to the feed ranges.
        ///
        /// - **Effective Ranges**:
        ///   - The method uses `GetEffectiveRangesAsync` to retrieve the actual ranges of partition keys that each feed range represents.
        ///   - These effective ranges are returned as lists of `Range`, which represent the partition key boundaries.
        ///
        /// - **Inclusivity Consistency**:
        ///   - Before performing the subset comparison, the method checks that the inclusivity of the boundary conditions (`IsMinInclusive` and `IsMaxInclusive`) is consistent across all ranges in both the x and y feed ranges.
        ///   - This ensures that the comparison between ranges is logically correct and avoids potential mismatches due to differing boundary conditions.
        ///
        /// - **Subset Check**:
        ///   - Finally, the method calls `ContainerCore.IsSubset`, which checks if the merged effective range of the y feed range is fully contained within the merged effective range of the x feed range.
        ///   - Merging the ranges ensures that the comparison accounts for multiple ranges and considers the full span of each feed range.
        ///
        /// - **Exception Handling**:
        ///   - Any exceptions related to document client errors are caught, and a `CosmosException` is thrown, wrapping the original `DocumentClientException`.
        /// </summary>
        /// <param name="x">The broader feed range representing the larger, encompassing logical partition.</param>
        /// <param name="y">The smaller, more granular feed range that needs to be checked for containment within the broader feed range.</param>
        /// <param name="cancellationToken">An optional cancellation token to cancel the operation before completion.</param>
        /// <returns>Returns a boolean indicating whether the y feed range is fully contained within the x feed range.</returns>
        public override async Task<bool> IsFeedRangePartOfAsync(
            FeedRange x,
            FeedRange y,
            CancellationToken cancellationToken = default)
        {
            using (ITrace trace = Tracing.Trace.GetRootTrace("ContainerCore FeedRange IsFeedRangePartOfAsync Async", TraceComponent.Unknown, Tracing.TraceLevel.Info))
            {
                if (x == null || y == null)
                {
                    throw new ArgumentNullException(x == null
                        ? nameof(x)
                        : nameof(y), $"Argument cannot be null.");
                }

                try
                {
                    FeedRangeInternal xFeedRangeInternal = ContainerCore.ConvertToFeedRangeInternal(x, nameof(x));
                    FeedRangeInternal yFeedRangeInternal = ContainerCore.ConvertToFeedRangeInternal(y, nameof(y));

                    PartitionKeyDefinition partitionKeyDefinition = await this.GetPartitionKeyDefinitionAsync(cancellationToken);

                    string containerRId = await this.GetCachedRIDAsync(
                        forceRefresh: false,
                        trace: trace,
                        cancellationToken: cancellationToken);

                    Routing.IRoutingMapProvider routingMapProvider = await this.ClientContext.DocumentClient.GetPartitionKeyRangeCacheAsync(trace);
                    List<Documents.Routing.Range<string>> xEffectiveRanges = await xFeedRangeInternal.GetEffectiveRangesAsync(
                        routingMapProvider: routingMapProvider,
                        containerRid: containerRId,
                        partitionKeyDefinition: partitionKeyDefinition,
                        trace: trace);
                    List<Documents.Routing.Range<string>> yEffectiveRanges = await yFeedRangeInternal.GetEffectiveRangesAsync(
                        routingMapProvider: routingMapProvider,
                        containerRid: containerRId,
                        partitionKeyDefinition: partitionKeyDefinition,
                        trace: trace);

                    ContainerCore.EnsureConsistentInclusivity(xEffectiveRanges);
                    ContainerCore.EnsureConsistentInclusivity(yEffectiveRanges);

                    return ContainerCore.IsSubset(
                        ContainerCore.MergeRanges(xEffectiveRanges),
                        ContainerCore.MergeRanges(yEffectiveRanges));
                }
                catch (DocumentClientException dce)
                {
                    throw CosmosExceptionFactory.Create(dce, trace);
                }
            }
        }

        /// <summary>
        /// Converts a given feed range to its internal representation (FeedRangeInternal).
        /// If the provided feed range is already of type FeedRangeInternal, it returns it directly.
        /// Otherwise, it attempts to parse the feed range into a FeedRangeInternal.
        /// If parsing fails, an <see cref="ArgumentException"/> is thrown.
        /// </summary>
        /// <param name="feedRange">The feed range to be converted into an internal representation.</param>
        /// <param name="paramName">The name of the parameter being converted, used for exception messages.</param>
        /// <returns>The converted FeedRangeInternal object.</returns>
        /// <exception cref="ArgumentException">Thrown when the provided feed range cannot be parsed into a known format.</exception>
        private static FeedRangeInternal ConvertToFeedRangeInternal(FeedRange feedRange, string paramName)
        {
            if (feedRange is not FeedRangeInternal feedRangeInternal)
            {
                if (!FeedRangeInternal.TryParse(feedRange.ToJsonString(), out feedRangeInternal))
                {
                    throw new ArgumentException(
                        string.Format("The provided string, '{0}', for '{1}', does not represent any known format.", feedRange.ToJsonString(), paramName));
                }
            }

            return feedRangeInternal;
        }

        /// <summary>
        /// Merges a list of feed ranges into a single range by taking the minimum value of the first range and the maximum value of the last range.
        /// This function ensures that the resulting range covers the entire span of the input ranges.
        ///
        /// - The method begins by checking if the list contains only one range:
        ///   - If there is only one range, it simply returns that range without performing any additional logic.
        ///
        /// - If the list contains multiple ranges:
        ///   - It first sorts the ranges based on the minimum value of each range using a custom comparator (`MinComparer`).
        ///   - It selects the first range (after sorting) to extract the minimum value, ensuring the merged range starts with the lowest value across all ranges.
        ///   - It selects the last range (after sorting) to extract the maximum value, ensuring the merged range ends with the highest value across all ranges.
        ///
        /// - The inclusivity of the boundaries (`IsMinInclusive` and `IsMaxInclusive`) is inherited from the first range in the list:
        ///   - `IsMinInclusive` from the first range determines whether the merged range includes its minimum value.
        ///   - `IsMaxInclusive` from the last range would generally be expected to influence whether the merged range includes its maximum value, but this method uses `IsMaxInclusive` from the first range for both boundaries.
        ///   - **Note**: This could result in unexpected behavior if inclusivity should differ for the merged max value.
        ///
        /// - The merged range spans the minimum value of the first range and the maximum value of the last range, effectively combining multiple ranges into a single, continuous range.
        /// </summary>
        /// <param name="ranges">The list of feed ranges to merge. Each range contains a minimum and maximum value along with boundary inclusivity flags (`IsMinInclusive`, `IsMaxInclusive`).</param>
        /// <returns>
        /// A new merged range with the minimum value from the first range and the maximum value from the last range.
        /// If the list contains a single range, it returns that range directly without modification.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown when the list of ranges is empty.
        /// </exception>
        private static Documents.Routing.Range<string> MergeRanges(
            List<Documents.Routing.Range<string>> ranges)
        {
            if (ranges.Count == 1)
            {
                return ranges.First();
            }

            ranges.Sort(Documents.Routing.Range<string>.MinComparer.Instance);

            Documents.Routing.Range<string> firstRange = ranges.First();
            Documents.Routing.Range<string> lastRange = ranges.Last();

            return new Documents.Routing.Range<string>(
                min: firstRange.Min,
                max: lastRange.Max,
                isMinInclusive: firstRange.IsMinInclusive,
                isMaxInclusive: firstRange.IsMaxInclusive);
        }

        /// <summary>
        /// Validates whether all ranges in the list have consistent inclusivity for both `IsMinInclusive` and `IsMaxInclusive` boundaries.
        /// This ensures that all ranges either have the same inclusivity or exclusivity for their minimum and maximum boundaries.
        /// If there are any inconsistencies in the inclusivity/exclusivity of the ranges, it throws an `InvalidOperationException`.
        ///
        /// The logic works as follows:
        /// - The method assumes that the `ranges` list is never null.
        /// - It starts by checking the first range in the list to establish a baseline for comparison.
        /// - It then iterates over the remaining ranges, comparing their `IsMinInclusive` and `IsMaxInclusive` values with those of the first range.
        /// - If any range differs from the first in terms of inclusivity or exclusivity (either for the min or max boundary), the method sets a flag (`areAnyDifferent`) and exits the loop early.
        /// - If any differences are found, the method gathers the distinct `IsMinInclusive` and `IsMaxInclusive` values found across all ranges.
        /// - It then throws an `InvalidOperationException`, including the distinct values in the exception message to indicate the specific inconsistencies.
        ///
        /// This method is useful in scenarios where the ranges need to have uniform inclusivity for boundary conditions.
        /// </summary>
        /// <param name="ranges">The list of ranges to validate. Each range has `IsMinInclusive` and `IsMaxInclusive` values that represent the inclusivity of its boundaries.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when `IsMinInclusive` or `IsMaxInclusive` values are inconsistent across ranges. The exception message includes details of the inconsistencies.
        /// </exception>
        /// <example>
        /// <![CDATA[
        /// List<Documents.Routing.Range<string>> ranges = new List<Documents.Routing.Range<string>>
        /// {
        ///     new Documents.Routing.Range<string> { IsMinInclusive = true, IsMaxInclusive = false },
        ///     new Documents.Routing.Range<string> { IsMinInclusive = true, IsMaxInclusive = true },
        ///     new Documents.Routing.Range<string> { IsMinInclusive = true, IsMaxInclusive = false },
        ///     new Documents.Routing.Range<string> { IsMinInclusive = false, IsMaxInclusive = false }
        /// };
        ///
        /// EnsureConsistentInclusivity(ranges);
        ///
        /// // This will throw an InvalidOperationException because there are different inclusivity values for IsMinInclusive and IsMaxInclusive.
        /// ]]>
        /// </example>
        internal static void EnsureConsistentInclusivity(List<Documents.Routing.Range<string>> ranges)
        {
            bool areAnyDifferent = false;
            Documents.Routing.Range<string> firstRange = ranges[0];

            foreach (Documents.Routing.Range<string> range in ranges.Skip(1))
            {
                if (range.IsMinInclusive != firstRange.IsMinInclusive || range.IsMaxInclusive != firstRange.IsMaxInclusive)
                {
                    areAnyDifferent = true;
                    break;
                }
            }

            if (areAnyDifferent)
            {
                string result = $"IsMinInclusive found: {string.Join(", ", ranges.Select(range => range.IsMinInclusive).Distinct())}, IsMaxInclusive found: {string.Join(", ", ranges.Select(range => range.IsMaxInclusive).Distinct())}.";

                throw new InvalidOperationException($"Not all 'IsMinInclusive' or 'IsMaxInclusive' values are the same. {result}");
            }
        }

        /// <summary>
        /// Determines whether the specified y range is entirely within the bounds of the x range.
        /// This includes checking both the minimum and maximum boundaries of the ranges for inclusion.
        ///
        /// The method checks whether the `Min` and `Max` boundaries of `y` are within `x`,
        /// taking into account whether each boundary is inclusive or exclusive.
        ///
        /// - For the `Max` boundary:
        ///   - If the x range's max is exclusive and the y range's max is inclusive, it checks whether the x range contains the y range's max value.
        ///   - If the x range's max is inclusive and the y range's max is exclusive, this combination is not supported and a <see cref="NotSupportedException"/> is thrown.
        ///   - For all other cases, it checks if the max values are equal or whether the x range contains the y range's max.
        ///   - This applies to the following combinations:
        ///     - (false, true): x max is exclusive, y max is inclusive.
        ///     - (true, true): Both max values are inclusive.
        ///     - (false, false): Both max values are exclusive.
        ///     - (true, false): x max is inclusive, y max is exclusive.
        ///       - **NotSupportedException Scenario:** This case is not supported because handling a scenario where the x range has an inclusive maximum and the y range has an exclusive maximum requires additional logic that is not implemented.
        ///       - If encountered, a <see cref="NotSupportedException"/> is thrown with a message explaining that this combination is not supported.
        ///
        /// - For the `Min` boundary:
        ///   - It checks whether the x range contains the y range's min value, regardless of inclusivity.
        ///
        /// The method ensures the y range is considered a subset only if both its min and max values fall within the x range.
        ///
        /// Summary of combinations for `x.IsMaxInclusive` and `y.IsMaxInclusive`:
        /// 1. x.IsMaxInclusive == false, y.IsMaxInclusive == true:
        ///    - The x range is exclusive at max, but the y range is inclusive. This is supported and will check if the x contains the y's max.
        /// 2. x.IsMaxInclusive == false, y.IsMaxInclusive == false:
        ///    - Both ranges are exclusive at max. This is supported and will check if the x contains the y's max.
        /// 3. x.IsMaxInclusive == true, y.IsMaxInclusive == true:
        ///    - Both ranges are inclusive at max. This is supported and will check if the max values are equal or if the x contains the y's max.
        /// 4. x.IsMaxInclusive == true, y.IsMaxInclusive == false:
        ///    - The x range is inclusive at max, but the y range is exclusive. This combination is not supported and will result in a <see cref="NotSupportedException"/> being thrown.
        ///
        /// The method returns true only if both the min and max boundaries of the y range are within the x range's boundaries.
        ///
        /// Additionally, the method performs null checks on the parameters:
        /// - If <paramref name="x"/> is null, an <see cref="ArgumentNullException"/> is thrown.
        /// - If <paramref name="y"/> is null, an <see cref="ArgumentNullException"/> is thrown.
        ///
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="x"/> or <paramref name="y"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Thrown when <paramref name="x"/> is inclusive at max and <paramref name="y"/> is exclusive at max.
        /// This combination is not supported and requires specific handling.
        /// </exception>
        /// </summary>
        /// <example>
        /// <![CDATA[
        /// Documents.Routing.Range<string> x = new Documents.Routing.Range<string>("A", "Z", true, true);
        /// Documents.Routing.Range<string> y = new Documents.Routing.Range<string>("B", "Y", true, true);
        ///
        /// bool isSubset = IsSubset(x, y);
        /// isSubset will be true because the y range (B-Y) is fully contained within the x range (A-Z).
        /// ]]>
        /// </example>
        /// <returns>
        /// Returns <c>true</c> if the y range is a subset of the x range, meaning the y range's
        /// minimum and maximum values fall within the bounds of the x range. Returns <c>false</c> otherwise.
        /// </returns>
        internal static bool IsSubset(
            Documents.Routing.Range<string> x,
            Documents.Routing.Range<string> y)
        {
            if (x is null)
            {
                throw new ArgumentNullException(nameof(x));
            }

            if (y is null)
            {
                throw new ArgumentNullException(nameof(y));
            }

            bool isMaxWithinX = (x.IsMaxInclusive, y.IsMaxInclusive) switch
            {
                (false, true) => x.Contains(y.Max),  // x max is exclusive, y max is inclusive
                (true, false) => throw new NotSupportedException("The combination where the x range's maximum is inclusive and the y range's maximum is exclusive is not supported in the current implementation."),

                _ => ContainerCore.IsYMaxWithinX(x, y) // Default for the following combinations:
                                                       // (true, true): Both max values are inclusive
                                                       // (false, false): Both max values are exclusive
            };

            bool isMinWithinX = x.Contains(y.Min);

            return isMinWithinX && isMaxWithinX;
        }

        /// <summary>
        /// Determines whether the given maximum value of the y range is either equal to or contained within the x range.
        /// </summary>
        /// <param name="x">The x range to compare against, which defines the boundary.</param>
        /// <param name="y">The y range to be checked.</param>
        /// <returns>True if the maximum value of the y range is equal to or contained within the x range; otherwise, false.</returns>
        private static bool IsYMaxWithinX(
            Documents.Routing.Range<string> x,
            Documents.Routing.Range<string> y)
        {
            return x.Max == y.Max || x.Contains(y.Max);
        }
    }
}
