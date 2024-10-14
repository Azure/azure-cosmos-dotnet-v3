// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using Microsoft.Azure.Cosmos.Telemetry.Collector;

    internal static class CosmosOperationMeter
    {
        internal static Histogram<double> RequestLatencyHistogram = null;
        internal static Histogram<double> RequestUnitsHistogram = null;
        internal static Histogram<int> ActualItemHistogram = null;
        internal static Histogram<int> InstanceCountHistogram = null;

        private static Meter cosmosMeter;

        public static void Initialize()
        {
            cosmosMeter ??= new Meter(OpenTelemetryMetricsConstant.OperationMetricName, OpenTelemetryMetricsConstant.MetricVersion100);
            
            CosmosOperationMeter.RequestLatencyHistogram = cosmosMeter.CreateHistogram<double>(name: OpenTelemetryMetricsConstant.OperationMetrics.LatencyName,
                unit: OpenTelemetryMetricsConstant.OperationMetrics.Sec,
                description: OpenTelemetryMetricsConstant.OperationMetrics.LatencyDesc);

            CosmosOperationMeter.RequestUnitsHistogram = cosmosMeter.CreateHistogram<double>(name: OpenTelemetryMetricsConstant.OperationMetrics.RUName,
                unit: OpenTelemetryMetricsConstant.OperationMetrics.RUUnit,
                description: OpenTelemetryMetricsConstant.OperationMetrics.RUDesc);

            CosmosOperationMeter.ActualItemHistogram = cosmosMeter.CreateHistogram<int>(name: OpenTelemetryMetricsConstant.OperationMetrics.ActualItemName,
                unit: OpenTelemetryMetricsConstant.OperationMetrics.Count, 
                description: OpenTelemetryMetricsConstant.OperationMetrics.ActualItemDesc);

            CosmosOperationMeter.InstanceCountHistogram = cosmosMeter.CreateHistogram<int>(name: OpenTelemetryMetricsConstant.OperationMetrics.InstanceMetricName,
                unit: OpenTelemetryMetricsConstant.OperationMetrics.Count,
                description: OpenTelemetryMetricsConstant.OperationMetrics.InstanceMetricDesc);
        }

        public static void RecordTelemetry(string operationName, string accountName, string containerName, string databaseName, OpenTelemetryAttributes attributes)
        {
            Func<KeyValuePair<string, object>[]> dimensionsFunc = () => new[]
            {
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.DbSystemName, "cosmosdb"),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.DbOperation, operationName),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ContainerName, containerName),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.DbName, databaseName),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServerAddress, accountName),

                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.Consistency, "consistency"),

                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.StatusCode, (int)attributes.StatusCode),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.SubStatusCode, (int)attributes.SubStatusCode)
            };

            CosmosOperationMeter.RecordActualItemCount(Convert.ToInt32(attributes.ItemCount), dimensionsFunc);
            CosmosOperationMeter.RecordRequestUnit(attributes.RequestCharge.Value, dimensionsFunc);
            CosmosOperationMeter.RecordRequestLatency(attributes.Diagnostics.GetClientElapsedTime(), dimensionsFunc);
            CosmosOperationMeter.RecordActiveInstances(dimensionsFunc);
        }

        public static void RecordActualItemCount(int actualItemCount, Func<KeyValuePair<string, object>[]> dimensionsFunc)
        {
            if (CosmosOperationMeter.ActualItemHistogram == null || !CosmosOperationMeter.ActualItemHistogram.Enabled)
            {
                return;
            }

            CosmosOperationMeter.ActualItemHistogram.Record(actualItemCount, dimensionsFunc());
        }

        internal static void RecordRequestUnit(double requestCharge, Func<KeyValuePair<string, object>[]> dimensionsFunc)
        {
            if (CosmosOperationMeter.RequestUnitsHistogram == null || !CosmosOperationMeter.RequestUnitsHistogram.Enabled)
            {
                return;
            }

            CosmosOperationMeter.RequestUnitsHistogram?.Record(requestCharge, dimensionsFunc());
        }

        internal static void RecordRequestLatency(TimeSpan? requestLatency, Func<KeyValuePair<string, object>[]> dimensionsFunc)
        {
            if (CosmosOperationMeter.RequestLatencyHistogram == null || !CosmosOperationMeter.RequestLatencyHistogram.Enabled || !requestLatency.HasValue)
            {
                return;
            }

            CosmosOperationMeter.RequestLatencyHistogram.Record(requestLatency.Value.Milliseconds, dimensionsFunc());
        }

        public static void RecordActiveInstances(Func<KeyValuePair<string, object>[]> dimensionsFunc)
        {
            if (CosmosOperationMeter.InstanceCountHistogram == null || !CosmosOperationMeter.InstanceCountHistogram.Enabled)
            {
                return;
            }

            CosmosOperationMeter.InstanceCountHistogram.Record(CosmosClient.NumberOfActiveClients, dimensionsFunc());
        }
    }
}
