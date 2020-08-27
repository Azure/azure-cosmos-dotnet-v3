//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Net.Sockets;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal static class DefaultTraceEx
    {
        public static void TraceException(Exception e)
        {
            AggregateException aggregateException = e as AggregateException;
            if (aggregateException != null)
            {
                foreach (Exception exception in aggregateException.InnerExceptions)
                {
                    DefaultTraceEx.TraceExceptionInternal(exception);
                }
            }
            else
            {
                DefaultTraceEx.TraceExceptionInternal(e);
            }
        }

        private static void TraceExceptionInternal(Exception e)
        {
            while (e != null)
            {
                Uri requestUri = null;
                DocumentClientException docClientException = e as DocumentClientException;
                if (docClientException != null)
                {
                    requestUri = docClientException.RequestUri;
                }

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
