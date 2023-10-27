﻿//------------------------------------------------------------
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
    using System.Threading.Tasks;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.Telemetry.Models;
    using Newtonsoft.Json;

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
            
            StringBuilder stringBuilder = new StringBuilder(ClientTelemetryOptions.PayloadSizeThreshold);
            
            JsonWriter writer = ClientTelemetryPayloadWriter.GetWriterWithSectionStartTag(stringBuilder, properties, "operationInfo");
            
            if (operationInfoSnapshot?.Any() == true)
            {
                foreach (KeyValuePair<OperationInfo, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> entry in operationInfoSnapshot)
                {
                    long lengthNow = stringBuilder.Length;

                    // Latency Metrics is not captured then don't add it in payload
                    string latencyMetrics = string.Empty;
                    if (entry.Value.latency != null && entry.Value.latency.TotalCount > 0)
                    {
                        OperationInfo payloadForLatency = entry.Key;
                        payloadForLatency.MetricInfo = new MetricInfo(ClientTelemetryOptions.RequestLatencyName, ClientTelemetryOptions.RequestLatencyUnit);
                        payloadForLatency.SetAggregators(entry.Value.latency, ClientTelemetryOptions.TicksToMsFactor);

                        latencyMetrics = JsonConvert.SerializeObject(payloadForLatency);
                    }

                    // RequestCharge Metrics is not captured then don't add it in payload
                    string requestChargeMetrics = string.Empty;
                    if (entry.Value.requestcharge != null && entry.Value.requestcharge.TotalCount > 0)
                    {
                        OperationInfo payloadForRequestCharge = entry.Key;
                        payloadForRequestCharge.MetricInfo = new MetricInfo(ClientTelemetryOptions.RequestChargeName, ClientTelemetryOptions.RequestChargeUnit);
                        payloadForRequestCharge.SetAggregators(entry.Value.requestcharge, ClientTelemetryOptions.HistogramPrecisionFactor);

                        requestChargeMetrics = JsonConvert.SerializeObject(payloadForRequestCharge);
                    }

                    int thisSectionLength = latencyMetrics.Length + requestChargeMetrics.Length;
                    if (lengthNow + thisSectionLength > ClientTelemetryOptions.PayloadSizeThreshold)
                    {
                        writer.WriteEndArray();
                        writer.WriteEndObject();

                        await callback.Invoke(stringBuilder.ToString());

                        writer = ClientTelemetryPayloadWriter.GetWriterWithSectionStartTag(stringBuilder, properties, "operationInfo");
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
                    long lengthNow = stringBuilder.Length;

                    string cacheLatencyMetrics = string.Empty;
                    if (entry.Value != null && entry.Value.TotalCount > 0)
                    {
                        CacheRefreshInfo payloadForLatency = entry.Key;
                        payloadForLatency.MetricInfo = new MetricInfo(ClientTelemetryOptions.RequestLatencyName, ClientTelemetryOptions.RequestLatencyUnit);
                        payloadForLatency.SetAggregators(entry.Value, ClientTelemetryOptions.TicksToMsFactor);

                        cacheLatencyMetrics = JsonConvert.SerializeObject(payloadForLatency);
                    }

                    if (lengthNow + cacheLatencyMetrics.Length > ClientTelemetryOptions.PayloadSizeThreshold)
                    {
                        writer.WriteEndArray();
                        writer.WriteEndObject();

                        await callback.Invoke(stringBuilder.ToString());

                        writer = ClientTelemetryPayloadWriter.GetWriterWithSectionStartTag(stringBuilder, properties, "cacheRefreshInfo");
                    }

                    writer.WriteRawValue(cacheLatencyMetrics);
                }
                writer.WriteEndArray();

            }

            if (sampledRequestInfo?.Any() == true)
            {
                writer.WritePropertyName("requestInfo");
                writer.WriteStartArray();
                
                foreach (RequestInfo entry in sampledRequestInfo)
                {
                    long lengthNow = stringBuilder.Length;
                  
                    string latencyMetrics = JsonConvert.SerializeObject(entry);

                    if (lengthNow + latencyMetrics.Length > ClientTelemetryOptions.PayloadSizeThreshold)
                    {
                        writer.WriteEndArray();
                        writer.WriteEndObject();
                        
                        await callback.Invoke(stringBuilder.ToString());

                        writer = ClientTelemetryPayloadWriter.GetWriterWithSectionStartTag(stringBuilder, properties, "requestInfo");
                    }

                    writer.WriteRawValue(latencyMetrics);
                }
                writer.WriteEndArray();
            }
           
            writer.WriteEndObject();

            await callback.Invoke(stringBuilder.ToString());
        }

        private static JsonWriter GetWriterWithSectionStartTag(
            StringBuilder stringBuilder, 
            ClientTelemetryProperties properties, 
            string sectionName)
        {
            stringBuilder.Clear();
            
            StringWriter stringWriter = new StringWriter(stringBuilder);
            
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
