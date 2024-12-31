// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

    internal interface IActivityAttributePopulator
    {
        public void PopulateAttributes(DiagnosticScope scope,
            string operationName,
            string databaseName,
            string containerName,
            Uri accountName,
            string userAgent,
            string machineId,
            string clientId,
            string connectionMode);

        public void PopulateAttributes(DiagnosticScope scope, Exception exception);

        public void PopulateAttributes(DiagnosticScope scope, 
            QueryTextMode? queryTextMode, 
            string operationType, 
            OpenTelemetryAttributes response);

        public KeyValuePair<string, object>[] PopulateOperationMeterDimensions(string operationName, 
            string containerName, 
            string databaseName, 
            Uri accountName,
            OpenTelemetryAttributes attributes, 
            Exception ex,
            OperationMetricsOptions optionFromRequest);

        public KeyValuePair<string, object>[] PopulateNetworkMeterDimensions(string operationName,
            Uri accountName,
            string containerName,
            string databaseName,
            OpenTelemetryAttributes attributes,
            Exception ex,
            NetworkMetricsOptions optionFromRequest,
            ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics tcpStats = null,
            ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics? httpStats = null);

        public KeyValuePair<string, object>[] PopulateInstanceCountDimensions(Uri accountName);
    }
}
