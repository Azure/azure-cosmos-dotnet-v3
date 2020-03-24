// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions
{
    using System;
    using System.Net;

    internal sealed class UnknownCosmosHttpException : CosmosHttpException
    {
        public UnknownCosmosHttpException(HttpStatusCode statusCode, int subStatusCode, CosmosDiagnosticsContext cosmosDiagnosticsContext)
            : this(statusCode, subStatusCode, cosmosDiagnosticsContext, message: null)
        {
        }

        public UnknownCosmosHttpException(HttpStatusCode statusCode, int subStatusCode, CosmosDiagnosticsContext cosmosDiagnosticsContext, string message)
            : this(statusCode, subStatusCode, cosmosDiagnosticsContext, message, innerException: null)
        {
        }

        public UnknownCosmosHttpException(HttpStatusCode statusCode, int subStatusCode, CosmosDiagnosticsContext cosmosDiagnosticsContext, string message, Exception innerException)
            : base(statusCode: statusCode, subStatusCode: subStatusCode, cosmosDiagnosticsContext: cosmosDiagnosticsContext, message: message, innerException: innerException)
        {
        }
    }
}
