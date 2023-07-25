//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Collector
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using Microsoft.Azure.Documents;

    internal class CacheTelemetryData
    {
        internal string cacheRefreshSource { get; set; }
        internal HashSet<(string regionName, Uri uri)> regionsContactedList { get; set; }
        internal TimeSpan? requestLatency { get; set; }
        internal HttpStatusCode statusCode { get; set; }
        internal string containerId { get; set; } = null;
        internal OperationType operationType { get; set; }
        internal ResourceType resourceType { get; set; }
        internal SubStatusCodes subStatusCode { get; set; }
        internal string databaseId { get; set; } = null;
        internal long responseSizeInBytes { get; set; } = 0;
        internal string consistencyLevel { get; set; } = null;
        internal string collectionLink { get; set; } = null;
    }
}
