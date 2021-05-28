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
            ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum = new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow);
            serviceRequest.RequestContext.ClientRequestStatistics = clientSideRequestStatisticsTraceDatum;

            //TODO: extrace auth into a separate handler
            string authorization = await ((ICosmosAuthorizationTokenProvider)this.client.DocumentClient).GetUserAuthorizationTokenAsync(
                serviceRequest.ResourceAddress,
                PathsHelper.GetResourcePath(request.ResourceType),
                request.Method.ToString(),
                serviceRequest.Headers,
                AuthorizationTokenType.PrimaryMasterKey,
                request.Trace);

            serviceRequest.Headers[HttpConstants.HttpHeaders.Authorization] = authorization;

            IStoreModel storeProxy = this.client.DocumentClient.GetStoreProxy(serviceRequest);
            using (ITrace processMessageAsyncTrace = request.Trace.StartChild(
                name: $"{storeProxy.GetType().FullName} Transport Request",
                TraceComponent.Transport,
                Tracing.TraceLevel.Info))
            {
                request.Trace = processMessageAsyncTrace;
                processMessageAsyncTrace.AddDatum("Client Side Request Stats", clientSideRequestStatisticsTraceDatum);

                DocumentServiceResponse response = request.OperationType == OperationType.Upsert
                        ? await this.ProcessUpsertAsync(storeProxy, serviceRequest, cancellationToken)
                        : await storeProxy.ProcessMessageAsync(serviceRequest, cancellationToken);

                return response.ToCosmosResponseMessage(
                    request,
                    serviceRequest.RequestContext.RequestChargeTracker);
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

        private async Task<DocumentServiceResponse> ProcessUpsertAsync(IStoreModel storeProxy, DocumentServiceRequest serviceRequest, CancellationToken cancellationToken)
        {
            DocumentServiceResponse response = await storeProxy.ProcessMessageAsync(serviceRequest, cancellationToken);
            this.client.DocumentClient.CaptureSessionToken(serviceRequest, response);
            return response;
        }
    }
}
