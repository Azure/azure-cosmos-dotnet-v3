//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
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
            CosmosDiagnosticsContext diagnosticsContext,
            Stream streamPayload,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.ProcessItemStreamAsync(
                partitionKey: partitionKey,
                itemId: null,
                streamPayload: streamPayload,
                operationType: OperationType.Create,
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);
        }

        public async Task<ItemResponse<T>> CreateItemAsync<T>(
            CosmosDiagnosticsContext diagnosticsContext,
            T item,
            PartitionKey? partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
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
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateItemResponse<T>(response);
        }

        public async Task<ResponseMessage> ReadItemStreamAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.ProcessItemStreamAsync(
                partitionKey: partitionKey,
                itemId: id,
                streamPayload: null,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);
        }

        public async Task<ItemResponse<T>> ReadItemAsync<T>(
            CosmosDiagnosticsContext diagnosticsContext,
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ResponseMessage response = await this.ProcessItemStreamAsync(
                partitionKey: partitionKey,
                itemId: id,
                streamPayload: null,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateItemResponse<T>(response);
        }

        public async Task<ResponseMessage> UpsertItemStreamAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            Stream streamPayload,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.ProcessItemStreamAsync(
                partitionKey: partitionKey,
                itemId: null,
                streamPayload: streamPayload,
                operationType: OperationType.Upsert,
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);
        }

        public async Task<ItemResponse<T>> UpsertItemAsync<T>(
            CosmosDiagnosticsContext diagnosticsContext,
            T item,
            PartitionKey? partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
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
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateItemResponse<T>(response);
        }

        public async Task<ResponseMessage> ReplaceItemStreamAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            Stream streamPayload,
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.ProcessItemStreamAsync(
                partitionKey: partitionKey,
                itemId: id,
                streamPayload: streamPayload,
                operationType: OperationType.Replace,
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);
        }

        public async Task<ItemResponse<T>> ReplaceItemAsync<T>(
            CosmosDiagnosticsContext diagnosticsContext,
            T item,
            string id,
            PartitionKey? partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
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
               diagnosticsContext: diagnosticsContext,
               cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateItemResponse<T>(response);
        }

        public async Task<ResponseMessage> DeleteItemStreamAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.ProcessItemStreamAsync(
                partitionKey: partitionKey,
                itemId: id,
                streamPayload: null,
                operationType: OperationType.Delete,
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);
        }

        public async Task<ItemResponse<T>> DeleteItemAsync<T>(
            CosmosDiagnosticsContext diagnosticsContext,
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ResponseMessage response = await this.ProcessItemStreamAsync(
                partitionKey: partitionKey,
                itemId: id,
                streamPayload: null,
                operationType: OperationType.Delete,
                requestOptions: requestOptions,
                diagnosticsContext: diagnosticsContext,
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

        /// <summary>
        /// Used in the compute gateway to support legacy gateway interface.
        /// </summary>
        public override async Task<TryExecuteQueryResult> TryExecuteQueryAsync(
            QueryFeatures supportedQueryFeatures,
            QueryDefinition queryDefinition,
            string continuationToken,
            FeedRangeInternal feedRangeInternal,
            QueryRequestOptions requestOptions,
            CancellationToken cancellationToken = default(CancellationToken))
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
                if (partitionKeyDefinitionObject is Documents.PartitionKeyDefinition definition)
                {
                    partitionKeyDefinition = definition;
                }
                else
                {
                    throw new ArgumentException(
                        "partitionkeydefinition has invalid type",
                        nameof(partitionKeyDefinitionObject));
                }
            }
            else
            {
                ContainerQueryProperties containerQueryProperties = await this.queryClient.GetCachedContainerQueryPropertiesAsync(
                    this.LinkUri,
                    requestOptions.PartitionKey,
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
            requestOptions = requestOptions ?? new QueryRequestOptions();

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
            QueryRequestOptions requestOptions = null)
        {
            requestOptions = requestOptions != null ? requestOptions : new QueryRequestOptions();

            return new CosmosLinqQuery<T>(
                this,
                this.ClientContext.ResponseFactory,
                (CosmosQueryClientCore)this.queryClient,
                continuationToken,
                requestOptions,
                allowSynchronousQueryExecution,
                this.ClientContext.ClientOptions.SerializerOptions);
        }

        public override FeedIterator<T> GetItemQueryIterator<T>(
            FeedRange feedRange,
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            requestOptions = requestOptions ?? new QueryRequestOptions();

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

            ChangeFeedObserverFactoryCore<T> observerFactory = new ChangeFeedObserverFactoryCore<T>(onChangesDelegate);
            ChangeFeedProcessorCore<T> changeFeedProcessor = new ChangeFeedProcessorCore<T>(observerFactory);
            return new ChangeFeedProcessorBuilder(
                processorName: processorName,
                container: this,
                changeFeedProcessor: changeFeedProcessor,
                applyBuilderConfiguration: changeFeedProcessor.ApplyBuildConfiguration);
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

            ChangeFeedEstimatorCore changeFeedEstimatorCore = new ChangeFeedEstimatorCore(estimationDelegate, estimationPeriod);
            return new ChangeFeedProcessorBuilder(
                processorName: processorName,
                container: this,
                changeFeedProcessor: changeFeedEstimatorCore,
                applyBuilderConfiguration: changeFeedEstimatorCore.ApplyBuildConfiguration);
        }

        public override TransactionalBatch CreateTransactionalBatch(PartitionKey partitionKey)
        {
            return new BatchCore(this, partitionKey);
        }

        public override async Task<IEnumerable<string>> GetChangeFeedTokensAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            Routing.PartitionKeyRangeCache pkRangeCache = await this.ClientContext.DocumentClient.GetPartitionKeyRangeCacheAsync();
            string containerRid = await this.GetRIDAsync(cancellationToken);
            IReadOnlyList<Documents.PartitionKeyRange> allRanges = await pkRangeCache.TryGetOverlappingRangesAsync(
                        containerRid,
                        new Documents.Routing.Range<string>(
                            Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                            Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                            isMinInclusive: true,
                            isMaxInclusive: false),
                        true);

            return allRanges.Select(e => StandByFeedContinuationToken.CreateForRange(containerRid, e.MinInclusive, e.MaxExclusive));
        }

        internal override FeedIterator GetStandByFeedIterator(
            ChangeFeedRequestOptions requestOptions = null)
        {
            ChangeFeedRequestOptions cosmosQueryRequestOptions = requestOptions as ChangeFeedRequestOptions ?? new ChangeFeedRequestOptions();

            return new StandByFeedIteratorCore(
                clientContext: this.ClientContext,
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
            requestOptions = requestOptions ?? new QueryRequestOptions();

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
                return FeedRangeIteratorCore.Create(
                    containerCore: this,
                    continuation: continuationToken,
                    feedRangeInternal: feedRange,
                    options: requestOptions);
            }

            return QueryIterator.Create(
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

        // Extracted partition key might be invalid as CollectionCache might be stale.
        // Stale collection cache is refreshed through PartitionKeyMismatchRetryPolicy
        // and partition-key is extracted again. 
        private async Task<ResponseMessage> ExtractPartitionKeyAndProcessItemStreamAsync<T>(
            PartitionKey? partitionKey,
            string itemId,
            T item,
            OperationType operationType,
            ItemRequestOptions requestOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (diagnosticsContext == null)
            {
                throw new ArgumentNullException(nameof(diagnosticsContext));
            }

            Stream itemStream;
            using (diagnosticsContext.CreateScope("ItemSerialize"))
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
                        diagnosticsContext: diagnosticsContext,
                        cancellationToken: cancellationToken);
            }

            PartitionKeyMismatchRetryPolicy requestRetryPolicy = null;
            while (true)
            {
                using (diagnosticsContext.CreateScope("ExtractPkValue"))
                {
                    partitionKey = await this.GetPartitionKeyValueFromStreamAsync(itemStream, cancellationToken);
                }

                ResponseMessage responseMessage = await this.ProcessItemStreamAsync(
                    partitionKey,
                    itemId,
                    itemStream,
                    operationType,
                    requestOptions,
                    diagnosticsContext: diagnosticsContext,
                    cancellationToken: cancellationToken);

                if (responseMessage.IsSuccessStatusCode)
                {
                    return responseMessage;
                }

                if (requestRetryPolicy == null)
                {
                    requestRetryPolicy = new PartitionKeyMismatchRetryPolicy(await this.ClientContext.DocumentClient.GetCollectionCacheAsync(), null);
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
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (diagnosticsContext == null)
            {
                throw new ArgumentNullException(nameof(diagnosticsContext));
            }

            if (requestOptions != null && requestOptions.IsEffectivePartitionKeyRouting)
            {
                partitionKey = null;
            }

            ContainerInternal.ValidatePartitionKey(partitionKey, requestOptions);
            Uri resourceUri = this.GetResourceUri(requestOptions, operationType, itemId);

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
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);

            return responseMessage;
        }

        public override async Task<PartitionKey> GetPartitionKeyValueFromStreamAsync(
            Stream stream,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (!stream.CanSeek)
            {
                throw new ArgumentException("Stream needs to be seekable", nameof(stream));
            }

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

                string[] tokens = await this.GetPartitionKeyPathTokensAsync(cancellation);
                for (int i = 0; i < tokens.Length - 1; i++)
                {
                    if (!pathTraversal.TryGetValue(tokens[i], out pathTraversal))
                    {
                        return PartitionKey.None;
                    }
                }

                if (!pathTraversal.TryGetValue(tokens[tokens.Length - 1], out CosmosElement partitionKeyValue))
                {
                    return PartitionKey.None;
                }

                return this.CosmosElementToPartitionKeyObject(partitionKeyValue);
            }
            finally
            {
                // MemoryStream casting leverage might change position 
                stream.Position = 0;
            }
        }

        private PartitionKey CosmosElementToPartitionKeyObject(CosmosElement cosmosElement)
        {
            // TODO: Leverage original serialization and avoid re-serialization (bug)
            switch (cosmosElement.Type)
            {
                case CosmosElementType.String:
                    CosmosString cosmosString = cosmosElement as CosmosString;
                    return new PartitionKey(cosmosString.Value);

                case CosmosElementType.Number:
                    CosmosNumber cosmosNumber = cosmosElement as CosmosNumber;
                    double value = Number64.ToDouble(cosmosNumber.Value);
                    return new PartitionKey(value);

                case CosmosElementType.Boolean:
                    CosmosBoolean cosmosBool = cosmosElement as CosmosBoolean;
                    return new PartitionKey(cosmosBool.Value);

                case CosmosElementType.Null:
                    return PartitionKey.Null;

                default:
                    throw new ArgumentException(
                        string.Format(CultureInfo.InvariantCulture, RMResources.UnsupportedPartitionKeyComponentValue, cosmosElement));
            }
        }

        private Uri GetResourceUri(RequestOptions requestOptions, OperationType operationType, string itemId)
        {
            if (requestOptions != null && requestOptions.TryGetResourceUri(out Uri resourceUri))
            {
                return resourceUri;
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
            StringBuilder stringBuilder = new StringBuilder(this.LinkUri.OriginalString.Length +
                                                            Paths.DocumentsPathSegment.Length + 2);
            stringBuilder.Append(this.LinkUri.OriginalString);
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
        private Uri ContcatCachedUriWithId(string resourceId)
        {
            return new Uri(this.cachedUriSegmentWithoutId + Uri.EscapeUriString(resourceId), UriKind.Relative);
        }
    }
}
