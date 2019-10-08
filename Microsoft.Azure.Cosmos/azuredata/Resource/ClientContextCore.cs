//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Data.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core.Pipeline;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;

    internal class ClientContextCore : CosmosClientContext
    {
        private const string DiagnosticScopeResourceUri = "resourceUri";
        private const string DiagnosticScopeOperationType = "operationType";
        private const string DiagnosticScopeResourceType = "resourceType";
        private const string DiagnosticScopeContainer = "resourceType";
        private const string DiagnosticScopeDiagnostics = "diagnostics";

        private readonly HttpPipeline pipeline;
        private readonly string diagnosticsPrefix;

        internal ClientContextCore(
            CosmosClient client,
            CosmosClientOptions clientOptions,
            CosmosSerializer userJsonSerializer,
            CosmosSerializer defaultJsonSerializer,
            CosmosSerializer sqlQuerySpecSerializer,
            CosmosResponseFactory cosmosResponseFactory,
            RequestInvokerHandler requestHandler,
            DocumentClient documentClient,
            string diagnosticsPrefix)
        {
            this.Client = client;
            this.ClientOptions = clientOptions;
            this.CosmosSerializer = userJsonSerializer;
            this.PropertiesSerializer = defaultJsonSerializer;
            this.SqlQuerySpecSerializer = sqlQuerySpecSerializer;
            this.ResponseFactory = cosmosResponseFactory;
            this.RequestHandler = requestHandler;
            this.DocumentClient = documentClient;

            this.ClientOptions.Transport = new ClientPipelineTransport(requestHandler);
            this.pipeline = HttpPipelineBuilder.Build(this.ClientOptions);
            this.diagnosticsPrefix = diagnosticsPrefix;
        }

        internal override CosmosClient Client { get; }

        internal override DocumentClient DocumentClient { get; }

        internal override CosmosSerializer CosmosSerializer { get; }

        internal override CosmosSerializer PropertiesSerializer { get; }

        internal override CosmosSerializer SqlQuerySpecSerializer { get; }

        internal override CosmosResponseFactory ResponseFactory { get; }

        internal override RequestInvokerHandler RequestHandler { get; }

        internal override CosmosClientOptions ClientOptions { get; }

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

        internal override async Task<ContainerProperties> GetCachedContainerPropertiesAsync(string containerUri, CancellationToken cancellationToken = default)
        {
            ClientCollectionCache collectionCache = await this.DocumentClient.GetCollectionCacheAsync();
            try
            {
                return await collectionCache.ResolveByNameAsync(
                    HttpConstants.Versions.CurrentVersion,
                    containerUri,
                    cancellationToken);
            }
            catch (DocumentClientException ex)
            {
                throw new CosmosException(ex.ToCosmosResponseMessage(null), ex.Message, ex.Error);
            }
        }

        internal override Task<T> ProcessResourceOperationAsync<T>(
            Uri resourceUri,
            ResourceType
            resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerCore cosmosContainerCore,
            PartitionKey? partitionKey,
            Stream streamPayload,
            Action<RequestMessage> requestEnricher,
            Func<ResponseMessage, T> responseCreator,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        internal override async Task<Response> ProcessResourceOperationStreamAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerCore cosmosContainerCore,
            PartitionKey? partitionKey,
            Stream streamPayload,
            Action<RequestMessage> requestEnricher,
            CancellationToken cancellationToken)
        {
            DiagnosticScope scope = this.pipeline.Diagnostics.CreateScope($"{this.diagnosticsPrefix}.{resourceType}-{operationType}");
            try
            {
                scope.AddAttribute(ClientContextCore.DiagnosticScopeResourceUri, resourceUri);
                scope.AddAttribute(ClientContextCore.DiagnosticScopeResourceType, resourceType);
                scope.AddAttribute(ClientContextCore.DiagnosticScopeOperationType, operationType);
                if (cosmosContainerCore != null)
                {
                    scope.AddAttribute(ClientContextCore.DiagnosticScopeContainer, cosmosContainerCore.LinkUri);
                }

                scope.Start();
                (RequestMessage requestMessage, ResponseMessage errorResponse) = await this.RequestHandler.TryCreateRequestMessageAsync(resourceUri, resourceType, operationType, requestOptions, cosmosContainerCore, partitionKey, streamPayload, requestEnricher, cancellationToken);
                if (errorResponse != null)
                {
                    return errorResponse;
                }

                using (requestMessage)
                {
                    // Should populate/generate in some smart way
                    requestMessage.ClientRequestId = Guid.NewGuid().ToString();
                    Response response = await this.pipeline.SendRequestAsync(requestMessage, cancellationToken);
                    ResponseMessage responseMessage = response as ResponseMessage;
                    Debug.Assert(responseMessage != null, "Pipeline did not deliver a ResponseMessage");
                    if (scope.IsEnabled && responseMessage != null)
                    {
                        scope.AddAttribute(ClientContextCore.DiagnosticScopeDiagnostics, responseMessage.Diagnostics);
                    }

                    return responseMessage;
                }
            }
            catch (Exception exception)
            {
                scope.Failed(exception);
                throw;
            }
            finally
            {
                scope.Dispose();
            }
        }

        internal override Task<Response> ProcessResourceOperationStreamAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            RequestOptions requestOptions,
            ContainerCore cosmosContainerCore,
            PartitionKey? partitionKey,
            string itemId,
            Stream streamPayload,
            Action<RequestMessage> requestEnricher,
            CancellationToken cancellationToken)
        {
            //if (this.IsBulkOperationSupported(resourceType, operationType))
            //{
            //    if (!partitionKey.HasValue)
            //    {
            //        throw new ArgumentOutOfRangeException(nameof(partitionKey));
            //    }

            //    return this.ProcessResourceOperationAsBulkStreamAsync(
            //        resourceUri: resourceUri,
            //        resourceType: resourceType,
            //        operationType: operationType,
            //        requestOptions: requestOptions,
            //        cosmosContainerCore: cosmosContainerCore,
            //        partitionKey: partitionKey.Value,
            //        itemId: itemId,
            //        streamPayload: streamPayload,
            //        requestEnricher: requestEnricher,
            //        cancellationToken: cancellationToken);
            //}

            return this.ProcessResourceOperationStreamAsync(
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: requestOptions,
                cosmosContainerCore: cosmosContainerCore,
                partitionKey: partitionKey,
                streamPayload: streamPayload,
                requestEnricher: requestEnricher,
                cancellationToken: cancellationToken);
        }

        internal override void ValidateResource(string id)
        {
            this.DocumentClient.ValidateResource(id);
        }
    }
}