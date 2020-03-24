// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is auto-generated code. Modify: HttpExceptionCodeGenerator.tt: 98

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Conflict
{
    using System;

    internal sealed class ConfigurationNameAlreadyExistsException : ConflictBaseException
    {
        public ConfigurationNameAlreadyExistsException(CosmosDiagnosticsContext cosmosDiagnosticsContext)
            : this(cosmosDiagnosticsContext, message: null)
        {
        }

        public ConfigurationNameAlreadyExistsException(CosmosDiagnosticsContext cosmosDiagnosticsContext, string message)
            : this(cosmosDiagnosticsContext, message, innerException: null)
        {
        }

        public ConfigurationNameAlreadyExistsException(CosmosDiagnosticsContext cosmosDiagnosticsContext, string message, Exception innerException)
            : base(
                subStatusCode: (int)ConflictSubStatusCode.ConfigurationNameAlreadyExists,
                cosmosDiagnosticsContext: cosmosDiagnosticsContext,
                message: message, 
                innerException: innerException)
        {
        }
    }
}
