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
    using Microsoft.Azure.Cosmos.Tracing;
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
            Exception ex = null)
        {
            if (!IsEnabled || !TryGetDiagnostics(attributes, ex, out ITrace diagnostics))
            {
                DefaultTrace.TraceWarning("NetworkMeter is not enabled or Diagnostics is not available.");
                return;
            }

            SummaryDiagnostics summaryDiagnostics = new SummaryDiagnostics(diagnostics);

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
                                                                                    tcpStats: stat);

                CosmosDbMeterUtil.RecordHistogramMetric<double>(stat.RequestLatency.TotalSeconds,
                                                                dimensionsFunc,
                                                                RequestLatencyHistogram);
                CosmosDbMeterUtil.RecordHistogramMetric<long>(stat?.StoreResult?.TransportRequestStats?.RequestBodySizeInBytes,
                                                                dimensionsFunc,
                                                                RequestBodySizeHistogram);
                CosmosDbMeterUtil.RecordHistogramMetric<long>(stat?.StoreResult?.TransportRequestStats?.ResponseBodySizeInBytes,
                                                                dimensionsFunc,
                                                                ResponseBodySizeHistogram);
                CosmosDbMeterUtil.RecordHistogramMetric<double>(stat?.StoreResult?.BackendRequestDurationInMs,
                                                                dimensionsFunc,
                                                                BackendLatencyHistogram,
                                                                (value) => Convert.ToDouble(value) / 1000);
                CosmosDbMeterUtil.RecordHistogramMetric<double>(CosmosDbMeterUtil.CalculateLatency(
                                                                    stat?.StoreResult?.TransportRequestStats?.channelAcquisitionStartedTime,
                                                                    stat?.StoreResult?.TransportRequestStats?.requestPipelinedTime,
                                                                    stat?.StoreResult?.TransportRequestStats?.requestFailedTime),
                                                                dimensionsFunc, ChannelAquisitionLatencyHistogram);
                CosmosDbMeterUtil.RecordHistogramMetric<double>(CosmosDbMeterUtil.CalculateLatency(
                                                                    stat?.StoreResult?.TransportRequestStats?.requestSentTime,
                                                                    stat?.StoreResult?.TransportRequestStats?.requestReceivedTime,
                                                                    stat?.StoreResult?.TransportRequestStats?.requestFailedTime),
                                                                dimensionsFunc, TransitLatencyHistogram);
                CosmosDbMeterUtil.RecordHistogramMetric<double>(CosmosDbMeterUtil.CalculateLatency(
                                                                    stat?.StoreResult?.TransportRequestStats?.requestReceivedTime,
                                                                    stat?.StoreResult?.TransportRequestStats?.requestCompletedTime,
                                                                    stat?.StoreResult?.TransportRequestStats?.requestFailedTime),
                                                                 dimensionsFunc, ReceivedLatencyHistogram);
            });

            summaryDiagnostics.HttpResponseStatistics.Value.ForEach(stat =>
            {
                Func<KeyValuePair<string, object>[]> dimensionsFunc = 
                            () => DimensionPopulator.PopulateNetworkMeterDimensions(getOperationName(), 
                                                                                    accountName, 
                                                                                    containerName, 
                                                                                    databaseName, 
                                                                                    attributes, 
                                                                                    ex, 
                                                                                    httpStats: stat);

                CosmosDbMeterUtil.RecordHistogramMetric<double>(stat.Duration.TotalSeconds, dimensionsFunc, RequestLatencyHistogram);
                CosmosDbMeterUtil.RecordHistogramMetric<long>(stat.HttpResponseMessage?.RequestMessage?.Content?.Headers?.ContentLength, dimensionsFunc, RequestBodySizeHistogram);
                CosmosDbMeterUtil.RecordHistogramMetric<long>(stat.ResponseContentLength, dimensionsFunc, ResponseBodySizeHistogram);
            });
        }

        private static bool TryGetDiagnostics(OpenTelemetryAttributes attributes, 
            Exception ex,
            out ITrace traces)
        {
            traces = null;

            // Retrieve diagnostics from the exception if applicable
            CosmosDiagnostics diagnostics = ex switch
            {
                CosmosOperationCanceledException cancelEx => cancelEx.Diagnostics,
                CosmosObjectDisposedException disposedEx => disposedEx.Diagnostics,
                CosmosNullReferenceException nullRefEx => nullRefEx.Diagnostics,
                CosmosException cosmosException => cosmosException.Diagnostics,
                _ when attributes != null => attributes.Diagnostics,
                _ => null
            };

            // Ensure diagnostics is not null and cast is valid
            if (diagnostics is CosmosTraceDiagnostics traceDiagnostics)
            {
                traces = traceDiagnostics.Value;
                return true;
            }

            return false;
        }
    }
}
