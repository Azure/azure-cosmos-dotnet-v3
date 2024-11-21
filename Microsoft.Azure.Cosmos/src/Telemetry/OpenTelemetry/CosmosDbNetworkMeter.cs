// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using Microsoft.Azure.Cosmos.Diagnostics;
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
        private static readonly IActivityAttributePopulator DimensionPopulator = TracesStabilityFactory.GetAttributePopulator();

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
        internal static void Initialize()
        {
            // If already initialized, do not initialize again
            if (IsEnabled)
            {
                return;
            }

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
            OpenTelemetryAttributes attributes = null,
            CosmosException ex = null)
        {
            if (!IsEnabled)
            {
                return;
            }

            SummaryDiagnostics summaryDiagnostics 
                = new SummaryDiagnostics(((CosmosTraceDiagnostics)(ex?.Diagnostics ?? attributes?.Diagnostics)).Value);
            summaryDiagnostics.StoreResponseStatistics.Value.ForEach(stat =>
            {
                if (stat?.StoreResult == null)
                {
                    return;
                }

                Func<KeyValuePair<string, object>[]> dimensionsFunc = () => DimensionPopulator.PopulateNetworkMeterDimensions(
                    getOperationName(), accountName, containerName, databaseName, attributes, ex, tcpStats: stat);

                CosmosDbMeterUtil.RecordHistogramMetric<double>(stat.RequestLatency.TotalSeconds, dimensionsFunc, RequestLatencyHistogram);
                CosmosDbMeterUtil.RecordHistogramMetric<long>(stat?.StoreResult?.TransportRequestStats?.RequestBodySizeInBytes, dimensionsFunc, RequestBodySizeHistogram);
                CosmosDbMeterUtil.RecordHistogramMetric<long>(stat?.StoreResult?.TransportRequestStats?.ResponseBodySizeInBytes, dimensionsFunc, ResponseBodySizeHistogram);
                CosmosDbMeterUtil.RecordHistogramMetric<double>(stat?.StoreResult?.BackendRequestDurationInMs, dimensionsFunc, BackendLatencyHistogram, (value) => Convert.ToDouble(value) / 1000);

                DateTime? requestCreatedTime = stat?.StoreResult?.TransportRequestStats?.requestCreatedTime;
                TimeSpan? channelAcquisitionStartedTime = stat?.StoreResult?.TransportRequestStats?.channelAcquisitionStartedTime;
                TimeSpan? requestPipelinedTime = stat?.StoreResult?.TransportRequestStats?.requestPipelinedTime;
                TimeSpan? transitTime = stat?.StoreResult?.TransportRequestStats?.requestSentTime;
                TimeSpan? requestReceivedTime = stat?.StoreResult?.TransportRequestStats?.requestReceivedTime;
                TimeSpan? requestFailedTime = stat?.StoreResult?.TransportRequestStats?.requestFailedTime;
                TimeSpan? requestCompletedTime = stat?.StoreResult?.TransportRequestStats?.requestCompletedTime;

                TimeSpan? channelAquisitionLatency = null;
                TimeSpan? caDiffTimeSpan = requestPipelinedTime ?? requestFailedTime;
                if (caDiffTimeSpan.HasValue)
                {
                    channelAquisitionLatency = caDiffTimeSpan.Value.Subtract(channelAcquisitionStartedTime.Value);
                }
                CosmosDbMeterUtil.RecordHistogramMetric<double>(channelAquisitionLatency, dimensionsFunc, ChannelAquisitionLatencyHistogram);

                TimeSpan? transitLatency = null;
                TimeSpan? rrDiffTimeSpan = requestReceivedTime ?? requestFailedTime;
                if (rrDiffTimeSpan.HasValue)
                {
                    transitLatency = rrDiffTimeSpan.Value.Subtract(transitTime.Value);
                }
                CosmosDbMeterUtil.RecordHistogramMetric<double>(transitLatency, dimensionsFunc, TransitLatencyHistogram);

                TimeSpan? receivedLatency = null;
                TimeSpan? rcDiffTimeSpan = requestCompletedTime ?? requestFailedTime;
                if (rrDiffTimeSpan.HasValue)
                {
                    receivedLatency = rcDiffTimeSpan.Value.Subtract(requestReceivedTime.Value);
                }
                CosmosDbMeterUtil.RecordHistogramMetric<double>(receivedLatency, dimensionsFunc, ReceivedLatencyHistogram);     
            });

            summaryDiagnostics.HttpResponseStatistics.Value.ForEach(stat =>
            {
                Func<KeyValuePair<string, object>[]> dimensionsFunc = () => DimensionPopulator.PopulateNetworkMeterDimensions(
                    getOperationName(), accountName, containerName, databaseName, attributes, ex, httpStats: stat);

                CosmosDbMeterUtil.RecordHistogramMetric<double>(stat.Duration.TotalSeconds, dimensionsFunc, RequestLatencyHistogram);
                CosmosDbMeterUtil.RecordHistogramMetric<long>(stat.HttpResponseMessage?.RequestMessage?.Content?.Headers?.ContentLength, dimensionsFunc, RequestBodySizeHistogram);
                CosmosDbMeterUtil.RecordHistogramMetric<long>(stat.ResponseContentLength, dimensionsFunc, ResponseBodySizeHistogram);
            });
        }
    }
}
