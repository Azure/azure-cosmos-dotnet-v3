//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Used to perform operations on items. There are two different types of operations.
    /// 1. The object operations where it serializes and deserializes the item on request/response
    /// 2. The stream response which takes a Stream containing a JSON serialized object and returns a response containing a Stream
    /// </summary>
    internal class CosmosItemsCore : CosmosItems
    {
        /// <summary>
        /// Cache the full URI segment without the last resource id.
        /// This allows only a single con-cat operation instead of building the full URI string each time.
        /// </summary>
        private string cachedUriSegmentWithoutId { get; }

        private readonly CosmosClientContext clientContext;
        private readonly CosmosQueryClient queryClient;

        internal CosmosItemsCore(
            CosmosClientContext clientContext,
            CosmosContainerCore container,
            CosmosQueryClient queryClient = null)
        {
            this.clientContext = clientContext;
            this.container = container;
            this.cachedUriSegmentWithoutId = this.GetResourceSegmentUriWithoutId();
            this.queryClient = queryClient ?? new CosmosQueryClientCore(this.clientContext, container);
        }

        internal readonly CosmosContainerCore container;

        public override Task<CosmosResponseMessage> CreateItemStreamAsync(
                    object partitionKey,
                    Stream streamPayload,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessItemStreamAsync(
                partitionKey,
                null,
                streamPayload,
                OperationType.Create,
                requestOptions,
                cancellationToken);
        }

        public override async Task<ItemResponse<T>> CreateItemAsync<T>(
            T item,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Tuple<bool, object, Stream> result = await this.GetItemStreamAsync<T>(item, requestOptions, cancellationToken);

            if (result.Item1)
            {
                if(requestOptions == null)
                {
                    requestOptions = new ItemRequestOptions();
                }
                requestOptions.PartitionKey = result.Item2;
            }

            Task<CosmosResponseMessage> response = this.WriteItemStreamAsync(
                itemId: null,
                streamPayload: result.Item3,
                operationType: OperationType.Create,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return await this.clientContext.ResponseFactory.CreateItemResponse<T>(response);
        }

        public override Task<CosmosResponseMessage> ReadItemStreamAsync(
                    object partitionKey,
                    string id,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessItemStreamAsync(
                partitionKey,
                id,
                null,
                OperationType.Read,
                requestOptions,
                cancellationToken);
        }

        public override Task<ItemResponse<T>> ReadItemAsync<T>(
            object partitionKey,
            string id,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.ReadItemStreamAsync(
                partitionKey: partitionKey,
                id: id,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.clientContext.ResponseFactory.CreateItemResponse<T>(response);
        }

        public override Task<CosmosResponseMessage> UpsertItemStreamAsync(
                    object partitionKey,
                    Stream streamPayload,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessItemStreamAsync(
                partitionKey,
                null,
                streamPayload,
                OperationType.Upsert,
                requestOptions,
                cancellationToken);
        }

        public override async Task<ItemResponse<T>> UpsertItemAsync<T>(            
            T item,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Tuple<bool, object, Stream> result = await this.GetItemStreamAsync<T>(item, requestOptions, cancellationToken);

            if (result.Item1)
            {
                if (requestOptions == null)
                {
                    requestOptions = new ItemRequestOptions();
                }
                requestOptions.PartitionKey = result.Item2;
            }

            Task<CosmosResponseMessage> response = this.WriteItemStreamAsync(   
                itemId: null,
                streamPayload: result.Item3,
                operationType: OperationType.Upsert,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return await this.clientContext.ResponseFactory.CreateItemResponse<T>(response);
        }

        public override Task<CosmosResponseMessage> ReplaceItemStreamAsync(
                    object partitionKey,
                    string id,
                    Stream streamPayload,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessItemStreamAsync(
                partitionKey,
                id,
                streamPayload,
                OperationType.Replace,
                requestOptions,
                cancellationToken);
        }

        public override async Task<ItemResponse<T>> ReplaceItemAsync<T>(            
            string id,
            T item,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Tuple<bool, object, Stream> result = await this.GetItemStreamAsync<T>(item, requestOptions, cancellationToken);

            if (result.Item1)
            {
                if (requestOptions == null)
                {
                    requestOptions = new ItemRequestOptions();
                }
                requestOptions.PartitionKey = result.Item2;
            }

            Task<CosmosResponseMessage> response = this.WriteItemStreamAsync(               
               itemId: id,
               streamPayload: result.Item3,
               operationType: OperationType.Replace,
               requestOptions: requestOptions,
               cancellationToken: cancellationToken);

            return await this.clientContext.ResponseFactory.CreateItemResponse<T>(response);
        }

        public override Task<CosmosResponseMessage> DeleteItemStreamAsync(
                    object partitionKey,
                    string id,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessItemStreamAsync(
                partitionKey,
                id,
                null,
                OperationType.Delete,
                requestOptions,
                cancellationToken);
        }

        public override Task<ItemResponse<T>> DeleteItemAsync<T>(
            object partitionKey,
            string id,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.DeleteItemStreamAsync(
               partitionKey: partitionKey,
               id: id,
               requestOptions: requestOptions,
               cancellationToken: cancellationToken);

            return this.clientContext.ResponseFactory.CreateItemResponse<T>(response);
        }

        public override FeedIterator<T> GetItemIterator<T>(
            int? maxItemCount = null,
            string continuationToken = null)
        {
            return new FeedIteratorCore<T>(
                maxItemCount,
                continuationToken,
                null,
                this.ItemFeedRequestExecutor<T>);
        }

        public override FeedIterator GetItemStreamIterator(
            int? maxItemCount = null,
            string continuationToken = null,
            ItemRequestOptions requestOptions = null)
        {
            return new FeedIteratorCore(maxItemCount, continuationToken, requestOptions, this.ItemStreamFeedRequestExecutor);
        }

        public override FeedIterator CreateItemQueryAsStream(
            CosmosSqlQueryDefinition sqlQueryDefinition,
            int maxConcurrency,
            object partitionKey = null,
            int? maxItemCount = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            requestOptions = requestOptions ?? new QueryRequestOptions();
            requestOptions.MaxConcurrency = maxConcurrency;
            requestOptions.EnableCrossPartitionQuery = true;
            requestOptions.RequestContinuation = continuationToken;
            requestOptions.MaxItemCount = maxItemCount;
            requestOptions.PartitionKey = partitionKey;

            CosmosQueryExecutionContext cosmosQueryExecution = new CosmosQueryExecutionContextFactory(
                client: this.queryClient,
                resourceTypeEnum: ResourceType.Document,
                operationType: OperationType.Query,
                resourceType: typeof(QueryResponse),
                sqlQuerySpec: sqlQueryDefinition.ToSqlQuerySpec(),
                queryRequestOptions: requestOptions,
                resourceLink: this.container.LinkUri,
                isContinuationExpected: true,
                allowNonValueAggregateQuery: true,
                correlatedActivityId: Guid.NewGuid());

            return new FeedIteratorCore(
                maxItemCount,
                continuationToken,
                requestOptions,
                this.QueryRequestExecutor,
                cosmosQueryExecution);
        }

        public override FeedIterator CreateItemQueryAsStream(
            string sqlQueryText,
            int maxConcurrency,
            object partitionKey = null,
            int? maxItemCount = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return this.CreateItemQueryAsStream(
                new CosmosSqlQueryDefinition(sqlQueryText),
                maxConcurrency,
                partitionKey,
                maxItemCount,
                continuationToken,
                requestOptions);
        }

        public override FeedIterator<T> CreateItemQuery<T>(
            CosmosSqlQueryDefinition sqlQueryDefinition,
            object partitionKey,
            int? maxItemCount = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            requestOptions = requestOptions ?? new QueryRequestOptions();
            requestOptions.PartitionKey = partitionKey;
            requestOptions.EnableCrossPartitionQuery = false;
            requestOptions.RequestContinuation = continuationToken;
            requestOptions.MaxItemCount = maxItemCount;

            CosmosQueryExecutionContext cosmosQueryExecution = new CosmosQueryExecutionContextFactory(
                client: this.queryClient,
                resourceTypeEnum: ResourceType.Document,
                operationType: OperationType.Query,
                resourceType: typeof(T),
                sqlQuerySpec: sqlQueryDefinition.ToSqlQuerySpec(),
                queryRequestOptions: requestOptions,
                resourceLink: this.container.LinkUri,
                isContinuationExpected: true,
                allowNonValueAggregateQuery: true,
                correlatedActivityId: Guid.NewGuid());

            return new FeedIteratorCore<T>(
                maxItemCount,
                continuationToken,
                requestOptions,
                this.NextResultSetAsync<T>,
                cosmosQueryExecution);
        }

        public override FeedIterator<T> CreateItemQuery<T>(
            string sqlQueryText,
            object partitionKey,
            int? maxItemCount = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return this.CreateItemQuery<T>(
                new CosmosSqlQueryDefinition(sqlQueryText),
                partitionKey,
                maxItemCount,
                continuationToken,
                requestOptions);
        }

        public override FeedIterator<T> CreateItemQuery<T>(
            CosmosSqlQueryDefinition sqlQueryDefinition,
            int maxConcurrency,
            int? maxItemCount = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            requestOptions = requestOptions ?? new QueryRequestOptions();
            requestOptions.EnableCrossPartitionQuery = true;
            requestOptions.RequestContinuation = continuationToken;
            requestOptions.MaxItemCount = maxItemCount;
            requestOptions.MaxConcurrency = maxConcurrency;

            CosmosQueryExecutionContext cosmosQueryExecution = new CosmosQueryExecutionContextFactory(
                client: this.queryClient,
                resourceTypeEnum: ResourceType.Document,
                operationType: OperationType.Query,
                resourceType: typeof(T),
                sqlQuerySpec: sqlQueryDefinition.ToSqlQuerySpec(),
                queryRequestOptions: requestOptions,
                resourceLink: this.container.LinkUri,
                isContinuationExpected: true,
                allowNonValueAggregateQuery: true,
                correlatedActivityId: Guid.NewGuid());

            return new FeedIteratorCore<T>(
                maxItemCount,
                continuationToken,
                requestOptions,
                this.NextResultSetAsync<T>,
                cosmosQueryExecution);
        }

        public override FeedIterator<T> CreateItemQuery<T>(
            string sqlQueryText,
            int maxConcurrency,
            int? maxItemCount = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return this.CreateItemQuery<T>(
                new CosmosSqlQueryDefinition(sqlQueryText),
                maxConcurrency,
                maxItemCount,
                continuationToken,
                requestOptions);
        }

        public override ChangeFeedProcessorBuilder CreateChangeFeedProcessorBuilder<T>(
            string workflowName,
            Func<IReadOnlyCollection<T>, CancellationToken, Task> onChangesDelegate)
        {
            if (workflowName == null)
            {
                throw new ArgumentNullException(nameof(workflowName));
            }

            if (onChangesDelegate == null)
            {
                throw new ArgumentNullException(nameof(onChangesDelegate));
            }

            ChangeFeedObserverFactoryCore<T> observerFactory = new ChangeFeedObserverFactoryCore<T>(onChangesDelegate);
            ChangeFeedProcessorCore<T> changeFeedProcessor = new ChangeFeedProcessorCore<T>(observerFactory);
            return new ChangeFeedProcessorBuilder(
                workflowName: workflowName,
                cosmosContainer: this.container,
                changeFeedProcessor: changeFeedProcessor,
                applyBuilderConfiguration: changeFeedProcessor.ApplyBuildConfiguration);
        }

        public override ChangeFeedProcessorBuilder CreateChangeFeedEstimatorBuilder(
            string workflowName,
            Func<long, CancellationToken, Task> estimationDelegate,
            TimeSpan? estimationPeriod = null)
        {
            if (workflowName == null)
            {
                throw new ArgumentNullException(nameof(workflowName));
            }

            if (estimationDelegate == null)
            {
                throw new ArgumentNullException(nameof(estimationDelegate));
            }

            ChangeFeedEstimatorCore changeFeedEstimatorCore = new ChangeFeedEstimatorCore(estimationDelegate, estimationPeriod);
            return new ChangeFeedProcessorBuilder(
                workflowName: workflowName,
                cosmosContainer: this.container,
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
                clientContext: this.clientContext,
                continuationToken: continuationToken,
                maxItemCount: maxItemCount,
                cosmosContainer: this.container,
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
                jsonSerializer: this.clientContext.JsonSerializer,
                hasMoreResults: !cosmosQueryExecution.IsDone);
        }

        internal async Task<CosmosResponseMessage> WriteItemStreamAsync(
            string itemId,
            Stream streamPayload,
            OperationType operationType,
            ItemRequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            return await this.ProcessItemStreamAsync(
                await this.GetPartitionKeyValueFromStreamAsync(streamPayload, requestOptions, cancellationToken),
                null,
                streamPayload,
                OperationType.Create,
                requestOptions,
                cancellationToken);
        }

        internal Task<CosmosResponseMessage> ProcessItemStreamAsync(
            object partitionKey,
            string itemId,
            Stream streamPayload,
            OperationType operationType,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            CosmosItemsCore.ValidatePartitionKey(partitionKey, requestOptions);
            Uri resourceUri = this.GetResourceUri(requestOptions, operationType, itemId);

            return this.clientContext.ProcessResourceOperationStreamAsync(
                resourceUri,
                ResourceType.Document,
                operationType,
                requestOptions,
                this.container,
                partitionKey,
                streamPayload,
                null,
                cancellationToken);
        }

        internal async Task<object> GetPartitionKeyValueFromStreamAsync(Stream stream, 
            ItemRequestOptions itemRequestOptions, 
            CancellationToken cancellation = default(CancellationToken))
        {     
            if(itemRequestOptions?.PartitionKey != null)
            {
                return itemRequestOptions.PartitionKey;
            }

            if (!stream.CanSeek)
            {
                throw new ArgumentException("Stream is needs to be seekable", nameof(stream));
            }

            stream.Position = 0;

            MemoryStream memoryStream = stream as MemoryStream;
            if (memoryStream == null)
            {
                memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
            }
                    
            IJsonNavigator jsonNavigator = JsonNavigator.Create(memoryStream.ToArray());
            IJsonNavigatorNode jsonNavigatorNode = jsonNavigator.GetRootNode();
            CosmosObject cosmosObject = CosmosObject.Create(jsonNavigator, jsonNavigatorNode);

            string[] tokens = await this.container.GetPartitionKeyPathTokensAsync(cancellation);
            
            for(int i = 0; i < tokens.Length - 1; i++)
            {                
                cosmosObject = cosmosObject[tokens[i]] as CosmosObject;

                if (cosmosObject == null)
                {
                    throw new ArgumentNullException(nameof(cosmosObject));
                }
            }
            
            return this.CosmosElementToObject(cosmosObject[tokens[tokens.Length - 1]]);
        }

        internal async Task<Tuple<bool, object, Stream>> GetItemStreamAsync<T>(T item, ItemRequestOptions itemRequestOptions, CancellationToken cancellation = default(CancellationToken))
        {
            //until https://github.com/Azure/azure-cosmos-dotnet-v3/pull/291 is merged and can just compare DefaultJsonSerializer and UserJsonSerializer
            CosmosJsonSerializerWrapper wrapperSerializer = (this.clientContext.JsonSerializer as CosmosJsonSerializerWrapper);
            CosmosDefaultJsonSerializer defaultSerializer = wrapperSerializer.InternalJsonSerializer as CosmosDefaultJsonSerializer;
            if (defaultSerializer != null && itemRequestOptions?.PartitionKey == null)
            {
                Stream stream = defaultSerializer.ToStream<T>(item, await this.container.GetPartitionKeyPathTokensAsync(cancellation), out object partitionKey);
                return new Tuple<bool, object, Stream>(true, partitionKey, stream);                                    
            }
            else
            {
                return new Tuple<bool, object, Stream>(false, itemRequestOptions?.PartitionKey, this.clientContext.JsonSerializer.ToStream<T>(item));
            }
        }

        private object CosmosElementToObject(CosmosElement cosmosElement)
        {
            if (cosmosElement?.Type == CosmosElementType.Array ||
                cosmosElement?.Type == CosmosElementType.Object ||
                cosmosElement?.Type == CosmosElementType.Binary)
            {                                                
                throw new InvalidOperationException(
                            string.Format(CultureInfo.InvariantCulture, RMResources.UnsupportedPartitionKeyComponentValue, cosmosElement));
            }

            if (cosmosElement?.Type == CosmosElementType.String)
            {
                CosmosString cosmosString = cosmosElement as CosmosString;
                return cosmosString.Value;
            }

            if (cosmosElement?.Type == CosmosElementType.Number)
            {
                CosmosNumber cosmosNumber = cosmosElement as CosmosNumber;
                return cosmosNumber.AsFloatingPoint();
            }

            if (cosmosElement?.Type == CosmosElementType.Boolean)
            {
                CosmosBoolean cosmosBool = cosmosElement as CosmosBoolean;
                return cosmosBool.Value;
            }

            if (cosmosElement?.Type == CosmosElementType.Guid)
            {
                CosmosGuid cosmosGuid = cosmosElement as CosmosGuid;
                return cosmosGuid.Value;
            }
            
            return null;
        }

        private Task<CosmosResponseMessage> ItemStreamFeedRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            RequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            Uri resourceUri = this.container.LinkUri;
            return this.clientContext.ProcessResourceOperationAsync<CosmosResponseMessage>(
                resourceUri: resourceUri,
                resourceType: ResourceType.Document,
                operationType: OperationType.ReadFeed,
                requestOptions: options,
                requestEnricher: request =>
                {
                    QueryRequestOptions.FillContinuationToken(request, continuationToken);
                    QueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                responseCreator: response => response,
                cosmosContainerCore: this.container,
                partitionKey: null,
                streamPayload: null,
                cancellationToken: cancellationToken);
        }

        private Task<FeedResponse<T>> ItemFeedRequestExecutor<T>(
            int? maxItemCount,
           string continuationToken,
           RequestOptions options,
           object state,
           CancellationToken cancellationToken)
        {
            Uri resourceUri = this.container.LinkUri;
            return this.clientContext.ProcessResourceOperationAsync<FeedResponse<T>>(
                resourceUri: resourceUri,
                resourceType: ResourceType.Document,
                operationType: OperationType.ReadFeed,
                requestOptions: options,
                requestEnricher: request =>
                {
                    QueryRequestOptions.FillContinuationToken(request, continuationToken);
                    QueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                responseCreator: response => this.clientContext.ResponseFactory.CreateResultSetQueryResponse<T>(response),
                cosmosContainerCore: this.container,
                partitionKey: null,
                streamPayload: null,
                cancellationToken: cancellationToken);
        }

        private async Task<CosmosResponseMessage> QueryRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            RequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            CosmosQueryExecutionContext cosmosQueryExecution = (CosmosQueryExecutionContext)state;
            return (CosmosResponseMessage)(await cosmosQueryExecution.ExecuteNextAsync(cancellationToken));
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
                    return this.container.LinkUri;

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

            if (requestOptions?.Properties != null
                && requestOptions.Properties.TryGetValue(
                    WFConstants.BackendHeaders.EffectivePartitionKeyString, out object effectivePartitionKeyValue)
                && effectivePartitionKeyValue != null)
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
            StringBuilder stringBuilder = new StringBuilder(this.container.LinkUri.OriginalString.Length +
                                                            Paths.DocumentsPathSegment.Length + 2);
            stringBuilder.Append(this.container.LinkUri.OriginalString);
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
