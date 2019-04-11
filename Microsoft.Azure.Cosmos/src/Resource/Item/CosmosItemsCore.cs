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
        private CosmosJsonSerializer cosmosJsonSerializer { get; }
        private CosmosClient client { get; }
        private CosmosQueryClient queryClient { get; }

        internal CosmosItemsCore(
            CosmosContainer container, 
            CosmosQueryClient cosmosQueryClient = null)
        {
            this.container = container;
            this.cosmosJsonSerializer = this.container.Client.CosmosJsonSerializer;
            this.cachedUriSegmentWithoutId = this.GetResourceSegmentUriWithoutId();
            this.client = container.Client;
            this.queryClient = cosmosQueryClient ?? new CosmosQueryClientCore(this.client, new DocumentQueryClient(this.client.DocumentClient));
        }

        internal readonly CosmosContainer container;

        public override Task<CosmosResponseMessage> CreateItemStreamAsync(
                    object partitionKey,
                    Stream streamPayload,
                    CosmosItemRequestOptions requestOptions = null,
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

        public override Task<CosmosItemResponse<T>> CreateItemAsync<T>(
            object partitionKey,
            T item,
            CosmosItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.CreateItemStreamAsync(
                partitionKey: partitionKey,
                streamPayload: this.cosmosJsonSerializer.ToStream<T>(item),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.client.ResponseFactory.CreateItemResponse<T>(response);
        }

        public override Task<CosmosResponseMessage> ReadItemStreamAsync(
                    object partitionKey,
                    string id,
                    CosmosItemRequestOptions requestOptions = null,
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

        public override Task<CosmosItemResponse<T>> ReadItemAsync<T>(
            object partitionKey,
            string id,
            CosmosItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.ReadItemStreamAsync(
                partitionKey: partitionKey,
                id: id,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.client.ResponseFactory.CreateItemResponse<T>(response);
        }

        public override Task<CosmosResponseMessage> UpsertItemStreamAsync(
                    object partitionKey,
                    Stream streamPayload,
                    CosmosItemRequestOptions requestOptions = null,
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

        public override Task<CosmosItemResponse<T>> UpsertItemAsync<T>(
            object partitionKey,
            T item,
            CosmosItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.UpsertItemStreamAsync(
                partitionKey: partitionKey,
                streamPayload: this.cosmosJsonSerializer.ToStream<T>(item),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.client.ResponseFactory.CreateItemResponse<T>(response);
        }

        public override Task<CosmosResponseMessage> ReplaceItemStreamAsync(
                    object partitionKey,
                    string id,
                    Stream streamPayload,
                    CosmosItemRequestOptions requestOptions = null,
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

        public override Task<CosmosItemResponse<T>> ReplaceItemAsync<T>(
            object partitionKey,
            string id,
            T item,
            CosmosItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.ReplaceItemStreamAsync(
               partitionKey: partitionKey,
               id: id,
               streamPayload: this.cosmosJsonSerializer.ToStream<T>(item),
               requestOptions: requestOptions,
               cancellationToken: cancellationToken);

            return this.client.ResponseFactory.CreateItemResponse<T>(response);
        }

        public override Task<CosmosResponseMessage> DeleteItemStreamAsync(
                    object partitionKey,
                    string id,
                    CosmosItemRequestOptions requestOptions = null,
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

        public override Task<CosmosItemResponse<T>> DeleteItemAsync<T>(
            object partitionKey,
            string id,
            CosmosItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.DeleteItemStreamAsync(
               partitionKey: partitionKey,
               id: id,
               requestOptions: requestOptions,
               cancellationToken: cancellationToken);

            return this.client.ResponseFactory.CreateItemResponse<T>(response);
        }

        public override CosmosResultSetIterator<T> GetItemIterator<T>(
            int? maxItemCount = null,
            string continuationToken = null)
        {
            return new CosmosDefaultResultSetIterator<T>(
                maxItemCount,
                continuationToken,
                null,
                this.ItemFeedRequestExecutor<T>);
        }

        public override CosmosFeedResultSetIterator GetItemStreamIterator(
            int? maxItemCount = null,
            string continuationToken = null,
            CosmosItemRequestOptions requestOptions = null)
        {
            return new CosmosFeedResultSetIteratorCore(maxItemCount, continuationToken, requestOptions, this.ItemStreamFeedRequestExecutor);
        }

        public override CosmosResultSetIterator CreateItemQueryAsStream(
            CosmosSqlQueryDefinition sqlQueryDefinition,
            int maxConcurrency,
            object partitionKey = null,
            int? maxItemCount = null,
            string continuationToken = null,
            CosmosQueryRequestOptions requestOptions = null)
        {
            requestOptions = requestOptions ?? new CosmosQueryRequestOptions();
            requestOptions.MaxConcurrency = maxConcurrency;
            requestOptions.EnableCrossPartitionQuery = true;
            requestOptions.RequestContinuation = continuationToken;
            requestOptions.MaxItemCount = maxItemCount;
            requestOptions.PartitionKey = partitionKey;

            IDocumentQueryExecutionContext documentQueryExecution = new CosmosQueryExecutionContextFactory(
                client: this.queryClient,
                resourceTypeEnum: ResourceType.Document,
                operationType: OperationType.Query,
                resourceType: typeof(CosmosQueryResponse),
                sqlQuerySpec: sqlQueryDefinition.ToSqlQuerySpec(),
                queryRequestOptions: requestOptions,
                resourceLink: this.container.LinkUri,
                isContinuationExpected: true,
                correlatedActivityId: Guid.NewGuid());

            return new CosmosResultSetIteratorCore(
                maxItemCount,
                continuationToken,
                requestOptions,
                this.QueryRequestExecutor,
                documentQueryExecution);
        }

        public override CosmosResultSetIterator CreateItemQueryAsStream(
            string sqlQueryText,
            int maxConcurrency,
            object partitionKey = null,
            int? maxItemCount = null,
            string continuationToken = null,
            CosmosQueryRequestOptions requestOptions = null)
        {
            return this.CreateItemQueryAsStream(
                new CosmosSqlQueryDefinition(sqlQueryText),
                maxConcurrency,
                partitionKey,
                maxItemCount,
                continuationToken,
                requestOptions);
        }

        public override CosmosResultSetIterator<T> CreateItemQuery<T>(
            CosmosSqlQueryDefinition sqlQueryDefinition,
            object partitionKey,
            int? maxItemCount = null,
            string continuationToken = null,
            CosmosQueryRequestOptions requestOptions = null)
        {
            requestOptions = requestOptions ?? new CosmosQueryRequestOptions();
            requestOptions.PartitionKey = partitionKey;
            requestOptions.EnableCrossPartitionQuery = false;
            requestOptions.RequestContinuation = continuationToken;
            requestOptions.MaxItemCount = maxItemCount;

            IDocumentQueryExecutionContext documentQueryExecution = new CosmosQueryExecutionContextFactory(
                client: this.queryClient,
                resourceTypeEnum: ResourceType.Document,
                operationType: OperationType.Query,
                resourceType: typeof(T),
                sqlQuerySpec: sqlQueryDefinition.ToSqlQuerySpec(),
                queryRequestOptions: requestOptions,
                resourceLink: this.container.LinkUri,
                isContinuationExpected: true,
                correlatedActivityId: Guid.NewGuid());

            return new CosmosDefaultResultSetIterator<T>(
                maxItemCount,
                continuationToken,
                requestOptions,
                this.NextResultSetAsync<T>,
                documentQueryExecution);
        }

        public override CosmosResultSetIterator<T> CreateItemQuery<T>(
            string sqlQueryText,
            object partitionKey,
            int? maxItemCount = null,
            string continuationToken = null,
            CosmosQueryRequestOptions requestOptions = null)
        {
            return this.CreateItemQuery<T>(
                new CosmosSqlQueryDefinition(sqlQueryText),
                partitionKey,
                maxItemCount,
                continuationToken,
                requestOptions);
        }

        public override CosmosResultSetIterator<T> CreateItemQuery<T>(
            CosmosSqlQueryDefinition sqlQueryDefinition,
            int maxConcurrency,
            int? maxItemCount = null,
            string continuationToken = null,
            CosmosQueryRequestOptions requestOptions = null)
        {
            requestOptions = requestOptions ?? new CosmosQueryRequestOptions();
            requestOptions.EnableCrossPartitionQuery = true;
            requestOptions.RequestContinuation = continuationToken;
            requestOptions.MaxItemCount = maxItemCount;
            requestOptions.MaxConcurrency = maxConcurrency;

            IDocumentQueryExecutionContext documentQueryExecution = new CosmosQueryExecutionContextFactory(
                client: this.queryClient,
                resourceTypeEnum: ResourceType.Document,
                operationType: OperationType.Query,
                resourceType: typeof(T),
                sqlQuerySpec: sqlQueryDefinition.ToSqlQuerySpec(),
                queryRequestOptions: requestOptions,
                resourceLink: this.container.LinkUri,
                isContinuationExpected: true,
                correlatedActivityId: Guid.NewGuid());

            return new CosmosDefaultResultSetIterator<T>(
                maxItemCount,
                continuationToken,
                requestOptions,
                this.NextResultSetAsync<T>,
                documentQueryExecution);
        }

        public override CosmosResultSetIterator<T> CreateItemQuery<T>(
            string sqlQueryText,
            int maxConcurrency,
            int? maxItemCount = null,
            string continuationToken = null,
            CosmosQueryRequestOptions requestOptions = null)
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
            Func<IReadOnlyList<T>, CancellationToken, Task> onChangesDelegate)
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
            return new ChangeFeedProcessorBuilder(workflowName, this.container, changeFeedProcessor, changeFeedProcessor.ApplyBuildConfiguration);
        }

        public override ChangeFeedProcessorBuilder CreateChangeFeedProcessorBuilder(
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
            return new ChangeFeedProcessorBuilder(workflowName, this.container, changeFeedEstimatorCore, changeFeedEstimatorCore.ApplyBuildConfiguration);
        }

        internal CosmosFeedResultSetIterator GetStandByFeedIterator(
            string continuationToken = null,
            int? maxItemCount = null,
            CosmosChangeFeedRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosChangeFeedRequestOptions cosmosQueryRequestOptions = requestOptions as CosmosChangeFeedRequestOptions ?? new CosmosChangeFeedRequestOptions();

            return new CosmosChangeFeedResultSetIteratorCore(
                continuationToken: continuationToken,
                maxItemCount: maxItemCount,
                cosmosContainer: (CosmosContainerCore)this.container,
                options: cosmosQueryRequestOptions);
        }

        internal async Task<CosmosQueryResponse<T>> NextResultSetAsync<T>(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            IDocumentQueryExecutionContext documentQueryExecution = (IDocumentQueryExecutionContext)state;

            try
            {
                FeedResponse<CosmosElement> feedResponse = await documentQueryExecution.ExecuteNextAsync(cancellationToken);
                return CosmosQueryResponse<T>.CreateResponse<T>(
                    feedResponse: feedResponse,
                    jsonSerializer: this.cosmosJsonSerializer,
                    hasMoreResults: !documentQueryExecution.IsDone,
                    resourceType: ResourceType.Document);
            }
            catch (DocumentClientException exception)
            {
                throw new CosmosException(
                    message: exception.Message,
                    statusCode: exception.StatusCode.HasValue ? exception.StatusCode.Value : HttpStatusCode.InternalServerError,
                    subStatusCode: (int)exception.GetSubStatus(),
                    activityId: exception.ActivityId,
                    requestCharge: exception.RequestCharge);
            }
        }

        internal Task<CosmosResponseMessage> ProcessItemStreamAsync(
            object partitionKey,
            string itemId,
            Stream streamPayload,
            OperationType operationType,
            CosmosRequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            CosmosItemsCore.ValidatePartitionKey(partitionKey, requestOptions);
            Uri resourceUri = this.GetResourceUri(requestOptions, operationType, itemId);

            return ExecUtils.ProcessResourceOperationStreamAsync(
                this.container.Database.Client,
                resourceUri,
                ResourceType.Document,
                operationType,
                requestOptions,
                partitionKey,
                streamPayload,
                null,
                cancellationToken);
        }

        private Task<CosmosResponseMessage> ItemStreamFeedRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            Uri resourceUri = this.container.LinkUri;
            return ExecUtils.ProcessResourceOperationAsync<CosmosResponseMessage>(
                client: this.container.Database.Client,
                resourceUri: resourceUri,
                resourceType: ResourceType.Document,
                operationType: OperationType.ReadFeed,
                requestOptions: options,
                requestEnricher: request =>
                {
                    CosmosQueryRequestOptions.FillContinuationToken(request, continuationToken);
                    CosmosQueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                responseCreator: response => response,
                partitionKey: null,
                streamPayload: null,
                cancellationToken: cancellationToken);
        }

        private Task<CosmosQueryResponse<T>> ItemFeedRequestExecutor<T>(
            int? maxItemCount,
           string continuationToken,
           CosmosRequestOptions options,
           object state,
           CancellationToken cancellationToken)
        {
            Uri resourceUri = this.container.LinkUri;
            return ExecUtils.ProcessResourceOperationAsync<CosmosQueryResponse<T>>(
                client: this.container.Database.Client,
                resourceUri: resourceUri,
                resourceType: ResourceType.Document,
                operationType: OperationType.ReadFeed,
                requestOptions: options,
                requestEnricher: request =>
                {
                    CosmosQueryRequestOptions.FillContinuationToken(request, continuationToken);
                    CosmosQueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                responseCreator: response => this.client.ResponseFactory.CreateResultSetQueryResponse<T>(response),
                partitionKey: null,
                streamPayload: null,
                cancellationToken: cancellationToken);
        }

        private async Task<CosmosQueryResponse> QueryRequestExecutor(
            string continuationToken,
            CosmosRequestOptions requestOptions,
            object state,
            CancellationToken cancellationToken)
        {
            IDocumentQueryExecutionContext documentQueryExecution = (IDocumentQueryExecutionContext)state;
            CosmosQueryRequestOptions queryRequestOptions = (CosmosQueryRequestOptions)requestOptions;
            // DEVNOTE: Remove try catch once query pipeline is converted to exceptionless
            try
            {
                FeedResponse<CosmosElement> feedResponse = await documentQueryExecution.ExecuteNextAsync(cancellationToken);
                return CosmosQueryResponse.CreateResponse(
                    feedResponse: feedResponse,
                    cosmosSerializationOptions: queryRequestOptions.CosmosSerializationOptions);
            }
            catch (DocumentClientException exception)
            {
                return new CosmosQueryResponse(
                        errorMessage: exception.Message,
                        httpStatusCode: exception.StatusCode.HasValue ? exception.StatusCode.Value : HttpStatusCode.InternalServerError,
                        retryAfter: exception.RetryAfter);
            }
            catch (AggregateException ae)
            {
                DocumentClientException exception = ae.InnerException as DocumentClientException;
                if (exception == null)
                {
                    throw;
                }

                return new CosmosQueryResponse(
                        errorMessage: exception.Message,
                        httpStatusCode: exception.StatusCode.HasValue ? exception.StatusCode.Value : HttpStatusCode.InternalServerError,
                        retryAfter: exception.RetryAfter);
            }
        }

        internal Uri GetResourceUri(CosmosRequestOptions requestOptions, OperationType operationType, string itemId)
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
        internal static void ValidatePartitionKey(object partitionKey, CosmosRequestOptions requestOptions)
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
