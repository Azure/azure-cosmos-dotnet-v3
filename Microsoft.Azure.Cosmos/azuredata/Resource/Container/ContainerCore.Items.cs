//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Data.Cosmos
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
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

        //private readonly CosmosQueryClient queryClient;

        public override Task<Response> CreateItemStreamAsync(
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

        public override Task<Response> ReadItemStreamAsync(
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

        public override Task<Response<T>> ReadItemAsync<T>(
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<Response> response = this.ReadItemStreamAsync(
                partitionKey: partitionKey,
                id: id,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateItemResponseAsync<T>(response, cancellationToken);
        }

        public override Task<Response> UpsertItemStreamAsync(
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

        public override Task<Response> ReplaceItemStreamAsync(
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

        public override async Task<Response<T>> ReplaceItemAsync<T>(
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

            Task<Response> response = this.ExtractPartitionKeyAndProcessItemStreamAsync(
               partitionKey: partitionKey,
               itemId: id,
               streamPayload: await this.ClientContext.CosmosSerializer.ToStreamAsync<T>(item, cancellationToken),
               operationType: OperationType.Replace,
               requestOptions: requestOptions,
               cancellationToken: cancellationToken);

            return await this.ClientContext.ResponseFactory.CreateItemResponseAsync<T>(response, cancellationToken);
        }

        public override Task<Response> DeleteItemStreamAsync(
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

#if PREVIEW
        public override Batch CreateBatch(PartitionKey partitionKey)
        {
            return new BatchCore(this, partitionKey);
        }
#endif

        // Extracted partition key might be invalid as CollectionCache might be stale.
        // Stale collection cache is refreshed through PartitionKeyMismatchRetryPolicy
        // and partition-key is extracted again. 
        internal async Task<Response> ExtractPartitionKeyAndProcessItemStreamAsync(
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
                Response response = await this.ProcessItemStreamAsync(
                    partitionKey,
                    itemId,
                    streamPayload,
                    operationType,
                    requestOptions,
                    extractPartitionKeyIfNeeded: true,
                    cancellationToken: cancellationToken);

                ResponseMessage responseMessage = response as ResponseMessage;

                if (responseMessage.IsSuccessStatusCode)
                {
                    return response;
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

        internal async Task<Response> ProcessItemStreamAsync(
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
                itemId,
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

                    double value;
                    if (cosmosNumber.IsFloatingPoint)
                    {
                        value = cosmosNumber.AsFloatingPoint().Value;
                    }
                    else
                    {
                        value = cosmosNumber.AsInteger().Value;
                    }

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
