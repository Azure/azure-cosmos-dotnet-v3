//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Data.Cosmos
{
    using System;
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
        private readonly HttpPipeline pipeline;

        internal ClientContextCore(
            CosmosClient client,
            CosmosClientOptions clientOptions,
            CosmosSerializer userJsonSerializer,
            CosmosSerializer defaultJsonSerializer,
            CosmosSerializer sqlQuerySpecSerializer,
            CosmosResponseFactory cosmosResponseFactory,
            RequestInvokerHandler requestHandler,
            DocumentClient documentClient)
        {
            this.Client = client;
            this.ClientOptions = clientOptions;
            this.CosmosSerializer = userJsonSerializer;
            this.PropertiesSerializer = defaultJsonSerializer;
            this.SqlQuerySpecSerializer = sqlQuerySpecSerializer;
            this.ResponseFactory = cosmosResponseFactory;
            this.RequestHandler = requestHandler;
            this.DocumentClient = documentClient;

            this.ClientOptions.Transport = new CosmosPipelineTransport(requestHandler);
            this.pipeline = HttpPipelineBuilder.Build(this.ClientOptions);
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
                throw new CosmosException(ex.ToResponse(null), ex.Message, ex.Error);
            }
        }

        internal override Task<T> ProcessResourceOperationAsync<T>(Uri resourceUri, ResourceType resourceType, OperationType operationType, RequestOptions requestOptions, ContainerCore cosmosContainerCore, PartitionKey? partitionKey, Stream streamPayload, Action<RequestMessage> requestEnricher, Func<ResponseMessage, T> responseCreator, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        internal override async Task<Response> ProcessResourceOperationStreamAsync(Uri resourceUri, ResourceType resourceType, OperationType operationType, RequestOptions requestOptions, ContainerCore cosmosContainerCore, PartitionKey? partitionKey, Stream streamPayload, Action<RequestMessage> requestEnricher, CancellationToken cancellationToken)
        {
            DiagnosticScope scope = this.pipeline.Diagnostics.CreateScope($"{resourceType}-{operationType}");
            try
            {
                scope.AddAttribute("resourceUri", resourceUri);
                scope.AddAttribute("resourceType", resourceType);
                scope.AddAttribute("operationType", operationType);
                if (cosmosContainerCore != null)
                {
                    scope.AddAttribute("container", cosmosContainerCore.LinkUri);
                }

                scope.Start();
                using (RequestMessage requestMessage = this.RequestHandler.CreateRequestMessage(resourceUri, resourceType, operationType, requestOptions, cosmosContainerCore, partitionKey, streamPayload, requestEnricher))
                {
                    // Should populate/generate in some smart way
                    requestMessage.ClientRequestId = Guid.NewGuid().ToString();
                    return await this.pipeline.SendRequestAsync(requestMessage, cancellationToken);
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

        internal override Task<Response> ProcessResourceOperationStreamAsync(Uri resourceUri, ResourceType resourceType, OperationType operationType, RequestOptions requestOptions, ContainerCore cosmosContainerCore, PartitionKey? partitionKey, string itemId, Stream streamPayload, Action<RequestMessage> requestEnricher, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        internal override void ValidateResource(string id)
        {
            this.DocumentClient.ValidateResource(id);
        }
    }
}