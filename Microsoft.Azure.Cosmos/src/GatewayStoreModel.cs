//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
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
        private readonly GlobalEndpointManager endpointManager;
        private readonly DocumentClientEventSource eventSource;
        private readonly ISessionContainer sessionContainer;
        private readonly ConsistencyLevel defaultConsistencyLevel;

        private GatewayStoreClient gatewayStoreClient;

        public GatewayStoreModel(
            GlobalEndpointManager endpointManager,
            ISessionContainer sessionContainer,
            ConsistencyLevel defaultConsistencyLevel,
            DocumentClientEventSource eventSource,
            JsonSerializerSettings serializerSettings,
            HttpClient httpClient)
        {
            this.endpointManager = endpointManager;
            this.sessionContainer = sessionContainer;
            this.defaultConsistencyLevel = defaultConsistencyLevel;
            this.eventSource = eventSource;

            this.gatewayStoreClient = new GatewayStoreClient(
                httpClient,
                this.eventSource,
                serializerSettings);
        }

        public virtual async Task<DocumentServiceResponse> ProcessMessageAsync(DocumentServiceRequest request, CancellationToken cancellationToken = default(CancellationToken))
        {
            GatewayStoreModel.ApplySessionToken(
                request,
                this.defaultConsistencyLevel,
                this.sessionContainer);

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

        internal static void ApplySessionToken(
            DocumentServiceRequest request,
            ConsistencyLevel defaultConsistencyLevel,
            ISessionContainer sessionContainer)
        {
            if (request.Headers == null)
            {
                Debug.Fail("DocumentServiceRequest does not have headers.");
                return;
            }

            // Master resource operations don't require session token.
            if (GatewayStoreModel.IsMasterOperation(request.ResourceType, request.OperationType))
            {
                if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.SessionToken]))
                {
                    request.Headers.Remove(HttpConstants.HttpHeaders.SessionToken);
                }

                return;
            }

            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.SessionToken]))
            {
                return; // User is explicitly controlling the session.
            }

            string requestConsistencyLevel = request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel];

            bool sessionConsistency =
                defaultConsistencyLevel == ConsistencyLevel.Session ||
                (!string.IsNullOrEmpty(requestConsistencyLevel)
                    && string.Equals(requestConsistencyLevel, ConsistencyLevel.Session.ToString(), StringComparison.OrdinalIgnoreCase));

            if (!sessionConsistency)
            {
                return; // Only apply the session token in case of session consistency
            }

            //Apply the ambient session.
            string sessionToken = sessionContainer.ResolveGlobalSessionToken(request);

            if (!string.IsNullOrEmpty(sessionToken))
            {
                request.Headers[HttpConstants.HttpHeaders.SessionToken] = sessionToken;
            }
        }

        // DEVNOTE: This can be replace with ReplicatedResourceClient.IsMasterOperation on next Direct sync
        internal static bool IsMasterOperation(
            ResourceType resourceType,
            OperationType operationType)
        {
            // Stored procedures, trigger, and user defined functions CRUD operations are done on
            // master so they do not require the session token. Stored procedures execute is not a master operation
            return ReplicatedResourceClient.IsMasterResource(resourceType) ||
                   GatewayStoreModel.IsStoredProcedureCrudOperation(resourceType, operationType) ||
                   resourceType == ResourceType.Trigger ||
                   resourceType == ResourceType.UserDefinedFunction ||
                   operationType == OperationType.QueryPlan;
        }

        internal static bool IsStoredProcedureCrudOperation(
            ResourceType resourceType,
            OperationType operationType)
        {
            return resourceType == ResourceType.StoredProcedure &&
                   operationType != Documents.OperationType.ExecuteJavaScript;
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
