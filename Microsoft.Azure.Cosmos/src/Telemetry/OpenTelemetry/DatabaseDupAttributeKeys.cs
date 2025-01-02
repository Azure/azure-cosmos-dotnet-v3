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

        public DatabaseDupAttributeKeys(CosmosClientTelemetryOptions metricsOptions) 
        {
            this.otelPopulator = new OpenTelemetryAttributeKeys(metricsOptions?.OperationMetricsOptions, metricsOptions?.NetworkMetricsOptions);
            this.appInsightPopulator = new AppInsightClassicAttributeKeys(metricsOptions?.OperationMetricsOptions);
        }

        public void PopulateAttributes(DiagnosticScope scope, 
            string operationName, 
            string databaseName, 
            string containerName, 
            Uri accountName, 
            string userAgent, 
            string machineId, 
            string clientId, 
            string connectionMode)
        {
            this.appInsightPopulator.PopulateAttributes(scope, operationName, databaseName, containerName, accountName, userAgent, machineId, clientId, connectionMode);
            this.otelPopulator.PopulateAttributes(scope, operationName, databaseName, containerName, accountName, userAgent, machineId, clientId, connectionMode);
        }

        public void PopulateAttributes(DiagnosticScope scope, Exception exception)
        {
            this.appInsightPopulator.PopulateAttributes(scope, exception);
            this.otelPopulator.PopulateAttributes(scope, exception);
        }

        public void PopulateAttributes(DiagnosticScope scope, 
            QueryTextMode? queryTextMode, 
            string operationType, 
            OpenTelemetryAttributes response)
        {
            this.appInsightPopulator.PopulateAttributes(scope, queryTextMode, operationType, response);
            this.otelPopulator.PopulateAttributes(scope, queryTextMode, operationType, response);
        }

        public KeyValuePair<string, object>[] PopulateInstanceCountDimensions(Uri accountName)
        {
            return this.MergeDimensions(
                () => this.appInsightPopulator.PopulateInstanceCountDimensions(accountName),
                () => this.otelPopulator.PopulateInstanceCountDimensions(accountName));
        }

        public KeyValuePair<string, object>[] PopulateNetworkMeterDimensions(string operationName, 
            Uri accountName, 
            string containerName, 
            string databaseName, 
            OpenTelemetryAttributes attributes, 
            Exception ex,
            NetworkMetricsOptions optionFromRequest,
            ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics tcpStats = null, 
            ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics? httpStats = null)
        {
            return this.MergeDimensions(
                () => this.appInsightPopulator.PopulateNetworkMeterDimensions(operationName, accountName, containerName, databaseName, attributes, ex, optionFromRequest, tcpStats, httpStats),
                () => this.otelPopulator.PopulateNetworkMeterDimensions(operationName, accountName, containerName, databaseName, attributes, ex, optionFromRequest, tcpStats, httpStats));
        }

        public KeyValuePair<string, object>[] PopulateOperationMeterDimensions(string operationName, 
            string containerName, 
            string databaseName, 
            Uri accountName, 
            OpenTelemetryAttributes attributes, 
            Exception ex,
            OperationMetricsOptions optionFromRequest)
        {
            return this.MergeDimensions(
               () => this.appInsightPopulator.PopulateOperationMeterDimensions(operationName, containerName, databaseName, accountName, attributes, ex, optionFromRequest),
               () => this.otelPopulator.PopulateOperationMeterDimensions(operationName, containerName, databaseName, accountName, attributes, ex, optionFromRequest));
        }

        private KeyValuePair<string, object>[] MergeDimensions(
           Func<IEnumerable<KeyValuePair<string, object>>> appInsightDimensionsProvider,
           Func<IEnumerable<KeyValuePair<string, object>>> otelDimensionsProvider)
        {
            KeyValuePair<string, object>[] appInsightDimensions = appInsightDimensionsProvider().ToArray();
            KeyValuePair<string, object>[] otelDimensions = otelDimensionsProvider().ToArray();

            KeyValuePair<string, object>[] dimensions = new KeyValuePair<string, object>[appInsightDimensions.Length + otelDimensions.Length];

            Array.Copy(appInsightDimensions, dimensions, appInsightDimensions.Length);
            Array.Copy(otelDimensions, 0, dimensions, appInsightDimensions.Length, otelDimensions.Length);

            return dimensions;
        }
    }
}
