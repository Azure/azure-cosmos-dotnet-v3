//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Documents;

    internal static class Extensions
    {
        private static readonly char[] NewLineCharacters = new[] { '\r', '\n' };

        internal static ResponseMessage ToCosmosResponseMessage(this DocumentServiceResponse documentServiceResponse, RequestMessage requestMessage)
        {
            Debug.Assert(requestMessage != null, nameof(requestMessage));

            ResponseMessage responseMessage = new ResponseMessage(documentServiceResponse.StatusCode, requestMessage);
            if (documentServiceResponse.ResponseBody != null)
            {
                responseMessage.Content = documentServiceResponse.ResponseBody;
            }

            if (documentServiceResponse.Headers != null)
            {
                foreach (string key in documentServiceResponse.Headers)
                {
                    responseMessage.Headers.Add(key, documentServiceResponse.Headers[key]);
                }
            }

            CosmosClientSideRequestStatistics cosmosClientSideRequestStatistics = documentServiceResponse.RequestStats as CosmosClientSideRequestStatistics;
            PointOperationStatistics pointOperationStatistics = new PointOperationStatistics(
                activityId: responseMessage.Headers.ActivityId,
                statusCode: documentServiceResponse.StatusCode,
                subStatusCode: documentServiceResponse.SubStatusCode,
                requestCharge: responseMessage.Headers.RequestCharge,
                errorMessage: responseMessage.ErrorMessage,
                method: requestMessage?.Method,
                requestUri: requestMessage?.RequestUri,
                requestSessionToken: requestMessage?.Headers?.Session,
                responseSessionToken: responseMessage.Headers.Session,
                clientSideRequestStatistics: cosmosClientSideRequestStatistics);

            requestMessage.DiagnosticsContext.AddDiagnosticsInternal(pointOperationStatistics);
            return responseMessage;
        }

        internal static ResponseMessage ToCosmosResponseMessage(this DocumentClientException documentClientException, RequestMessage requestMessage)
        {
            CosmosDiagnosticsContext diagnosticsContext = requestMessage?.DiagnosticsContext;
            if (diagnosticsContext == null)
            {
                diagnosticsContext = CosmosDiagnosticsContextCore.Create();
            }

            CosmosException cosmosException = CosmosExceptionFactory.Create(
                documentClientException,
                diagnosticsContext);

            PointOperationStatistics pointOperationStatistics = new PointOperationStatistics(
                activityId: cosmosException.Headers.ActivityId,
                statusCode: cosmosException.StatusCode,
                subStatusCode: (int)SubStatusCodes.Unknown,
                requestCharge: cosmosException.Headers.RequestCharge,
                errorMessage: cosmosException.Message,
                method: requestMessage?.Method,
                requestUri: requestMessage?.RequestUri,
                requestSessionToken: requestMessage?.Headers?.Session,
                responseSessionToken: cosmosException.Headers.Session,
                clientSideRequestStatistics: documentClientException.RequestStatistics as CosmosClientSideRequestStatistics);

            diagnosticsContext.AddDiagnosticsInternal(pointOperationStatistics);

            // if StatusCode is null it is a client business logic error and it never hit the backend, so throw
            if (documentClientException.StatusCode == null)
            {
                throw cosmosException;
            }

            // if there is a status code then it came from the backend, return error as http error instead of throwing the exception
            ResponseMessage responseMessage = cosmosException.ToCosmosResponseMessage(requestMessage);
            
            if (requestMessage != null)
            {
                requestMessage.Properties.Remove(nameof(DocumentClientException));
                requestMessage.Properties.Add(nameof(DocumentClientException), documentClientException);
            }

            return responseMessage;
        }

        internal static ResponseMessage ToCosmosResponseMessage(this StoreResponse storeResponse, RequestMessage requestMessage)
        {
            // If it's considered a failure create the corresponding CosmosException
            if (((int)storeResponse.StatusCode >= 200) && ((int)storeResponse.StatusCode <= 299))
            {
                CosmosException cosmosException = CosmosExceptionFactory.Create(
                    storeResponse,
                    requestMessage);

                return cosmosException.ToCosmosResponseMessage(requestMessage);
            }

            // Is status code conversion lossy? 
            ResponseMessage responseMessage = new ResponseMessage((HttpStatusCode)storeResponse.Status, requestMessage);
            if (storeResponse.ResponseBody != null)
            {
                responseMessage.Content = storeResponse.ResponseBody;
            }

            return responseMessage;
        }

        internal static Headers ToCosmosHeaders(this StoreResponse storeResponse)
        {
            Headers headers = new Headers();
            for (int i = 0; i < storeResponse.ResponseHeaderNames.Length; i++)
            {
                headers.Add(storeResponse.ResponseHeaderNames[i], storeResponse.ResponseHeaderValues[i]);
            }

            return headers;
        }

        internal static void TraceException(Exception exception)
        {
            AggregateException aggregateException = exception as AggregateException;
            if (aggregateException != null)
            {
                foreach (Exception tempException in aggregateException.InnerExceptions)
                {
                    Extensions.TraceExceptionInternal(tempException);
                }
            }
            else
            {
                Extensions.TraceExceptionInternal(exception);
            }
        }

        public static async Task<IDisposable> UsingWaitAsync(
            this SemaphoreSlim semaphoreSlim,
            CancellationToken cancellationToken)
        {
            await semaphoreSlim.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new UsableSemaphoreWrapper(semaphoreSlim);
        }

        private static void TraceExceptionInternal(Exception exception)
        {
            while (exception != null)
            {
                Uri requestUri = null;

                SocketException socketException = exception as SocketException;
                if (socketException != null)
                {
                    DefaultTrace.TraceWarning(
                        "Exception {0}: RequesteUri: {1}, SocketErrorCode: {2}, {3}, {4}",
                        exception.GetType(),
                        requestUri,
                        socketException.SocketErrorCode,
                        exception.Message,
                        exception.StackTrace);
                }
                else
                {
                    DefaultTrace.TraceWarning(
                        "Exception {0}: RequestUri: {1}, {2}, {3}",
                        exception.GetType(),
                        requestUri,
                        exception.Message,
                        exception.StackTrace);
                }

                exception = exception.InnerException;
            }
        }
    }
}
