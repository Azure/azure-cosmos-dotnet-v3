//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class GatewayStoreClient : TransportClient
    {
        private readonly ICommunicationEventSource eventSource;
        private readonly CosmosHttpClient httpClient;
        private readonly JsonSerializerSettings SerializerSettings;
        private static readonly HttpMethod httpPatchMethod = new HttpMethod(HttpConstants.HttpMethods.Patch);

        public GatewayStoreClient(
            CosmosHttpClient httpClient,
            ICommunicationEventSource eventSource,
            JsonSerializerSettings serializerSettings = null)
        {
            this.httpClient = httpClient;
            this.SerializerSettings = serializerSettings;
            this.eventSource = eventSource;
        }

        public async Task<DocumentServiceResponse> InvokeAsync(
           DocumentServiceRequest request,
           ResourceType resourceType,
           Uri physicalAddress,
           CancellationToken cancellationToken)
        {
            using (HttpResponseMessage responseMessage = await this.InvokeClientAsync(request, resourceType, physicalAddress, cancellationToken))
            {
                return await GatewayStoreClient.ParseResponseAsync(responseMessage, request.SerializerSettings ?? this.SerializerSettings, request);
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
            Uri physicalAddress = GatewayStoreClient.IsFeedRequest(request.OperationType) ?
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
                    INameValueCollection headers = GatewayStoreClient.ExtractResponseHeaders(responseMessage);
                    Stream contentStream = await GatewayStoreClient.BufferContentIfAvailableAsync(responseMessage);
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
                    INameValueCollection headers = GatewayStoreClient.ExtractResponseHeaders(responseMessage);
                    Stream contentStream = await GatewayStoreClient.BufferContentIfAvailableAsync(responseMessage);
                    return new DocumentServiceResponse(
                        body: contentStream,
                        headers: headers,
                        statusCode: responseMessage.StatusCode,
                        clientSideRequestStatistics: requestStatistics,
                        serializerSettings: serializerSettings);
                }
                else
                {
                    throw await GatewayStoreClient.CreateDocumentClientExceptionAsync(responseMessage, requestStatistics);
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
        ///   Is the media type not "application/json"?
        ///      return DocumentClientExcpetion with responseMessage and header information.
        ///
        ///   Is the header content-length == 0 and media type is "application/json"? Test case sensitivity.
        ///      return DocumentClientException with message 'No response content from gateway.'
        ///
        ///   Is the content actual length == 0 after a trim and media type is "application/json"? Test case sensitivity. Whitespace scenarios.
        ///      return DocumentClientException with message 'No response content from gateway.'
        ///
        ///   Is the content not parseable as json, but content length != 0 and media type is "application/json"? Test case sensitivity.
        ///      return DocumentClientException with message set to raw non-json message from response.
        /// </summary>
        /// <param name="responseMessage"></param>
        /// <param name="requestStatistics"></param>
        internal static async Task<DocumentClientException> CreateDocumentClientExceptionAsync(
            HttpResponseMessage responseMessage,
            IClientSideRequestStatistics requestStatistics)
        {
            if (responseMessage is null)
            {
                throw new ArgumentNullException(nameof(responseMessage));
            }

            if (requestStatistics is null)
            {
                throw new ArgumentNullException(nameof(requestStatistics));
            }

            // Ask, what is the purpose of this, really?
            // The only impact of try parse fail is an empty resourceIdOrFullName.

            if (!PathsHelper.TryParsePathSegments(
                resourceUrl: responseMessage.RequestMessage.RequestUri.LocalPath,
                isFeed: out _,
                resourcePath: out _,
                resourceIdOrFullName: out string resourceIdOrFullName,
                isNameBased: out _))
            {
                // if resourceLink is invalid - we will not set resourceAddress in exception.
            }

            try
            {
                Stream readStream = await responseMessage.Content.ReadAsStreamAsync();
                Error error = Documents.Resource.LoadFrom<Error>(readStream);

                if (responseMessage.Content?.Headers?.ContentLength == 0 ||
                    error.Message.Trim().Length == 0)
                {
                    error = new Error
                    {
                        Code = responseMessage.StatusCode.ToString(),
                        Message = "No response content from gateway."
                    };
                }

                return new DocumentClientException(
                    error,
                    responseMessage.Headers,
                    responseMessage.StatusCode)
                {
                    StatusDescription = responseMessage.ReasonPhrase,
                    ResourceAddress = resourceIdOrFullName,
                    RequestStatistics = requestStatistics
                };
            }
            catch
            {
                StringBuilder context = new StringBuilder();
                context.AppendLine(await responseMessage.Content.ReadAsStringAsync());

                HttpRequestMessage requestMessage = responseMessage.RequestMessage;
                if (requestMessage != null)
                {
                    context.AppendLine($"RequestUri: {requestMessage.RequestUri};");
                    context.AppendLine($"RequestMethod: {requestMessage.Method.Method};");

                    if (requestMessage.Headers != null)
                    {
                        foreach (KeyValuePair<string, IEnumerable<string>> header in requestMessage.Headers)
                        {
                            context.AppendLine($"Header: {header.Key} Length: {string.Join(",", header.Value).Length};");
                        }
                    }
                }

                return new DocumentClientException(
                    message: context.ToString(),
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
        }

        internal static bool IsAllowedRequestHeader(string headerName)
        {
            if (!headerName.StartsWith("x-ms", StringComparison.OrdinalIgnoreCase))
            {
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

            HttpRequestMessage requestMessage = new HttpRequestMessage(httpMethod, physicalAddress);

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
            
            return requestMessage;
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:DisposeObjectsBeforeLosingScope", Justification = "Disposable object returned by method")]
        private Task<HttpResponseMessage> InvokeClientAsync(
           DocumentServiceRequest request,
           ResourceType resourceType,
           Uri physicalAddress,
           CancellationToken cancellationToken)
        {
            return this.httpClient.SendHttpAsync(
                () => this.PrepareRequestMessageAsync(request, physicalAddress),
                resourceType,
                HttpTimeoutPolicy.GetTimeoutPolicy(request),
                request.RequestContext.ClientRequestStatistics,
                cancellationToken);
        }
    }
}
