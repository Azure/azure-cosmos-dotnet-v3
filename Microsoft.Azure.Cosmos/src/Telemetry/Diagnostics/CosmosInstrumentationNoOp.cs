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

        public void Record(Uri accountName, string userAgent, ConnectionMode connectionMode)
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

        void ICosmosInstrumentation.RecordWithException(double? requestCharge, string operationType, HttpStatusCode? statusCode, string databaseId, string containerId, Exception exception, string queryText, string subStatusCode, string pageSize)
        {
            // NoOp
        }

        void ICosmosInstrumentation.Record(double? requestCharge, string operationType, HttpStatusCode? statusCode, string databaseId, string containerId, string queryText, string subStatusCode, string pageSize, long? requestSize, long? responseSize)
        {
            // NoOp
        }
    }
}
