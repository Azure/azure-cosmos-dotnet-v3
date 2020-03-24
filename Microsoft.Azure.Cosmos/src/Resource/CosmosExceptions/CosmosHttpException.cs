// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions
{
    using System;
    using System.Net;

    internal abstract class CosmosHttpException : CosmosException
    {
        protected CosmosHttpException(HttpStatusCode statusCode, int subStatusCode, CosmosDiagnosticsContext cosmosDiagnosticsContext)
            : this(statusCode, subStatusCode, cosmosDiagnosticsContext, message: null)
        {
        }

        protected CosmosHttpException(HttpStatusCode statusCode, int subStatusCode, CosmosDiagnosticsContext cosmosDiagnosticsContext, string message)
            : this(statusCode, subStatusCode, cosmosDiagnosticsContext, message, innerException: null)
        {
        }

        protected CosmosHttpException(HttpStatusCode statusCode, int subStatusCode, CosmosDiagnosticsContext cosmosDiagnosticsContext, string message, Exception innerException)
#pragma warning disable CS0618 // Type or member is obsolete
            : base(
                  statusCode: statusCode,
                  message: message,
                  subStatusCode: subStatusCode,
                  stackTrace: null,
                  activityId: null,
                  requestCharge: 0,
                  retryAfter: null,
                  headers: null,
                  diagnosticsContext: cosmosDiagnosticsContext,
                  error: null,
                  innerException: innerException)
#pragma warning restore CS0618 // Type or member is obsolete
        {
        }
    }
}