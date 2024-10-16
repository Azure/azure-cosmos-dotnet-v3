// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Xml.Linq;

    internal static class CosmosOperationMeter
    {
        internal static Histogram<double> RequestLatencyHistogram = null;
        internal static Histogram<double> RequestUnitsHistogram = null;
        internal static Histogram<int> ActualItemHistogram = null;
        internal static UpDownCounter<int> InstanceCounter = null;

        private static Meter cosmosMeter;

        public static void Initialize()
        {
            cosmosMeter ??= new Meter(OpenTelemetryMetricsConstant.OperationMetrics.MeterName, OpenTelemetryMetricsConstant.OperationMetrics.Version);
            
            CosmosOperationMeter.RequestLatencyHistogram = cosmosMeter.CreateHistogram<double>(name: OpenTelemetryMetricsConstant.OperationMetrics.Name.Latency,
                unit: OpenTelemetryMetricsConstant.OperationMetrics.Unit.Sec,
                description: OpenTelemetryMetricsConstant.OperationMetrics.Description.Latency);

            CosmosOperationMeter.RequestUnitsHistogram = cosmosMeter.CreateHistogram<double>(name: OpenTelemetryMetricsConstant.OperationMetrics.Name.RequestCharge,
                unit: OpenTelemetryMetricsConstant.OperationMetrics.Unit.RequestUnit,
                description: OpenTelemetryMetricsConstant.OperationMetrics.Description.RequestCharge);

            CosmosOperationMeter.ActualItemHistogram = cosmosMeter.CreateHistogram<int>(name: OpenTelemetryMetricsConstant.OperationMetrics.Name.RowCount,
                unit: OpenTelemetryMetricsConstant.OperationMetrics.Unit.Count, 
                description: OpenTelemetryMetricsConstant.OperationMetrics.Description.RowCount);

            CosmosOperationMeter.InstanceCounter = cosmosMeter.CreateUpDownCounter<int>(name: OpenTelemetryMetricsConstant.OperationMetrics.Name.ActiveInstances,
                unit: OpenTelemetryMetricsConstant.OperationMetrics.Unit.Count,
                description: OpenTelemetryMetricsConstant.OperationMetrics.Description.ActiveInstances);
        }

        public static void RecordTelemetry(string operationName, Uri accountName, string containerName, string databaseName, OpenTelemetryAttributes attributes)
        {
            Func<KeyValuePair<string, object>[]> dimensionsFunc = () => new[]
            {
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.DbSystemName, "cosmosdb"),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.DbOperation, operationName),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ContainerName, containerName),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.DbName, databaseName),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServerAddress, accountName.Host),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServerPort, accountName.Port),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ConsistencyLevel, attributes.ConsistencyLevel),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.StatusCode, (int)attributes.StatusCode),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.SubStatusCode, (int)attributes.SubStatusCode)
            };

            CosmosOperationMeter.RecordActualItemCount(Convert.ToInt32(attributes.ItemCount), dimensionsFunc);
            CosmosOperationMeter.RecordRequestUnit(attributes.RequestCharge.Value, dimensionsFunc);
            CosmosOperationMeter.RecordRequestLatency(attributes.Diagnostics.GetClientElapsedTime(), dimensionsFunc);
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

        public static void AddActiveInstance(Uri account)
        {
            if (CosmosOperationMeter.InstanceCounter == null || !CosmosOperationMeter.InstanceCounter.Enabled)
            {
                return;
            }

            Func<KeyValuePair<string, object>[]> dimensionsFunc = () => new[]
            {
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.DbSystemName, "cosmosdb"),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServerAddress, account.Host),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServerPort, account.Port),
            };

            CosmosOperationMeter.InstanceCounter.Add(1, dimensionsFunc());
        }

        public static void DisposeActiveInstance(Uri account)
        {
            if (CosmosOperationMeter.InstanceCounter == null || !CosmosOperationMeter.InstanceCounter.Enabled)
            {
                return;
            }

            Func<KeyValuePair<string, object>[]> dimensionsFunc = () => new[]
            {
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.DbSystemName, "cosmosdb"),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServerAddress, account.Host),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServerPort, account.Port),
            };

            CosmosOperationMeter.InstanceCounter.Add(-1, dimensionsFunc());
        }
    }
}
