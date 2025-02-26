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

    // Marking it as non-sealed in order to unit test it using Moq framework
    internal class ThinClientStoreModel : GatewayStoreModel, IDisposable
    {
        private ProxyStoreClient proxyStoreClient;

        public ThinClientStoreModel(
            GlobalEndpointManager endpointManager,
            ISessionContainer sessionContainer,
            ConsistencyLevel defaultConsistencyLevel,
            DocumentClientEventSource eventSource,
            JsonSerializerSettings serializerSettings,
            CosmosHttpClient httpClient,
            Uri proxyEndpoint,
            string globalDatabaseAccountName)
            : base(endpointManager, sessionContainer, defaultConsistencyLevel, eventSource, serializerSettings, httpClient)
        {
            this.proxyStoreClient = new ProxyStoreClient(
                httpClient,
                eventSource,
                proxyEndpoint,
                globalDatabaseAccountName,
                serializerSettings);
        }

        public override async Task<DocumentServiceResponse> ProcessMessageAsync(DocumentServiceRequest request, CancellationToken cancellationToken = default)
        {
            DefaultTrace.TraceInformation("In {0}, OperationType: {1}, ResourceType: {2}", nameof(ThinClientStoreModel), request.OperationType, request.ResourceType);

            await GatewayStoreModel.ApplySessionTokenAsync(
                request,
                base.defaultConsistencyLevel,
                base.sessionContainer,
                base.partitionKeyRangeCache,
                base.clientCollectionCache,
                base.endpointManager);

            DocumentServiceResponse response;
            try
            {
                Uri physicalAddress = ProxyStoreClient.IsFeedRequest(request.OperationType) ? base.GetFeedUri(request) : base.GetEntityUri(request);
                // Collect region name only for document resources
                if (request.ResourceType.Equals(ResourceType.Document) && base.endpointManager.TryGetLocationForGatewayDiagnostics(request.RequestContext.LocationEndpointToRoute, out string regionName))
                {
                    request.RequestContext.RegionName = regionName;
                }
                response = await this.proxyStoreClient.InvokeAsync(request, request.ResourceType, physicalAddress, cancellationToken);
            }
            catch (DocumentClientException exception)
            {
                if ((!ReplicatedResourceClient.IsMasterResource(request.ResourceType)) &&
                    (exception.StatusCode == HttpStatusCode.PreconditionFailed || exception.StatusCode == HttpStatusCode.Conflict
                    || (exception.StatusCode == HttpStatusCode.NotFound && exception.GetSubStatus() != SubStatusCodes.ReadSessionNotAvailable)))
                {
                    await base.CaptureSessionTokenAndHandleSplitAsync(exception.StatusCode, exception.GetSubStatus(), request, exception.Headers);
                }

                throw;
            }

            await base.CaptureSessionTokenAndHandleSplitAsync(response.StatusCode, response.SubStatusCode, request, response.Headers);
            return response;
        }

        public new void Dispose()
        {
            this.Dispose(true);
        }

        private void Dispose(bool disposing)
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
        }
    }
}
