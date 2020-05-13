//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
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

        private GatewayStoreClient gatewayStoreClient;
        private CookieContainer cookieJar;

        private GatewayStoreModel(
            GlobalEndpointManager endpointManager,
            ISessionContainer sessionContainer,
            ConsistencyLevel defaultConsistencyLevel,
            DocumentClientEventSource eventSource)
        {
            // CookieContainer is not really required, but is helpful in debugging.
            this.cookieJar = new CookieContainer();
            this.endpointManager = endpointManager;
            this.sessionContainer = sessionContainer;
            this.defaultConsistencyLevel = defaultConsistencyLevel;
            this.eventSource = eventSource;
        }

        public GatewayStoreModel(
            GlobalEndpointManager endpointManager,
            ISessionContainer sessionContainer,
            TimeSpan requestTimeout,
            ConsistencyLevel defaultConsistencyLevel,
            DocumentClientEventSource eventSource,
            JsonSerializerSettings serializerSettings,
            UserAgentContainer userAgent,
            ApiType apiType,
            HttpMessageHandler messageHandler)
            : this(endpointManager,
                  sessionContainer,
                  defaultConsistencyLevel,
                  eventSource)
        {
            this.InitializeGatewayStoreClient(
                requestTimeout,
                serializerSettings,
                userAgent,
                apiType,
                new HttpClient(messageHandler ?? new HttpClientHandler { CookieContainer = this.cookieJar }));
        }

        public GatewayStoreModel(
            GlobalEndpointManager endpointManager,
            ISessionContainer sessionContainer,
            TimeSpan requestTimeout,
            ConsistencyLevel defaultConsistencyLevel,
            DocumentClientEventSource eventSource,
            JsonSerializerSettings serializerSettings,
            UserAgentContainer userAgent,
            ApiType apiType,
            Func<HttpClient> httpClientFactory)
            : this(endpointManager,
                  sessionContainer,
                  defaultConsistencyLevel,
                  eventSource)
        {
            HttpClient httpClient = httpClientFactory();
            if (httpClient == null)
            {
                throw new InvalidOperationException("HttpClientFactory did not produce an HttpClient");
            }

            this.InitializeGatewayStoreClient(
                requestTimeout,
                serializerSettings,
                userAgent,
                apiType,
                httpClient);

        }

        private void InitializeGatewayStoreClient(
            TimeSpan requestTimeout,
            JsonSerializerSettings serializerSettings,
            UserAgentContainer userAgent,
            ApiType apiType,
            HttpClient httpClient)
        {
            // Use max of client specified and our own request timeout value when sending
            // requests to gateway. Otherwise, we will have gateway's transient
            // error hiding retries are of no use.
            httpClient.Timeout = (requestTimeout > this.requestTimeout) ? requestTimeout : this.requestTimeout;
            httpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };

            httpClient.AddUserAgentHeader(userAgent);
            httpClient.AddApiTypeHeader(apiType);

            // Set requested API version header that can be used for
            // version enforcement.
            httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.Version,
                HttpConstants.Versions.CurrentVersion);

            httpClient.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.Accept, RuntimeConstants.MediaTypes.Json);

            this.gatewayStoreClient = new GatewayStoreClient(
                httpClient,
                this.eventSource,
                serializerSettings);
        }

        public virtual async Task<DocumentServiceResponse> ProcessMessageAsync(DocumentServiceRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.ApplySessionToken(request);

            DocumentServiceResponse response;
            try
            {
                Uri physicalAddress = GatewayStoreClient.IsFeedRequest(request.OperationType) ? this.GetFeedUri(request) : this.GetEntityUri(request);
                response = await this.gatewayStoreClient.InvokeAsync(request, request.ResourceType, physicalAddress, cancellationToken);
            }
            catch (DocumentClientException exception)
            {
                if ((!ReplicatedResourceClient.IsMasterResource(request.ResourceType)) &&
                    (exception.StatusCode == HttpStatusCode.PreconditionFailed || exception.StatusCode == HttpStatusCode.Conflict
                    || (exception.StatusCode == HttpStatusCode.NotFound && exception.GetSubStatus() != SubStatusCodes.ReadSessionNotAvailable)))
                {
                    this.CaptureSessionToken(exception.StatusCode, exception.GetSubStatus(), request, exception.Headers);
                }

                throw;
            }

            this.CaptureSessionToken(response.StatusCode, response.SubStatusCode, request, response.Headers);
            return response;
        }

        public virtual async Task<AccountProperties> GetDatabaseAccountAsync(HttpRequestMessage requestMessage, CancellationToken cancellationToken = default(CancellationToken))
        {
            AccountProperties databaseAccount = null;

            // Get the ServiceDocumentResource from the gateway.
            using (HttpResponseMessage responseMessage =
                await this.gatewayStoreClient.SendHttpAsync(requestMessage, cancellationToken))
            {
                using (DocumentServiceResponse documentServiceResponse = await ClientExtensions.ParseResponseAsync(responseMessage))
                {
                    databaseAccount = CosmosResource.FromStream<AccountProperties>(documentServiceResponse);
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

        private void CaptureSessionToken(
            HttpStatusCode? statusCode,
            SubStatusCodes subStatusCode,
            DocumentServiceRequest request,
            INameValueCollection responseHeaders)
        {
            // Exceptionless can try to capture session token from CompleteResponse
            if (request.IsValidStatusCodeForExceptionlessRetry((int)statusCode, subStatusCode))
            {
                // Not capturing on master resources
                if (ReplicatedResourceClient.IsMasterResource(request.ResourceType))
                {
                    return;
                }

                // Only capturing on 409, 412, 404 && !1002
                if (statusCode != HttpStatusCode.PreconditionFailed
                    && statusCode != HttpStatusCode.Conflict
                        && (statusCode != HttpStatusCode.NotFound || subStatusCode == SubStatusCodes.ReadSessionNotAvailable))
                {
                    return;
                }
            }

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

            if (request.Headers != null &&
                 request.OperationType == OperationType.QueryPlan)
            {
                {
                    string isPlanOnlyString = request.Headers[HttpConstants.HttpHeaders.IsQueryPlanRequest];
                    bool isPlanOnly = false;
                    if (bool.TryParse(isPlanOnlyString, out isPlanOnly) && isPlanOnly)
                    {
                        return; // for query plan session token is not needed
                    }
                }
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
                if (this.gatewayStoreClient != null)
                {
                    try
                    {
                        this.gatewayStoreClient.Dispose();
                    }
                    catch (Exception exception)
                    {
                        DefaultTrace.TraceWarning("Exception {0} thrown during dispose of HttpClient, this could happen if there are inflight request during the dispose of client",
                            exception);
                    }

                    this.gatewayStoreClient = null;
                }
            }
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
