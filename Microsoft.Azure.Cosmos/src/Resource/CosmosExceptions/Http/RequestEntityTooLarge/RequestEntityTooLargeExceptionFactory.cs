// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is auto-generated code. Modify: HttpExceptionCodeGenerator.tt: 198

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.RequestEntityTooLarge
{
    using System;

    internal static class RequestEntityTooLargeExceptionFactory
    {
        public static RequestEntityTooLargeBaseException Create(
            int? subStatusCode = null,
            CosmosDiagnosticsContext cosmosDiagnosticsContext = null,
            string message = null,
            Exception innerException = null)
        {
            cosmosDiagnosticsContext = cosmosDiagnosticsContext ?? new CosmosDiagnosticsContextCore();
            if (!subStatusCode.HasValue)
            {
                return new RequestEntityTooLargeException(cosmosDiagnosticsContext, message, innerException);
            }

            switch (subStatusCode.Value)
            {
                default:
                    return new UnknownRequestEntityTooLargeException(subStatusCode.Value, cosmosDiagnosticsContext, message, innerException);
            }
        }
    }
}
