// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions
{
    using System;
    using System.Diagnostics;
    using System.Net;

    internal sealed class CosmosRequestTimeoutException : CosmosHttpException
    {
        public CosmosRequestTimeoutException()
            : base(statusCode: HttpStatusCode.RequestTimeout, message: null)
        {
        }

        public CosmosRequestTimeoutException(string message)
            : base(statusCode: HttpStatusCode.RequestTimeout, message: message)
        {
        }

        public CosmosRequestTimeoutException(string message, Exception innerException)
            : base(statusCode: HttpStatusCode.RequestTimeout, message: message, innerException: innerException)
        {
        }

        internal CosmosRequestTimeoutException(
            int subStatusCode,
            string message,
            StackTrace stackTrace,
            string activityId,
            double requestCharge,
            TimeSpan? retryAfter,
            Headers headers,
            CosmosDiagnosticsContext diagnosticsContext,
            Exception innerException)
            : base(HttpStatusCode.RequestTimeout,
             subStatusCode,
             message,
             stackTrace,
             activityId,
             requestCharge,
             retryAfter,
             headers,
             diagnosticsContext,
             innerException)
        {
        }
    }
}
