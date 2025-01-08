// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

    internal static class CosmosDbNetworkMeter
    {
        /// <summary>
        /// Meter instance for capturing various metrics related to Cosmos DB operations.
        /// </summary>
        private static readonly Meter NetworkMeter = new Meter(CosmosDbClientMetrics.NetworkMetrics.MeterName, CosmosDbClientMetrics.NetworkMetrics.Version);

        /// <summary>
        /// Populator Used for Dimension Attributes
        /// </summary>
        internal static IActivityAttributePopulator DimensionPopulator;

        private static Histogram<double> RequestLatencyHistogram = null;

        private static Histogram<long> RequestBodySizeHistogram = null;

        private static Histogram<long> ResponseBodySizeHistogram = null;

        private static Histogram<double> ChannelAquisitionLatencyHistogram = null;

        private static Histogram<double> BackendLatencyHistogram = null;

        private static Histogram<double> TransitLatencyHistogram = null;

        private static Histogram<double> ReceivedLatencyHistogram = null;

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

            CosmosDbNetworkMeter.RequestLatencyHistogram ??= NetworkMeter.CreateHistogram<double>(name: CosmosDbClientMetrics.NetworkMetrics.Name.Latency,
                unit: CosmosDbClientMetrics.NetworkMetrics.Unit.Sec,
                description: CosmosDbClientMetrics.NetworkMetrics.Description.Latency);

            CosmosDbNetworkMeter.RequestBodySizeHistogram ??= NetworkMeter.CreateHistogram<long>(name: CosmosDbClientMetrics.NetworkMetrics.Name.RequestBodySize,
                unit: CosmosDbClientMetrics.NetworkMetrics.Unit.Bytes,
                description: CosmosDbClientMetrics.NetworkMetrics.Description.RequestBodySize);

            CosmosDbNetworkMeter.ResponseBodySizeHistogram ??= NetworkMeter.CreateHistogram<long>(name: CosmosDbClientMetrics.NetworkMetrics.Name.ResponseBodySize,
                unit: CosmosDbClientMetrics.NetworkMetrics.Unit.Bytes,
                description: CosmosDbClientMetrics.NetworkMetrics.Description.ResponseBodySize);

            CosmosDbNetworkMeter.ChannelAquisitionLatencyHistogram ??= NetworkMeter.CreateHistogram<double>(name: CosmosDbClientMetrics.NetworkMetrics.Name.ChannelAquisitionLatency,
                unit: CosmosDbClientMetrics.NetworkMetrics.Unit.Sec,
                description: CosmosDbClientMetrics.NetworkMetrics.Description.ChannelAquisitionLatency);

            CosmosDbNetworkMeter.BackendLatencyHistogram ??= NetworkMeter.CreateHistogram<double>(name: CosmosDbClientMetrics.NetworkMetrics.Name.BackendLatency,
                unit: CosmosDbClientMetrics.NetworkMetrics.Unit.Sec,
                description: CosmosDbClientMetrics.NetworkMetrics.Description.BackendLatency);

            CosmosDbNetworkMeter.TransitLatencyHistogram ??= NetworkMeter.CreateHistogram<double>(name: CosmosDbClientMetrics.NetworkMetrics.Name.TransitTimeLatency,
                unit: CosmosDbClientMetrics.NetworkMetrics.Unit.Sec,
                description: CosmosDbClientMetrics.NetworkMetrics.Description.TransitTimeLatency);

            CosmosDbNetworkMeter.ReceivedLatencyHistogram ??= NetworkMeter.CreateHistogram<double>(name: CosmosDbClientMetrics.NetworkMetrics.Name.ReceivedTimeLatency,
                unit: CosmosDbClientMetrics.NetworkMetrics.Unit.Sec,
                description: CosmosDbClientMetrics.NetworkMetrics.Description.ReceivedTimeLatency);

            IsEnabled = true;
        }

        internal static void RecordTelemetry(Func<string> getOperationName,
            Uri accountName,
            string containerName,
            string databaseName,
            NetworkMetricsOptions networkMetricsOptions,
            OpenTelemetryAttributes attributes = null,
            Exception ex = null)
        {
            if (!IsEnabled || !CosmosDbMeterUtil.TryGetDiagnostics(attributes, ex, out CosmosTraceDiagnostics diagnostics))
            {
                DefaultTrace.TraceWarning("Network Meter is not enabled or Diagnostics is not available.");
                return;
            }

            SummaryDiagnostics summaryDiagnostics = new SummaryDiagnostics(diagnostics.Value);

            summaryDiagnostics.StoreResponseStatistics.Value.ForEach(stat =>
            {
                if (stat?.StoreResult == null)
                {
                    return;
                }

                Func<KeyValuePair<string, object>[]> dimensionsFunc = 
                            () => DimensionPopulator.PopulateNetworkMeterDimensions(getOperationName(),
                                                                                    accountName,
                                                                                    containerName,
                                                                                    databaseName,
                                                                                    attributes,
                                                                                    ex,
                                                                                    networkMetricsOptions,
                                                                                    tcpStats: stat);

                CosmosDbMeterUtil.TryNetworkMetricsValues(stat, out NetworkMetricData metricData);

                CosmosDbMeterUtil.RecordHistogramMetric<double>(metricData.Latency, dimensionsFunc, RequestLatencyHistogram);
                CosmosDbMeterUtil.RecordHistogramMetric<long>(metricData.RequestBodySize, dimensionsFunc, RequestBodySizeHistogram);
                CosmosDbMeterUtil.RecordHistogramMetric<long>(metricData.ResponseBodySize, dimensionsFunc, ResponseBodySizeHistogram);
                CosmosDbMeterUtil.RecordHistogramMetric<double>(metricData.BackendLatency, dimensionsFunc, BackendLatencyHistogram, (value) => Convert.ToDouble(value) / 1000);
                CosmosDbMeterUtil.RecordHistogramMetric<double>(metricData.ChannelAcquisitionLatency, dimensionsFunc, ChannelAquisitionLatencyHistogram);
                CosmosDbMeterUtil.RecordHistogramMetric<double>(metricData.TransitTimeLatency, dimensionsFunc, TransitLatencyHistogram);
                CosmosDbMeterUtil.RecordHistogramMetric<double>(metricData.ReceivedLatency, dimensionsFunc, ReceivedLatencyHistogram);
            });

            summaryDiagnostics.HttpResponseStatistics.Value.ForEach(stat =>
            {
                KeyValuePair<string, object>[] dimensionsFunc()
                {
                    return DimensionPopulator.PopulateNetworkMeterDimensions(getOperationName(),
                                                                             accountName,
                                                                             containerName,
                                                                             databaseName,
                                                                             attributes,
                                                                             ex,
                                                                             networkMetricsOptions,
                                                                             httpStats: stat);
                }

                NetworkMetricData metricData = CosmosDbMeterUtil.GetNetworkMetricsValues(stat);

                CosmosDbMeterUtil.RecordHistogramMetric<double>(metricData.Latency, dimensionsFunc, RequestLatencyHistogram);
                CosmosDbMeterUtil.RecordHistogramMetric<long>(metricData.RequestBodySize, dimensionsFunc, RequestBodySizeHistogram);
                CosmosDbMeterUtil.RecordHistogramMetric<long>(metricData.ResponseBodySize, dimensionsFunc, ResponseBodySizeHistogram);
            });
        }

        /// <summary>
        /// Resets the histograms and counters for capturing Cosmos DB metrics in Tests.
        /// </summary>
        public static void Reset()
        {
            if (IsEnabled)
            {
                IsEnabled = false;

                RequestLatencyHistogram = null;
                RequestBodySizeHistogram = null;
                ResponseBodySizeHistogram = null;
                ChannelAquisitionLatencyHistogram = null;
                BackendLatencyHistogram = null;
                TransitLatencyHistogram = null;
                ReceivedLatencyHistogram = null;
            }
           
        }
    }
}
