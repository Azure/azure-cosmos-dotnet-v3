//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.Telemetry.Models;

    internal static class ClientTelemetryPayloadWriter
    {
        public static async Task SerializedPayloadChunksAsync(
            ClientTelemetryProperties properties,
            ConcurrentDictionary<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> operationInfoSnapshot,
            ConcurrentDictionary<CacheRefreshInfo, LongConcurrentHistogram> cacheRefreshInfoSnapshot,
            IReadOnlyList<RequestInfo> sampledRequestInfo,
            Func<string, Task> callback)
        {
            if (properties == null)
            {
                throw new ArgumentNullException(nameof(properties));
            }

            MemoryStream stream = new MemoryStream();

            Utf8JsonWriter writer = ClientTelemetryPayloadWriter.GetWriterWithSectionStartTag(stream, properties, "operationInfo");
            
            if (operationInfoSnapshot?.Any() == true)
            {
                foreach (KeyValuePair<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> entry in operationInfoSnapshot)
                {
                    long lengthNow = writer.BytesPending;
                    
                    OperationInfo payloadForLatency = entry.Key;
                    payloadForLatency.MetricInfo = new MetricInfo(ClientTelemetryOptions.RequestLatencyName, ClientTelemetryOptions.RequestLatencyUnit);
                    payloadForLatency.SetAggregators(entry.Value.latency, ClientTelemetryOptions.TicksToMsFactor);

                    OperationInfo payloadForRequestCharge = payloadForLatency.Copy();
                    payloadForRequestCharge.MetricInfo = new MetricInfo(ClientTelemetryOptions.RequestChargeName, ClientTelemetryOptions.RequestChargeUnit);
                    payloadForRequestCharge.SetAggregators(entry.Value.requestcharge, ClientTelemetryOptions.HistogramPrecisionFactor);
                    
                    string latencyMetrics = JsonSerializer.Serialize(payloadForLatency);
                    string requestChargeMetrics = JsonSerializer.Serialize(payloadForRequestCharge);

                    int thisSectionLength = latencyMetrics.Length + requestChargeMetrics.Length;
                    if (lengthNow + thisSectionLength > ClientTelemetryOptions.PayloadSizeThresholdInBytes)
                    {
                        writer.WriteEndArray();
                        writer.WriteEndObject();

                        writer.Flush();

                        await callback.Invoke(Encoding.UTF8.GetString(stream.ToArray()));

                        stream = new MemoryStream();
                        writer = ClientTelemetryPayloadWriter.GetWriterWithSectionStartTag(stream, properties, "operationInfo");
                    }

                    writer.WriteRawValue(latencyMetrics);
                    writer.WriteRawValue(requestChargeMetrics);
                }

            }
            writer.WriteEndArray();

            if (cacheRefreshInfoSnapshot?.Any() == true)
            {
                writer.WritePropertyName("cacheRefreshInfo");
                writer.WriteStartArray();
                
                foreach (KeyValuePair<CacheRefreshInfo, LongConcurrentHistogram> entry in cacheRefreshInfoSnapshot)
                {
                    long lengthNow = writer.BytesPending;
                        
                    CacheRefreshInfo payloadForLatency = entry.Key;
                    payloadForLatency.MetricInfo = new MetricInfo(ClientTelemetryOptions.RequestLatencyName, ClientTelemetryOptions.RequestLatencyUnit);
                    payloadForLatency.SetAggregators(entry.Value, ClientTelemetryOptions.TicksToMsFactor);

                    string latencyMetrics = JsonSerializer.Serialize(payloadForLatency);

                    if (lengthNow + latencyMetrics.Length > ClientTelemetryOptions.PayloadSizeThresholdInBytes)
                    {
                        writer.WriteEndArray();
                        writer.WriteEndObject();

                        writer.Flush();

                        await callback.Invoke(Encoding.UTF8.GetString(stream.ToArray()));
                        stream = new MemoryStream();
                        writer = ClientTelemetryPayloadWriter.GetWriterWithSectionStartTag(stream, properties, "cacheRefreshInfo");
                    }

                    writer.WriteRawValue(latencyMetrics);
                }
                writer.WriteEndArray();

            }

            if (sampledRequestInfo?.Any() == true)
            {
                writer.WritePropertyName("requestInfo");
                writer.WriteStartArray();
                
                foreach (RequestInfo entry in sampledRequestInfo)
                {
                    long lengthNow = writer.BytesPending;
                  
                    string latencyMetrics = JsonSerializer.Serialize(entry);

                    if (lengthNow + latencyMetrics.Length > ClientTelemetryOptions.PayloadSizeThresholdInBytes)
                    {
                        writer.WriteEndArray();
                        writer.WriteEndObject();
                        
                        writer.Flush();

                        await callback.Invoke(Encoding.UTF8.GetString(stream.ToArray()));
                        stream = new MemoryStream();
                        writer = ClientTelemetryPayloadWriter.GetWriterWithSectionStartTag(stream, properties, "requestInfo");
                    }

                    writer.WriteRawValue(latencyMetrics);
                }
                writer.WriteEndArray();
            }
           
            writer.WriteEndObject();

            writer.Flush();

            await callback.Invoke(Encoding.UTF8.GetString(stream.ToArray()));
        }

        private static Utf8JsonWriter GetWriterWithSectionStartTag(
            MemoryStream stream, 
            ClientTelemetryProperties properties, 
            string sectionName)
        {
            Utf8JsonWriter writer = new Utf8JsonWriter(stream);

            writer.WriteStartObject();

            properties.Write(writer);

            writer.WritePropertyName(sectionName);

            writer.WriteStartArray();
            return writer;
        }
    }
}
