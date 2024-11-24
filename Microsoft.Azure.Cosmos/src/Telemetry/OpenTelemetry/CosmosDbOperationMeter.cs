// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;

    /// <summary>
    /// CosmosOperationMeter is a utility class responsible for collecting and recording telemetry metrics related to Cosmos DB operations.
    /// It includes histograms and counters that capture metrics like request latency, request units, item count, and active instances.
    /// </summary>
    internal static class CosmosDbOperationMeter
    {
        /// <summary>
        /// Meter instance for capturing various metrics related to Cosmos DB operations.
        /// </summary>
        private static readonly Meter OperationMeter = new Meter(CosmosDbClientMetrics.OperationMetrics.MeterName, CosmosDbClientMetrics.OperationMetrics.Version);

        /// <summary>
        /// Populator Used for Dimension Attributes
        /// </summary>
        private static readonly IActivityAttributePopulator DimensionPopulator = TracesStabilityFactory.GetAttributePopulator();

        /// <summary>
        /// Histogram to record request latency (in seconds) for Cosmos DB operations.
        /// </summary>
        private static Histogram<double> RequestLatencyHistogram = null;

        /// <summary>
        /// Histogram to record request units consumed during Cosmos DB operations.
        /// </summary>
        private static Histogram<double> RequestUnitsHistogram = null;

        /// <summary>
        /// Histogram to record the actual number of items involved in the operation.
        /// </summary>
        private static Histogram<int> ActualItemHistogram = null;

        /// <summary>
        /// UpDownCounter to track the number of active instances interacting with Cosmos DB.
        /// </summary>
        private static UpDownCounter<int> ActiveInstanceCounter = null;

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

            CosmosDbOperationMeter.RequestLatencyHistogram ??= OperationMeter.CreateHistogram<double>(name: CosmosDbClientMetrics.OperationMetrics.Name.Latency,
                unit: CosmosDbClientMetrics.OperationMetrics.Unit.Sec,
                description: CosmosDbClientMetrics.OperationMetrics.Description.Latency);

            CosmosDbOperationMeter.RequestUnitsHistogram ??= OperationMeter.CreateHistogram<double>(name: CosmosDbClientMetrics.OperationMetrics.Name.RequestCharge,
                unit: CosmosDbClientMetrics.OperationMetrics.Unit.RequestUnit,
                description: CosmosDbClientMetrics.OperationMetrics.Description.RequestCharge);

            CosmosDbOperationMeter.ActualItemHistogram ??= OperationMeter.CreateHistogram<int>(name: CosmosDbClientMetrics.OperationMetrics.Name.RowCount,
                unit: CosmosDbClientMetrics.OperationMetrics.Unit.Count, 
                description: CosmosDbClientMetrics.OperationMetrics.Description.RowCount);

            CosmosDbOperationMeter.ActiveInstanceCounter ??= OperationMeter.CreateUpDownCounter<int>(name: CosmosDbClientMetrics.OperationMetrics.Name.ActiveInstances,
                unit: CosmosDbClientMetrics.OperationMetrics.Unit.Count,
                description: CosmosDbClientMetrics.OperationMetrics.Description.ActiveInstances);

            IsEnabled = true;
        }

        /// <summary>
        /// Records telemetry data for Cosmos DB operations.
        /// </summary>
        internal static void RecordTelemetry(
            Func<string> getOperationName,
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

            try
            {
                Func<KeyValuePair<string, object>[]> dimensionsFunc = () =>
                    DimensionPopulator.PopulateOperationMeterDimensions(
                        getOperationName(), containerName, databaseName, accountName, attributes, ex);

                CosmosDbMeterUtil.RecordHistogramMetric<int>(value: attributes?.ItemCount ?? ex?.Headers?.ItemCount, 
                    dimensionsFunc, ActualItemHistogram, 
                    Convert.ToInt32);
                CosmosDbMeterUtil.RecordHistogramMetric<double>(value: attributes?.RequestCharge ?? ex?.Headers?.RequestCharge, 
                    dimensionsFunc, RequestUnitsHistogram);
                CosmosDbMeterUtil.RecordHistogramMetric<double>(value: attributes?.Diagnostics?.GetClientElapsedTime() ?? ex?.Diagnostics?.GetClientElapsedTime(),
                    dimensionsFunc, RequestLatencyHistogram, 
                    t => ((TimeSpan)t).TotalSeconds);
            }
            catch (Exception exception)
            {
                DefaultTrace.TraceWarning($"Failed to record telemetry data. {exception.StackTrace}");
            }
        }

        /// <summary>
        /// Adjusts the count of active Cosmos DB instances.
        /// </summary>
        internal static void AdjustInstanceCount(Uri accountEndpoint, int adjustment)
        {
            if (!IsEnabled || ActiveInstanceCounter == null || !ActiveInstanceCounter.Enabled)
            {
                return;
            }

            try
            {
                KeyValuePair<string, object>[] dimensions = new[]
                {
                    new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.DbSystemName, OpenTelemetryCoreRecorder.CosmosDb),
                    new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServerAddress, accountEndpoint.Host),
                    new KeyValuePair<string, object>(OpenTelemetryAttributeKeys.ServerPort, accountEndpoint.Port)
                };

                ActiveInstanceCounter.Add(adjustment, dimensions);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceWarning($"Failed to adjust instance count. {ex.StackTrace}");
            }
        }

        internal static void AddInstanceCount(Uri accountEndpoint)
        {
            AdjustInstanceCount(accountEndpoint, 1);
        }

        internal static void RemoveInstanceCount(Uri accountEndpoint)
        {
            AdjustInstanceCount(accountEndpoint, -1);
        }
    }
}
