//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Handlers
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;

    //TODO: write unit test for this handler
    internal class TransportHandler : CosmosRequestHandler
    {
        private readonly CosmosClient client;

        public TransportHandler(CosmosClient client)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            this.client = client;
        }

        public override async Task<CosmosResponseMessage> SendAsync(
            CosmosRequestMessage request, 
            CancellationToken cancellationToken)
        {
            try
            {
                using (new ActivityScope(Guid.NewGuid()))
                {
                    DocumentServiceResponse response = await this.ProcessMessageAsync(request, cancellationToken);
                    return response.ToCosmosResponseMessage(request);
                }
            }
            //catch DocumentClientException and exceptions that inherit it. Other exception types happen before a backend request
            catch (DocumentClientException ex)
            {
                return ex.ToCosmosResponseMessage(request);
            }
            catch (AggregateException ex)
            {
                // TODO: because the SDK underneath this path uses ContinueWith or task.Result we need to catch AggregateExceptions here
                // in order to ensure that underlying DocumentClientExceptions get propagated up correctly. Once all ContinueWith and .Result 
                // is removed this catch can be safely removed.
                AggregateException innerExceptions = ex.Flatten();
                Exception docClientException = innerExceptions.InnerExceptions.FirstOrDefault(innerEx => innerEx is DocumentClientException);
                if (docClientException != null)
                {
                    return ((DocumentClientException)docClientException).ToCosmosResponseMessage(request);
                }

                throw;
            }
        }

        private Task<DocumentServiceResponse> ProcessMessageAsync(
            CosmosRequestMessage request, 
            CancellationToken cancellationToken)
        {
            DocumentServiceRequest serviceRequest = request.ToDocumentServiceRequest();

            //TODO: extrace auth into a separate handler
            string authorization = ((IAuthorizationTokenProvider)this.client.DocumentClient).GetUserAuthorizationToken(
                serviceRequest.ResourceAddress,
                PathsHelper.GetResourcePath(request.ResourceType),
                request.Method.ToString(), serviceRequest.Headers, AuthorizationTokenType.PrimaryMasterKey);
            serviceRequest.Headers[HttpConstants.HttpHeaders.Authorization] = authorization;
                
            IStoreModel storeProxy = this.client.DocumentClient.GetStoreProxy(serviceRequest);
            if (request.OperationType == OperationType.Upsert)
            {
                return this.ProcessUpsertAsync(storeProxy, serviceRequest, cancellationToken);
            }
            else
            {
                return storeProxy.ProcessMessageAsync(serviceRequest, cancellationToken);
            }
        }

        private async Task<DocumentServiceResponse> ProcessUpsertAsync(IStoreModel storeProxy, DocumentServiceRequest serviceRequest, CancellationToken cancellationToken)
        {
            DocumentServiceResponse response = await storeProxy.ProcessMessageAsync(serviceRequest, cancellationToken);
            this.client.DocumentClient.CaptureSessionToken(serviceRequest, response);
            return response;
        }
    }
}
