//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using static Microsoft.Azure.Cosmos.ThinClientTransportSerializer;

    /// <summary>
    /// A TransportClient that sends requests to proxy endpoint. 
    /// And then processes the response back into DocumentServiceResponse objects.
    /// </summary>
    internal class ThinClientStoreClient : GatewayStoreClient
    {
        private readonly ObjectPool<BufferProviderWrapper> bufferProviderWrapperPool;

        public ThinClientStoreClient(
            CosmosHttpClient httpClient,
            ICommunicationEventSource eventSource,
            JsonSerializerSettings serializerSettings = null)
            : base(httpClient,
                  eventSource,
                  serializerSettings)
        {
            this.bufferProviderWrapperPool = new ObjectPool<BufferProviderWrapper>(() => new BufferProviderWrapper());
        }

        public override async Task<DocumentServiceResponse> InvokeAsync(
           DocumentServiceRequest request,
           ResourceType resourceType,
           Uri physicalAddress,
           Uri thinClientEndpoint,
           string globalDatabaseAccountName,
           ClientCollectionCache clientCollectionCache,
           CancellationToken cancellationToken)
        {
            using (HttpResponseMessage responseMessage = await this.InvokeClientAsync(
                request,
                resourceType,
                physicalAddress,
                thinClientEndpoint,
                globalDatabaseAccountName,
                clientCollectionCache,
                cancellationToken))
            {
                HttpResponseMessage proxyResponse = await ThinClientTransportSerializer.ConvertProxyResponseAsync(responseMessage);
                return await ThinClientStoreClient.ParseResponseAsync(proxyResponse, request.SerializerSettings ?? base.SerializerSettings, request);
            }
        }

        internal override async Task<StoreResponse> InvokeStoreAsync(Uri baseAddress, ResourceOperation resourceOperation, DocumentServiceRequest request)
        {
            Uri physicalAddress = ThinClientStoreClient.IsFeedRequest(request.OperationType) ?
                HttpTransportClient.GetResourceFeedUri(resourceOperation.resourceType, baseAddress, request) :
                HttpTransportClient.GetResourceEntryUri(resourceOperation.resourceType, baseAddress, request);

            using (HttpResponseMessage responseMessage = await this.InvokeClientAsync(
                request,
                resourceOperation.resourceType,
                physicalAddress,
                default,
                default,
                default,
                default))
            {
                return await HttpTransportClient.ProcessHttpResponse(request.ResourceAddress, string.Empty, responseMessage, physicalAddress, request);
            }
        }

        private async ValueTask<HttpRequestMessage> PrepareRequestForProxyAsync(
            DocumentServiceRequest request,
            Uri physicalAddress,
            Uri thinClientEndpoint,
            string globalDatabaseAccountName,
            ClientCollectionCache clientCollectionCache)
        {
            HttpRequestMessage requestMessage = base.PrepareRequestMessageAsync(request, physicalAddress).Result;
            requestMessage.Version = new Version(2, 0);

            BufferProviderWrapper bufferProviderWrapper = this.bufferProviderWrapperPool.Get();
            try
            {
                requestMessage.Headers.TryAddWithoutValidation(
                    ThinClientConstants.ProxyOperationType,
                    request.OperationType.ToOperationTypeString());

                requestMessage.Headers.TryAddWithoutValidation(
                    ThinClientConstants.ProxyResourceType,
                    request.ResourceType.ToResourceTypeString());

                Stream contentStream = await ThinClientTransportSerializer.SerializeProxyRequestAsync(
                    bufferProviderWrapper,
                    globalDatabaseAccountName,
                    clientCollectionCache,
                    requestMessage);

                if (!contentStream.CanSeek)
                {
                    throw new InvalidOperationException(
                        $"The serializer returned a non-seekable stream ({contentStream.GetType().FullName}).");
                }

                requestMessage.Content = new StreamContent(contentStream);
                requestMessage.Content.Headers.ContentLength = contentStream.Length;
                requestMessage.Headers.Clear();
                requestMessage.RequestUri = thinClientEndpoint;
                requestMessage.Method = HttpMethod.Post;

                return requestMessage;
            }
            finally
            {
                this.bufferProviderWrapperPool.Return(bufferProviderWrapper);
            }
        }

        private Task<HttpResponseMessage> InvokeClientAsync(
           DocumentServiceRequest request,
           ResourceType resourceType,
           Uri physicalAddress,
           Uri thinClientEndpoint,
           string globalDatabaseAccountName,
           ClientCollectionCache clientCollectionCache,
           CancellationToken cancellationToken)
        {
            DefaultTrace.TraceInformation("In {0}, OperationType: {1}, ResourceType: {2}", nameof(ThinClientStoreClient), request.OperationType, request.ResourceType);
            return base.httpClient.SendHttpAsync(
                () => this.PrepareRequestForProxyAsync(request, physicalAddress, thinClientEndpoint, globalDatabaseAccountName, clientCollectionCache),
                resourceType,
                HttpTimeoutPolicy.GetTimeoutPolicy(request),
                request.RequestContext.ClientRequestStatistics,
                cancellationToken);
        }

        internal class ObjectPool<T>
        {
            private readonly ConcurrentBag<T> Objects;
            private readonly Func<T> ObjectGenerator;

            public ObjectPool(Func<T> objectGenerator)
            {
                this.ObjectGenerator = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
                this.Objects = new ConcurrentBag<T>();
            }

            public T Get()
            {
                return this.Objects.TryTake(out T item) ? item : this.ObjectGenerator();
            }

            public void Return(T item)
            {
                this.Objects.Add(item);
            }
        }
    }
}