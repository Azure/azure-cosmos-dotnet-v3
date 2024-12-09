// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using System.Net.Http;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using static Microsoft.Azure.Cosmos.Tracing.TraceData.ClientSideRequestStatisticsTraceDatum;

    internal static class CosmosDbMeterUtil
    {
        internal static void RecordHistogramMetric<T>(
            object value,
            Func<KeyValuePair<string, object>[]> dimensionsFunc,
            Histogram<T> histogram,
            Func<object, T> converter = null)
            where T : struct
        {
            if (histogram == null || !histogram.Enabled || value == null)
            {
                return;
            }

            try
            {
                T convertedValue = converter != null ? converter(value) : (T)value;
                histogram.Record(convertedValue, dimensionsFunc());
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceWarning($"Failed to record metric. {ex}");
            }
        }

        internal static double? CalculateLatency(
          TimeSpan? start,
          TimeSpan? end,
          TimeSpan? failed)
        {
            TimeSpan? requestend = end ?? failed;
            return start.HasValue && requestend.HasValue ? (requestend.Value - start.Value).TotalSeconds : (double?)null;
        }

        internal static bool TryGetDiagnostics(OpenTelemetryAttributes attributes,
          Exception ex,
          out CosmosTraceDiagnostics traces)
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
            if (diagnostics != null && diagnostics is CosmosTraceDiagnostics traceDiagnostics)
            {
                traces = traceDiagnostics;
                return true;
            }

            return false;
        }

        internal static bool TryOperationMetricsValues(
             OpenTelemetryAttributes attributes,
             Exception ex,
             out OperationMetricData values)
        {
            // Attempt to cast the exception to CosmosException
            CosmosException cosmosException = ex as CosmosException;

            // Retrieve item count and request charge, prioritizing attributes if available
            string itemCount = attributes?.ItemCount ?? cosmosException?.Headers?.ItemCount;
            double? requestCharge = attributes?.RequestCharge ?? cosmosException?.Headers?.RequestCharge;

            // If neither value is available, return false
            if (itemCount == null && requestCharge == null)
            {
                values = null;
                return false;
            }

            // Create the OperationMetricValue instance
            values = new OperationMetricData(itemCount, requestCharge);

            return true;
        }

        internal static bool TryNetworkMetricsValues(
            StoreResponseStatistics stats,
            out NetworkMetricData values)
        {
            double latency = stats.RequestLatency.TotalSeconds;
            long? requestBodySize = stats?.StoreResult?.TransportRequestStats?.RequestBodySizeInBytes;
            long? responseBodySize = stats?.StoreResult?.TransportRequestStats?.ResponseBodySizeInBytes;
            double backendLatency = Convert.ToDouble(stats?.StoreResult?.BackendRequestDurationInMs);
            double? channelAcquisitionLatency = CosmosDbMeterUtil.CalculateLatency(
                                                stats?.StoreResult?.TransportRequestStats?.channelAcquisitionStartedTime,
                                                stats?.StoreResult?.TransportRequestStats?.requestPipelinedTime,
                                                stats?.StoreResult?.TransportRequestStats?.requestFailedTime);
            double? transitTimeLatency = CosmosDbMeterUtil.CalculateLatency(
                                                stats?.StoreResult?.TransportRequestStats?.requestSentTime,
                                                stats?.StoreResult?.TransportRequestStats?.requestReceivedTime,
                                                stats?.StoreResult?.TransportRequestStats?.requestFailedTime);
            double? receivedLatency = CosmosDbMeterUtil.CalculateLatency(
                                                stats?.StoreResult?.TransportRequestStats?.requestReceivedTime,
                                                stats?.StoreResult?.TransportRequestStats?.requestCompletedTime,
                                                stats?.StoreResult?.TransportRequestStats?.requestFailedTime);

            values = new NetworkMetricData(latency, requestBodySize, responseBodySize, backendLatency, channelAcquisitionLatency, transitTimeLatency, receivedLatency);
            
            return true;
        }

        internal static NetworkMetricData GetNetworkMetricsValues(
            HttpResponseStatistics stats)
        {
            double latency = stats.Duration.TotalSeconds;
            long? requestBodySize = GetPayloadSize(stats.HttpResponseMessage?.RequestMessage?.Content);
            long? responseBodySize = stats.ResponseContentLength ?? GetPayloadSize(stats.HttpResponseMessage?.Content);

            return new NetworkMetricData(latency, requestBodySize, responseBodySize);
        }

        private static long GetPayloadSize(HttpContent content)
        {
            if (content == null)
            {
                return 0;
            }

            long contentLength = 0;
            try
            {
                if (content.Headers != null && content.Headers.ContentLength != null)
                {
                    contentLength = content.Headers.ContentLength.Value;
                }
            }
            catch (ObjectDisposedException)
            {
                // ignore and return content length as 0
            }

            return contentLength;
        }

        internal static string[] GetRegions(CosmosDiagnostics diagnostics)
        {
            if (diagnostics?.GetContactedRegions() is not IReadOnlyList<(string regionName, Uri uri)> contactedRegions)
            {
                return null;
            }

            return contactedRegions
                .Select(region => region.regionName)
                .Distinct()
                .ToArray();
        }

        internal static int? GetStatusCode(OpenTelemetryAttributes attributes,
          Exception ex)
        {
            return ex switch
            {
                CosmosException cosmosException => (int)cosmosException.StatusCode,
                _ when attributes != null => (int)attributes.StatusCode,
                _ => null
            };
        }

        internal static int? GetSubStatusCode(OpenTelemetryAttributes attributes,
            Exception ex)
        {
            return ex switch
            {
                CosmosException cosmosException => (int)cosmosException.SubStatusCode,
                _ when attributes != null => (int)attributes.SubStatusCode,
                _ => null
            };
        }

    }
}
