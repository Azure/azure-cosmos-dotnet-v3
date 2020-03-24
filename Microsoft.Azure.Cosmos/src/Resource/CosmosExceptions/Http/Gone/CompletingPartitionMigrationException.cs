// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is auto-generated code. Modify: HttpExceptionCodeGenerator.tt: 98

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Gone
{
    using System;

    internal sealed class CompletingPartitionMigrationException : GoneBaseException
    {
        public CompletingPartitionMigrationException(CosmosDiagnosticsContext cosmosDiagnosticsContext)
            : this(cosmosDiagnosticsContext, message: null)
        {
        }

        public CompletingPartitionMigrationException(CosmosDiagnosticsContext cosmosDiagnosticsContext, string message)
            : this(cosmosDiagnosticsContext, message, innerException: null)
        {
        }

        public CompletingPartitionMigrationException(CosmosDiagnosticsContext cosmosDiagnosticsContext, string message, Exception innerException)
            : base(
                subStatusCode: (int)GoneSubStatusCode.CompletingPartitionMigration,
                cosmosDiagnosticsContext: cosmosDiagnosticsContext,
                message: message, 
                innerException: innerException)
        {
        }
    }
}
