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
        internal HttpStatusCode StatusCode { get; set; }
        internal SubStatusCodes SubStatusCode { get; set; }
        internal OperationType OperationType { get; set; }
        internal ResourceType ResourceType { get; set; }
        internal string ContainerId { get; set; }
        internal string DatabaseId { get; set; }
        internal long ResponseSizeInBytes { get; set; } = 0;
        internal string ConsistencyLevel { get; set; } = null;
        internal IReadOnlyCollection<(string regionName, Uri uri)> RegionsContactedList { get; set; }
        internal TimeSpan? RequestLatency { get; set; }

        internal double RequestCharge { get; set; } // Required only for operation level telemetry
        internal string CollectionLink { get; set; } = null; // Required only for collection cache telemetry
        internal ITrace Trace { get; set; } // Required to fetch network level telemetry out of the trace object
        internal ITrace RequestTrace { get; set; }
    }
}