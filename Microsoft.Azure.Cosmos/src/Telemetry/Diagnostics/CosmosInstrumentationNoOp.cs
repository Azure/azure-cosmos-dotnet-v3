// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using System.Net;
    using Documents;

    internal class CosmosInstrumentationNoOp : ICosmosInstrumentation
    {
        public void MarkFailed(Exception ex)
        {
            // NoOp
        }

        public void AddAttributesToScope()
        {
            // NoOp
        }

        public void Record(double requestCharge, OperationType operationType, HttpStatusCode statusCode, string databaseId = null,
            string containerId = null, string queryText = null)
        {
            // NoOp
        }

        public void Record(Uri accountName, string userAgent, ConnectionMode connectionMode)
        {
            // NoOp
        }

        public void Record(CosmosDiagnostics diagnostics)
        {
            // NoOp
        }

        public void Dispose()
        {
            // NoOp
        }
    }
}
