// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using System.Collections.Generic;
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
        /// Recording values
        /// </summary>
        /// <param name="databaseId"></param>
        /// <param name="operationType"></param>
        /// <param name="accountName"></param>
        /// <param name="clientId"></param>
        /// <param name="machineId"></param>
        /// <param name="containerId"></param>
        /// <param name="statusCode"></param>
        /// <param name="userAgent"></param>
        /// <param name="requestSize"></param>
        /// <param name="responseSize"></param>
        /// <param name="regionsContacted"></param>
        /// <param name="retryCount"></param>
        /// <param name="connectionMode"></param>
        /// <param name="itemCount"></param>
        /// <param name="requestCharge"></param>
        /// <param name="exception"></param>
        public void Record(
            string databaseId = null,
            string operationType = null,
            Uri accountName = null,
            string clientId = null,
            string machineId = null,
            string containerId = null,
            HttpStatusCode? statusCode = null,
            string userAgent = null,
            long? requestSize = null,
            long? responseSize = null,
            IList<string> regionsContacted = null,
            Int16? retryCount = null,
            ConnectionMode? connectionMode = null,
            int? itemCount = null,
            double? requestCharge = null,
            Exception exception = null)

            /// <summary>
            /// Record Values
            /// </summary>
            /// <param name="trace"></param>
        public void Record(ITrace trace);
        }
}
