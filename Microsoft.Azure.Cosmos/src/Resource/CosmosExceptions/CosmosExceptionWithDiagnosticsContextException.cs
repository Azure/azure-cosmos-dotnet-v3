// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions
{
    using System;

    internal sealed class CosmosExceptionWithDiagnosticsContextException : CosmosException
    {
        public CosmosExceptionWithDiagnosticsContextException(
            Exception exception,
            CosmosDiagnosticsContext cosmosDiagnosticsContext)
#pragma warning disable CS0618 // Type or member is obsolete
            : base(System.Net.HttpStatusCode.OK,
                  message: "Encounted exception with diagnostics.",
                  subStatusCode: 0,
                  stackTrace: null,
                  activityId: null,
                  requestCharge: 0,
                  retryAfter: null,
                  headers: null,
                  diagnosticsContext: cosmosDiagnosticsContext,
                  error: null,
                  innerException: exception)
#pragma warning restore CS0618 // Type or member is obsolete
        {
        }
    }
}
