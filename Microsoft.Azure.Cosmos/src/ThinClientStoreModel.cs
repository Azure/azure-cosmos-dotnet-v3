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
    /// It applies session tokens, resolves partition key ranges, and delegates requests to ThinClientStoreClient.
    /// </summary>
    internal class ThinClientStoreModel : GatewayStoreModel
    {
        private ThinClientStoreClient thinClientStoreClient;

        public ThinClientStoreModel(
            GlobalEndpointManager endpointManager,
            ISessionContainer sessionContainer,
            ConsistencyLevel defaultConsistencyLevel,
            DocumentClientEventSource eventSource,
            JsonSerializerSettings serializerSettings,
            CosmosHttpClient httpClient)
            : base(endpointManager,
                  sessionContainer,
                  defaultConsistencyLevel,
                  eventSource,
                  serializerSettings,
                  httpClient)
        {
            this.thinClientStoreClient = new ThinClientStoreClient(
                httpClient,
                eventSource,
                serializerSettings);
        }

        public override async Task<DocumentServiceResponse> ProcessMessageAsync(
            DocumentServiceRequest request,
            CancellationToken cancellationToken = default)
        {
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
                Uri physicalAddress = ThinClientStoreClient.IsFeedRequest(request.OperationType) ? base.GetFeedUri(request) : base.GetEntityUri(request);
                if (request.ResourceType.Equals(ResourceType.Document) && base.endpointManager.TryGetLocationForGatewayDiagnostics(
                    request.RequestContext.LocationEndpointToRoute,
                    out string regionName))
                {
                    request.RequestContext.RegionName = regionName;
                }

                AccountProperties properties = await this.GetDatabaseAccountSafeAsync();
                response = await this.thinClientStoreClient.InvokeAsync(
                    request,
                    request.ResourceType,
                    physicalAddress,
                    properties.ThinClientEndpoint,
                    properties.Id,
                    base.clientCollectionCache,
                    cancellationToken);
            }
            catch (DocumentClientException exception)
            {
                if ((!ReplicatedResourceClient.IsMasterResource(request.ResourceType)) &&
                    (exception.StatusCode == HttpStatusCode.PreconditionFailed || exception.StatusCode == HttpStatusCode.Conflict
                    || (exception.StatusCode == HttpStatusCode.NotFound && exception.GetSubStatus() != SubStatusCodes.ReadSessionNotAvailable)))
                {
                    await base.CaptureSessionTokenAndHandleSplitAsync(
                        exception.StatusCode,
                        exception.GetSubStatus(),
                        request,
                        exception.Headers);
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

        private async Task<AccountProperties> GetDatabaseAccountSafeAsync()
        {
            try
            {
                AccountProperties accountProperties = await this.endpointManager.GetDatabaseAccountAsync();

                if (accountProperties != null)
                {
                    return accountProperties;
                }

                throw new InvalidOperationException("Failed to retrieve AccountProperties. The response was null.");
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Exception while retrieving database account information: {0}", ex.Message);
                throw;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.thinClientStoreClient != null)
                {
                    try
                    {
                        this.thinClientStoreClient.Dispose();
                    }
                    catch (Exception exception)
                    {
                        DefaultTrace.TraceWarning("Exception {0} thrown during dispose of HttpClient, this could happen if there are inflight request during the dispose of client",
                            exception);
                    }
                    this.thinClientStoreClient = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}