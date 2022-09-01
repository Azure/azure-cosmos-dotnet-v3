﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Handler;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal class ClientContextCore : CosmosClientContext
    {
        private readonly BatchAsyncContainerExecutorCache batchExecutorCache;
        private readonly CosmosClient client;
        private readonly DocumentClient documentClient;
        private readonly CosmosSerializerCore serializerCore;
        private readonly CosmosResponseFactoryInternal responseFactory;
        private readonly RequestInvokerHandler requestHandler;
        private readonly CosmosClientOptions clientOptions;
        private readonly ClientTelemetry telemetry;

        private readonly string userAgent;
        private bool isDisposed = false;

        private ClientContextCore(
            CosmosClient client,
            CosmosClientOptions clientOptions,
            CosmosSerializerCore serializerCore,
            CosmosResponseFactoryInternal cosmosResponseFactory,
            RequestInvokerHandler requestHandler,
            DocumentClient documentClient,
            string userAgent,
            BatchAsyncContainerExecutorCache batchExecutorCache,
            ClientTelemetry telemetry)
        {
            this.client = client;
            this.clientOptions = clientOptions;
            this.serializerCore = serializerCore;
            this.responseFactory = cosmosResponseFactory;
            this.requestHandler = requestHandler;
            this.documentClient = documentClient;
            this.userAgent = userAgent;
            this.batchExecutorCache = batchExecutorCache;
            this.telemetry = telemetry;
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
            HttpMessageHandler httpMessageHandler = CosmosHttpClientCore.CreateHttpClientHandler(
                clientOptions.GatewayModeMaxConnectionLimit,
                clientOptions.WebProxy);

            DocumentClient documentClient = new DocumentClient(
               cosmosClient.Endpoint,
               cosmosClient.AuthorizationTokenProvider,
               apitype: clientOptions.ApiType,
               sendingRequestEventArgs: clientOptions.SendingRequestEventArgs,
               transportClientHandlerFactory: clientOptions.TransportClientHandlerFactory,
               connectionPolicy: clientOptions.GetConnectionPolicy(cosmosClient.ClientId),
               enableCpuMonitor: clientOptions.EnableCpuMonitor,
               storeClientFactory: clientOptions.StoreClientFactory,
               desiredConsistencyLevel: clientOptions.GetDocumentsConsistencyLevel(),
               handler: httpMessageHandler,
               sessionContainer: clientOptions.SessionContainer);

            return ClientContextCore.Create(
                cosmosClient,
                documentClient,
                clientOptions);
        }

        internal static CosmosClientContext Create(
            CosmosClient cosmosClient,
            DocumentClient documentClient,
            CosmosClientOptions clientOptions,
            RequestInvokerHandler requestInvokerHandler = null)
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

            ConnectionPolicy connectionPolicy = clientOptions.GetConnectionPolicy(cosmosClient.ClientId);
            ClientTelemetry telemetry = null;
            if (connectionPolicy.EnableClientTelemetry)
            {
                try
                {
                    telemetry = ClientTelemetry.CreateAndStartBackgroundTelemetry(
                        clientId: cosmosClient.Id,
                        documentClient: documentClient,
                        userAgent: connectionPolicy.UserAgentContainer.UserAgent,
                        connectionMode: connectionPolicy.ConnectionMode,
                        authorizationTokenProvider: cosmosClient.AuthorizationTokenProvider,
                        diagnosticsHelper: DiagnosticsHandlerHelper.Instance,
                        preferredRegions: clientOptions.ApplicationPreferredRegions);

                } 
                catch (Exception ex)
                {
                    DefaultTrace.TraceInformation($"Error While starting Telemetry Job : {ex.Message}. Hence disabling Client Telemetry");
                    connectionPolicy.EnableClientTelemetry = false;
                }
               
            } 
            else
            {
                DefaultTrace.TraceInformation("Client Telemetry Disabled.");
            }

            if (requestInvokerHandler == null)
            {
                //Request pipeline 
                ClientPipelineBuilder clientPipelineBuilder = new ClientPipelineBuilder(
                    cosmosClient,
                    clientOptions.ConsistencyLevel,
                    clientOptions.CustomHandlers,
                    telemetry: telemetry);

                requestInvokerHandler = clientPipelineBuilder.Build();
            }

            CosmosSerializerCore serializerCore = CosmosSerializerCore.Create(
                clientOptions.Serializer,
                clientOptions.SerializerOptions);

            // This sets the serializer on client options which gives users access to it if a custom one is not configured.
            clientOptions.SetSerializerIfNotConfigured(serializerCore.GetCustomOrDefaultSerializer());

            CosmosResponseFactoryInternal responseFactory = new CosmosResponseFactoryCore(serializerCore);

            return new ClientContextCore(
                client: cosmosClient,
                clientOptions: clientOptions,
                serializerCore: serializerCore,
                cosmosResponseFactory: responseFactory,
                requestHandler: requestInvokerHandler,
                documentClient: documentClient,
                userAgent: documentClient.ConnectionPolicy.UserAgentContainer.UserAgent,
                batchExecutorCache: new BatchAsyncContainerExecutorCache(),
                telemetry: telemetry);
        }

        /// <summary>
        /// The Cosmos client that is used for the request
        /// </summary>
        internal override CosmosClient Client => this.ThrowIfDisposed(this.client);

        internal override DocumentClient DocumentClient => this.ThrowIfDisposed(this.documentClient);

        internal override CosmosSerializerCore SerializerCore => this.ThrowIfDisposed(this.serializerCore);

        internal override CosmosResponseFactoryInternal ResponseFactory => this.ThrowIfDisposed(this.responseFactory);

        internal override RequestInvokerHandler RequestHandler => this.ThrowIfDisposed(this.requestHandler);

        internal override CosmosClientOptions ClientOptions => this.ThrowIfDisposed(this.clientOptions);

        internal override string UserAgent => this.ThrowIfDisposed(this.userAgent);

        /// <summary>
        /// Generates the URI link for the resource
        /// </summary>
        /// <param name="parentLink">The parent link URI (/dbs/mydbId) </param>
        /// <param name="uriPathSegment">The URI path segment</param>
        /// <param name="id">The id of the resource</param>
        /// <returns>A resource link in the format of {parentLink}/this.UriPathSegment/this.Name with this.Name being a Uri escaped version</returns>
        internal override string CreateLink(
            string parentLink,
            string uriPathSegment,
            string id)
        {
            this.ThrowIfDisposed();
            int parentLinkLength = parentLink?.Length ?? 0;
            string idUriEscaped = Uri.EscapeUriString(id);

            Debug.Assert(parentLinkLength == 0 || !parentLink.EndsWith("/"));

            StringBuilder stringBuilder = new StringBuilder(parentLinkLength + 2 + uriPathSegment.Length + idUriEscaped.Length);
            if (parentLinkLength > 0)
            {
                stringBuilder.Append(parentLink);
                stringBuilder.Append("/");
            }

            stringBuilder.Append(uriPathSegment);
            stringBuilder.Append("/");
            stringBuilder.Append(idUriEscaped);
            return stringBuilder.ToString();
        }

        internal override void ValidateResource(string resourceId)
        {
            this.ThrowIfDisposed();
            this.DocumentClient.ValidateResource(resourceId);
        }

        internal override Task<TResult> 
            OperationHelperAsync<TResult>(
            string operationName,
            RequestOptions requestOptions,
            Func<ITrace, Task<TResult>> task,
            Func<TResult, OpenTelemetryAttributes> openTelemetry,
            TraceComponent traceComponent = TraceComponent.Transport,
            Tracing.TraceLevel traceLevel = Tracing.TraceLevel.Info)
        {
            return SynchronizationContext.Current == null ?
                this.OperationHelperWithRootTraceAsync(operationName, 
                                                       requestOptions, 
                                                       task,
                                                       openTelemetry,
                                                       traceComponent,
                                                       traceLevel) :
                this.OperationHelperWithRootTraceWithSynchronizationContextAsync(operationName, 
                                                                  requestOptions, 
                                                                  task,
                                                                  openTelemetry,
                                                                  traceComponent,
                                                                  traceLevel);
        }

        private async Task<TResult> OperationHelperWithRootTraceAsync<TResult>(
            string operationName,
            RequestOptions requestOptions,
            Func<ITrace, Task<TResult>> task,
            Func<TResult, OpenTelemetryAttributes> openTelemetry,
            TraceComponent traceComponent,
            Tracing.TraceLevel traceLevel)
        {
            bool disableDiagnostics = requestOptions != null && requestOptions.DisablePointOperationDiagnostics;

            using (ITrace trace = disableDiagnostics ? NoOpTrace.Singleton : (ITrace)Tracing.Trace.GetRootTrace(operationName, traceComponent, traceLevel))
            {
                trace.AddDatum("Client Configuration", this.client.ClientConfigurationTraceDatum);

                return await this.RunWithDiagnosticsHelperAsync(
                    trace,
                    task,
                    openTelemetry,
                    operationName,
                    requestOptions);
            }
        }

        private Task<TResult> OperationHelperWithRootTraceWithSynchronizationContextAsync<TResult>(
            string operationName,
            RequestOptions requestOptions,
            Func<ITrace, Task<TResult>> task,
            Func<TResult, OpenTelemetryAttributes> openTelemetry,
            TraceComponent traceComponent,
            Tracing.TraceLevel traceLevel)
        {
            Debug.Assert(SynchronizationContext.Current != null, "This should only be used when a SynchronizationContext is specified");

            string syncContextVirtualAddress = SynchronizationContext.Current.ToString();

            // Used on NETFX applications with SynchronizationContext when doing locking calls
            return Task.Run(async () =>
            {
                bool disableDiagnostics = requestOptions != null && requestOptions.DisablePointOperationDiagnostics;

                using (ITrace trace = disableDiagnostics ? NoOpTrace.Singleton : (ITrace)Tracing.Trace.GetRootTrace(operationName, traceComponent, traceLevel))
                {
                    trace.AddDatum("Synchronization Context", syncContextVirtualAddress);

                    return await this.RunWithDiagnosticsHelperAsync(
                        trace,
                        task,
                        openTelemetry,
                        operationName,
                        requestOptions);
                }
            });
        }

        internal override Task<ResponseMessage> ProcessResourceOperationStreamAsync(
            string resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerInternal cosmosContainerCore,
            PartitionKey? partitionKey,
            string itemId,
            Stream streamPayload,
            Action<RequestMessage> requestEnricher,
            ITrace trace,
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
                    operationType: operationType,
                    requestOptions: requestOptions,
                    cosmosContainerCore: cosmosContainerCore,
                    partitionKey: partitionKey.Value,
                    itemId: itemId,
                    streamPayload: streamPayload,
                    trace: trace,
                    cancellationToken: cancellationToken);
            }

            return this.ProcessResourceOperationStreamAsync(
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: requestOptions,
                cosmosContainerCore: cosmosContainerCore,
                feedRange: partitionKey.HasValue ? new FeedRangePartitionKey(partitionKey.Value) : null,
                streamPayload: streamPayload,
                requestEnricher: requestEnricher,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        internal override Task<ResponseMessage> ProcessResourceOperationStreamAsync(
            string resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerInternal cosmosContainerCore,
            FeedRange feedRange,
            Stream streamPayload,
            Action<RequestMessage> requestEnricher,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            this.ThrowIfDisposed();
            return this.RequestHandler.SendAsync(
                resourceUriString: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: requestOptions,
                cosmosContainerCore: cosmosContainerCore,
                feedRange: feedRange,
                streamPayload: streamPayload,
                requestEnricher: requestEnricher,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        internal override Task<T> ProcessResourceOperationAsync<T>(
            string resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerInternal cosmosContainerCore,
            FeedRange feedRange,
            Stream streamPayload,
            Action<RequestMessage> requestEnricher,
            Func<ResponseMessage, T> responseCreator,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            this.ThrowIfDisposed();

            return this.RequestHandler.SendAsync<T>(
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: requestOptions,
                cosmosContainerCore: cosmosContainerCore,
                feedRange: feedRange,
                streamPayload: streamPayload,
                requestEnricher: requestEnricher,
                responseCreator: responseCreator,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        internal override async Task<ContainerProperties> GetCachedContainerPropertiesAsync(
            string containerUri,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            using (ITrace childTrace = trace.StartChild("Get Container Properties", TraceComponent.Transport, Tracing.TraceLevel.Info))
            {
                this.ThrowIfDisposed();
                ClientCollectionCache collectionCache = await this.DocumentClient.GetCollectionCacheAsync(childTrace);
                try
                {
                    return await collectionCache.ResolveByNameAsync(
                        HttpConstants.Versions.CurrentVersion,
                        containerUri,
                        forceRefesh: false,
                        trace: childTrace,
                        clientSideRequestStatistics: null,
                        cancellationToken: cancellationToken);
                }
                catch (DocumentClientException ex)
                {
                    throw CosmosExceptionFactory.Create(ex, childTrace);
                }
            }
        }

        internal override BatchAsyncContainerExecutor GetExecutorForContainer(ContainerInternal container)
        {
            this.ThrowIfDisposed();

            if (!this.ClientOptions.AllowBulkExecution)
            {
                return null;
            }

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
                    this.telemetry?.Dispose();
                    this.batchExecutorCache.Dispose();
                    this.DocumentClient.Dispose();
                }

                this.isDisposed = true;
            }
        }

        private async Task<TResult> RunWithDiagnosticsHelperAsync<TResult>(
            ITrace trace,
            Func<ITrace, Task<TResult>> task,
            Func<TResult, OpenTelemetryAttributes> openTelemetry,
            string operationName,
            RequestOptions requestOptions)
        {
            using (OpenTelemetryCoreRecorder recorder = 
                                OpenTelemetryRecorderFactory.CreateRecorder(
                                    operationName: operationName,
                                    requestOptions: requestOptions,
                                    clientContext: this.isDisposed ? null : this))
            using (new ActivityScope(Guid.NewGuid()))
            {
                try
                {
                    // Record Operation Name
                    recorder.Record(OpenTelemetryAttributeKeys.DbOperation, operationName);

                    TResult result = await task(trace).ConfigureAwait(false);
                    if (openTelemetry != null && recorder.IsEnabled)
                    {
                        // Record request response information
                        OpenTelemetryAttributes response = openTelemetry(result);
                        recorder.Record(response);
                    }

                    return result;
                }
                catch (OperationCanceledException oe) when (!(oe is CosmosOperationCanceledException))
                {
                    CosmosOperationCanceledException operationCancelledException = new CosmosOperationCanceledException(oe, trace);
                    recorder.MarkFailed(operationCancelledException);
                    
                    throw operationCancelledException;
                }
                catch (ObjectDisposedException objectDisposed) when (!(objectDisposed is CosmosObjectDisposedException))
                {
                    CosmosObjectDisposedException objectDisposedException = new CosmosObjectDisposedException(
                        objectDisposed,
                        this.client,
                        trace);
                    recorder.MarkFailed(objectDisposedException);

                    throw objectDisposedException;
                }
                catch (NullReferenceException nullRefException) when (!(nullRefException is CosmosNullReferenceException))
                {
                    CosmosNullReferenceException nullException = new CosmosNullReferenceException(
                        nullRefException,
                        trace);
                    recorder.MarkFailed(nullException);

                    throw nullException;
                }
                catch (Exception ex)
                {
                    recorder.MarkFailed(ex);

                    throw;
                }
            }
        }

        private async Task<ResponseMessage> ProcessResourceOperationAsBulkStreamAsync(
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerInternal cosmosContainerCore,
            PartitionKey partitionKey,
            string itemId,
            Stream streamPayload,
            ITrace trace,
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
                cosmosClientContext: this);

            TransactionalBatchOperationResult batchOperationResult = await cosmosContainerCore.BatchExecutor.AddAsync(
                itemBatchOperation,
                trace,
                itemRequestOptions,
                cancellationToken);

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
                || operationType == OperationType.Replace
                || operationType == OperationType.Patch);
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
