//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Collections;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Routing;
    using Newtonsoft.Json;

    // Marking it as non-sealed in order to unit test it using Moq framework
    internal class GatewayStoreModel : IStoreModel, IDisposable
    {
        // Gateway has backoff/retry logic to hide transient errors.
        private readonly TimeSpan requestTimeout = TimeSpan.FromSeconds(65);
        private readonly GlobalEndpointManager endpointManager;
        private readonly DocumentClientEventSource eventSource;
        private readonly ISessionContainer sessionContainer;
        private readonly ConsistencyLevel defaultConsistencyLevel;

        private HttpClient httpClient;
        private CookieContainer cookieJar;

        public GatewayStoreModel(
            GlobalEndpointManager endpointManager,
            ISessionContainer sessionContainer,
            TimeSpan requestTimeout,
            ConsistencyLevel defaultConsistencyLevel,
            DocumentClientEventSource eventSource,
            UserAgentContainer userAgent,
            ApiType apiType = ApiType.None,
            HttpMessageHandler messageHandler = null)
        {
            // CookieContainer is not really required, but is helpful in debugging.
            this.cookieJar = new CookieContainer();
            this.endpointManager = endpointManager;
            this.httpClient = new HttpClient(messageHandler ?? new HttpClientHandler { CookieContainer = this.cookieJar });
            this.sessionContainer = sessionContainer;
            this.defaultConsistencyLevel = defaultConsistencyLevel;

            // Use max of client specified and our own request timeout value when sending
            // requests to gateway. Otherwise, we will have gateway's transient
            // error hiding retries are of no use.
            this.httpClient.Timeout = (requestTimeout > this.requestTimeout) ? requestTimeout : this.requestTimeout;
            this.httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
            
            this.httpClient.AddUserAgentHeader(userAgent);
            this.httpClient.AddApiTypeHeader(apiType);

            // Set requested API version header that can be used for
            // version enforcement.
            this.httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.Version,
                HttpConstants.Versions.CurrentVersion);

            this.httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.Accept, RuntimeConstants.MediaTypes.Json);

            this.eventSource = eventSource;
        }

        internal JsonSerializerSettings SerializerSettings { get; set; }

        public virtual async Task<DocumentServiceResponse> ProcessMessageAsync(DocumentServiceRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.ApplySessionToken(request);

            DocumentServiceResponse response;
            try
            {
                response = await this.InvokeAsync(request, request.ResourceType, cancellationToken);
            }
            catch (DocumentClientException exception)
            {
                if ((!ReplicatedResourceClient.IsMasterResource(request.ResourceType)) &&
                    (exception.StatusCode == HttpStatusCode.PreconditionFailed || exception.StatusCode == HttpStatusCode.Conflict
                    || (exception.StatusCode == HttpStatusCode.NotFound && exception.GetSubStatus() != SubStatusCodes.ReadSessionNotAvailable)))
                {
                    this.CaptureSessionToken(request, exception.Headers);
                }

                throw;
            }

            this.CaptureSessionToken(request, response.Headers);
            return response;
        }

        public virtual async Task<CosmosAccountSettings> GetDatabaseAccountAsync(HttpRequestMessage requestMessage)
        {
            CosmosAccountSettings databaseAccount = null;

            // Get the ServiceDocumentResource from the gateway.
            using (HttpResponseMessage responseMessage =
                await this.httpClient.SendHttpAsync(requestMessage))
            {
                using (DocumentServiceResponse documentServiceResponse = await ClientExtensions.ParseResponseAsync(responseMessage))
                {
                    databaseAccount = documentServiceResponse.GetInternalResource<CosmosAccountSettings>(CosmosAccountSettings.CreateNewInstance);
                }

                long longValue;
                IEnumerable<string> headerValues;
                if (responseMessage.Headers.TryGetValues(HttpConstants.HttpHeaders.MaxMediaStorageUsageInMB, out headerValues) &&
                    (headerValues.Count() != 0))
                {
                    if (long.TryParse(headerValues.First(), out longValue))
                    {
                        databaseAccount.MaxMediaStorageUsageInMB = longValue;
                    }
                }

                if (responseMessage.Headers.TryGetValues(HttpConstants.HttpHeaders.CurrentMediaStorageUsageInMB, out headerValues) &&
                    (headerValues.Count() != 0))
                {
                    if (long.TryParse(headerValues.First(), out longValue))
                    {
                        databaseAccount.MediaStorageUsageInMB = longValue;
                    }
                }

                if (responseMessage.Headers.TryGetValues(HttpConstants.HttpHeaders.DatabaseAccountConsumedDocumentStorageInMB, out headerValues) &&
                   (headerValues.Count() != 0))
                {
                    if (long.TryParse(headerValues.First(), out longValue))
                    {
                        databaseAccount.ConsumedDocumentStorageInMB = longValue;
                    }
                }

                if (responseMessage.Headers.TryGetValues(HttpConstants.HttpHeaders.DatabaseAccountProvisionedDocumentStorageInMB, out headerValues) &&
                   (headerValues.Count() != 0))
                {
                    if (long.TryParse(headerValues.First(), out longValue))
                    {
                        databaseAccount.ProvisionedDocumentStorageInMB = longValue;
                    }
                }

                if (responseMessage.Headers.TryGetValues(HttpConstants.HttpHeaders.DatabaseAccountReservedDocumentStorageInMB, out headerValues) &&
                   (headerValues.Count() != 0))
                {
                    if (long.TryParse(headerValues.First(), out longValue))
                    {
                        databaseAccount.ReservedDocumentStorageInMB = longValue;
                    }
                }
            }

            return databaseAccount;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public static bool IsFeedRequest(OperationType requestOperationType)
        {
            return requestOperationType == OperationType.Create ||
                requestOperationType == OperationType.Upsert ||
                requestOperationType == OperationType.ReadFeed ||
                requestOperationType == OperationType.Query ||
                requestOperationType == OperationType.SqlQuery ||
                requestOperationType == OperationType.Batch;
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

        private void CaptureSessionToken(DocumentServiceRequest request, INameValueCollection responseHeaders)
        {
            if (request.ResourceType == ResourceType.Collection && request.OperationType == OperationType.Delete)
            {
                string resourceId;

                if (request.IsNameBased)
                {
                    resourceId = responseHeaders[HttpConstants.HttpHeaders.OwnerId];
                }
                else
                {
                    resourceId = request.ResourceId;
                }

                this.sessionContainer.ClearTokenByResourceId(resourceId);
            }
            else
            {
                this.sessionContainer.SetSessionToken(request, responseHeaders);
            }
        }

        private void ApplySessionToken(DocumentServiceRequest request)
        {
            if (request.Headers != null &&
                !string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.SessionToken]))
            {
                if (ReplicatedResourceClient.IsMasterResource(request.ResourceType))
                {
                    request.Headers.Remove(HttpConstants.HttpHeaders.SessionToken);
                }
                return; //User is explicitly controlling the session.
            }

            string requestConsistencyLevel = request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel];

            bool sessionConsistency =
                this.defaultConsistencyLevel == ConsistencyLevel.Session ||
                (!string.IsNullOrEmpty(requestConsistencyLevel)
                    && string.Equals(requestConsistencyLevel, ConsistencyLevel.Session.ToString(), StringComparison.OrdinalIgnoreCase));

            if (!sessionConsistency || ReplicatedResourceClient.IsMasterResource(request.ResourceType))
            {
                return; // Only apply the session token in case of session consistency and when resource is not a master resource
            }

            //Apply the ambient session.
            string sessionToken = this.sessionContainer.ResolveGlobalSessionToken(request);

            if (!string.IsNullOrEmpty(sessionToken))
            {
                request.Headers[HttpConstants.HttpHeaders.SessionToken] = sessionToken;
            }
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.httpClient != null)
                {
                    try
                    {
                        this.httpClient.Dispose();
                    }
                    catch (Exception exception)
                    {
                        DefaultTrace.TraceWarning("Exception {0} thrown during dispose of HttpClient, this could happen if there are inflight request during the dispose of client",
                            exception);
                    }

                    this.httpClient = null;
                }
            }
        }

        async private Task<DocumentServiceResponse> InvokeAsync(DocumentServiceRequest request, ResourceType resourceType, CancellationToken cancellationToken)
        {
            Func<Task<DocumentServiceResponse>> funcDelegate = async () =>
            {
                using (HttpRequestMessage requestMessage = await this.PrepareRequestMessageAsync(request))
                {
                    DateTime sendTimeUtc = DateTime.UtcNow;
                    Guid localGuid = Guid.NewGuid();  // For correlating HttpRequest and HttpResponse Traces

                    this.eventSource.Request(
                        Guid.Empty,
                        localGuid,
                        requestMessage.RequestUri.ToString(),
                        resourceType.ToResourceTypeString(),
                        requestMessage.Headers);

                    using (HttpResponseMessage responseMessage = await this.httpClient.SendAsync(requestMessage, cancellationToken))
                    {
                        DateTime receivedTimeUtc = DateTime.UtcNow;
                        double durationInMilliSeconds = (receivedTimeUtc - sendTimeUtc).TotalMilliseconds;

                        IEnumerable<string> headerValues;
                        Guid activityId = Guid.Empty;
                        if (responseMessage.Headers.TryGetValues(HttpConstants.HttpHeaders.ActivityId, out headerValues) &&
                            headerValues.Count() != 0)
                        {
                            activityId = new Guid(headerValues.First());
                        }

                        this.eventSource.Response(
                            activityId,
                            localGuid,
                            (short)responseMessage.StatusCode,
                            durationInMilliSeconds,
                            responseMessage.Headers);

                        return await ClientExtensions.ParseResponseAsync(responseMessage, request.SerializerSettings ?? this.SerializerSettings, request);
                    }
                }
            };

            return await BackoffRetryUtility<DocumentServiceResponse>.ExecuteAsync(funcDelegate, new WebExceptionRetryPolicy(), cancellationToken);
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:DisposeObjectsBeforeLosingScope", Justification = "Disposable object returned by method")]
        private async Task<HttpRequestMessage> PrepareRequestMessageAsync(DocumentServiceRequest request)
        {
            HttpMethod httpMethod = HttpMethod.Head;
            if (request.OperationType == OperationType.Create ||
                request.OperationType == OperationType.Upsert ||
                request.OperationType == OperationType.Query ||
                request.OperationType == OperationType.SqlQuery ||
                request.OperationType == OperationType.Batch ||
                request.OperationType == OperationType.ExecuteJavaScript)
            {
                httpMethod = HttpMethod.Post;
            }
            else if (request.OperationType == OperationType.Read ||
                request.OperationType == OperationType.ReadFeed)
            {
                httpMethod = HttpMethod.Get;
            }
            else if (request.OperationType == OperationType.Replace)
            {
                httpMethod = HttpMethod.Put;
            }
            else if (request.OperationType == OperationType.Delete)
            {
                httpMethod = HttpMethod.Delete;
            }
            else
            {
                throw new NotImplementedException();
            }

            HttpRequestMessage requestMessage = new HttpRequestMessage(httpMethod,
                GatewayStoreModel.IsFeedRequest(request.OperationType) ? this.GetFeedUri(request) : this.GetEntityUri(request));

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
                    if (GatewayStoreModel.IsAllowedRequestHeader(key))
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
            return requestMessage;
        }

        private Uri GetEntityUri(DocumentServiceRequest entity)
        {
            string contentLocation = entity.Headers[HttpConstants.HttpHeaders.ContentLocation];

            if (!string.IsNullOrEmpty(contentLocation))
            {
                return new Uri(this.endpointManager.ResolveServiceEndpoint(entity), new Uri(contentLocation).AbsolutePath);
            }

            return new Uri(this.endpointManager.ResolveServiceEndpoint(entity), PathsHelper.GeneratePath(entity.ResourceType, entity, false));
        }

        private Uri GetFeedUri(DocumentServiceRequest request)
        {
            return new Uri(this.endpointManager.ResolveServiceEndpoint(request), PathsHelper.GeneratePath(request.ResourceType, request, true));
        }
    }
}
