//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    /// <summary>
    /// An IStoreModelExtension implementation that routes operations through the ThinClient proxy. 
    /// It applies session tokens, resolves partition key ranges, and delegates requests to ProxyStoreClient.
    /// </summary>
    internal class ThinClientStoreModel : GatewayStoreModel
    {
        private readonly IGlobalEndpointManager endpointManager;
        private readonly ConsistencyLevel defaultConsistencyLevel;
        private readonly DocumentClientEventSource eventSource;
        private ProxyStoreClient proxyStoreClient;

        public ThinClientStoreModel(
            IGlobalEndpointManager endpointManager,
            ISessionContainer sessionContainer,
            ConsistencyLevel defaultConsistencyLevel,
            DocumentClientEventSource eventSource,
            JsonSerializerSettings serializerSettings,
            CosmosHttpClient httpClient,
            Uri proxyEndpoint,
            string globalDatabaseAccountName)
            : base(
                  (IGlobalEndpointManager)endpointManager,
                  sessionContainer,
                  defaultConsistencyLevel,
                  eventSource,
                  serializerSettings,
                  httpClient)
        {
            this.endpointManager = endpointManager;
            this.defaultConsistencyLevel = defaultConsistencyLevel;
            this.eventSource = eventSource;

            this.proxyStoreClient = new ProxyStoreClient(
                httpClient,
                this.eventSource,
                proxyEndpoint,
                globalDatabaseAccountName,
                serializerSettings);
        }

        public override async Task<DocumentServiceResponse> ProcessMessageAsync(
            DocumentServiceRequest request,
            CancellationToken cancellationToken = default)
        {
            await GatewayStoreModel.ApplySessionTokenAsync(
                request,
                this.defaultConsistencyLevel,
                this.sessionContainer,
                this.partitionKeyRangeCache,
                this.clientCollectionCache,
                this.endpointManager);

            DocumentServiceResponse response;
            try
            {
                Uri physicalAddress = ProxyStoreClient.IsFeedRequest(request.OperationType)
                    ? this.GetFeedUri(request)
                    : this.GetEntityUri(request);

                if (request.ResourceType.Equals(ResourceType.Document) &&
                    this.endpointManager.TryGetLocationForGatewayDiagnostics(
                        request.RequestContext.LocationEndpointToRoute,
                        out string regionName))
                {
                    request.RequestContext.RegionName = regionName;
                }

                response = await this.proxyStoreClient.InvokeAsync(
                    request,
                    request.ResourceType,
                    physicalAddress,
                    cancellationToken);
            }
            catch (DocumentClientException ex)
            {
                if (!ReplicatedResourceClient.IsMasterResource(request.ResourceType) &&
                    (ex.StatusCode == HttpStatusCode.PreconditionFailed
                     || ex.StatusCode == HttpStatusCode.Conflict
                     || (ex.StatusCode == HttpStatusCode.NotFound
                         && ex.GetSubStatus() != SubStatusCodes.ReadSessionNotAvailable)))
                {
                    await this.CaptureSessionTokenAndHandleSplitAsync(
                        ex.StatusCode,
                        ex.GetSubStatus(),
                        request,
                        ex.Headers);
                }
                throw;
            }

            await this.CaptureSessionTokenAndHandleSplitAsync(
                response.StatusCode,
                response.SubStatusCode,
                request,
                response.Headers);

            return response;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.proxyStoreClient != null)
                {
                    try
                    {
                        this.proxyStoreClient.Dispose();
                    }
                    catch (Exception exception)
                    {
                        DefaultTrace.TraceWarning("Exception {0} thrown during dispose of HttpClient, this could happen if there are inflight request during the dispose of client",
                            exception);
                    }
                    this.proxyStoreClient = null;
                }
            }
            base.Dispose(disposing);
        }

        public new void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}