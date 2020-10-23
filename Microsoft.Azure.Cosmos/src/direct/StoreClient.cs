//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Newtonsoft.Json;

    /// <summary>
    /// Instantiated to issue direct connectivity requests to the backend on:
    ///     - Gateway (for gateway mode clients)
    ///     - Client (for direct mode clients)
    /// StoreClient uses the ReplicatedResourceClient to make requests to the backend.
    /// </summary>
    internal sealed class StoreClient : IStoreClient
    {
        private readonly ISessionContainer sessionContainer;
        private readonly ReplicatedResourceClient replicatedResourceClient;
        // TODO(ovplaton): Remove transportClient from this class after removing
        // AddDisableRntbdChannelCallback. The field isn't used for anything else.
        private readonly TransportClient transportClient;
        private readonly IServiceConfigurationReader serviceConfigurationReader;
        private readonly bool enableRequestDiagnostics;

        public StoreClient(
            IAddressResolver addressResolver,
            ISessionContainer sessionContainer,
            IServiceConfigurationReader serviceConfigurationReader,
            IAuthorizationTokenProvider userTokenProvider,
            Protocol protocol,
            TransportClient transportClient,
            bool enableRequestDiagnostics = false,
            bool enableReadRequestsFallback = false,
            bool useMultipleWriteLocations = false,
            bool detectClientConnectivityIssues = false,
            RetryWithConfiguration retryWithConfiguration = null)
        {
            this.transportClient = transportClient;
            this.serviceConfigurationReader = serviceConfigurationReader;
            this.sessionContainer = sessionContainer;
            this.enableRequestDiagnostics = enableRequestDiagnostics;

            this.replicatedResourceClient = new ReplicatedResourceClient(
                addressResolver,
                sessionContainer,
                protocol,
                this.transportClient,
                this.serviceConfigurationReader,
                userTokenProvider,
                enableReadRequestsFallback,
                useMultipleWriteLocations,
                detectClientConnectivityIssues,
                retryWithConfiguration);
        }

        internal JsonSerializerSettings SerializerSettings { get; set; }

        #region Test hooks
        public string LastReadAddress
        {
            get
            {
                return this.replicatedResourceClient.LastReadAddress;
            }
            set
            {
                this.replicatedResourceClient.LastReadAddress = value;
            }
        }

        public string LastWriteAddress
        {
            get
            {
                return this.replicatedResourceClient.LastWriteAddress;
            }
        }

        public bool ForceAddressRefresh
        {
            get
            {
                return this.replicatedResourceClient.ForceAddressRefresh;
            }
            set
            {
                this.replicatedResourceClient.ForceAddressRefresh = value;
            }
        }
        #endregion

        public Task<DocumentServiceResponse> ProcessMessageAsync(DocumentServiceRequest request, IRetryPolicy retryPolicy = null, Func<DocumentServiceRequest, Task> prepareRequestAsyncDelegate = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessMessageAsync(request, cancellationToken, retryPolicy, prepareRequestAsyncDelegate);
        }

        // Decides what to execute based on the ResourceOperation property of the DocumentServiceRequest argument
        public async Task<DocumentServiceResponse> ProcessMessageAsync(DocumentServiceRequest request, CancellationToken cancellationToken, IRetryPolicy retryPolicy = null, Func < DocumentServiceRequest, Task> prepareRequestAsyncDelegate = null)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            await request.EnsureBufferedBodyAsync();

            StoreResponse storeResponse = null;
            try
            {
                storeResponse = retryPolicy != null
                    ? await BackoffRetryUtility<StoreResponse>.ExecuteAsync(() => this.replicatedResourceClient.InvokeAsync(request, prepareRequestAsyncDelegate, cancellationToken), retryPolicy, cancellationToken)
                    : await this.replicatedResourceClient.InvokeAsync(request, prepareRequestAsyncDelegate, cancellationToken);
            }
            catch (DocumentClientException exception)
            {
                if(request.RequestContext.ClientRequestStatistics != null)
                {
                    exception.RequestStatistics = request.RequestContext.ClientRequestStatistics;
                }

                this.UpdateResponseHeader(request, exception.Headers);

                if ((!ReplicatedResourceClient.IsMasterResource(request.ResourceType)) &&
                    (exception.StatusCode == HttpStatusCode.PreconditionFailed || exception.StatusCode == HttpStatusCode.Conflict
                    || (exception.StatusCode == HttpStatusCode.NotFound && exception.GetSubStatus() != SubStatusCodes.ReadSessionNotAvailable)))
                {
                    this.CaptureSessionToken(exception.StatusCode, exception.GetSubStatus(), request, exception.Headers);
                }

                throw;
            }

            return this.CompleteResponse(storeResponse, request);
        }

        #region Response/Headers helper
        private DocumentServiceResponse CompleteResponse(
            StoreResponse storeResponse,
            DocumentServiceRequest request)
        {
            INameValueCollection headersFromStoreResponse = StoreClient.GetHeadersFromStoreResponse(storeResponse);
            this.UpdateResponseHeader(request, headersFromStoreResponse);
            this.CaptureSessionToken((HttpStatusCode)storeResponse.Status, storeResponse.SubStatusCode, request, headersFromStoreResponse);

            DocumentServiceResponse response = new DocumentServiceResponse(
                storeResponse.ResponseBody,
                headersFromStoreResponse,
                (HttpStatusCode)storeResponse.Status,
                this.enableRequestDiagnostics ? request.RequestContext.ClientRequestStatistics : null,
                request.SerializerSettings ?? this.SerializerSettings);

            return response;
        }

        private long GetLSN(INameValueCollection headers)
        {
            long result = -1;
            string value = headers[WFConstants.BackendHeaders.LSN];

            if (!string.IsNullOrEmpty(value))
            {
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
                {
                    return result;
                }
            }

            return -1;
        }

        private void UpdateResponseHeader(DocumentServiceRequest request, INameValueCollection headers)
        {
            string requestConsistencyLevel = request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel];

            bool sessionConsistency =
                this.serviceConfigurationReader.DefaultConsistencyLevel == ConsistencyLevel.Session ||
                (!string.IsNullOrEmpty(requestConsistencyLevel)
                    && string.Equals(requestConsistencyLevel, ConsistencyLevel.Session.ToString(), StringComparison.OrdinalIgnoreCase));

            long storeLSN = this.GetLSN(headers);
            if (storeLSN == -1)
                return;

            string version = request.Headers[HttpConstants.HttpHeaders.Version];
            version = string.IsNullOrEmpty(version) ? HttpConstants.Versions.CurrentVersion : version;

            if (string.Compare(version, HttpConstants.Versions.v2015_12_16, StringComparison.Ordinal) < 0)
            {
                headers[HttpConstants.HttpHeaders.SessionToken] = string.Format(CultureInfo.InvariantCulture, "{0}", storeLSN);
            }
            else
            {
                string partitionKeyRangeId = headers[WFConstants.BackendHeaders.PartitionKeyRangeId];

                if (string.IsNullOrEmpty(partitionKeyRangeId))
                {
                    string inputSession = request.Headers[HttpConstants.HttpHeaders.SessionToken];
                    if (!string.IsNullOrEmpty(inputSession) && inputSession.IndexOf(":", StringComparison.Ordinal) >= 1)
                    {
                        partitionKeyRangeId = inputSession.Substring(0, inputSession.IndexOf(":", StringComparison.Ordinal));
                    }
                    else
                    {
                        partitionKeyRangeId = "0";
                    }
                }

                ISessionToken sessionToken = null;
                string sessionTokenResponseHeader = headers[HttpConstants.HttpHeaders.SessionToken];
                if (!string.IsNullOrEmpty(sessionTokenResponseHeader))
                {
                    sessionToken = SessionTokenHelper.Parse(sessionTokenResponseHeader);
                }
                else if (!VersionUtility.IsLaterThan(version, HttpConstants.VersionDates.v2018_06_18))
                {
                    sessionToken = new SimpleSessionToken(storeLSN);
                }

                if (sessionToken != null)
                {
                    headers[HttpConstants.HttpHeaders.SessionToken] = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}:{1}",
                        partitionKeyRangeId,
                        sessionToken.ConvertToString());
                }
            }
        }

        private void CaptureSessionToken(HttpStatusCode? statusCode, SubStatusCodes subStatusCode, DocumentServiceRequest request, INameValueCollection headers)
        {
            // Exceptionless can try to capture session token from CompleteResponse
            if (request.IsValidStatusCodeForExceptionlessRetry((int) statusCode, subStatusCode))
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
                    resourceId = headers[HttpConstants.HttpHeaders.OwnerId];
                }
                else
                {
                    resourceId = request.ResourceId;
                }
                this.sessionContainer.ClearTokenByResourceId(resourceId);
            }
            else
            {
                this.sessionContainer.SetSessionToken(request, headers);
            }
        }
        
        private static INameValueCollection GetHeadersFromStoreResponse(StoreResponse storeResponse)
        {
            return storeResponse.Headers;
        }

        #endregion

        #region RNTBD Transition

        // Helper for the transition from RNTBD v1 to v2. Delete it after
        // deleting the RNTBD v1 client.
        internal void AddDisableRntbdChannelCallback(Action action)
        {
            Rntbd.TransportClient rntbdTransportClient =
                this.transportClient as Rntbd.TransportClient;
            if (rntbdTransportClient == null)
            {
                return;
            }
            rntbdTransportClient.OnDisableRntbdChannel += action;
        }

        #endregion
    }
}