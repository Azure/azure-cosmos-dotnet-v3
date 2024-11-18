//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

    internal class DatabaseDupAttributeKeys : IActivityAttributePopulator
    {
        private readonly IActivityAttributePopulator appInsightPopulator;
        private readonly IActivityAttributePopulator otelPopulator;

        public DatabaseDupAttributeKeys() 
        { 
            this.otelPopulator = new OpenTelemetryAttributeKeys();
            this.appInsightPopulator = new AppInsightClassicAttributeKeys();
        }

        public void PopulateAttributes(DiagnosticScope scope, string operationName, string databaseName, string containerName, Uri accountName, string userAgent, string machineId, string clientId, string connectionMode)
        {
            this.appInsightPopulator.PopulateAttributes(scope, operationName, databaseName, containerName, accountName, userAgent, machineId, clientId, connectionMode);
            this.otelPopulator.PopulateAttributes(scope, operationName, databaseName, containerName, accountName, userAgent, machineId, clientId, connectionMode);
        }

        public void PopulateAttributes(DiagnosticScope scope, Exception exception)
        {
            this.appInsightPopulator.PopulateAttributes(scope, exception);
            this.otelPopulator.PopulateAttributes(scope, exception);
        }

        public void PopulateAttributes(DiagnosticScope scope, QueryTextMode? queryTextMode, string operationType, OpenTelemetryAttributes response)
        {
            this.appInsightPopulator.PopulateAttributes(scope, queryTextMode, operationType, response);
            this.otelPopulator.PopulateAttributes(scope, queryTextMode, operationType, response);
        }

        public KeyValuePair<string, object>[] PopulateNetworkMeterDimensions(string operationName, 
            Uri accountName, 
            string containerName, 
            string databaseName, 
            OpenTelemetryAttributes attributes, 
            CosmosException ex, 
            ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics tcpStats = null, 
            ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics? httpStats = null)
        {
            KeyValuePair<string, object>[] appInsightDimensions = this.appInsightPopulator
               .PopulateNetworkMeterDimensions(operationName, accountName, containerName, databaseName, attributes, ex, tcpStats, httpStats)
               .ToArray();
            KeyValuePair<string, object>[] otelDimensions = this.otelPopulator
                .PopulateNetworkMeterDimensions(operationName, accountName, containerName, databaseName, attributes, ex, tcpStats, httpStats)
                .ToArray();

            KeyValuePair<string, object>[] dimensions
                = new KeyValuePair<string, object>[appInsightDimensions.Length + otelDimensions.Length];
            dimensions
                .Concat(appInsightDimensions)
                .Concat(otelDimensions);
            return dimensions;
        }

        public KeyValuePair<string, object>[] PopulateOperationMeterDimensions(string operationName, 
            string containerName, 
            string databaseName, 
            Uri accountName, 
            OpenTelemetryAttributes attributes, 
            CosmosException ex)
        {
            KeyValuePair<string, object>[] appInsightDimensions = this.appInsightPopulator
                .PopulateOperationMeterDimensions(operationName, containerName, databaseName, accountName, attributes, ex)
                .ToArray();
            KeyValuePair<string, object>[] otelDimensions = this.otelPopulator
                .PopulateOperationMeterDimensions(operationName, containerName, databaseName, accountName, attributes, ex)
                .ToArray();

            KeyValuePair<string, object>[] dimensions 
                = new KeyValuePair<string, object>[appInsightDimensions.Length + otelDimensions.Length];
            dimensions
                .Concat(appInsightDimensions)
                .Concat(otelDimensions);
            return dimensions;
        }
    }
}
