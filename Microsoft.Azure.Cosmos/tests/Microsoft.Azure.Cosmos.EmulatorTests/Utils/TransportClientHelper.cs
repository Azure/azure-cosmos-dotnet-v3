//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq.Dynamic;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Newtonsoft.Json;

    internal static class TransportClientHelper
    {
        internal static Container GetContainerWithItemTransportException(
            string databaseId,
            string containerId,
            Guid activityId,
            string transportExceptionSourceDescription)
        {
            return GetContainerWithIntercepter(
                databaseId,
                containerId,
                (uri, resourceOperation, request) => TransportClientHelper.ThrowTransportExceptionOnItemOperation(
                    uri,
                    resourceOperation,
                    request,
                    activityId,
                    transportExceptionSourceDescription));
        }

        internal static Container GetContainerWithIntercepter(
            string databaseId,
            string containerId,
            Action<Uri, ResourceOperation, DocumentServiceRequest> interceptor,
            bool useGatewayMode = false,
            Func<Uri, ResourceOperation, DocumentServiceRequest, StoreResponse> interceptorWithStoreResult = null)
        {
            CosmosClient clientWithIntercepter = TestCommon.CreateCosmosClient(
               builder =>
               {
                   if (useGatewayMode)
                   {
                       builder.WithConnectionModeGateway();
                   }

                   builder.WithTransportClientHandlerFactory(transportClient => new TransportClientWrapper(
                       transportClient,
                       interceptor,
                       interceptorWithStoreResult));
               });

            return clientWithIntercepter.GetContainer(databaseId, containerId);
        }

        public static StoreResponse ReturnThrottledStoreResponseOnItemOperation(
                Uri physicalAddress,
                ResourceOperation resourceOperation,
                DocumentServiceRequest request,
                Guid activityId,
                string errorMessage)
        {
            if (request.ResourceType == ResourceType.Document)
            {
                StoreRequestNameValueCollection headers = new StoreRequestNameValueCollection();
                headers.Add(HttpConstants.HttpHeaders.ActivityId, activityId.ToString());
                headers.Add(WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.WriteForbidden).ToString(CultureInfo.InvariantCulture));
                headers.Add(HttpConstants.HttpHeaders.RetryAfterInMilliseconds, TimeSpan.FromMilliseconds(100).TotalMilliseconds.ToString(CultureInfo.InvariantCulture));
                headers.Add(HttpConstants.HttpHeaders.RequestCharge, ((double)9001).ToString(CultureInfo.InvariantCulture));

                StoreResponse storeResponse = new StoreResponse()
                {
                    Status = 429,
                    Headers = headers,
                    ResponseBody = new MemoryStream(Encoding.UTF8.GetBytes(errorMessage))
                };

                return storeResponse;
            }

            return null;
        }

        public static void ThrowTransportExceptionOnItemOperation(
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

        public static void ThrowForbiddendExceptionOnItemOperation(
                Uri physicalAddress,
                DocumentServiceRequest request,
                string activityId,
                string errorMessage)
        {
            if (request.ResourceType == ResourceType.Document)
            {
                StoreRequestNameValueCollection headers = new StoreRequestNameValueCollection();
                headers.Add(HttpConstants.HttpHeaders.ActivityId, activityId.ToString());
                headers.Add(WFConstants.BackendHeaders.SubStatus, ((int)SubStatusCodes.WriteForbidden).ToString(CultureInfo.InvariantCulture));
                headers.Add(HttpConstants.HttpHeaders.RequestCharge, ((double)9001).ToString(CultureInfo.InvariantCulture));

                ForbiddenException forbiddenException = new ForbiddenException(
                    errorMessage,
                    headers,
                    physicalAddress);

                throw forbiddenException;
            }
        }

        internal sealed class TransportClientWrapper : TransportClient
        {
            private readonly TransportClient baseClient;
            private readonly Action<Uri, ResourceOperation, DocumentServiceRequest> interceptor;
            private readonly Func<Uri, ResourceOperation, DocumentServiceRequest, StoreResponse> interceptorWithStoreResult;

            internal TransportClientWrapper(
                TransportClient client,
                Action<Uri, ResourceOperation, DocumentServiceRequest> interceptor,
                Func<Uri, ResourceOperation, DocumentServiceRequest, StoreResponse> interceptorWithStoreResult = null)
            {
                Debug.Assert(client != null);
                Debug.Assert(interceptor != null);

                this.baseClient = client;
                this.interceptor = interceptor;
                this.interceptorWithStoreResult = interceptorWithStoreResult;
            }

            internal TransportClientWrapper(
                TransportClient client,
                Func<Uri, ResourceOperation, DocumentServiceRequest, StoreResponse> interceptorWithStoreResult)
            {
                Debug.Assert(client != null);
                Debug.Assert(interceptorWithStoreResult != null);

                this.baseClient = client;
                this.interceptorWithStoreResult = interceptorWithStoreResult;
            }

            internal override async Task<StoreResponse> InvokeStoreAsync(
                Uri physicalAddress,
                ResourceOperation resourceOperation,
                DocumentServiceRequest request)
            {
                this.interceptor?.Invoke(physicalAddress, resourceOperation, request);

                if (this.interceptorWithStoreResult != null)
                {
                    StoreResponse storeResponse = this.interceptorWithStoreResult(physicalAddress, resourceOperation, request);

                    if (storeResponse != null)
                    {
                        return storeResponse;
                    }
                }

                return await this.baseClient.InvokeStoreAsync(physicalAddress, resourceOperation, request);
            }
        }
    }
}
