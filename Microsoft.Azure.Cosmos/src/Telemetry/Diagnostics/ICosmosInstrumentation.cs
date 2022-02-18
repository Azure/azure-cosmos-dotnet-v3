// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using System.Net;
    using Documents;

#if INTERNAL
    public
#else
    internal
#endif 
        interface ICosmosInstrumentation : IDisposable
        {
            public void MarkFailed(Exception ex);

            public void AddAttributesToScope();

            public void Record(double requestCharge,
                OperationType operationType,
                HttpStatusCode statusCode, 
                string databaseId = null, 
                string containerId = null,
                string queryText = null);

            public void Record(Uri accountName, string userAgent, ConnectionMode connectionMode);

            public void Record(CosmosDiagnostics diagnostics);
        }
}
