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
        public CosmosRequestTimeoutException(string message)
            : base(statusCode: HttpStatusCode.RequestTimeout, message: message)
        {
        }

        internal CosmosRequestTimeoutException(
            string message,
            int subStatusCode = default,
            StackTrace stackTrace = default,
            string activityId = default,
            double requestCharge = default,
            TimeSpan? retryAfter = default,
            Headers headers = default,
            CosmosDiagnosticsContext diagnosticsContext = default,
            Exception innerException = default)
            : base(
                HttpStatusCode.RequestTimeout,
                message,
                subStatusCode,
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
