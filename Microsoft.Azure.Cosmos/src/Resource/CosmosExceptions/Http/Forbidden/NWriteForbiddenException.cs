// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is auto-generated code. Modify: HttpExceptionCodeGenerator.tt: 98

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Forbidden
{
    using System;

    internal sealed class NWriteForbiddenException : ForbiddenBaseException
    {
        public NWriteForbiddenException(CosmosDiagnosticsContext cosmosDiagnosticsContext)
            : this(cosmosDiagnosticsContext, message: null)
        {
        }

        public NWriteForbiddenException(CosmosDiagnosticsContext cosmosDiagnosticsContext, string message)
            : this(cosmosDiagnosticsContext, message, innerException: null)
        {
        }

        public NWriteForbiddenException(CosmosDiagnosticsContext cosmosDiagnosticsContext, string message, Exception innerException)
            : base(
                subStatusCode: (int)ForbiddenSubStatusCode.NWriteForbidden,
                cosmosDiagnosticsContext: cosmosDiagnosticsContext,
                message: message, 
                innerException: innerException)
        {
        }
    }
}
