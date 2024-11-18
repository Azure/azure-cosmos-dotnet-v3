// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Telemetry.OpenTelemetry;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

    internal static class CosmosNetworkMeter
    {
        /// <summary>
        /// Meter instance for capturing various metrics related to Cosmos DB operations.
        /// </summary>
        internal static Meter NetworkMeter = new Meter(CosmosDbClientMetrics.NetworkMetrics.MeterName, CosmosDbClientMetrics.NetworkMetrics.Version);

        internal static Histogram<double> RequestLatencyHistogram = null;

        internal static Histogram<long> RequestBodySizeHistogram = null;

        internal static Histogram<double> ResponseBodySizeHistogram = null;

        internal static Histogram<double> ChannelAquisitionLatencyHistogram = null;

        internal static Histogram<double> BackendLatencyHistogram = null;

        internal static Histogram<double> TransitLatencyHistogram = null;

        internal static Histogram<double> ReceivedLatencyHistogram = null;

        private static bool IsEnabled = false;

        private static IActivityAttributePopulator activityAttributePopulator;

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

            activityAttributePopulator = TracesStabilityFactory.GetAttributePopulator();

            CosmosNetworkMeter.RequestLatencyHistogram ??= NetworkMeter.CreateHistogram<double>(name: CosmosDbClientMetrics.NetworkMetrics.Name.Latency,
                unit: CosmosDbClientMetrics.NetworkMetrics.Unit.Sec,
                description: CosmosDbClientMetrics.NetworkMetrics.Description.Latency);

            CosmosNetworkMeter.RequestBodySizeHistogram ??= NetworkMeter.CreateHistogram<long>(name: CosmosDbClientMetrics.NetworkMetrics.Name.RequestBodySize,
                unit: CosmosDbClientMetrics.NetworkMetrics.Unit.Bytes,
                description: CosmosDbClientMetrics.NetworkMetrics.Description.RequestBodySize);

            CosmosNetworkMeter.ResponseBodySizeHistogram ??= NetworkMeter.CreateHistogram<double>(name: CosmosDbClientMetrics.NetworkMetrics.Name.ResponseBodySize,
                unit: CosmosDbClientMetrics.NetworkMetrics.Unit.Bytes,
                description: CosmosDbClientMetrics.NetworkMetrics.Description.ResponseBodySize);

            CosmosNetworkMeter.ChannelAquisitionLatencyHistogram ??= NetworkMeter.CreateHistogram<double>(name: CosmosDbClientMetrics.NetworkMetrics.Name.ChannelAquisitionLatency,
                unit: CosmosDbClientMetrics.NetworkMetrics.Unit.Sec,
                description: CosmosDbClientMetrics.NetworkMetrics.Description.ChannelAquisitionLatency);

            CosmosNetworkMeter.BackendLatencyHistogram ??= NetworkMeter.CreateHistogram<double>(name: CosmosDbClientMetrics.NetworkMetrics.Name.BackendLatency,
                unit: CosmosDbClientMetrics.NetworkMetrics.Unit.Sec,
                description: CosmosDbClientMetrics.NetworkMetrics.Description.BackendLatency);

            CosmosNetworkMeter.TransitLatencyHistogram ??= NetworkMeter.CreateHistogram<double>(name: CosmosDbClientMetrics.NetworkMetrics.Name.TransitTimeLatency,
                unit: CosmosDbClientMetrics.NetworkMetrics.Unit.Sec,
                description: CosmosDbClientMetrics.NetworkMetrics.Description.TransitTimeLatency);

            CosmosNetworkMeter.ReceivedLatencyHistogram ??= NetworkMeter.CreateHistogram<double>(name: CosmosDbClientMetrics.NetworkMetrics.Name.ReceivedTimeLatency,
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

                KeyValuePair<string, object>[] dimension = activityAttributePopulator.PopulateNetworkMeterDimensions(
                    getOperationName(), accountName, containerName, databaseName, attributes, ex, tcpStats: stat);

                CosmosNetworkMeter.RecordRequestLatency(stat.RequestLatency.TotalSeconds, dimension);
                CosmosNetworkMeter.RecordRequestBodySize(stat?.StoreResult?.TransportRequestStats?.RequestBodySizeInBytes, dimension);
                CosmosNetworkMeter.RecordResponseBodySize(stat?.StoreResult?.TransportRequestStats?.ResponseBodySizeInBytes, dimension);
                CosmosNetworkMeter.RecordBackendLatency(stat?.StoreResult?.BackendRequestDurationInMs, dimension);
            });

            summaryDiagnostics.HttpResponseStatistics.Value.ForEach(stat =>
            {
                KeyValuePair<string, object>[] dimension = activityAttributePopulator.PopulateNetworkMeterDimensions(
                    getOperationName(), accountName, containerName, databaseName, attributes, ex, httpStats: stat);

                CosmosNetworkMeter.RecordRequestLatency(stat.Duration.TotalSeconds, dimension);
                CosmosNetworkMeter.RecordRequestBodySize(stat.HttpResponseMessage?.RequestMessage?.Content?.Headers?.ContentLength, dimension);
                CosmosNetworkMeter.RecordResponseBodySize(stat.ResponseContentLength, dimension);
            });
        }

        internal static void RecordRequestLatency(double latency, KeyValuePair<string, object>[] dimension)
        {
            if (!IsEnabled || CosmosNetworkMeter.RequestLatencyHistogram == null ||
                !CosmosNetworkMeter.RequestLatencyHistogram.Enabled)
            {
                return;
            }

            CosmosNetworkMeter.RequestLatencyHistogram.Record(latency, dimension);
        }

        internal static void RecordRequestBodySize(long? bodySize, KeyValuePair<string, object>[] dimension)
        {
            if (!IsEnabled || 
                CosmosNetworkMeter.RequestBodySizeHistogram == null || 
                !bodySize.HasValue ||
                !CosmosNetworkMeter.ResponseBodySizeHistogram.Enabled)
            {
                return;
            }

            CosmosNetworkMeter.RequestBodySizeHistogram.Record(bodySize.Value, dimension);
        }

        internal static void RecordResponseBodySize(long? bodySize, KeyValuePair<string, object>[] dimension)
        {
            if (!IsEnabled || 
                CosmosNetworkMeter.ResponseBodySizeHistogram == null || 
                !bodySize.HasValue ||
                !CosmosNetworkMeter.ResponseBodySizeHistogram.Enabled)
            {
                return;
            }

            CosmosNetworkMeter.ResponseBodySizeHistogram.Record(bodySize.Value, dimension);
        }

        internal static void RecordBackendLatency(string latencyInMs, KeyValuePair<string, object>[] dimension)
        {
            if (!IsEnabled || 
                CosmosNetworkMeter.BackendLatencyHistogram == null || 
                string.IsNullOrEmpty(latencyInMs) ||
                !CosmosNetworkMeter.BackendLatencyHistogram.Enabled)
            {
                return;
            }

            CosmosNetworkMeter.BackendLatencyHistogram.Record(Convert.ToDouble(latencyInMs) / 1000, dimension);
        }

    }
}
