// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions
{
    using System;
    using System.Diagnostics;
    using System.Net;

    internal sealed class CosmosBadRequestException : CosmosHttpException
    {
        internal CosmosBadRequestException(
            string message,
            int subStatusCode = default,
            string stackTrace = default,
            string activityId = default,
            double requestCharge = default,
            TimeSpan? retryAfter = default,
            Headers headers = default,
            CosmosDiagnosticsContext diagnosticsContext = default,
            Exception innerException = default)
            : base(
                HttpStatusCode.BadRequest,
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
