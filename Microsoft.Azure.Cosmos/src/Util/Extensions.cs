//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal static class Extensions
    {
        private static readonly char[] NewLineCharacters = new[] { '\r', '\n' };

        internal static bool IsSuccess(this HttpStatusCode httpStatusCode)
        {
            return ((int)httpStatusCode >= 200) && ((int)httpStatusCode <= 299);
        }

        internal static ResponseMessage ToCosmosResponseMessage(this DocumentServiceResponse documentServiceResponse, RequestMessage requestMessage)
        {
            Debug.Assert(requestMessage != null, nameof(requestMessage));
            Headers headers = documentServiceResponse.Headers.ToCosmosHeaders();

            // Only record point operation stats if ClientSideRequestStats did not record the response.
            CosmosClientSideRequestStatistics clientSideRequestStatistics = documentServiceResponse.RequestStats as CosmosClientSideRequestStatistics;
            if (clientSideRequestStatistics == null ||
                (clientSideRequestStatistics.ContactedReplicas.Count == 0 && clientSideRequestStatistics.FailedReplicas.Count == 0))
            {
                requestMessage.DiagnosticsContext.AddDiagnosticsInternal(new PointOperationStatistics(
                    activityId: headers.ActivityId,
                    responseTimeUtc: DateTime.UtcNow,
                    statusCode: documentServiceResponse.StatusCode,
                    subStatusCode: documentServiceResponse.SubStatusCode,
                    requestCharge: headers.RequestCharge,
                    errorMessage: null,
                    method: requestMessage?.Method,
                    requestUri: requestMessage?.RequestUri,
                    requestSessionToken: requestMessage?.Headers?.Session,
                    responseSessionToken: headers.Session));
            }

            // If it's considered a failure create the corresponding CosmosException
            if (!documentServiceResponse.StatusCode.IsSuccess())
            {
                CosmosException cosmosException = CosmosExceptionFactory.Create(
                    documentServiceResponse,
                    headers,
                    requestMessage);

                return cosmosException.ToCosmosResponseMessage(requestMessage);
            }

            ResponseMessage responseMessage = new ResponseMessage(
                statusCode: documentServiceResponse.StatusCode,
                requestMessage: requestMessage,
                headers: headers,
                cosmosException: null,
                diagnostics: requestMessage.DiagnosticsContext)
            {
                Content = documentServiceResponse.ResponseBody
            };

            return responseMessage;
        }

        internal static ResponseMessage ToCosmosResponseMessage(this DocumentClientException documentClientException, RequestMessage requestMessage)
        {
            CosmosDiagnosticsContext diagnosticsContext = requestMessage?.DiagnosticsContext;
            if (requestMessage != null)
            {
                diagnosticsContext = requestMessage.DiagnosticsContext;

                if (diagnosticsContext == null)
                {
                    throw new ArgumentNullException("Request message should contain a DiagnosticsContext");
                }
            }
            else
            {
                diagnosticsContext = new CosmosDiagnosticsContextCore();
            }

            CosmosException cosmosException = CosmosExceptionFactory.Create(
                documentClientException,
                diagnosticsContext);

            PointOperationStatistics pointOperationStatistics = new PointOperationStatistics(
                activityId: cosmosException.Headers.ActivityId,
                statusCode: cosmosException.StatusCode,
                subStatusCode: (int)SubStatusCodes.Unknown,
                responseTimeUtc: DateTime.UtcNow,
                requestCharge: cosmosException.Headers.RequestCharge,
                errorMessage: cosmosException.Message,
                method: requestMessage?.Method,
                requestUri: requestMessage?.RequestUri,
                requestSessionToken: requestMessage?.Headers?.Session,
                responseSessionToken: cosmosException.Headers.Session);

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

        internal static Headers ToCosmosHeaders(this StoreResponse storeResponse)
        {
            Headers headers = new Headers();
            for (int i = 0; i < storeResponse.ResponseHeaderNames.Length; i++)
            {
                headers.Add(storeResponse.ResponseHeaderNames[i], storeResponse.ResponseHeaderValues[i]);
            }

            return headers;
        }

        internal static Headers ToCosmosHeaders(this INameValueCollection nameValueCollection)
        {
            Headers headers = new Headers();
            foreach (string key in nameValueCollection)
            {
                headers.Add(key, nameValueCollection[key]);
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
