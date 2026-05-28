//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.FaultInjection;
    using Newtonsoft.Json;
    using static Microsoft.Azure.Cosmos.ThinClientTransportSerializer;

    /// <summary>
    /// A TransportClient that sends requests to proxy endpoint. 
    /// And then processes the response back into DocumentServiceResponse objects.
    /// </summary>
    internal class ThinClientStoreClient : GatewayStoreClient
    {
        private readonly GlobalPartitionEndpointManager globalPartitionEndpointManager;
        private readonly ObjectPool<BufferProviderWrapper> bufferProviderWrapperPool;
        private readonly UserAgentContainer userAgentContainer;
        private readonly IChaosInterceptor chaosInterceptor;

        public ThinClientStoreClient(
            CosmosHttpClient httpClient,
            UserAgentContainer userAgentContainer,
            ICommunicationEventSource eventSource,
            GlobalPartitionEndpointManager globalPartitionEndpointManager,
            JsonSerializerSettings serializerSettings = null,
            IChaosInterceptor chaosInterceptor = null)
            : base(httpClient,
                  eventSource,
                  globalPartitionEndpointManager,
                  serializerSettings)
        {
            this.bufferProviderWrapperPool = new ObjectPool<BufferProviderWrapper>(() => new BufferProviderWrapper());
            this.globalPartitionEndpointManager = globalPartitionEndpointManager;
            this.userAgentContainer = userAgentContainer
                ?? throw new ArgumentNullException(nameof(userAgentContainer),
                "UserAgentContainer cannot be null when initializing ThinClientStoreClient.");
            this.chaosInterceptor = chaosInterceptor;
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
                if (this.chaosInterceptor != null)
                {
                    request.Headers.Set("FAULTINJECTION_IS_PROXY", "true");
                    (bool hasFault, HttpResponseMessage fiResponseMessage) = await this.chaosInterceptor.OnHttpRequestCallAsync(request, cancellationToken);
                    if (hasFault)
                    {
                        DefaultTrace.TraceInformation("Chaos interceptor injected fault for request: {0}", request);
                        fiResponseMessage.RequestMessage = responseMessage.RequestMessage;
                        request.Headers.Remove("FAULTINJECTION_IS_PROXY");
                        return await ThinClientStoreClient.ParseResponseAsync(fiResponseMessage, request.SerializerSettings ?? base.SerializerSettings, request);
                    }
                }

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
                PartitionKeyRange partitionKeyRange = request.RequestContext?.ResolvedPartitionKeyRange;

                if (partitionKeyRange != null)
                {
                   requestMessage.Headers.TryAddWithoutValidation(
                       ThinClientConstants.ProxyStartEpk,
                       partitionKeyRange?.MinInclusive);

                   requestMessage.Headers.TryAddWithoutValidation(
                       ThinClientConstants.ProxyEndEpk,
                       partitionKeyRange?.MaxExclusive);
                }

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
                requestMessage.Headers.TryAddWithoutValidation(
                    ThinClientConstants.UserAgent,
                    this.userAgentContainer.UserAgent);

                Guid activityId = Trace.CorrelationManager.ActivityId;
                Debug.Assert(activityId != Guid.Empty);
                requestMessage.Headers.TryAddWithoutValidation(
                    HttpConstants.HttpHeaders.ActivityId, activityId.ToString());

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
                HttpTimeoutPolicy.GetTimeoutPolicy(request, isThinClientEnabled: true),
                request.RequestContext.ClientRequestStatistics,
                cancellationToken,
                request);
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