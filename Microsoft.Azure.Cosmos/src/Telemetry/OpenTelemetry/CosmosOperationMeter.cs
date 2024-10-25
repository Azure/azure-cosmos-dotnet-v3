// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;

    /// <summary>
    /// CosmosOperationMeter is a utility class responsible for collecting and recording telemetry metrics related to Cosmos DB operations.
    /// It includes histograms and counters that capture metrics like request latency, request units, item count, and active instances.
    /// </summary>
    internal static class CosmosOperationMeter
    {
        /// <summary>
        /// Meter instance for capturing various metrics related to Cosmos DB operations.
        /// </summary>
        internal static Meter OperationMeter = new Meter(CosmosDbClientMetrics.OperationMetrics.MeterName, CosmosDbClientMetrics.OperationMetrics.Version);

        /// <summary>
        /// Histogram to record request latency (in seconds) for Cosmos DB operations.
        /// </summary>
        internal static Histogram<double> RequestLatencyHistogram = null;

        /// <summary>
        /// Histogram to record request units consumed during Cosmos DB operations.
        /// </summary>
        internal static Histogram<double> RequestUnitsHistogram = null;

        /// <summary>
        /// Histogram to record the actual number of items involved in the operation.
        /// </summary>
        internal static Histogram<int> ActualItemHistogram = null;

        /// <summary>
        /// UpDownCounter to track the number of active instances interacting with Cosmos DB.
        /// </summary>
        internal static UpDownCounter<int> ActiveInstanceCounter = null;

        /// <summary>
        /// Flag to check if metrics is enabled
        /// </summary>
        private static bool IsEnabled = false;

        /// <summary>
        /// Initializes the histograms and counters for capturing Cosmos DB metrics.
        /// </summary>
        internal static void Initialize()
        {
            // If already initialized, do not initialize again
            if (IsEnabled)
            {
                return;
            }

            CosmosOperationMeter.RequestLatencyHistogram ??= OperationMeter.CreateHistogram<double>(name: CosmosDbClientMetrics.OperationMetrics.Name.Latency,
                unit: CosmosDbClientMetrics.OperationMetrics.Unit.Sec,
                description: CosmosDbClientMetrics.OperationMetrics.Description.Latency);

            CosmosOperationMeter.RequestUnitsHistogram ??= OperationMeter.CreateHistogram<double>(name: CosmosDbClientMetrics.OperationMetrics.Name.RequestCharge,
                unit: CosmosDbClientMetrics.OperationMetrics.Unit.RequestUnit,
                description: CosmosDbClientMetrics.OperationMetrics.Description.RequestCharge);

            CosmosOperationMeter.ActualItemHistogram ??= OperationMeter.CreateHistogram<int>(name: CosmosDbClientMetrics.OperationMetrics.Name.RowCount,
                unit: CosmosDbClientMetrics.OperationMetrics.Unit.Count, 
                description: CosmosDbClientMetrics.OperationMetrics.Description.RowCount);

            CosmosOperationMeter.ActiveInstanceCounter ??= OperationMeter.CreateUpDownCounter<int>(name: CosmosDbClientMetrics.OperationMetrics.Name.ActiveInstances,
                unit: CosmosDbClientMetrics.OperationMetrics.Unit.Count,
                description: CosmosDbClientMetrics.OperationMetrics.Description.ActiveInstances);

            IsEnabled = true;
        }

        /// <summary>
        /// Records telemetry data related to Cosmos DB operations. This includes request latency, request units, and item counts.
        /// </summary>
        /// <param name="getOperationName">Generate operation name</param>
        /// <param name="accountName">The URI of the Cosmos DB account.</param>
        /// <param name="containerName">The name of the container involved in the operation.</param>
        /// <param name="databaseName">The name of the database involved in the operation.</param>
        /// <param name="attributes">Optional OpenTelemetry attributes related to the operation.</param>
        /// <param name="ex">Optional exception object to capture error details.</param>
        internal static void RecordTelemetry(Func<string> getOperationName,
            Uri accountName, 
            string containerName, 
            string databaseName, 
            OpenTelemetryAttributes attributes = null, 
            CosmosException ex = null)
        {
            if (!IsEnabled)
            {
                return;
            }

            Func<KeyValuePair<string, object>[]> dimensionsFunc = () => new KeyValuePair<string, object>[]
            {
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.DbSystemName, OpenTelemetryCoreRecorder.CosmosDb),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ContainerName, containerName),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.DbName, databaseName),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServerAddress, accountName.Host),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServerPort, accountName.Port),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.DbOperation, getOperationName()),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.StatusCode, (int)(attributes?.StatusCode ?? ex?.StatusCode)),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.SubStatusCode, attributes?.SubStatusCode ?? ex?.SubStatusCode),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ConsistencyLevel, attributes?.ConsistencyLevel ?? ex?.Headers?.ConsistencyLevel),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ErrorType, ex?.Message)
            };

            CosmosOperationMeter.RecordActualItemCount(attributes?.ItemCount ?? ex?.Headers?.ItemCount, dimensionsFunc);
            CosmosOperationMeter.RecordRequestUnit(attributes?.RequestCharge ?? ex?.Headers?.RequestCharge, dimensionsFunc);
            CosmosOperationMeter.RecordRequestLatency(attributes?.Diagnostics?.GetClientElapsedTime() ?? ex?.Diagnostics?.GetClientElapsedTime(), dimensionsFunc);
        }

        /// <summary>
        /// Records the actual item count metric for a Cosmos DB operation.
        /// </summary>
        /// <param name="actualItemCount">The number of items returned or affected by the operation.</param>
        /// <param name="dimensionsFunc">A function providing telemetry dimensions for the metric.</param>
        internal static void RecordActualItemCount(string actualItemCount, Func<KeyValuePair<string, object>[]> dimensionsFunc)
        {
            if (!IsEnabled || CosmosOperationMeter.ActualItemHistogram == null || 
                !CosmosOperationMeter.ActualItemHistogram.Enabled || string.IsNullOrEmpty(actualItemCount))
            {
                return;
            }

            CosmosOperationMeter.ActualItemHistogram.Record(Convert.ToInt32(actualItemCount), dimensionsFunc());
        }

        /// <summary>
        /// Records the request units (RU/s) consumed by the operation.
        /// </summary>
        /// <param name="requestCharge">The RU/s value for the operation.</param>
        /// <param name="dimensionsFunc">A function providing telemetry dimensions for the metric.</param>
        internal static void RecordRequestUnit(double? requestCharge, Func<KeyValuePair<string, object>[]> dimensionsFunc)
        {
            if (!IsEnabled || CosmosOperationMeter.RequestUnitsHistogram == null || 
                !CosmosOperationMeter.RequestUnitsHistogram.Enabled || !requestCharge.HasValue)
            {
                return;
            }

            CosmosOperationMeter.RequestUnitsHistogram.Record(requestCharge.Value, dimensionsFunc());
        }

        /// <summary>
        /// Records the latency (in seconds) for a Cosmos DB operation.
        /// </summary>
        /// <param name="requestLatency">The latency of the operation.</param>
        /// <param name="dimensionsFunc">A function providing telemetry dimensions for the metric.</param>
        internal static void RecordRequestLatency(TimeSpan? requestLatency, Func<KeyValuePair<string, object>[]> dimensionsFunc)
        {
            if (!IsEnabled || CosmosOperationMeter.ActiveInstanceCounter == null || 
                !CosmosOperationMeter.ActiveInstanceCounter.Enabled || !requestLatency.HasValue)
            {
                return;
            }

            CosmosOperationMeter.RequestLatencyHistogram.Record(requestLatency.Value.TotalMilliseconds / 1000, dimensionsFunc());
        }

        /// <summary>
        /// Increases the count of active Cosmos DB instances.
        /// </summary>
        /// <param name="accountEndpoint">The URI of the account endpoint.</param>
        internal static void AddInstanceCount(Uri accountEndpoint)
        {
            if (!IsEnabled || CosmosOperationMeter.ActiveInstanceCounter == null || 
                !CosmosOperationMeter.ActiveInstanceCounter.Enabled)
            {
                return;
            }

            KeyValuePair<string, object>[] dimensions = new[]
            {
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.DbSystemName, OpenTelemetryCoreRecorder.CosmosDb),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServerAddress, accountEndpoint.Host),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServerPort, accountEndpoint.Port)
            };

            Console.WriteLine("active instance metriuc collected");
            CosmosOperationMeter.ActiveInstanceCounter.Add(1, dimensions);
        }

        /// <summary>
        /// Decreases the count of active Cosmos DB instances.
        /// </summary>
        /// <param name="accountEndpoint">The URI of the account endpoint.</param>
        internal static void RemoveInstanceCount(Uri accountEndpoint)
        {
            if (!IsEnabled || CosmosOperationMeter.ActiveInstanceCounter == null || 
                !CosmosOperationMeter.ActiveInstanceCounter.Enabled)
            {
                return;
            }

            KeyValuePair<string, object>[] dimensions = new[]
            {
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.DbSystemName, OpenTelemetryCoreRecorder.CosmosDb),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServerAddress, accountEndpoint.Host),
                new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServerPort, accountEndpoint.Port)
            };

            CosmosOperationMeter.ActiveInstanceCounter.Add(-1, dimensions);
        }
    }
}
