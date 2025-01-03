// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Telemetry.Models;

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
        internal static IActivityAttributePopulator DimensionPopulator;

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
        internal static void Initialize(CosmosClientTelemetryOptions metricsOptions = null)
        {
            // If already initialized, do not initialize again
            if (IsEnabled)
            {
                return;
            }

            DimensionPopulator = TracesStabilityFactory.GetAttributePopulator(metricsOptions);

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
            OperationMetricsOptions operationMetricsOptions,
            OpenTelemetryAttributes attributes = null,
            Exception ex = null)
        {
            if (!IsEnabled)
            {
                return;
            }

            Func<KeyValuePair<string, object>[]> dimensionsFunc = () =>
                DimensionPopulator.PopulateOperationMeterDimensions(
                    getOperationName(),
                    containerName, 
                    databaseName, 
                    accountName, 
                    attributes, 
                    ex,
                    operationMetricsOptions);

            if (CosmosDbMeterUtil.TryOperationMetricsValues(attributes, ex, out OperationMetricData value))
            {
                CosmosDbMeterUtil.RecordHistogramMetric<int>(value.ItemCount, dimensionsFunc, ActualItemHistogram, Convert.ToInt32);
                CosmosDbMeterUtil.RecordHistogramMetric<double>(value.RequestCharge, dimensionsFunc, RequestUnitsHistogram);
            }

            if (CosmosDbMeterUtil.TryGetDiagnostics(attributes, ex, out CosmosTraceDiagnostics diagnostics))
            {
                CosmosDbMeterUtil.RecordHistogramMetric<double>(value: diagnostics.GetClientElapsedTime(),
                                                                dimensionsFunc, RequestLatencyHistogram,
                                                                t => ((TimeSpan)t).TotalSeconds);
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
                KeyValuePair<string, object>[] dimensions = DimensionPopulator.PopulateInstanceCountDimensions(accountEndpoint);

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

        /// <summary>
        /// Resets the histograms and counters for capturing Cosmos DB metrics in Tests
        /// </summary>
        internal static void Reset()
        {
            if (IsEnabled)
            {
                IsEnabled = false;

                RequestLatencyHistogram = null;
                RequestUnitsHistogram = null;
                ActualItemHistogram = null;
                ActiveInstanceCounter = null;
            }
        }
    }
}
