// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions
{
    using System;
    using System.Diagnostics;
    using System.Net;

    internal abstract class CosmosHttpException : CosmosException
    {
        protected CosmosHttpException(HttpStatusCode statusCode)
            : this(statusCode, message: null, innerException: null)
        {
        }

        protected CosmosHttpException(HttpStatusCode statusCode, string message)
            : this(statusCode, message: message, innerException: null)
        {
        }

        protected CosmosHttpException(HttpStatusCode statusCode, string message, Exception innerException)
            : base(statusCode: statusCode, message: message, inner: innerException)
        {
        }

        internal CosmosHttpException(
            HttpStatusCode statusCodes,
            int subStatusCode,
            string message,
            StackTrace stackTrace,
            string activityId,
            double requestCharge,
            TimeSpan? retryAfter,
            Headers headers,
            CosmosDiagnosticsContext diagnosticsContext,
            Exception innerException)
            : base(statusCodes,
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
