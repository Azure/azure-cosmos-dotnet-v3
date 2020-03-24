// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is auto-generated code. Modify: HttpExceptionCodeGenerator.tt: 98

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Conflict
{
    using System;

    internal sealed class DatabaseNameAlreadyExistsException : ConflictBaseException
    {
        public DatabaseNameAlreadyExistsException(CosmosDiagnosticsContext cosmosDiagnosticsContext)
            : this(cosmosDiagnosticsContext, message: null)
        {
        }

        public DatabaseNameAlreadyExistsException(CosmosDiagnosticsContext cosmosDiagnosticsContext, string message)
            : this(cosmosDiagnosticsContext, message, innerException: null)
        {
        }

        public DatabaseNameAlreadyExistsException(CosmosDiagnosticsContext cosmosDiagnosticsContext, string message, Exception innerException)
            : base(
                subStatusCode: (int)ConflictSubStatusCode.DatabaseNameAlreadyExists,
                cosmosDiagnosticsContext: cosmosDiagnosticsContext,
                message: message, 
                innerException: innerException)
        {
        }
    }
}
