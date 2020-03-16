//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json.Bson;

    internal class ClientContextCore : CosmosClientContext
    {
        private readonly BatchAsyncContainerExecutorCache batchExecutorCache;
        private readonly CosmosClient client;
        private readonly DocumentClient documentClient;
        private readonly CosmosSerializerCore serializerCore;
        private readonly CosmosResponseFactory responseFactory;
        private readonly RequestInvokerHandler requestHandler;
        private readonly CosmosClientOptions clientOptions;
        private readonly string userAgent;
        private readonly EncryptionProcessor encryptionProcessor;
        private readonly DekCache dekCache;
        private bool isDisposed = false;

        internal ClientContextCore(
            CosmosClient client,
            CosmosClientOptions clientOptions,
            CosmosSerializerCore serializerCore,
            CosmosResponseFactory cosmosResponseFactory,
            RequestInvokerHandler requestHandler,
            DocumentClient documentClient,
            string userAgent,
            EncryptionProcessor encryptionProcessor,
            DekCache dekCache,
            BatchAsyncContainerExecutorCache batchExecutorCache)
        {
            this.client = client;
            this.clientOptions = clientOptions;
            this.serializerCore = serializerCore;
            this.responseFactory = cosmosResponseFactory;
            this.requestHandler = requestHandler;
            this.documentClient = documentClient;
            this.userAgent = userAgent;
            this.encryptionProcessor = encryptionProcessor;
            this.dekCache = dekCache;
            this.batchExecutorCache = batchExecutorCache;
        }

        internal static CosmosClientContext Create(
            CosmosClient cosmosClient,
            CosmosClientOptions clientOptions)
        {
            if (cosmosClient == null)
            {
                throw new ArgumentNullException(nameof(cosmosClient));
            }

            clientOptions = ClientContextCore.CreateOrCloneClientOptions(clientOptions);

            DocumentClient documentClient = new DocumentClient(
               cosmosClient.Endpoint,
               cosmosClient.AccountKey,
               apitype: clientOptions.ApiType,
               sendingRequestEventArgs: clientOptions.SendingRequestEventArgs,
               transportClientHandlerFactory: clientOptions.TransportClientHandlerFactory,
               connectionPolicy: clientOptions.GetConnectionPolicy(),
               enableCpuMonitor: clientOptions.EnableCpuMonitor,
               storeClientFactory: clientOptions.StoreClientFactory,
               desiredConsistencyLevel: clientOptions.GetDocumentsConsistencyLevel(),
               handler: ClientContextCore.CreateHttpClientHandler(clientOptions),
               sessionContainer: clientOptions.SessionContainer);

            return ClientContextCore.Create(
                cosmosClient,
                documentClient,
                clientOptions);
        }

        internal static CosmosClientContext Create(
            CosmosClient cosmosClient,
            DocumentClient documentClient,
            CosmosClientOptions clientOptions)
        {
            if (cosmosClient == null)
            {
                throw new ArgumentNullException(nameof(cosmosClient));
            }

            if (documentClient == null)
            {
                throw new ArgumentNullException(nameof(documentClient));
            }

            clientOptions = ClientContextCore.CreateOrCloneClientOptions(clientOptions);

            //Request pipeline 
            ClientPipelineBuilder clientPipelineBuilder = new ClientPipelineBuilder(
                cosmosClient,
                clientOptions.ConsistencyLevel,
                clientOptions.CustomHandlers);

            RequestInvokerHandler requestHandler = clientPipelineBuilder.Build();

            CosmosSerializerCore serializerCore = CosmosSerializerCore.Create(
                clientOptions.Serializer,
                clientOptions.SerializerOptions);

            CosmosResponseFactory responseFactory = new CosmosResponseFactory(serializerCore);

            return new ClientContextCore(
                client: cosmosClient,
                clientOptions: clientOptions,
                serializerCore: serializerCore,
                cosmosResponseFactory: responseFactory,
                requestHandler: requestHandler,
                documentClient: documentClient,
                userAgent: documentClient.ConnectionPolicy.UserAgentContainer.UserAgent,
                encryptionProcessor: new EncryptionProcessor(),
                dekCache: new DekCache(),
                batchExecutorCache: new BatchAsyncContainerExecutorCache());
        }

        /// <summary>
        /// The Cosmos client that is used for the request
        /// </summary>
        internal override CosmosClient Client => this.ThrowIfDisposed(this.client);

        internal override DocumentClient DocumentClient => this.ThrowIfDisposed(this.documentClient);

        internal override CosmosSerializerCore SerializerCore => this.ThrowIfDisposed(this.serializerCore);

        internal override CosmosResponseFactory ResponseFactory => this.ThrowIfDisposed(this.responseFactory);

        internal override RequestInvokerHandler RequestHandler => this.ThrowIfDisposed(this.requestHandler);

        internal override CosmosClientOptions ClientOptions => this.ThrowIfDisposed(this.clientOptions);

        internal override string UserAgent => this.ThrowIfDisposed(this.userAgent);

        internal override EncryptionProcessor EncryptionProcessor => this.ThrowIfDisposed(this.encryptionProcessor);

        internal override DekCache DekCache => this.ThrowIfDisposed(this.dekCache);

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
            this.ThrowIfDisposed();
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
            this.ThrowIfDisposed();
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
            this.ThrowIfDisposed();
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
            this.ThrowIfDisposed();
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
            this.ThrowIfDisposed();
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
            CancellationToken cancellationToken)
        {
            this.ThrowIfDisposed();
            CosmosDiagnosticsContextCore diagnosticsContext = new CosmosDiagnosticsContextCore();
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

        internal override BatchAsyncContainerExecutor GetExecutorForContainer(ContainerCore container)
        {
            this.ThrowIfDisposed();
            return this.batchExecutorCache.GetExecutorForContainer(container, this);
        }

        public override void Dispose()
        {
            this.Dispose(true);
        }

        /// <summary>
        /// Dispose of cosmos client
        /// </summary>
        /// <param name="disposing">True if disposing</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                if (disposing)
                {
                    this.batchExecutorCache.Dispose();
                    this.DocumentClient.Dispose();
                }

                this.isDisposed = true;
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
            this.ThrowIfDisposed();
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
            this.ThrowIfDisposed();
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

        private static HttpClientHandler CreateHttpClientHandler(CosmosClientOptions clientOptions)
        {
            if (clientOptions == null || (clientOptions.WebProxy == null))
            {
                return null;
            }

            HttpClientHandler httpClientHandler = new HttpClientHandler
            {
                Proxy = clientOptions.WebProxy
            };

            return httpClientHandler;
        }

        private static CosmosClientOptions CreateOrCloneClientOptions(CosmosClientOptions clientOptions)
        {
            if (clientOptions == null)
            {
                return new CosmosClientOptions();
            }

            return clientOptions.Clone();
        }

        internal T ThrowIfDisposed<T>(T input)
        {
            this.ThrowIfDisposed();

            return input;
        }

        private void ThrowIfDisposed()
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException($"Accessing {nameof(CosmosClient)} after it is disposed is invalid.");
            }
        }
    }
}