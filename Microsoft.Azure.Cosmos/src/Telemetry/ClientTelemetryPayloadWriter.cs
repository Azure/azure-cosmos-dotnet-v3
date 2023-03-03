//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using Newtonsoft.Json;
    
    internal static class ClientTelemetryPayloadWriter
    {
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
            
            IJsonTextWriterExtensions writer = ClientTelemetryPayloadWriter.ResetWriter(properties, "operationInfo");
            
            if (operationInfoSnapshot != null && operationInfoSnapshot.Count > 0)
            {
                var opsEnumerator = operationInfoSnapshot.GetEnumerator();
                while (opsEnumerator.MoveNext())
                {
                    long lengthNow = writer.CurrentLength;
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
                        writer.WriteArrayEnd();
                        writer.WriteObjectEnd();

                        await callback.Invoke(Encoding.UTF8.GetString(writer.GetResult().Span));

                        writer = ClientTelemetryPayloadWriter.ResetWriter(properties, "operationInfo");
                    }

                    writer.WriteRawJsonValue(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(latencyMetrics)), false);
                    writer.WriteRawJsonValue(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(requestChargeMetrics)), false);
                }

            }
            writer.WriteArrayEnd();

            if (cacheRefreshInfoSnapshot != null && cacheRefreshInfoSnapshot.Count > 0)
            {
                writer.WriteFieldName("cacheRefreshInfo");
                writer.WriteArrayStart();
                var crEnumerator = cacheRefreshInfoSnapshot.GetEnumerator();
                while (crEnumerator.MoveNext())
                {
                    long lengthNow = writer.CurrentLength;

                    KeyValuePair<CacheRefreshInfo, LongConcurrentHistogram> entry = crEnumerator.Current;

                    CacheRefreshInfo payloadForLatency = entry.Key;
                    payloadForLatency.MetricInfo = new MetricInfo(ClientTelemetryOptions.RequestLatencyName, ClientTelemetryOptions.RequestLatencyUnit);
                    payloadForLatency.SetAggregators(entry.Value, ClientTelemetryOptions.TicksToMsFactor);

                    string latencyMetrics = JsonConvert.SerializeObject(payloadForLatency);

                    if (lengthNow + latencyMetrics.Length > ClientTelemetryOptions.PayloadSizeThreshold)
                    {
                        writer.WriteArrayEnd();
                        writer.WriteObjectEnd();

                        await callback.Invoke(Encoding.UTF8.GetString(writer.GetResult().Span));

                        writer = ClientTelemetryPayloadWriter.ResetWriter(properties, "cacheRefreshInfo");
                    }

                    writer.WriteRawJsonValue(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(latencyMetrics)), false);
                }
                writer.WriteArrayEnd();

            }

            if (requestInfoSnapshot != null && requestInfoSnapshot.Count > 0)
            {
                writer.WriteFieldName("requestInfo");
                writer.WriteArrayStart();
                var riEnumerator = requestInfoSnapshot.GetEnumerator();
                while (riEnumerator.MoveNext())
                {
                    long lengthNow = writer.CurrentLength;
                    KeyValuePair<RequestInfo, LongConcurrentHistogram> entry = riEnumerator.Current;

                    MetricInfo metricInfo = new MetricInfo(ClientTelemetryOptions.RequestLatencyName, ClientTelemetryOptions.RequestLatencyUnit);
                    metricInfo.SetAggregators(entry.Value, ClientTelemetryOptions.TicksToMsFactor);

                    RequestInfo payloadForLatency = entry.Key;
                    payloadForLatency.Metrics.Add(metricInfo);
                    string latencyMetrics = JsonConvert.SerializeObject(payloadForLatency);

                    if (lengthNow + latencyMetrics.Length > ClientTelemetryOptions.PayloadSizeThreshold)
                    {
                        writer.WriteArrayEnd();
                        writer.WriteObjectEnd();

                        await callback.Invoke(Encoding.UTF8.GetString(writer.GetResult().Span));

                        writer = ClientTelemetryPayloadWriter.ResetWriter(properties, "requestInfo");
                    }

                    writer.WriteRawJsonValue(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(latencyMetrics)), false);
                }
                writer.WriteArrayEnd();
            }
           
            writer.WriteObjectEnd();

            await callback.Invoke(Encoding.UTF8.GetString(writer.GetResult().Span));
        }

        private static IJsonTextWriterExtensions ResetWriter(ClientTelemetryProperties properties, string sectionName)
        {
            IJsonTextWriterExtensions writer = (IJsonTextWriterExtensions)Json.JsonWriter.Create(JsonSerializationFormat.Text);

            writer.WriteObjectStart();

            properties.Write(writer);

            writer.WriteFieldName(sectionName);

            writer.WriteArrayStart();
            return writer;
        }
    }
}
