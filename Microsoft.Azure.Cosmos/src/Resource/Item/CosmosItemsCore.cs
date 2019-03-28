//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Internal;
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

        internal CosmosItemsCore(CosmosContainer container)
        {
            this.client = container.Client;
            this.container = container;
            this.cosmosJsonSerializer = this.container.Client.CosmosJsonSerializer;
            this.cachedUriSegmentWithoutId = this.GetResourceSegmentUriWithoutId();
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
            requestOptions.maxConcurrency = maxConcurrency;
            requestOptions.EnableCrossPartitionQuery = true;

            FeedOptions feedOptions = requestOptions.ToFeedOptions();
            feedOptions.RequestContinuation = continuationToken;
            feedOptions.MaxItemCount = maxItemCount;
            if (partitionKey != null)
            {
                PartitionKey pk = new PartitionKey(partitionKey);
                feedOptions.PartitionKey = pk;
            }

            DocumentQuery<CosmosQueryResponse> documentQuery = (DocumentQuery<CosmosQueryResponse>)this.client.DocumentClient.CreateDocumentQuery<CosmosQueryResponse>(
                collectionLink: this.container.LinkUri.OriginalString,
                feedOptions: feedOptions,
                querySpec: sqlQueryDefinition.ToSqlQuerySpec());

            return new CosmosResultSetIteratorCore(
                maxItemCount,
                continuationToken,
                requestOptions,
                this.QueryRequestExecutor,
                documentQuery);
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
            CosmosQueryRequestOptions options = requestOptions ?? new CosmosQueryRequestOptions();
            if (partitionKey != null)
            {
                PartitionKey pk = new PartitionKey(partitionKey);
                options.PartitionKey = pk;
            }

            options.EnableCrossPartitionQuery = false;

            return new CosmosDefaultResultSetIterator<T>(
                maxItemCount,
                continuationToken,
                options,
                this.NextResultSetAsync<T>,
                sqlQueryDefinition.ToSqlQuerySpec());
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
            CosmosQueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosQueryRequestOptions options = requestOptions ?? new CosmosQueryRequestOptions();
            options.maxConcurrency = maxConcurrency;
            options.EnableCrossPartitionQuery = true;

            return new CosmosDefaultResultSetIterator<T>(
                maxItemCount,
                continuationToken,
                options,
                this.NextResultSetAsync<T>,
                sqlQueryDefinition.ToSqlQuerySpec());
        }

        public override CosmosResultSetIterator<T> CreateItemQuery<T>(
            string sqlQueryText,
            int maxConcurrency,
            int? maxItemCount = null,
            string continuationToken = null,
            CosmosQueryRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.CreateItemQuery<T>(
                new CosmosSqlQueryDefinition(sqlQueryText),
                maxConcurrency,
                maxItemCount,
                continuationToken,
                requestOptions,
                cancellationToken);
        }

        internal async Task<CosmosQueryResponse<T>> NextResultSetAsync<T>(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            CosmosQueryRequestOptions cosmosQueryRequestOptions = options as CosmosQueryRequestOptions ?? new CosmosQueryRequestOptions();
            FeedOptions feedOptions = cosmosQueryRequestOptions.ToFeedOptions();
            feedOptions.RequestContinuation = continuationToken;
            feedOptions.MaxItemCount = maxItemCount;

            IDocumentQuery<T> documentClientResult = this.client.DocumentClient.CreateDocumentQuery<T>(
                collectionLink: this.container.LinkUri.OriginalString,
                feedOptions: feedOptions,
                querySpec: state as SqlQuerySpec).AsDocumentQuery();

            try
            {
                FeedResponse<T> feedResponse = await documentClientResult.ExecuteNextAsync<T>(cancellationToken);
                return CosmosQueryResponse<T>.CreateResponse<T>(feedResponse, feedResponse.ResponseContinuation, documentClientResult.HasMoreResults);
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
            object state,
            CancellationToken cancellationToken)
        {
            DocumentQuery<CosmosQueryResponse> documentQuery = (DocumentQuery<CosmosQueryResponse>)state;
            // DEVNOTE: Remove try catch once query pipeline is converted to exceptionless
            try
            {
                return await documentQuery.ExecuteNextQueryStreamAsync(cancellationToken);
            }
            catch (DocumentClientException exception)
            {
                return new CosmosQueryResponse(
                        errorMessage: exception.Message,
                        httpStatusCode: exception.StatusCode.HasValue ? exception.StatusCode.Value : HttpStatusCode.InternalServerError,
                        retryAfter: exception.RetryAfter);
            }catch(AggregateException ae)
            {
                DocumentClientException exception = ae.InnerException as DocumentClientException;
                if(exception == null)
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
