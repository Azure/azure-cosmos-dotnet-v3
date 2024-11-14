// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using global::Azure.Core;

    internal interface IActivityAttributePopulator
    {
        public void PopulateAttributes(DiagnosticScope scope,
            string operationName,
            string databaseName,
            string containerName,
            Uri accountName,
            string userAgent,
            string machineId,
            string clientId,
            string connectionMode);

        public void PopulateAttributes(DiagnosticScope scope, Exception exception);

        public void PopulateAttributes(DiagnosticScope scope, 
            QueryTextMode? queryTextMode, 
            string operationType, 
            OpenTelemetryAttributes response);
     }
}
