// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Diagnostics
{
    using System;
    using System.Net;
    using Documents;
    using Microsoft.Azure.Cosmos.Tracing;

    internal class CosmosInstrumentationNoOp : ICosmosInstrumentation
    {
        public void MarkFailed(Exception ex)
        {
            // NoOp
        }

        public void Record(ITrace trace)
        {
            // NoOp
        }

        public void Dispose()
        {
            // NoOp
        }

        public void Record(double? requestCharge = null, string operationType = null, HttpStatusCode? statusCode = null, string databaseId = null, string containerId = null, string subStatusCode = null, int? itemCount = null, long? requestSize = null, long? responseSize = null, Uri accountName = null, string userAgent = null, ConnectionMode? connectionMode = null, Exception exception = null)
        {
            // NoOp
        }
    }
}
