//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using Newtonsoft.Json;

    internal static class ClientTelemetryPayloadWriter
    {
        private static readonly StringBuilder stringBuilder = new StringBuilder(ClientTelemetryOptions.PayloadSizeThreshold);
        private static readonly StringWriter stringWriter = new StringWriter(stringBuilder);
        
        public static async Task SerializedPayloadChunksAsync(
            ClientTelemetryProperties properties,
            ConcurrentDictionary<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> operationInfoSnapshot,
            ConcurrentDictionary<CacheRefreshInfo, LongConcurrentHistogram> cacheRefreshInfoSnapshot,
            ConcurrentDictionary<RequestInfo, LongConcurrentHistogram> requestInfoSnapshot,
            Func<string, Task> callback)
        {
            if (properties == null)
            {
                throw new ArgumentNullException(nameof(properties));
            }

            JsonWriter writer = ClientTelemetryPayloadWriter.GetWriter(properties, "operationInfo");
            
            if (operationInfoSnapshot != null && operationInfoSnapshot.Count > 0)
            {
                var opsEnumerator = operationInfoSnapshot.GetEnumerator();
                while (opsEnumerator.MoveNext())
                {
                    long lengthNow = stringBuilder.Length;
                    KeyValuePair<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> entry = opsEnumerator.Current;

                    OperationInfo payloadForLatency = entry.Key;
                    payloadForLatency.MetricInfo = new MetricInfo(ClientTelemetryOptions.RequestLatencyName, ClientTelemetryOptions.RequestLatencyUnit);
                    payloadForLatency.SetAggregators(entry.Value.latency, ClientTelemetryOptions.TicksToMsFactor);

                    OperationInfo payloadForRequestCharge = payloadForLatency.Copy();
                    payloadForRequestCharge.MetricInfo = new MetricInfo(ClientTelemetryOptions.RequestChargeName, ClientTelemetryOptions.RequestChargeUnit);
                    payloadForRequestCharge.SetAggregators(entry.Value.requestcharge, ClientTelemetryOptions.HistogramPrecisionFactor);

                    string latencyMetrics = JsonConvert.SerializeObject(payloadForLatency);
                    string requestChargeMetrics = JsonConvert.SerializeObject(payloadForRequestCharge);
                    
                    if (lengthNow + latencyMetrics.Length + requestChargeMetrics.Length > ClientTelemetryOptions.PayloadSizeThreshold)
                    {
                        writer.WriteEndArray();
                        writer.WriteEndObject();

                        await callback.Invoke(stringBuilder.ToString());

                        writer = ClientTelemetryPayloadWriter.GetWriter(properties, "operationInfo");
                    }

                    writer.WriteRawValue(latencyMetrics);
                    writer.WriteRawValue(requestChargeMetrics);
                }

            }
            writer.WriteEndArray();

            if (cacheRefreshInfoSnapshot != null && cacheRefreshInfoSnapshot.Count > 0)
            {
                writer.WritePropertyName("cacheRefreshInfo");
                writer.WriteStartArray();
                var crEnumerator = cacheRefreshInfoSnapshot.GetEnumerator();
                while (crEnumerator.MoveNext())
                {
                    long lengthNow = stringBuilder.Length;

                    KeyValuePair<CacheRefreshInfo, LongConcurrentHistogram> entry = crEnumerator.Current;

                    CacheRefreshInfo payloadForLatency = entry.Key;
                    payloadForLatency.MetricInfo = new MetricInfo(ClientTelemetryOptions.RequestLatencyName, ClientTelemetryOptions.RequestLatencyUnit);
                    payloadForLatency.SetAggregators(entry.Value, ClientTelemetryOptions.TicksToMsFactor);

                    string latencyMetrics = JsonConvert.SerializeObject(payloadForLatency);

                    if (lengthNow + latencyMetrics.Length > ClientTelemetryOptions.PayloadSizeThreshold)
                    {
                        writer.WriteEndArray();
                        writer.WriteEndObject();

                        await callback.Invoke(stringBuilder.ToString());

                        writer = ClientTelemetryPayloadWriter.GetWriter(properties, "cacheRefreshInfo");
                    }

                    writer.WriteRawValue(latencyMetrics);
                }
                writer.WriteEndArray();

            }

            if (requestInfoSnapshot != null && requestInfoSnapshot.Count > 0)
            {
                writer.WritePropertyName("requestInfo");
                writer.WriteStartArray();
                var riEnumerator = requestInfoSnapshot.GetEnumerator();
                while (riEnumerator.MoveNext())
                {
                    long lengthNow = stringBuilder.Length;
                    KeyValuePair<RequestInfo, LongConcurrentHistogram> entry = riEnumerator.Current;

                    MetricInfo metricInfo = new MetricInfo(ClientTelemetryOptions.RequestLatencyName, ClientTelemetryOptions.RequestLatencyUnit);
                    metricInfo.SetAggregators(entry.Value, ClientTelemetryOptions.TicksToMsFactor);

                    RequestInfo payloadForLatency = entry.Key;
                    payloadForLatency.Metrics.Add(metricInfo);
                    string latencyMetrics = JsonConvert.SerializeObject(payloadForLatency);

                    if (lengthNow + latencyMetrics.Length > ClientTelemetryOptions.PayloadSizeThreshold)
                    {
                        writer.WriteEndArray();
                        writer.WriteEndObject();
                        
                        await callback.Invoke(stringBuilder.ToString());

                        writer = ClientTelemetryPayloadWriter.GetWriter(properties, "requestInfo");
                    }

                    writer.WriteRawValue(latencyMetrics);
                }
                writer.WriteEndArray();
            }
           
            writer.WriteEndObject();

            await callback.Invoke(stringBuilder.ToString());
        }

        private static JsonWriter GetWriter(ClientTelemetryProperties properties, string sectionName)
        {
            stringBuilder.Clear();
            
            JsonWriter writer = new JsonTextWriter(stringWriter)
            {
                AutoCompleteOnClose = false
            };

            writer.WriteStartObject();

            properties.Write(writer);

            writer.WritePropertyName(sectionName);

            writer.WriteStartArray();
            return writer;
        }
    }
}
