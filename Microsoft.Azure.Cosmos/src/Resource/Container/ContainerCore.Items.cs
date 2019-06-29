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
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Used to perform operations on items. There are two different types of operations.
    /// 1. The object operations where it serializes and deserializes the item on request/response
    /// 2. The stream response which takes a Stream containing a JSON serialized object and returns a response containing a Stream
    /// </summary>
    internal partial class ContainerCore : Container
    {
        /// <summary>
        /// Cache the full URI segment without the last resource id.
        /// This allows only a single con-cat operation instead of building the full URI string each time.
        /// </summary>
        private string cachedUriSegmentWithoutId { get; }

        private readonly CosmosQueryClient queryClient;

        public override Task<ResponseMessage> CreateItemStreamAsync(
                    Stream streamPayload,
                    PartitionKey partitionKey,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessItemStreamAsync(
                partitionKey,
                null,
                streamPayload,
                OperationType.Create,
                requestOptions,
                extractPartitionKeyIfNeeded: false,
                cancellationToken: cancellationToken);
        }

        public override Task<ItemResponse<T>> CreateItemAsync<T>(
            T item,
            PartitionKey? partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            Task<ResponseMessage> response = this.ExtractPartitionKeyAndProcessItemStreamAsync(
                partitionKey: partitionKey,
                itemId: null,
                streamPayload: this.ClientContext.CosmosSerializer.ToStream<T>(item),
                operationType: OperationType.Create,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateItemResponseAsync<T>(response);
        }

        public override Task<ResponseMessage> ReadItemStreamAsync(
                    string id,
                    PartitionKey partitionKey,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessItemStreamAsync(
                partitionKey,
                id,
                null,
                OperationType.Read,
                requestOptions,
                extractPartitionKeyIfNeeded: false,
                cancellationToken: cancellationToken);
        }

        public override Task<ItemResponse<T>> ReadItemAsync<T>(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<ResponseMessage> response = this.ReadItemStreamAsync(
                partitionKey: partitionKey,
                id: id,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateItemResponseAsync<T>(response);
        }

        public override Task<ResponseMessage> UpsertItemStreamAsync(
                    Stream streamPayload,
                    PartitionKey partitionKey,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessItemStreamAsync(
                partitionKey,
                null,
                streamPayload,
                OperationType.Upsert,
                requestOptions,
                extractPartitionKeyIfNeeded: false,
                cancellationToken: cancellationToken);
        }

        public override Task<ItemResponse<T>> UpsertItemAsync<T>(
            T item,
            PartitionKey? partitionKey = null,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            Task<ResponseMessage> response = this.ExtractPartitionKeyAndProcessItemStreamAsync(
                partitionKey: partitionKey,
                itemId: null,
                streamPayload: this.ClientContext.CosmosSerializer.ToStream<T>(item),
                operationType: OperationType.Upsert,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateItemResponseAsync<T>(response);
        }

        public override Task<ResponseMessage> ReplaceItemStreamAsync(
                    Stream streamPayload,
                    string id,
                    PartitionKey partitionKey,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessItemStreamAsync(
                partitionKey,
                id,
                streamPayload,
                OperationType.Replace,
                requestOptions,
                extractPartitionKeyIfNeeded: false,
                cancellationToken: cancellationToken);
        }

        public override Task<ItemResponse<T>> ReplaceItemAsync<T>(
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

            Task<ResponseMessage> response = this.ExtractPartitionKeyAndProcessItemStreamAsync(
               partitionKey: partitionKey,
               itemId: id,
               streamPayload: this.ClientContext.CosmosSerializer.ToStream<T>(item),
               operationType: OperationType.Replace,
               requestOptions: requestOptions,
               cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateItemResponseAsync<T>(response);
        }

        public override Task<ResponseMessage> DeleteItemStreamAsync(
                    string id,
                    PartitionKey partitionKey,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessItemStreamAsync(
                partitionKey,
                id,
                null,
                OperationType.Delete,
                requestOptions,
                extractPartitionKeyIfNeeded: false,
                cancellationToken: cancellationToken);
        }

        public override Task<ItemResponse<T>> DeleteItemAsync<T>(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<ResponseMessage> response = this.DeleteItemStreamAsync(
               partitionKey: partitionKey,
               id: id,
               requestOptions: requestOptions,
               cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateItemResponseAsync<T>(response);
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
            requestOptions = requestOptions ?? new QueryRequestOptions();

            if (requestOptions.IsEffectivePartitionKeyRouting)
            {
                requestOptions.PartitionKey = null;
            }

            if (queryDefinition == null)
            {
                return new FeedStatelessIteratorCore(
                    this.ClientContext,
                    this.LinkUri,
                    resourceType: ResourceType.Document,
                    queryDefinition: null,
                    continuationToken: continuationToken,
                    options: requestOptions);
            }

            CosmosQueryExecutionContext cosmosQueryExecution = new CosmosQueryExecutionContextFactory(
                client: this.queryClient,
                resourceTypeEnum: ResourceType.Document,
                operationType: OperationType.Query,
                resourceType: typeof(QueryResponse),
                sqlQuerySpec: queryDefinition.ToSqlQuerySpec(),
                continuationToken: continuationToken,
                queryRequestOptions: requestOptions,
                resourceLink: this.LinkUri,
                isContinuationExpected: true,
                allowNonValueAggregateQuery: true,
                correlatedActivityId: Guid.NewGuid());

            return new FeedIteratorCore(
                requestOptions.MaxItemCount,
                continuationToken,
                requestOptions,
                this.QueryRequestExecutorAsync,
                cosmosQueryExecution);
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

            if (queryDefinition == null)
            {
                FeedStatelessIteratorCore feedStatelessIterator = new FeedStatelessIteratorCore(
                    this.ClientContext,
                    this.LinkUri,
                    resourceType: ResourceType.Document,
                    queryDefinition: null,
                    continuationToken: continuationToken,
                    options: requestOptions);

                return new FeedStatelessIteratorCore<T>(feedStatelessIterator,
                    responseCreator: this.ClientContext.ResponseFactory.CreateResultSetQueryResponse<T>);
            }

            CosmosQueryExecutionContext cosmosQueryExecution = new CosmosQueryExecutionContextFactory(
                client: this.queryClient,
                resourceTypeEnum: ResourceType.Document,
                operationType: OperationType.Query,
                resourceType: typeof(T),
                sqlQuerySpec: queryDefinition.ToSqlQuerySpec(),
                continuationToken: continuationToken,
                queryRequestOptions: requestOptions,
                resourceLink: this.LinkUri,
                isContinuationExpected: true,
                allowNonValueAggregateQuery: true,
                correlatedActivityId: Guid.NewGuid());

            return new FeedIteratorCore<T>(
                requestOptions.MaxItemCount,
                continuationToken,
                requestOptions,
                this.NextResultSetAsync<T>,
                cosmosQueryExecution);
        }

        public override IOrderedQueryable<T> GetItemLinqQueryable<T>(
            bool allowSynchronousQueryExecution = false,
            QueryRequestOptions requestOptions = null)
        {
            requestOptions = requestOptions != null ? requestOptions : new QueryRequestOptions();

            return new CosmosLinqQuery<T>(
                this,
                this.ClientContext.CosmosSerializer,
                (CosmosQueryClientCore)this.queryClient,
                requestOptions,
                allowSynchronousQueryExecution);
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

        internal FeedIterator GetStandByFeedIterator(
            string continuationToken = null,
            int? maxItemCount = null,
            ChangeFeedRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ChangeFeedRequestOptions cosmosQueryRequestOptions = requestOptions as ChangeFeedRequestOptions ?? new ChangeFeedRequestOptions();

            return new ChangeFeedResultSetIteratorCore(
                clientContext: this.ClientContext,
                continuationToken: continuationToken,
                maxItemCount: maxItemCount,
                container: this,
                options: cosmosQueryRequestOptions);
        }

        internal async Task<FeedResponse<T>> NextResultSetAsync<T>(
            int? maxItemCount,
            string continuationToken,
            RequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            CosmosQueryExecutionContext cosmosQueryExecution = (CosmosQueryExecutionContext)state;
            QueryResponse queryResponse = await cosmosQueryExecution.ExecuteNextAsync(cancellationToken);
            queryResponse.EnsureSuccessStatusCode();

            return QueryResponse<T>.CreateResponse<T>(
                cosmosQueryResponse: queryResponse,
                jsonSerializer: this.ClientContext.CosmosSerializer,
                hasMoreResults: !cosmosQueryExecution.IsDone);
        }

        // Extracted partition key might be invalid as CollectionCache might be stale.
        // Stale collection cache is refreshed through PartitionKeyMismatchRetryPolicy
        // and partition-key is extracted again. 
        internal async Task<ResponseMessage> ExtractPartitionKeyAndProcessItemStreamAsync(
            PartitionKey? partitionKey,
            string itemId,
            Stream streamPayload,
            OperationType operationType,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            PartitionKeyMismatchRetryPolicy requestRetryPolicy = null;
            while (true)
            {
                ResponseMessage responseMessage = await this.ProcessItemStreamAsync(
                    partitionKey,
                    itemId,
                    streamPayload,
                    operationType,
                    requestOptions,
                    extractPartitionKeyIfNeeded: true,
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

        internal async Task<ResponseMessage> ProcessItemStreamAsync(
            PartitionKey? partitionKey,
            string itemId,
            Stream streamPayload,
            OperationType operationType,
            RequestOptions requestOptions,
            bool extractPartitionKeyIfNeeded,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (requestOptions != null && requestOptions.IsEffectivePartitionKeyRouting)
            {
                partitionKey = null;
            }

            if (extractPartitionKeyIfNeeded && partitionKey == null)
            {
                partitionKey = await this.GetPartitionKeyValueFromStreamAsync(streamPayload, cancellationToken);
            }

            ContainerCore.ValidatePartitionKey(partitionKey, requestOptions);
            Uri resourceUri = this.GetResourceUri(requestOptions, operationType, itemId);

            return await this.ClientContext.ProcessResourceOperationStreamAsync(
                resourceUri,
                ResourceType.Document,
                operationType,
                requestOptions,
                this,
                partitionKey,
                streamPayload,
                null,
                cancellationToken);
        }

        internal async Task<PartitionKey> GetPartitionKeyValueFromStreamAsync(
            Stream stream,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (!stream.CanSeek)
            {
                throw new ArgumentException("Stream is needs to be seekable", nameof(stream));
            }

            try
            {
                stream.Position = 0;

                MemoryStream memoryStream = stream as MemoryStream;
                if (memoryStream == null)
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
                    pathTraversal = pathTraversal[tokens[i]] as CosmosObject;
                    if (pathTraversal == null)
                    {
                        return PartitionKey.None;
                    }
                }

                CosmosElement partitionKeyValue = pathTraversal[tokens[tokens.Length - 1]];
                if (partitionKeyValue == null)
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
                    double? number = cosmosNumber.AsFloatingPoint();
                    if (!number.HasValue)
                    {
                        throw new ArgumentException(
                            string.Format(CultureInfo.InvariantCulture, RMResources.UnsupportedPartitionKeyComponentValue, cosmosElement));
                    }

                    return new PartitionKey(cosmosNumber.AsFloatingPoint().Value);

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

        private Task<ResponseMessage> ItemStreamFeedRequestExecutorAsync(
            int? maxItemCount,
            string continuationToken,
            RequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            return this.ClientContext.ProcessResourceOperationAsync<ResponseMessage>(
                resourceUri: this.LinkUri,
                resourceType: ResourceType.Document,
                operationType: OperationType.ReadFeed,
                requestOptions: options,
                requestEnricher: request =>
                {
                    QueryRequestOptions.FillContinuationToken(request, continuationToken);
                    QueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                responseCreator: response => response,
                cosmosContainerCore: this,
                partitionKey: null,
                streamPayload: null,
                cancellationToken: cancellationToken);
        }

        private Task<FeedResponse<T>> ItemFeedRequestExecutorAsync<T>(
            int? maxItemCount,
            string continuationToken,
            RequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            return this.ClientContext.ProcessResourceOperationAsync<FeedResponse<T>>(
                resourceUri: this.LinkUri,
                resourceType: ResourceType.Document,
                operationType: OperationType.ReadFeed,
                requestOptions: options,
                requestEnricher: request =>
                {
                    QueryRequestOptions.FillContinuationToken(request, continuationToken);
                    QueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                responseCreator: response => this.ClientContext.ResponseFactory.CreateResultSetQueryResponse<T>(response),
                cosmosContainerCore: this,
                partitionKey: null,
                streamPayload: null,
                cancellationToken: cancellationToken);
        }

        private async Task<ResponseMessage> QueryRequestExecutorAsync(
            int? maxItemCount,
            string continuationToken,
            RequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            // This catches exception thrown by the caches and converts it to QueryResponse
            try
            {
                CosmosQueryExecutionContext cosmosQueryExecution = (CosmosQueryExecutionContext)state;
                return await cosmosQueryExecution.ExecuteNextAsync(cancellationToken);
            }
            catch (DocumentClientException exception)
            {
                return exception.ToCosmosResponseMessage(request: null);
            }
            catch (CosmosException exception)
            {
                return new ResponseMessage(
                    headers: exception.Headers,
                    requestMessage: null,
                    errorMessage: exception.Message,
                    statusCode: exception.StatusCode,
                    error: exception.Error);
            }
            catch (AggregateException ae)
            {
                ResponseMessage errorMessage = TransportHandler.AggregateExceptionConverter(ae, null);
                if (errorMessage != null)
                {
                    return errorMessage;
                }

                throw;
            }
        }

        internal Uri GetResourceUri(RequestOptions requestOptions, OperationType operationType, string itemId)
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
        /// Throw an exception if the partition key is null or empty string
        /// </summary>
        internal static void ValidatePartitionKey(object partitionKey, RequestOptions requestOptions)
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
