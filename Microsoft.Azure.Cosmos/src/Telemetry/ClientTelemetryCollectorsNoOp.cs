//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal class ClientTelemetryCollectorsNoOp : IClientTelemetryCollectors
    {
        public void CollectCacheInfo(string cacheRefreshSource, HashSet<(string regionName, Uri uri)> regionsContactedList, TimeSpan? requestLatency, HttpStatusCode statusCode, string containerId, OperationType operationType, ResourceType resourceType, SubStatusCodes subStatusCode, string databaseId, long responseSizeInBytes = 0, string consistencyLevel = null)
        {
            // NoOps
            DefaultTrace.TraceVerbose("No Ops Client telemetry cache data collector");
        }

        public void CollectOperationInfo(CosmosDiagnostics cosmosDiagnostics, HttpStatusCode statusCode, long responseSizeInBytes, string containerId, string databaseId, OperationType operationType, ResourceType resourceType, string consistencyLevel, double requestCharge, SubStatusCodes subStatusCode, ITrace trace)
        {
            //NoOps
            DefaultTrace.TraceVerbose("No Ops Client telemetry operation data collector");
        }

        public void Dispose()
        {
            // NoOps
            DefaultTrace.TraceVerbose("No Ops Client telemetry Collector dispose");
        }
    }
}
