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
    using Microsoft.Azure.Documents;

    internal static class Extensions
    {
        private static readonly char[] NewLineCharacters = new[] { '\r', '\n' };

        internal static ResponseMessage ToCosmosResponseMessage(this DocumentServiceResponse response, RequestMessage requestMessage)
        {
            Debug.Assert(requestMessage != null, nameof(requestMessage));

            ResponseMessage cosmosResponse = new ResponseMessage(response.StatusCode, requestMessage);
            if (response.ResponseBody != null)
            {
                cosmosResponse.Content = response.ResponseBody;
            }

            if (response.Headers != null)
            {
                foreach (string key in response.Headers)
                {
                    cosmosResponse.Headers.Add(key, response.Headers[key]);
                }
            }

            if (response.RequestStats != null)
            {
                CosmosClientSideRequestStatistics cosmosClientSideRequestStatistics = response.RequestStats as CosmosClientSideRequestStatistics;
                if (cosmosClientSideRequestStatistics != null)
                {
                    cosmosResponse.Diagnostics = new PointOperationStatistics(cosmosClientSideRequestStatistics);
                }
            }

            return cosmosResponse;
        }

        internal static ResponseMessage ToCosmosResponseMessage(this DocumentClientException dce, RequestMessage request)
        {
            // if StatusCode is null it is a client business logic error and it never hit the backend, so throw
            if (dce.StatusCode == null)
            {
                throw dce;
            }

            // if there is a status code then it came from the backend, return error as http error instead of throwing the exception
            ResponseMessage cosmosResponse = new ResponseMessage(dce.StatusCode ?? HttpStatusCode.InternalServerError, request);
            string reasonPhraseString = string.Empty;
            if (!string.IsNullOrEmpty(dce.Message))
            {
                if (dce.Message.IndexOfAny(Extensions.NewLineCharacters) >= 0)
                {
                    StringBuilder sb = new StringBuilder(dce.Message);
                    sb = sb.Replace("\r", string.Empty);
                    sb = sb.Replace("\n", string.Empty);
                    reasonPhraseString = sb.ToString();
                }
                else
                {
                    reasonPhraseString = dce.Message;
                }
            }

            cosmosResponse.ErrorMessage = reasonPhraseString;
            cosmosResponse.Error = dce.Error;

            if (dce.Headers != null)
            {
                foreach (string header in dce.Headers.AllKeys())
                {
                    cosmosResponse.Headers.Add(header, dce.Headers[header]);
                }
            }

            if (request != null)
            {
                request.Properties.Remove(nameof(DocumentClientException));
                request.Properties.Add(nameof(DocumentClientException), dce);
            }

            return cosmosResponse;
        }

        internal static ResponseMessage ToCosmosResponseMessage(this StoreResponse response, RequestMessage request)
        {
            // Is status code conversion lossy? 
            ResponseMessage httpResponse = new ResponseMessage((HttpStatusCode)response.Status, request);
            if (response.ResponseBody != null)
            {
                httpResponse.Content = response.ResponseBody;
            }

            for (int i = 0; i < response.ResponseHeaderNames.Length; i++)
            {
                httpResponse.Headers.Add(response.ResponseHeaderNames[i], response.ResponseHeaderValues[i]);
            }

            return httpResponse;
        }

        internal static void TraceException(Exception e)
        {
            AggregateException aggregateException = e as AggregateException;
            if (aggregateException != null)
            {
                foreach (Exception exception in aggregateException.InnerExceptions)
                {
                    Extensions.TraceExceptionInternal(exception);
                }
            }
            else
            {
                Extensions.TraceExceptionInternal(e);
            }
        }

        public static async Task<IDisposable> UsingWaitAsync(
            this SemaphoreSlim semaphoreSlim,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            await semaphoreSlim.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new UsableSemaphoreWrapper(semaphoreSlim);
        }

        private static void TraceExceptionInternal(Exception e)
        {
            while (e != null)
            {
                Uri requestUri = null;

                SocketException socketException = e as SocketException;
                if (socketException != null)
                {
                    DefaultTrace.TraceWarning(
                        "Exception {0}: RequesteUri: {1}, SocketErrorCode: {2}, {3}, {4}",
                        e.GetType(),
                        requestUri,
                        socketException.SocketErrorCode,
                        e.Message,
                        e.StackTrace);
                }
                else
                {
                    DefaultTrace.TraceWarning(
                        "Exception {0}: RequestUri: {1}, {2}, {3}",
                        e.GetType(),
                        requestUri,
                        e.Message,
                        e.StackTrace);
                }

                e = e.InnerException;
            }
        }
    }
}
