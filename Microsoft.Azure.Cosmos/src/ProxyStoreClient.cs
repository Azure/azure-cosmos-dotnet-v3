//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Newtonsoft.Json;
    using static Microsoft.Azure.Cosmos.ThinClientTransportSerializer;

    internal class ProxyStoreClient : TransportClient
    {
        private readonly ICommunicationEventSource eventSource;
        private readonly CosmosHttpClient httpClient;
        private readonly Uri proxyEndpoint;
        private readonly JsonSerializerSettings SerializerSettings;
        private static readonly HttpMethod httpPatchMethod = new HttpMethod(HttpConstants.HttpMethods.Patch);
        private readonly ObjectPool<BufferProviderWrapper> bufferProviderWrapperPool;
        private readonly string globalDatabaseAccountName;

        public ProxyStoreClient(
            CosmosHttpClient httpClient,
            ICommunicationEventSource eventSource,
            Uri proxyEndpoint,
            string globalDatabaseAccountName,
            JsonSerializerSettings serializerSettings = null)
        {
            this.proxyEndpoint = proxyEndpoint;
            this.httpClient = httpClient;
            this.SerializerSettings = serializerSettings;
            this.eventSource = eventSource;
            this.globalDatabaseAccountName = globalDatabaseAccountName;
            this.bufferProviderWrapperPool = new ObjectPool<BufferProviderWrapper>(() => new BufferProviderWrapper());
        }

        public async Task<DocumentServiceResponse> InvokeAsync(
           DocumentServiceRequest request,
           ResourceType resourceType,
           Uri physicalAddress,
           CancellationToken cancellationToken)
        {
            using (HttpResponseMessage responseMessage = await this.InvokeClientAsync(request, resourceType, physicalAddress, cancellationToken))
            {
                HttpResponseMessage proxyResponse = await ThinClientTransportSerializer.ConvertProxyResponseAsync(responseMessage);
                return await ProxyStoreClient.ParseResponseAsync(proxyResponse, request.SerializerSettings ?? this.SerializerSettings, request);
            }
        }

        public static bool IsFeedRequest(OperationType requestOperationType)
        {
            return requestOperationType == OperationType.Create ||
                requestOperationType == OperationType.Upsert ||
                requestOperationType == OperationType.ReadFeed ||
                requestOperationType == OperationType.Query ||
                requestOperationType == OperationType.SqlQuery ||
                requestOperationType == OperationType.QueryPlan ||
                requestOperationType == OperationType.Batch;
        }

        internal override async Task<StoreResponse> InvokeStoreAsync(Uri baseAddress, ResourceOperation resourceOperation, DocumentServiceRequest request)
        {
            Uri physicalAddress = ProxyStoreClient.IsFeedRequest(request.OperationType) ?
                HttpTransportClient.GetResourceFeedUri(resourceOperation.resourceType, baseAddress, request) :
                HttpTransportClient.GetResourceEntryUri(resourceOperation.resourceType, baseAddress, request);

            using (HttpResponseMessage responseMessage = await this.InvokeClientAsync(request, resourceOperation.resourceType, physicalAddress, default))
            {
                return await HttpTransportClient.ProcessHttpResponse(request.ResourceAddress, string.Empty, responseMessage, physicalAddress, request);
            }
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:DisposeObjectsBeforeLosingScope", Justification = "Disposable object returned by method")]
        internal Task<HttpResponseMessage> SendHttpAsync(
            Func<ValueTask<HttpRequestMessage>> requestMessage,
            ResourceType resourceType,
            HttpTimeoutPolicy timeoutPolicy,
            IClientSideRequestStatistics clientSideRequestStatistics,
            CancellationToken cancellationToken = default)
        {
            return this.httpClient.SendHttpAsync(
                createRequestMessageAsync: requestMessage,
                resourceType: resourceType,
                timeoutPolicy: timeoutPolicy,
                clientSideRequestStatistics: clientSideRequestStatistics,
                cancellationToken: cancellationToken);
        }

        internal static async Task<DocumentServiceResponse> ParseResponseAsync(HttpResponseMessage responseMessage, JsonSerializerSettings serializerSettings = null, DocumentServiceRequest request = null)
        {
            using (responseMessage)
            {
                IClientSideRequestStatistics requestStatistics = request?.RequestContext?.ClientRequestStatistics;
                if ((int)responseMessage.StatusCode < 400)
                {
                    INameValueCollection headers = ProxyStoreClient.ExtractResponseHeaders(responseMessage);
                    Stream contentStream = await ProxyStoreClient.BufferContentIfAvailableAsync(responseMessage);
                    return new DocumentServiceResponse(
                        body: contentStream,
                        headers: headers,
                        statusCode: responseMessage.StatusCode,
                        clientSideRequestStatistics: requestStatistics,
                        serializerSettings: serializerSettings);
                }
                else if (request != null
                    && request.IsValidStatusCodeForExceptionlessRetry((int)responseMessage.StatusCode))
                {
                    INameValueCollection headers = ProxyStoreClient.ExtractResponseHeaders(responseMessage);
                    Stream contentStream = await ProxyStoreClient.BufferContentIfAvailableAsync(responseMessage);
                    return new DocumentServiceResponse(
                        body: contentStream,
                        headers: headers,
                        statusCode: responseMessage.StatusCode,
                        clientSideRequestStatistics: requestStatistics,
                        serializerSettings: serializerSettings);
                }
                else
                {
                    throw await ProxyStoreClient.CreateDocumentClientExceptionAsync(responseMessage, requestStatistics);
                }
            }
        }

        internal static INameValueCollection ExtractResponseHeaders(HttpResponseMessage responseMessage)
        {
            INameValueCollection headers = new HttpResponseHeadersWrapper(
                responseMessage.Headers,
                responseMessage.Content?.Headers);

            return headers;
        }

        /// <summary>
        /// Creating a new DocumentClientException using the Gateway response message.
        /// </summary>
        /// <param name="responseMessage"></param>
        /// <param name="requestStatistics"></param>
        internal static async Task<DocumentClientException> CreateDocumentClientExceptionAsync(
            HttpResponseMessage responseMessage,
            IClientSideRequestStatistics requestStatistics)
        {
            if (!PathsHelper.TryParsePathSegments(
                resourceUrl: responseMessage.RequestMessage.RequestUri.LocalPath,
                isFeed: out _,
                resourcePath: out _,
                resourceIdOrFullName: out string resourceIdOrFullName,
                isNameBased: out _))
            {
                // if resourceLink is invalid - we will not set resourceAddress in exception.
            }

            // If service rejects the initial payload like header is to large it will return an HTML error instead of JSON.
            if (string.Equals(responseMessage.Content?.Headers?.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase) &&
                responseMessage.Content?.Headers.ContentLength > 0)
            {
                try
                {
                    Stream contentAsStream = await responseMessage.Content.ReadAsStreamAsync();
                    Error error = JsonSerializable.LoadFrom<Error>(stream: contentAsStream);

                    return new DocumentClientException(
                        errorResource: error,
                        responseHeaders: responseMessage.Headers,
                        statusCode: responseMessage.StatusCode)
                    {
                        StatusDescription = responseMessage.ReasonPhrase,
                        ResourceAddress = resourceIdOrFullName,
                        RequestStatistics = requestStatistics
                    };
                }
                catch
                {
                }
            }

            StringBuilder contextBuilder = new StringBuilder();
            contextBuilder.AppendLine(await responseMessage.Content.ReadAsStringAsync());

            HttpRequestMessage requestMessage = responseMessage.RequestMessage;

            if (requestMessage != null)
            {
                contextBuilder.AppendLine($"RequestUri: {requestMessage.RequestUri};");
                contextBuilder.AppendLine($"RequestMethod: {requestMessage.Method.Method};");

                if (requestMessage.Headers != null)
                {
                    foreach (KeyValuePair<string, IEnumerable<string>> header in requestMessage.Headers)
                    {
                        contextBuilder.AppendLine($"Header: {header.Key} Length: {string.Join(",", header.Value).Length};");
                    }
                }
            }

            return new DocumentClientException(
                message: contextBuilder.ToString(),
                innerException: null,
                responseHeaders: responseMessage.Headers,
                statusCode: responseMessage.StatusCode,
                requestUri: responseMessage.RequestMessage.RequestUri)
            {
                StatusDescription = responseMessage.ReasonPhrase,
                ResourceAddress = resourceIdOrFullName,
                RequestStatistics = requestStatistics
            };
        }

        internal static bool IsAllowedRequestHeader(string headerName)
        {
            if (!headerName.StartsWith("x-ms", StringComparison.OrdinalIgnoreCase))
            {
#pragma warning disable IDE0066 // Convert switch statement to expression
                switch (headerName)
                {
                    //Just flow the header which are settable at RequestMessage level and the one we care.
                    case HttpConstants.HttpHeaders.Authorization:
                    case HttpConstants.HttpHeaders.Accept:
                    case HttpConstants.HttpHeaders.ContentType:
                    case HttpConstants.HttpHeaders.Host:
                    case HttpConstants.HttpHeaders.IfMatch:
                    case HttpConstants.HttpHeaders.IfModifiedSince:
                    case HttpConstants.HttpHeaders.IfNoneMatch:
                    case HttpConstants.HttpHeaders.IfRange:
                    case HttpConstants.HttpHeaders.IfUnmodifiedSince:
                    case HttpConstants.HttpHeaders.UserAgent:
                    case HttpConstants.HttpHeaders.Prefer:
                    case HttpConstants.HttpHeaders.Query:
                    case HttpConstants.HttpHeaders.A_IM:
                        return true;

                    default:
                        return false;
                }
#pragma warning restore IDE0066 // Convert switch statement to expression
            }
            return true;
        }

        private static async Task<Stream> BufferContentIfAvailableAsync(HttpResponseMessage responseMessage)
        {
            if (responseMessage.Content == null)
            {
                return null;
            }

            MemoryStream bufferedStream = new MemoryStream();
            await responseMessage.Content.CopyToAsync(bufferedStream);
            bufferedStream.Position = 0;
            return bufferedStream;
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:DisposeObjectsBeforeLosingScope", Justification = "Disposable object returned by method")]
        private async ValueTask<HttpRequestMessage> PrepareRequestMessageAsync(
            DocumentServiceRequest request,
            Uri physicalAddress)
        {
            HttpMethod httpMethod = HttpMethod.Head;
            if (request.OperationType == OperationType.Create ||
                request.OperationType == OperationType.Upsert ||
                request.OperationType == OperationType.Query ||
                request.OperationType == OperationType.SqlQuery ||
                request.OperationType == OperationType.Batch ||
                request.OperationType == OperationType.ExecuteJavaScript ||
                request.OperationType == OperationType.QueryPlan ||
                (request.ResourceType == ResourceType.PartitionKey && request.OperationType == OperationType.Delete))
            {
                httpMethod = HttpMethod.Post;
            }
            else if (ChangeFeedHelper.IsChangeFeedWithQueryRequest(request.OperationType, request.Body != null))
            {
                // ChangeFeed with payload is a CF with query support and will
                // be a query POST request.
                httpMethod = HttpMethod.Post;
            }
            else if (request.OperationType == OperationType.Read
                || request.OperationType == OperationType.ReadFeed)
            {
                httpMethod = HttpMethod.Get;
            }
            else if ((request.OperationType == OperationType.Replace)
                || (request.OperationType == OperationType.CollectionTruncate))
            {
                httpMethod = HttpMethod.Put;
            }
            else if (request.OperationType == OperationType.Delete)
            {
                httpMethod = HttpMethod.Delete;
            }
            else if (request.OperationType == OperationType.Patch)
            {
                // There isn't support for PATCH method in .NetStandard 2.0
                httpMethod = httpPatchMethod;
            }
            else
            {
                throw new NotImplementedException();
            }

            HttpRequestMessage requestMessage = new (httpMethod, physicalAddress)
            {
                Version = new Version(2, 0),
            };

            // The StreamContent created below will own and dispose its underlying stream, but we may need to reuse the stream on the 
            // DocumentServiceRequest for future requests. Hence we need to clone without incurring copy cost, so that when
            // HttpRequestMessage -> StreamContent -> MemoryStream all get disposed, the original stream will be left open.
            if (request.Body != null)
            {
                await request.EnsureBufferedBodyAsync();
                MemoryStream clonedStream = new MemoryStream();
                // WriteTo doesn't use and update Position of source stream. No point in setting/restoring it.
                request.CloneableBody.WriteTo(clonedStream);
                clonedStream.Position = 0;

                requestMessage.Content = new StreamContent(clonedStream);
            }

            if (request.Headers != null)
            {
                foreach (string key in request.Headers)
                {
                    if (GatewayStoreClient.IsAllowedRequestHeader(key))
                    {
                        if (key.Equals(HttpConstants.HttpHeaders.ContentType, StringComparison.OrdinalIgnoreCase))
                        {
                            requestMessage.Content.Headers.ContentType = new MediaTypeHeaderValue(request.Headers[key]);
                        }
                        else
                        {
                            requestMessage.Headers.TryAddWithoutValidation(key, request.Headers[key]);
                        }
                    }
                }
            }

            if (request.Properties != null)
            {
                foreach (KeyValuePair<string, object> property in request.Properties)
                {
                    requestMessage.Properties.Add(property);
                }
            }

            // add activityId
            Guid activityId = System.Diagnostics.Trace.CorrelationManager.ActivityId;
            Debug.Assert(activityId != Guid.Empty);
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.ActivityId, activityId.ToString());

            string regionName = request?.RequestContext?.RegionName;
            if (regionName != null)
            {
                requestMessage.Properties.Add(ClientSideRequestStatisticsTraceDatum.HttpRequestRegionNameProperty, regionName);
            }

            BufferProviderWrapper bufferProviderWrapper = this.bufferProviderWrapperPool.Get();
            try
            {
                requestMessage.Headers.TryAddWithoutValidation("x-ms-thinclient-proxy-operation-type", request.OperationType.ToOperationTypeString());
                requestMessage.Headers.TryAddWithoutValidation("x-ms-thinclient-proxy-resource-type", request.ResourceType.ToResourceTypeString());
                Stream contentStream = await ThinClientTransportSerializer.SerializeProxyRequestAsync(bufferProviderWrapper, this.globalDatabaseAccountName, requestMessage);

                // force Http2, post and route to the thin client endpoint.
                requestMessage.Content = new StreamContent(contentStream);
                requestMessage.Content.Headers.ContentLength = contentStream.Length;
                requestMessage.Headers.Clear();

                // Force Http 2.0 on the request
                // this.forceHttp20Action.Invoke(request);

                requestMessage.RequestUri = this.proxyEndpoint;
                requestMessage.Method = HttpMethod.Post;

                return requestMessage;
            }
            finally
            {
                this.bufferProviderWrapperPool.Return(bufferProviderWrapper);
            }
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:DisposeObjectsBeforeLosingScope", Justification = "Disposable object returned by method")]
        private Task<HttpResponseMessage> InvokeClientAsync(
           DocumentServiceRequest request,
           ResourceType resourceType,
           Uri physicalAddress,
           CancellationToken cancellationToken)
        {
            DefaultTrace.TraceInformation("In {0}, OperationType: {1}, ResourceType: {2}", nameof(ProxyStoreClient), request.OperationType, request.ResourceType);

            return this.httpClient.SendHttpAsync(
                () => this.PrepareRequestMessageAsync(request, physicalAddress),
                resourceType,
                HttpTimeoutPolicy.GetTimeoutPolicy(request),
                request.RequestContext.ClientRequestStatistics,
                cancellationToken);
        }

        public class ObjectPool<T>
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
