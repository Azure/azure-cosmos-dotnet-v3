//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;

    internal class ClientContextCore : CosmosClientContext
    {
        internal ClientContextCore(
            CosmosClient client,
            CosmosClientOptions clientOptions,
            CosmosSerializerCore serializerCore,
            CosmosResponseFactory cosmosResponseFactory,
            RequestInvokerHandler requestHandler,
            DocumentClient documentClient,
            string userAgent,
            EncryptionProcessor encryptionProcessor = null,
            DekCache dekCache = null)
        {
            this.Client = client;
            this.ClientOptions = clientOptions;
            this.SerializerCore = serializerCore;
            this.ResponseFactory = cosmosResponseFactory;
            this.RequestHandler = requestHandler;
            this.DocumentClient = documentClient;
            this.UserAgent = userAgent;
            this.EncryptionProcessor = encryptionProcessor;
            this.DekCache = dekCache;
        }

        /// <summary>
        /// The Cosmos client that is used for the request
        /// </summary>
        internal override CosmosClient Client { get; }

        internal override DocumentClient DocumentClient { get; }

        internal override CosmosSerializerCore SerializerCore { get; }

        internal override CosmosResponseFactory ResponseFactory { get; }

        internal override RequestInvokerHandler RequestHandler { get; }

        internal override CosmosClientOptions ClientOptions { get; }

        internal override string UserAgent { get; }

        internal override EncryptionProcessor EncryptionProcessor { get; }

        internal override DekCache DekCache { get; }

        /// <summary>
        /// Generates the URI link for the resource
        /// </summary>
        /// <param name="parentLink">The parent link URI (/dbs/mydbId) </param>
        /// <param name="uriPathSegment">The URI path segment</param>
        /// <param name="id">The id of the resource</param>
        /// <returns>A resource link in the format of {parentLink}/this.UriPathSegment/this.Name with this.Name being a Uri escaped version</returns>
        internal override Uri CreateLink(
            string parentLink,
            string uriPathSegment,
            string id)
        {
            int parentLinkLength = parentLink?.Length ?? 0;
            string idUriEscaped = Uri.EscapeUriString(id);

            StringBuilder stringBuilder = new StringBuilder(parentLinkLength + 2 + uriPathSegment.Length + idUriEscaped.Length);
            if (parentLinkLength > 0)
            {
                stringBuilder.Append(parentLink);
                stringBuilder.Append("/");
            }

            stringBuilder.Append(uriPathSegment);
            stringBuilder.Append("/");
            stringBuilder.Append(idUriEscaped);
            return new Uri(stringBuilder.ToString(), UriKind.Relative);
        }

        internal override void ValidateResource(string resourceId)
        {
            this.DocumentClient.ValidateResource(resourceId);
        }

        internal override Task<ResponseMessage> ProcessResourceOperationStreamAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerCore cosmosContainerCore,
            PartitionKey? partitionKey,
            string itemId,
            Stream streamPayload,
            Action<RequestMessage> requestEnricher,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (this.IsBulkOperationSupported(resourceType, operationType))
            {
                if (!partitionKey.HasValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(partitionKey));
                }

                if (requestEnricher != null)
                {
                    throw new ArgumentException($"Bulk does not support {nameof(requestEnricher)}");
                }

                return this.ProcessResourceOperationAsBulkStreamAsync(
                    resourceUri: resourceUri,
                    resourceType: resourceType,
                    operationType: operationType,
                    requestOptions: requestOptions,
                    cosmosContainerCore: cosmosContainerCore,
                    partitionKey: partitionKey.Value,
                    itemId: itemId,
                    streamPayload: streamPayload,
                    diagnosticsContext: diagnosticsContext,
                    cancellationToken: cancellationToken);
            }

            return this.ProcessResourceOperationStreamAsync(
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: requestOptions,
                cosmosContainerCore: cosmosContainerCore,
                partitionKey: partitionKey,
                streamPayload: streamPayload,
                requestEnricher: requestEnricher,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);
        }

        internal override Task<ResponseMessage> ProcessResourceOperationStreamAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerCore cosmosContainerCore,
            PartitionKey? partitionKey,
            Stream streamPayload,
            Action<RequestMessage> requestEnricher,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            return this.RequestHandler.SendAsync(
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: requestOptions,
                cosmosContainerCore: cosmosContainerCore,
                partitionKey: partitionKey,
                streamPayload: streamPayload,
                requestEnricher: requestEnricher,
                diagnosticsContext: diagnosticsContext,
                cancellationToken: cancellationToken);
        }

        internal override Task<T> ProcessResourceOperationAsync<T>(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerCore cosmosContainerCore,
            PartitionKey? partitionKey,
            Stream streamPayload,
            Action<RequestMessage> requestEnricher,
            Func<ResponseMessage, T> responseCreator,
            CosmosDiagnosticsContext diagnosticsScope,
            CancellationToken cancellationToken)
        {
            return this.RequestHandler.SendAsync<T>(
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: requestOptions,
                cosmosContainerCore: cosmosContainerCore,
                partitionKey: partitionKey,
                streamPayload: streamPayload,
                requestEnricher: requestEnricher,
                responseCreator: responseCreator,
                diagnosticsScope: diagnosticsScope,
                cancellationToken: cancellationToken);
        }

        internal override async Task<ContainerProperties> GetCachedContainerPropertiesAsync(
            string containerUri,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosDiagnosticsContextCore diagnosticsContext = new CosmosDiagnosticsContextCore(null);
            ClientCollectionCache collectionCache = await this.DocumentClient.GetCollectionCacheAsync();
            try
            {
                using (diagnosticsContext.CreateScope("ContainerCache.ResolveByNameAsync"))
                {
                    return await collectionCache.ResolveByNameAsync(
                        HttpConstants.Versions.CurrentVersion,
                        containerUri,
                        cancellationToken);
                }
            }
            catch (DocumentClientException ex)
            {
                throw CosmosExceptionFactory.Create(ex, diagnosticsContext);
            }
        }

        private async Task<ResponseMessage> ProcessResourceOperationAsBulkStreamAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerCore cosmosContainerCore,
            PartitionKey partitionKey,
            string itemId,
            Stream streamPayload,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            ItemRequestOptions itemRequestOptions = requestOptions as ItemRequestOptions;
            TransactionalBatchItemRequestOptions batchItemRequestOptions = TransactionalBatchItemRequestOptions.FromItemRequestOptions(itemRequestOptions);
            ItemBatchOperation itemBatchOperation = new ItemBatchOperation(
                operationType: operationType,
                operationIndex: 0,
                partitionKey: partitionKey,
                id: itemId,
                resourceStream: streamPayload,
                requestOptions: batchItemRequestOptions,
                diagnosticsContext: diagnosticsContext);

            TransactionalBatchOperationResult batchOperationResult = await cosmosContainerCore.BatchExecutor.AddAsync(itemBatchOperation, itemRequestOptions, cancellationToken);
            return batchOperationResult.ToResponseMessage();
        }

        private bool IsBulkOperationSupported(
            ResourceType resourceType,
            OperationType operationType)
        {
            if (!this.ClientOptions.AllowBulkExecution)
            {
                return false;
            }

            return resourceType == ResourceType.Document
                && (operationType == OperationType.Create
                || operationType == OperationType.Upsert
                || operationType == OperationType.Read
                || operationType == OperationType.Delete
                || operationType == OperationType.Replace);
        }
    }
}