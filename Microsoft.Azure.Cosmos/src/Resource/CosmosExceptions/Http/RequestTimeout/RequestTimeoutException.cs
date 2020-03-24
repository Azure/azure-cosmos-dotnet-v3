// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is auto-generated code. Modify: HttpExceptionCodeGenerator.tt: 133

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.RequestTimeout
{
    using System;

    internal sealed class RequestTimeoutException : RequestTimeoutBaseException
    {
        public RequestTimeoutException(CosmosDiagnosticsContext cosmosDiagnosticsContext)
            : this(cosmosDiagnosticsContext, message: null)
        {
        }

        public RequestTimeoutException(CosmosDiagnosticsContext cosmosDiagnosticsContext, string message)
            : this(cosmosDiagnosticsContext, message, innerException: null)
        {
        }

        public RequestTimeoutException(CosmosDiagnosticsContext cosmosDiagnosticsContext, string message, Exception innerException)
            : base(subStatusCode: 0, cosmosDiagnosticsContext: cosmosDiagnosticsContext, message: message, innerException: innerException)
        {
        }
    }
}
