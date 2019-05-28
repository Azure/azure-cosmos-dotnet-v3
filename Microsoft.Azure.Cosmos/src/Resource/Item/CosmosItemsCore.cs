//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Used to perform operations on items. There are two different types of operations.
    /// 1. The object operations where it serializes and deserializes the item on request/response
    /// 2. The stream response which takes a Stream containing a JSON serialized object and returns a response containing a Stream
    /// </summary>
    internal partial class CosmosContainerCore : CosmosContainer
    {
        /// <summary>
        /// Cache the full URI segment without the last resource id.
        /// This allows only a single con-cat operation instead of building the full URI string each time.
        /// </summary>
        private string cachedUriSegmentWithoutId { get; }

        private readonly CosmosQueryClient queryClient;

        public override Task<CosmosResponseMessage> CreateItemAsStreamAsync(
                    object partitionKey,
                    Stream streamPayload,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessItemAsStreamAsync(
                partitionKey,
                null,
                streamPayload,
                OperationType.Create,
                requestOptions,
                cancellationToken);
        }

        public override Task<ItemResponse<T>> CreateItemAsync<T>(
            object partitionKey,
            T item,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.CreateItemAsStreamAsync(
                partitionKey: partitionKey,
                streamPayload: this.ClientContext.CosmosSerializer.ToStream<T>(item),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateItemResponseAsync<T>(response);
        }

        public override Task<CosmosResponseMessage> ReadItemAsStreamAsync(
                    object partitionKey,
                    string id,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessItemAsStreamAsync(
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
            Task<CosmosResponseMessage> response = this.ReadItemAsStreamAsync(
                partitionKey: partitionKey,
                id: id,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateItemResponseAsync<T>(response);
        }

        public override Task<CosmosResponseMessage> UpsertItemAsStreamAsync(
                    object partitionKey,
                    Stream streamPayload,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessItemAsStreamAsync(
                partitionKey,
                null,
                streamPayload,
                OperationType.Upsert,
                requestOptions,
                cancellationToken);
        }

        public override Task<ItemResponse<T>> UpsertItemAsync<T>(
            object partitionKey,
            T item,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.UpsertItemAsStreamAsync(
                partitionKey: partitionKey,
                streamPayload: this.ClientContext.CosmosSerializer.ToStream<T>(item),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateItemResponseAsync<T>(response);
        }

        public override Task<CosmosResponseMessage> ReplaceItemAsStreamAsync(
                    object partitionKey,
                    string id,
                    Stream streamPayload,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessItemAsStreamAsync(
                partitionKey,
                id,
                streamPayload,
                OperationType.Replace,
                requestOptions,
                cancellationToken);
        }

        public override Task<ItemResponse<T>> ReplaceItemAsync<T>(
            object partitionKey,
            string id,
            T item,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.ReplaceItemAsStreamAsync(
               partitionKey: partitionKey,
               id: id,
               streamPayload: this.ClientContext.CosmosSerializer.ToStream<T>(item),
               requestOptions: requestOptions,
               cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateItemResponseAsync<T>(response);
        }

        public override Task<CosmosResponseMessage> DeleteItemAsStreamAsync(
                    object partitionKey,
                    string id,
                    ItemRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessItemAsStreamAsync(
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
            Task<CosmosResponseMessage> response = this.DeleteItemAsStreamAsync(
               partitionKey: partitionKey,
               id: id,
               requestOptions: requestOptions,
               cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateItemResponseAsync<T>(response);
        }

        public override FeedIterator<T> GetItemsIterator<T>(
            int? maxItemCount = null,
            string continuationToken = null)
        {
            return new FeedIteratorCore<T>(
                maxItemCount,
                continuationToken,
                null,
                this.ItemFeedRequestExecutorAsync<T>);
        }

        public override FeedIterator GetItemsStreamIterator(
            int? maxItemCount = null,
            string continuationToken = null,
            ItemRequestOptions requestOptions = null)
        {
            return new FeedIteratorCore(maxItemCount, continuationToken, requestOptions, this.ItemStreamFeedRequestExecutorAsync);
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
                resourceLink: this.LinkUri,
                isContinuationExpected: true,
                allowNonValueAggregateQuery: true,
                correlatedActivityId: Guid.NewGuid());

            return new FeedIteratorCore(
                maxItemCount,
                continuationToken,
                requestOptions,
                this.QueryRequestExecutorAsync,
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
                resourceLink: this.LinkUri,
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
                resourceLink: this.LinkUri,
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
                cosmosContainer: this,
                changeFeedProcessor: changeFeedProcessor,
                applyBuilderConfiguration: changeFeedProcessor.ApplyBuildConfiguration);
        }

        public override ChangeFeedProcessorBuilder CreateChangeFeedEstimatorBuilder(
            string workflowName,
            Func<IReadOnlyList<RemainingLeaseTokenWork>, CancellationToken, Task> estimationDelegate,
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
                cosmosContainer: this,
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
                cosmosContainer: this,
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

        internal Task<CosmosResponseMessage> ProcessItemAsStreamAsync(
            object partitionKey,
            string itemId,
            Stream streamPayload,
            OperationType operationType,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            CosmosContainerCore.ValidatePartitionKey(partitionKey, requestOptions);
            Uri resourceUri = this.GetResourceUri(requestOptions, operationType, itemId);

            return this.ClientContext.ProcessResourceOperationAsStreamAsync(
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

        private Task<CosmosResponseMessage> ItemStreamFeedRequestExecutorAsync(
            int? maxItemCount,
            string continuationToken,
            RequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            return this.ClientContext.ProcessResourceOperationAsync<CosmosResponseMessage>(
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

        private async Task<CosmosResponseMessage> QueryRequestExecutorAsync(
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
                return (CosmosResponseMessage)(await cosmosQueryExecution.ExecuteNextAsync(cancellationToken));
            }
            catch (DocumentClientException exception)
            {
                return exception.ToCosmosResponseMessage(request: null);
            }
            catch (CosmosException exception)
            {
                return new CosmosResponseMessage(
                    headers: exception.Headers,
                    requestMessage: null,
                    errorMessage: exception.Message,
                    statusCode: exception.StatusCode,
                    error: exception.Error);
            }
            catch (AggregateException ae)
            {
                CosmosResponseMessage errorMessage = TransportHandler.AggregateExceptionConverter(ae, null);
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
