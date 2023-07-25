//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Collector
{
    using System.Net;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal class OperationTelemetryData
    {
        internal CosmosDiagnostics cosmosDiagnostics { get; set; }
        internal HttpStatusCode statusCode { get; set; }
        internal long responseSizeInBytes { get; set; }
        internal string containerId { get; set; }
        internal string databaseId { get; set; }
        internal OperationType operationType { get; set; }
        internal ResourceType resourceType { get; set; }
        internal string consistencyLevel { get; set; }
        internal double requestCharge { get; set; }
        internal SubStatusCodes subStatusCode { get; set; }
        internal ITrace trace { get; set; }
    }
}
