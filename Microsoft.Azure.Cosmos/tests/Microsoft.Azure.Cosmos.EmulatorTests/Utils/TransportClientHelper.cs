//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.Azure.Documents;


    internal static class TransportClientHelper
    {
        internal static Container GetContainerWithItemTransportException(
            string databaseId,
            string containerId,
            Guid activityId,
            string transportExceptionSourceDescription)
        {
            CosmosClient clientWithIntercepter = TestCommon.CreateCosmosClient(
               builder =>
               {
                   builder.WithTransportClientHandlerFactory(transportClient => new TransportClientWrapper(
                       transportClient,
                       (uri, resourceOperation, request) => TransportClientHelper.ThrowTransportExceptionOnItemOperation(
                           uri,
                           resourceOperation,
                           request,
                           activityId,
                           transportExceptionSourceDescription)));
               });

            return clientWithIntercepter.GetContainer(databaseId, containerId);
        }

        private static void ThrowTransportExceptionOnItemOperation(
                Uri physicalAddress,
                ResourceOperation resourceOperation,
                DocumentServiceRequest request,
                Guid activityId,
                string transportExceptionSourceDescription)
        {
            if (request.ResourceType == ResourceType.Document)
            {
                TransportException transportException = new TransportException(
                    errorCode: TransportErrorCode.RequestTimeout,
                    innerException: null,
                    activityId: activityId,
                    requestUri: physicalAddress,
                    sourceDescription: transportExceptionSourceDescription,
                    userPayload: true,
                    payloadSent: false);

                throw Documents.Rntbd.TransportExceptions.GetRequestTimeoutException(physicalAddress, Guid.NewGuid(),
                    transportException);
            }
        }

        internal sealed class TransportClientWrapper : TransportClient
        {
            private readonly TransportClient baseClient;
            private readonly Action<Uri, ResourceOperation, DocumentServiceRequest> interceptor;

            internal TransportClientWrapper(
                TransportClient client,
                Action<Uri, ResourceOperation, DocumentServiceRequest> interceptor)
            {
                Debug.Assert(client != null);
                Debug.Assert(interceptor != null);

                this.baseClient = client;
                this.interceptor = interceptor;
            }

            internal override async Task<StoreResponse> InvokeStoreAsync(
                Uri physicalAddress,
                ResourceOperation resourceOperation,
                DocumentServiceRequest request)
            {
                this.interceptor(physicalAddress, resourceOperation, request);

                StoreResponse response = await this.baseClient.InvokeStoreAsync(physicalAddress, resourceOperation, request);
                return response;
            }
        }
    }
}
