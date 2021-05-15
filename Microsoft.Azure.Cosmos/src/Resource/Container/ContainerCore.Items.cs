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
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.ReadFeed;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Serializer;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

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

            ReadManyHelper readManyHelper = new ReadManyQueryHelper(await this.GetPartitionKeyDefinitionAsync(),
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

        /// <summary>
        /// Used in the compute gateway to support legacy gateway interface.
        /// </summary>
        public override async Task<TryExecuteQueryResult> TryExecuteQueryAsync(
            QueryFeatures supportedQueryFeatures,
            QueryDefinition queryDefinition,
            string continuationToken,
            FeedRangeInternal feedRangeInternal,
            QueryRequestOptions requestOptions,
            CancellationToken cancellationToken = default)
        {
            if (queryDefinition == null)
            {
                throw new ArgumentNullException(nameof(queryDefinition));
            }

            if (requestOptions == null)
            {
                throw new ArgumentNullException(nameof(requestOptions));
            }

            if (feedRangeInternal != null)
            {
                // The user has scoped down to a physical partition or logical partition.
                // In either case let the query execute as a passthrough.
                QueryIterator passthroughQueryIterator = QueryIterator.Create(
                    containerCore: this,
                    client: this.queryClient,
                    clientContext: this.ClientContext,
                    sqlQuerySpec: queryDefinition.ToSqlQuerySpec(),
                    continuationToken: continuationToken,
                    feedRangeInternal: feedRangeInternal,
                    queryRequestOptions: requestOptions,
                    resourceLink: this.LinkUri,
                    isContinuationExpected: false,
                    allowNonValueAggregateQuery: true,
                    forcePassthrough: true, // Forcing a passthrough, since we don't want to get the query plan nor try to rewrite it.
                    partitionedQueryExecutionInfo: null);

                return new QueryPlanIsSupportedResult(passthroughQueryIterator);
            }

            cancellationToken.ThrowIfCancellationRequested();

            Documents.PartitionKeyDefinition partitionKeyDefinition;
            if (requestOptions.Properties != null
                && requestOptions.Properties.TryGetValue("x-ms-query-partitionkey-definition", out object partitionKeyDefinitionObject))
            {
                if (!(partitionKeyDefinitionObject is Documents.PartitionKeyDefinition definition))
                {
                    throw new ArgumentException(
                        "partitionkeydefinition has invalid type",
                        nameof(partitionKeyDefinitionObject));
                }

                partitionKeyDefinition = definition;
            }
            else
            {
                ContainerQueryProperties containerQueryProperties = await this.queryClient.GetCachedContainerQueryPropertiesAsync(
                    this.LinkUri,
                    requestOptions.PartitionKey,
                    NoOpTrace.Singleton,
                    cancellationToken);
                partitionKeyDefinition = containerQueryProperties.PartitionKeyDefinition;
            }

            QueryPlanHandler queryPlanHandler = new QueryPlanHandler(this.queryClient);

            TryCatch<(PartitionedQueryExecutionInfo queryPlan, bool supported)> tryGetQueryInfoAndIfSupported = await queryPlanHandler.TryGetQueryInfoAndIfSupportedAsync(
                supportedQueryFeatures,
                queryDefinition.ToSqlQuerySpec(),
                partitionKeyDefinition,
                requestOptions.PartitionKey.HasValue,
                cancellationToken);

            if (tryGetQueryInfoAndIfSupported.Failed)
            {
                return new FailedToGetQueryPlanResult(tryGetQueryInfoAndIfSupported.Exception);
            }

            (PartitionedQueryExecutionInfo queryPlan, bool supported) = tryGetQueryInfoAndIfSupported.Result;
            TryExecuteQueryResult tryExecuteQueryResult;
            if (supported)
            {
                QueryIterator queryIterator = QueryIterator.Create(
                    containerCore: this,
                    client: this.queryClient,
                    clientContext: this.ClientContext,
                    sqlQuerySpec: queryDefinition.ToSqlQuerySpec(),
                    continuationToken: continuationToken,
                    feedRangeInternal: feedRangeInternal,
                    queryRequestOptions: requestOptions,
                    resourceLink: this.LinkUri,
                    isContinuationExpected: false,
                    allowNonValueAggregateQuery: true,
                    forcePassthrough: false,
                    partitionedQueryExecutionInfo: queryPlan);

                tryExecuteQueryResult = new QueryPlanIsSupportedResult(queryIterator);
            }
            else
            {
                tryExecuteQueryResult = new QueryPlanNotSupportedResult(queryPlan);
            }

            return tryExecuteQueryResult;
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

            if (linqSerializerOptions == null && this.ClientContext.ClientOptions.SerializerOptions != null)
            {
                linqSerializerOptions = new CosmosLinqSerializerOptions
                {
                    PropertyNamingPolicy = this.ClientContext.ClientOptions.SerializerOptions.PropertyNamingPolicy
                };
            }

            return new CosmosLinqQuery<T>(
                this,
                this.ClientContext.ResponseFactory,
                (CosmosQueryClientCore)this.queryClient,
                continuationToken,
                requestOptions,
                allowSynchronousQueryExecution,
                linqSerializerOptions);
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
            return this.GetChangeFeedProcessorBuilderPrivate(processorName, observerFactory);
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
            return this.GetChangeFeedProcessorBuilderPrivate(processorName, observerFactory);
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
            return this.GetChangeFeedProcessorBuilderPrivate(processorName, observerFactory);
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
            return this.GetChangeFeedProcessorBuilderPrivate(processorName, observerFactory);
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
            return this.GetChangeFeedProcessorBuilderPrivate(processorName, observerFactory);
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

            ChangeFeedPaginationOptions changeFeedPaginationOptions = new ChangeFeedPaginationOptions(
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
                    requestOptions);

                DocumentContainer documentContainer = new DocumentContainer(networkAttachedDocumentContainer);

                ReadFeedPaginationOptions.PaginationDirection? direction = null;
                if ((requestOptions.Properties != null) && requestOptions.Properties.TryGetValue(HttpConstants.HttpHeaders.EnumerationDirection, out object enumerationDirection))
                {
                    direction = (byte)enumerationDirection == (byte)RntbdConstants.RntdbEnumerationDirection.Reverse ? ReadFeedPaginationOptions.PaginationDirection.Reverse : ReadFeedPaginationOptions.PaginationDirection.Forward;
                }

                ReadFeedPaginationOptions readFeedPaginationOptions = new ReadFeedPaginationOptions(
                    direction,
                    pageSizeHint: requestOptions.MaxItemCount ?? int.MaxValue);

                return new ReadFeedIteratorCore(
                    documentContainer,
                    continuationToken,
                    readFeedPaginationOptions,
                    requestOptions,
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
                forcePassthrough: false,
                partitionedQueryExecutionInfo: null);
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
                    forcePassthrough: false,
                    partitionedQueryExecutionInfo: null);
            }
            else
            {
                ReadFeedPaginationOptions.PaginationDirection? direction = null;
                if ((queryRequestOptions.Properties != null) && queryRequestOptions.Properties.TryGetValue(HttpConstants.HttpHeaders.EnumerationDirection, out object enumerationDirection))
                {
                    direction = (byte)enumerationDirection == (byte)RntbdConstants.RntdbEnumerationDirection.Reverse ? ReadFeedPaginationOptions.PaginationDirection.Reverse : ReadFeedPaginationOptions.PaginationDirection.Forward;
                }

                ReadFeedPaginationOptions readFeedPaginationOptions = new ReadFeedPaginationOptions(
                    direction,
                    pageSizeHint: queryRequestOptions.MaxItemCount ?? int.MaxValue);

                feedIterator = new ReadFeedIteratorCore(
                    documentContainer: documentContainer,
                    queryRequestOptions: queryRequestOptions,
                    continuationToken: continuationToken,
                    readFeedPaginationOptions: readFeedPaginationOptions,
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
                queryRequestOptions);
            DocumentContainer documentContainer = new DocumentContainer(networkAttachedDocumentContainer);

            ReadFeedPaginationOptions.PaginationDirection? direction = null;
            if ((queryRequestOptions?.Properties != null) && queryRequestOptions.Properties.TryGetValue(HttpConstants.HttpHeaders.EnumerationDirection, out object enumerationDirection))
            {
                direction = (byte)enumerationDirection == (byte)RntbdConstants.RntdbEnumerationDirection.Reverse ? ReadFeedPaginationOptions.PaginationDirection.Reverse : ReadFeedPaginationOptions.PaginationDirection.Forward;
            }

            ReadFeedPaginationOptions readFeedPaginationOptions = new ReadFeedPaginationOptions(
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
                PartitionKeyDefinition pKeyDefinition = await this.GetPartitionKeyDefinitionAsync();
                if (partitionKey.HasValue && partitionKey.Value != PartitionKey.None && partitionKey.Value.InternalKey.Components.Count != pKeyDefinition.Paths.Count)
                {
                    throw new ArgumentException(RMResources.MissingPartitionKeyValue);
                }

                return await this.ProcessItemStreamAsync(
                        partitionKey,
                        itemId,
                        itemStream,
                        operationType,
                        requestOptions,
                        trace: trace,
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
          CancellationToken cancellationToken = default(CancellationToken))
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

        private ChangeFeedProcessorBuilder GetChangeFeedProcessorBuilderPrivate(
            string processorName,
            ChangeFeedObserverFactory observerFactory)
        {
            ChangeFeedProcessorCore changeFeedProcessor = new ChangeFeedProcessorCore(observerFactory);
            return new ChangeFeedProcessorBuilder(
                processorName: processorName,
                container: this,
                changeFeedProcessor: changeFeedProcessor,
                applyBuilderConfiguration: changeFeedProcessor.ApplyBuildConfiguration);
        }
    }
}
