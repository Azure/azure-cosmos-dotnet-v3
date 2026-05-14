//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;

    //TODO: write unit test for this handler
    internal class TransportHandler : RequestHandler
    {
        private readonly CosmosClient client;

        public TransportHandler(CosmosClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public override async Task<ResponseMessage> SendAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            try
            {
                ResponseMessage response = await this.ProcessMessageAsync(request, cancellationToken);
                Debug.Assert(System.Diagnostics.Trace.CorrelationManager.ActivityId != Guid.Empty, "Trace activity id is missing");

                return response;
            }
            //catch DocumentClientException and exceptions that inherit it. Other exception types happen before a backend request
            catch (DocumentClientException ex)
            {
                Debug.Assert(System.Diagnostics.Trace.CorrelationManager.ActivityId != Guid.Empty, "Trace activity id is missing");
                return ex.ToCosmosResponseMessage(request);
            }
            catch (CosmosException ce)
            {
                Debug.Assert(System.Diagnostics.Trace.CorrelationManager.ActivityId != Guid.Empty, "Trace activity id is missing");
                return ce.ToCosmosResponseMessage(request);
            }
            catch (OperationCanceledException ex)
            {
                // Catch Operation Cancelled Exception and convert to Timeout 408 if the user did not cancel it.
                // Throw the exception if the user cancelled.
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                Debug.Assert(System.Diagnostics.Trace.CorrelationManager.ActivityId != Guid.Empty, "Trace activity id is missing");
                CosmosException cosmosException = CosmosExceptionFactory.CreateRequestTimeoutException(
                                                            message: ex.Data?["Message"].ToString(),
                                                            headers: new Headers()
                                                            {
                                                                ActivityId = System.Diagnostics.Trace.CorrelationManager.ActivityId.ToString()
                                                            },
                                                            innerException: ex,
                                                            trace: request.Trace);
                return cosmosException.ToCosmosResponseMessage(request);
            }
            catch (AggregateException ex)
            {
                Debug.Assert(System.Diagnostics.Trace.CorrelationManager.ActivityId != Guid.Empty, "Trace activity id is missing");
                // TODO: because the SDK underneath this path uses ContinueWith or task.Result we need to catch AggregateExceptions here
                // in order to ensure that underlying DocumentClientExceptions get propagated up correctly. Once all ContinueWith and .Result 
                // is removed this catch can be safely removed.
                ResponseMessage errorMessage = AggregateExceptionConverter(ex, request);
                if (errorMessage != null)
                {
                    return errorMessage;
                }

                throw;
            }
        }

        internal async Task<ResponseMessage> ProcessMessageAsync(
            RequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            DocumentServiceRequest serviceRequest = request.ToDocumentServiceRequest();

            // Hedging-Detection API: record the dispatched region + reason on the trace's
            // HedgingDetectionState. This is the actual dispatch point for the operation
            // (after ClientRetryPolicy.OnBeforeSendRequest has resolved the routing endpoint,
            // before the wire send is invoked). Hedge arms set the reason to Hedging on
            // Properties prior to entering this handler; retries set the reason to
            // OperationRetry/RegionFailover via ClientRetryPolicy.OnBeforeSendRequest; all
            // other first attempts default to Initial.
            TransportHandler.AppendDispatchedRegion(request, serviceRequest, this.client.DocumentClient.GlobalEndpointManager);

            ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum = new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow, request.Trace);
            serviceRequest.RequestContext.ClientRequestStatistics = clientSideRequestStatisticsTraceDatum;

            //TODO: extract auth into a separate handler
            string authorization = await ((ICosmosAuthorizationTokenProvider)this.client.DocumentClient).GetUserAuthorizationTokenAsync(
                serviceRequest.ResourceAddress,
                PathsHelper.GetResourcePath(request.ResourceType),
                request.Method.ToString(),
                serviceRequest.Headers,
                AuthorizationTokenType.PrimaryMasterKey,
                request.Trace);

            serviceRequest.Headers[HttpConstants.HttpHeaders.Authorization] = authorization;

            // GetStoreProxy now throws ObjectDisposedException if client is disposed.
            // The null check below is a safety net for any unexpected scenarios.
            IStoreModel storeProxy = this.client.DocumentClient.GetStoreProxy(serviceRequest);
            if (storeProxy == null)
            {
                // Retry once as a safety measure
                storeProxy = this.client.DocumentClient.GetStoreProxy(serviceRequest);

                if (storeProxy == null)
                {
                    throw new InvalidOperationException(
                        "StoreProxy is unexpectedly null. This may indicate a race condition during client initialization or disposal.");
                }
            }

            using (ITrace processMessageAsyncTrace = request.Trace.StartChild(
                            name: $"{storeProxy.GetType().FullName} Transport Request",
                            component: TraceComponent.Transport,
                            level: Tracing.TraceLevel.Info))
            {
                request.Trace = processMessageAsyncTrace;
                processMessageAsyncTrace.AddDatum("Client Side Request Stats", clientSideRequestStatisticsTraceDatum);

                DocumentServiceResponse response = null;
                try
                {
                    response = await storeProxy.ProcessMessageAsync(serviceRequest, cancellationToken);
                }
                catch (DocumentClientException dce)
                {
                    // Enrich diagnostics context in-case of auth failures 
                    if (dce.StatusCode == System.Net.HttpStatusCode.Unauthorized || dce.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        TimeSpan authProvideLifeSpan = this.client.DocumentClient.cosmosAuthorization.GetAge();
                        processMessageAsyncTrace.AddDatum("AuthProvider LifeSpan InSec", authProvideLifeSpan.TotalSeconds);
                    }

                    throw;
                }
                finally
                {
                    processMessageAsyncTrace.Summary.UpdateRegionContacted(clientSideRequestStatisticsTraceDatum);
                }
               
                ResponseMessage responseMessage = response.ToCosmosResponseMessage(
                    request,
                    serviceRequest.RequestContext.RequestChargeTracker);

                // Enrich diagnostics context in-case of auth failures 
                if (responseMessage?.StatusCode == System.Net.HttpStatusCode.Unauthorized || responseMessage?.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    TimeSpan authProvideLifeSpan = this.client.DocumentClient.cosmosAuthorization.GetAge();
                    processMessageAsyncTrace.AddDatum("AuthProvider LifeSpan InSec", authProvideLifeSpan.TotalSeconds);
                }

                return responseMessage;
            }
        }

        internal static ResponseMessage AggregateExceptionConverter(AggregateException aggregateException, RequestMessage request)
        {
            AggregateException innerExceptions = aggregateException.Flatten();
            DocumentClientException docClientException = (DocumentClientException)innerExceptions.InnerExceptions.FirstOrDefault(innerEx => innerEx is DocumentClientException);
            if (docClientException != null)
            {
                return docClientException.ToCosmosResponseMessage(request);
            }

            Exception exception = innerExceptions.InnerExceptions.FirstOrDefault(innerEx => innerEx is CosmosException);
            if (exception is CosmosException cosmosException)
            {
                return cosmosException.ToCosmosResponseMessage(request);
            }

            return null;
        }

        /// <summary>
        /// Appends a <see cref="RequestedRegion"/> entry to the operation's
        /// <see cref="HedgingDetectionState"/> at the dispatch point (after the routing
        /// endpoint is resolved, before the wire send is invoked).
        /// </summary>
        /// <remarks>
        /// <para>
        /// The reason is read from <see cref="DocumentServiceRequest.Properties"/> under the
        /// well-known key <see cref="HedgingDetectionState.DispatchReasonPropertyKey"/>, which
        /// upstream sites (ClientRetryPolicy, CrossRegionHedgingAvailabilityStrategy) set to
        /// signal why this particular dispatch is happening. Absence of the key implies a
        /// first attempt and defaults to <see cref="RequestedRegionReason.Initial"/>. The key
        /// is removed after consumption so that subsequent retries on the same request
        /// re-default unless a new reason is set.
        /// </para>
        /// <para>
        /// The region name is resolved from the routing endpoint via
        /// <see cref="GlobalEndpointManager.GetLocation(Uri)"/> (DocumentServiceRequest's own
        /// <c>RegionName</c> is not populated until later in the dispatch chain, by
        /// <c>GatewayStoreModel</c> / <c>AddressResolver</c>).
        /// </para>
        /// </remarks>
        internal static void AppendDispatchedRegion(
            RequestMessage requestMessage,
            DocumentServiceRequest serviceRequest,
            GlobalEndpointManager globalEndpointManager)
        {
            if (requestMessage == null || serviceRequest == null)
            {
                return;
            }

            HedgingDetectionState state = requestMessage.Trace?.Summary?.HedgingDetectionState;
            if (state == null)
            {
                return;
            }

            // Resolve reason; consume the property so retries don't carry a stale value.
            RequestedRegionReason reason = RequestedRegionReason.Initial;
            if (serviceRequest.Properties != null
                && serviceRequest.Properties.TryGetValue(HedgingDetectionState.DispatchReasonPropertyKey, out object reasonObj)
                && reasonObj is RequestedRegionReason resolvedReason)
            {
                reason = resolvedReason;
                serviceRequest.Properties.Remove(HedgingDetectionState.DispatchReasonPropertyKey);
            }

            // Resolve region name from the routing endpoint URI.
            string regionName = null;
            Uri endpoint = serviceRequest.RequestContext?.LocationEndpointToRoute;
            if (endpoint != null && globalEndpointManager != null)
            {
                regionName = globalEndpointManager.GetLocation(endpoint);
            }

            // Skip if we couldn't resolve a name; better empty than misleading "unknown".
            if (string.IsNullOrEmpty(regionName))
            {
                return;
            }

            state.AppendRequested(regionName, reason);
        }
    }
}
