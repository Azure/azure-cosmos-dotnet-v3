//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal static class Extensions
    {
        private static readonly char[] NewLineCharacters = new[] { '\r', '\n' };

        internal static bool IsSuccess(this HttpStatusCode httpStatusCode)
        {
            return ((int)httpStatusCode >= 200) && ((int)httpStatusCode <= 299);
        }

        public static void Add(this INameValueCollection nameValueCollection, string headerName, IEnumerable<string> values)
        {
            if (headerName == null)
            {
                throw new ArgumentNullException(nameof(headerName));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            nameValueCollection.Add(headerName, string.Join(",", values));
        }

        public static T GetHeaderValue<T>(this INameValueCollection nameValueCollection, string key)
        {
            string value = nameValueCollection[key];

            if (string.IsNullOrEmpty(value))
            {
                return default;
            }

            if (typeof(T) == typeof(double))
            {
                return (T)(object)double.Parse(value, CultureInfo.InvariantCulture);
            }

            return (T)(object)value;
        }

        internal static ResponseMessage ToCosmosResponseMessage(
            this DocumentServiceResponse documentServiceResponse,
            RequestMessage requestMessage,
            RequestChargeTracker requestChargeTracker)
        {
            Debug.Assert(requestMessage != null, nameof(requestMessage));
            Headers headers = new Headers(documentServiceResponse.Headers);

            if (requestChargeTracker != null && headers.RequestCharge < requestChargeTracker.TotalRequestCharge)
            {
                headers.RequestCharge = requestChargeTracker.TotalRequestCharge;
                DefaultTrace.TraceWarning(
                    "Header RequestCharge {0} is less than the RequestChargeTracker: {1}; URI {2}, OperationType: {3}",
                    headers.RequestCharge,
                    requestChargeTracker.TotalRequestCharge,
                    requestMessage?.RequestUriString,
                    requestMessage?.OperationType);
            }

            // Only record point operation stats if ClientSideRequestStats did not record the response.
            if (!(documentServiceResponse.RequestStats != null) ||
                (documentServiceResponse.RequestStats.ContactedReplicas.Count == 0 && documentServiceResponse.RequestStats.FailedReplicas.Count == 0))
            {
                PointOperationStatisticsTraceDatum pointOperationStatistics = new PointOperationStatisticsTraceDatum(
                    activityId: headers.ActivityId,
                    responseTimeUtc: DateTime.UtcNow,
                    statusCode: documentServiceResponse.StatusCode,
                    subStatusCode: documentServiceResponse.SubStatusCode,
                    requestCharge: headers.RequestCharge,
                    errorMessage: null,
                    method: requestMessage?.Method,
                    requestUri: requestMessage?.RequestUriString,
                    requestSessionToken: requestMessage?.Headers?.Session,
                    responseSessionToken: headers.Session,
                    beLatencyInMs: headers.BackendRequestDurationMilliseconds);

                requestMessage.Trace.AddDatum(nameof(PointOperationStatisticsTraceDatum), pointOperationStatistics);
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
                trace: null)
            {
                Content = documentServiceResponse.ResponseBody
            };

            return responseMessage;
        }

        internal static ResponseMessage ToCosmosResponseMessage(this DocumentClientException documentClientException, RequestMessage requestMessage)
        {
            CosmosException cosmosException = CosmosExceptionFactory.Create(
                documentClientException,
                requestMessage?.Trace ?? NoOpTrace.Singleton);

            if (requestMessage?.Trace != null)
            {
                PointOperationStatisticsTraceDatum pointOperationStatistics = new PointOperationStatisticsTraceDatum(
                    activityId: cosmosException.Headers.ActivityId,
                    statusCode: cosmosException.StatusCode,
                    subStatusCode: cosmosException.Headers.SubStatusCode,
                    responseTimeUtc: DateTime.UtcNow,
                    requestCharge: cosmosException.Headers.RequestCharge,
                    errorMessage: documentClientException.ToString(),
                    method: requestMessage?.Method,
                    requestUri: requestMessage?.RequestUriString,
                    requestSessionToken: requestMessage?.Headers?.Session,
                    responseSessionToken: cosmosException.Headers.Session,
                    beLatencyInMs: cosmosException.Headers.BackendRequestDurationMilliseconds);

                requestMessage?.Trace.AddDatum("Point Operation Statistics", pointOperationStatistics);
            }

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

        internal static void TraceException(Exception exception)
        {
            if (exception is AggregateException aggregateException)
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
            ITrace trace,
            CancellationToken cancellationToken)
        {
            using (trace.StartChild("Using Wait"))
            {
                await semaphoreSlim.WaitAsync(cancellationToken).ConfigureAwait(false);
                return new UsableSemaphoreWrapper(semaphoreSlim);
            }
        }

        private static void TraceExceptionInternal(Exception exception)
        {
            while (exception != null)
            {
                Uri requestUri = null;

                if (exception is SocketException socketException)
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
