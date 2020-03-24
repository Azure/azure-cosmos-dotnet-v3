// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is auto-generated code. Modify: HttpExceptionCodeGenerator.tt: 198

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.RequestTimeout
{
    using System;

    internal static class RequestTimeoutExceptionFactory
    {
        public static RequestTimeoutBaseException Create(
            int? subStatusCode = null,
            CosmosDiagnosticsContext cosmosDiagnosticsContext = null,
            string message = null,
            Exception innerException = null)
        {
            cosmosDiagnosticsContext = cosmosDiagnosticsContext ?? new CosmosDiagnosticsContextCore();
            if (!subStatusCode.HasValue)
            {
                return new RequestTimeoutException(cosmosDiagnosticsContext, message, innerException);
            }

            switch (subStatusCode.Value)
            {
                default:
                    return new UnknownRequestTimeoutException(subStatusCode.Value, cosmosDiagnosticsContext, message, innerException);
            }
        }
    }
}
