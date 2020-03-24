// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is auto-generated code. Modify: HttpExceptionCodeGenerator.tt: 163

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.BadRequest
{
    using System;

    internal sealed class UnknownBadRequestException : BadRequestBaseException
    {
        public UnknownBadRequestException(int subStatusCode, CosmosDiagnosticsContext cosmosDiagnosticsContext)
            : this(subStatusCode, cosmosDiagnosticsContext, message: null)
        {
        }

        public UnknownBadRequestException(int subStatusCode, CosmosDiagnosticsContext cosmosDiagnosticsContext, string message)
            : this(subStatusCode, cosmosDiagnosticsContext, message, innerException: null)
        {
        }

        public UnknownBadRequestException(int subStatusCode, CosmosDiagnosticsContext cosmosDiagnosticsContext, string message, Exception innerException)
            : base(subStatusCode: subStatusCode, cosmosDiagnosticsContext: cosmosDiagnosticsContext, message: message, innerException: innerException)
        {
        }
    }
}
