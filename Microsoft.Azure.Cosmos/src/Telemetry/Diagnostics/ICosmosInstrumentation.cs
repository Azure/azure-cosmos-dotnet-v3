// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using System.Net;
    using Documents;

    /// <summary>
    /// Cosmos Instrumentation Interface
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif 
        interface ICosmosInstrumentation : IDisposable
        {
            /// <summary>
            /// Mark Failed
            /// </summary>
            /// <param name="ex"></param>
            public void MarkFailed(Exception ex);

            /// <summary>
            /// Add attributes to scope
            /// </summary>
            public void AddAttributesToScope();

            /// <summary>
            /// Record values
            /// </summary>
            /// <param name="requestCharge"></param>
            /// <param name="operationType"></param>
            /// <param name="statusCode"></param>
            /// <param name="databaseId"></param>
            /// <param name="containerId"></param>
            /// <param name="queryText"></param>
            public void Record(double requestCharge,
                string operationType,
                HttpStatusCode statusCode, 
                string databaseId = null, 
                string containerId = null,
                string queryText = null);

            /// <summary>
            /// Record Values
            /// </summary>
            /// <param name="accountName"></param>
            /// <param name="userAgent"></param>
            /// <param name="connectionMode"></param>
            public void Record(Uri accountName, string userAgent, ConnectionMode connectionMode);

            /// <summary>
            /// Record Values
            /// </summary>
            /// <param name="diagnostics"></param>
            public void Record(CosmosDiagnostics diagnostics);
        }
}
