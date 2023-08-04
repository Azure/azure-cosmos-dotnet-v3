//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Collector
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal class TelemetryInformation
    {
        internal HttpStatusCode statusCode { get; set; }
        internal SubStatusCodes subStatusCode { get; set; }
        internal OperationType operationType { get; set; }
        internal ResourceType resourceType { get; set; }
        internal string containerId { get; set; }
        internal string databaseId { get; set; }
        internal long responseSizeInBytes { get; set; } = 0;
        internal string consistencyLevel { get; set; } = null;
        internal IReadOnlyCollection<(string regionName, Uri uri)> regionsContactedList { get; set; }
        internal TimeSpan? requestLatency { get; set; }

        internal double requestCharge { get; set; } // Required only for operation level telemetry
        internal string collectionLink { get; set; } = null; // Required only for collection cache telemetry
        internal ITrace trace { get; set; } // Required to fetch network level telemetry out of the trace object
    }
}
