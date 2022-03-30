// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using System.Net;
    using Documents;
    using Microsoft.Azure.Cosmos.Tracing;

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
            /// Record Values
            /// </summary>
            /// <param name="requestCharge"></param>
            /// <param name="operationType"></param>
            /// <param name="statusCode"></param>
            /// <param name="databaseId"></param>
            /// <param name="containerId"></param>
            /// <param name="subStatusCode"></param>
            /// <param name="itemCount"></param>
            /// <param name="requestSize"></param>
            /// <param name="responseSize"></param>
            /// <param name="accountName"></param>
            /// <param name="userAgent"></param>
            /// <param name="connectionMode"></param>
            /// <param name="exception"></param>
            public void Record(double? requestCharge = null,
                string operationType = null,
                HttpStatusCode? statusCode = null, 
                string databaseId = null, 
                string containerId = null,
                string subStatusCode = null,
                int? itemCount = null,
                long? requestSize = null,
                long? responseSize = null,
                Uri accountName = null, 
                string userAgent = null, 
                ConnectionMode? connectionMode = null,
                Exception exception = null);

            /// <summary>
            /// Record Values
            /// </summary>
            /// <param name="trace"></param>
            public void Record(ITrace trace);
        }
}
