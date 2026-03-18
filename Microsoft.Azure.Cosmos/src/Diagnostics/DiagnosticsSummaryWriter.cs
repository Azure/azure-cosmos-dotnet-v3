// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Text;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Produces compacted summary JSON from an ITrace tree.
    /// Groups requests by region, keeps first/last in full detail,
    /// and aggregates middle entries by (StatusCode, SubStatusCode).
    /// </summary>
    internal static class DiagnosticsSummaryWriter
    {
        private const string UnknownRegion = "Unknown";

        /// <summary>
        /// Produces the summary JSON string for the given trace.
        /// If the output exceeds maxSizeBytes, returns a truncated indicator.
        /// </summary>
        public static string WriteSummary(
            ITrace trace,
            int maxSizeBytes)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            List<RequestEntry> entries = CollectRequestEntries(trace);

            string summaryJson = BuildSummaryJson(trace, entries);

            if (Encoding.UTF8.GetByteCount(summaryJson) <= maxSizeBytes)
            {
                return summaryJson;
            }

            return BuildTruncatedJson(trace, entries.Count);
        }

        private static List<RequestEntry> CollectRequestEntries(ITrace trace)
        {
            List<RequestEntry> entries = new List<RequestEntry>();
            CollectRequestEntriesRecursive(trace, entries);
            return entries;
        }

        private static void CollectRequestEntriesRecursive(ITrace currentTrace, List<RequestEntry> entries)
        {
            foreach (object datum in currentTrace.Data.Values)
            {
                if (datum is ClientSideRequestStatisticsTraceDatum clientSideStats)
                {
                    foreach (ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics storeStat
                        in clientSideStats.StoreResponseStatisticsList)
                    {
                        if (storeStat.IsSupplementalResponse)
                        {
                            continue;
                        }

                        entries.Add(new RequestEntry(
                            region: storeStat.Region ?? UnknownRegion,
                            statusCode: (int)storeStat.StoreResult.StatusCode,
                            subStatusCode: (int)storeStat.StoreResult.SubStatusCode,
                            requestCharge: storeStat.StoreResult.RequestCharge,
                            durationMs: storeStat.RequestLatency.TotalMilliseconds,
                            requestStartTimeUtc: storeStat.RequestStartTime,
                            endpoint: storeStat.LocationEndpoint?.ToString(),
                            operationType: storeStat.RequestOperationType.ToString(),
                            resourceType: storeStat.RequestResourceType.ToString()));
                    }

                    foreach (ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics httpStat
                        in clientSideStats.HttpResponseStatisticsList)
                    {
                        int statusCode = 0;
                        int subStatusCode = 0;
                        double requestCharge = 0;

                        if (httpStat.HttpResponseMessage != null)
                        {
                            statusCode = (int)httpStat.HttpResponseMessage.StatusCode;
                            subStatusCode = GetHttpSubStatusCode(httpStat);

                            if (httpStat.HttpResponseMessage.Headers.TryGetValues(
                                HttpConstants.HttpHeaders.RequestCharge,
                                out IEnumerable<string> chargeValues))
                            {
                                string chargeStr = chargeValues.FirstOrDefault();
                                if (chargeStr != null)
                                {
                                    double.TryParse(chargeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out requestCharge);
                                }
                            }
                        }

                        entries.Add(new RequestEntry(
                            region: httpStat.Region ?? UnknownRegion,
                            statusCode: statusCode,
                            subStatusCode: subStatusCode,
                            requestCharge: requestCharge,
                            durationMs: httpStat.Duration.TotalMilliseconds,
                            requestStartTimeUtc: httpStat.RequestStartTime,
                            endpoint: httpStat.RequestUri?.Host,
                            operationType: httpStat.HttpMethod?.ToString(),
                            resourceType: httpStat.ResourceType.ToString()));
                    }
                }
            }

            foreach (ITrace childTrace in currentTrace.Children)
            {
                CollectRequestEntriesRecursive(childTrace, entries);
            }
        }

        private static int GetHttpSubStatusCode(
            ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics httpStat)
        {
            if (httpStat.HttpResponseMessage?.Headers != null
                && httpStat.HttpResponseMessage.Headers.TryGetValues(
                    WFConstants.BackendHeaders.SubStatus,
                    out IEnumerable<string> values))
            {
                string first = values.FirstOrDefault();
                if (first != null
                    && int.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out int sub))
                {
                    return sub;
                }
            }

            return 0;
        }

        private static string BuildSummaryJson(ITrace trace, List<RequestEntry> entries)
        {
            IJsonWriter writer = JsonWriter.Create(JsonSerializationFormat.Text);
            writer.WriteObjectStart();
            writer.WriteFieldName("Summary");
            writer.WriteObjectStart();

            writer.WriteFieldName("DiagnosticsVerbosity");
            writer.WriteStringValue("Summary");

            writer.WriteFieldName("TotalDurationMs");
            writer.WriteNumberValue(trace.Duration.TotalMilliseconds);

            double totalRequestCharge = 0;
            foreach (RequestEntry e in entries)
            {
                totalRequestCharge += e.RequestCharge;
            }

            writer.WriteFieldName("TotalRequestCharge");
            writer.WriteNumberValue(totalRequestCharge);

            writer.WriteFieldName("TotalRequestCount");
            writer.WriteNumberValue(entries.Count);

            // Group by region, preserving chronological order within each group
            Dictionary<string, List<RequestEntry>> regionGroups = new Dictionary<string, List<RequestEntry>>();
            List<string> regionOrder = new List<string>();

            foreach (RequestEntry entry in entries.OrderBy(e => e.RequestStartTimeUtc ?? DateTime.MinValue))
            {
                if (!regionGroups.TryGetValue(entry.Region, out List<RequestEntry> group))
                {
                    group = new List<RequestEntry>();
                    regionGroups[entry.Region] = group;
                    regionOrder.Add(entry.Region);
                }

                group.Add(entry);
            }

            writer.WriteFieldName("RegionsSummary");
            writer.WriteArrayStart();

            foreach (string region in regionOrder)
            {
                List<RequestEntry> regionEntries = regionGroups[region];
                WriteRegionSummary(writer, region, regionEntries);
            }

            writer.WriteArrayEnd();

            writer.WriteObjectEnd(); // Summary
            writer.WriteObjectEnd(); // root

            return Encoding.UTF8.GetString(writer.GetResult().Span);
        }

        private static void WriteRegionSummary(
            IJsonWriter writer,
            string region,
            List<RequestEntry> entries)
        {
            writer.WriteObjectStart();

            writer.WriteFieldName("Region");
            writer.WriteStringValue(region);

            double regionRequestCharge = 0;
            foreach (RequestEntry e in entries)
            {
                regionRequestCharge += e.RequestCharge;
            }

            writer.WriteFieldName("RequestCount");
            writer.WriteNumberValue(entries.Count);

            writer.WriteFieldName("TotalRequestCharge");
            writer.WriteNumberValue(regionRequestCharge);

            // First entry (always present)
            writer.WriteFieldName("First");
            WriteRequestEntryDetail(writer, entries[0]);

            // Last entry (only if more than 1)
            if (entries.Count > 1)
            {
                writer.WriteFieldName("Last");
                WriteRequestEntryDetail(writer, entries[entries.Count - 1]);
            }

            // Aggregated groups for middle entries (all except first and last)
            if (entries.Count > 2)
            {
                List<RequestEntry> middleEntries = entries.GetRange(1, entries.Count - 2);

                // Group by (StatusCode, SubStatusCode)
                Dictionary<(int, int), List<RequestEntry>> statusGroups =
                    new Dictionary<(int, int), List<RequestEntry>>();

                foreach (RequestEntry entry in middleEntries)
                {
                    (int, int) key = (entry.StatusCode, entry.SubStatusCode);
                    if (!statusGroups.TryGetValue(key, out List<RequestEntry> group))
                    {
                        group = new List<RequestEntry>();
                        statusGroups[key] = group;
                    }

                    group.Add(entry);
                }

                writer.WriteFieldName("AggregatedGroups");
                writer.WriteArrayStart();

                foreach (KeyValuePair<(int, int), List<RequestEntry>> kvp in statusGroups)
                {
                    WriteAggregatedGroup(writer, kvp.Key.Item1, kvp.Key.Item2, kvp.Value);
                }

                writer.WriteArrayEnd();
            }

            writer.WriteObjectEnd();
        }

        private static void WriteRequestEntryDetail(IJsonWriter writer, RequestEntry entry)
        {
            writer.WriteObjectStart();

            writer.WriteFieldName("StatusCode");
            writer.WriteNumberValue(entry.StatusCode);

            writer.WriteFieldName("SubStatusCode");
            writer.WriteNumberValue(entry.SubStatusCode);

            writer.WriteFieldName("RequestCharge");
            writer.WriteNumberValue(entry.RequestCharge);

            writer.WriteFieldName("DurationMs");
            writer.WriteNumberValue(entry.DurationMs);

            writer.WriteFieldName("Region");
            writer.WriteStringValue(entry.Region);

            if (entry.Endpoint != null)
            {
                writer.WriteFieldName("Endpoint");
                writer.WriteStringValue(entry.Endpoint);
            }

            if (entry.RequestStartTimeUtc.HasValue)
            {
                writer.WriteFieldName("RequestStartTimeUtc");
                writer.WriteStringValue(entry.RequestStartTimeUtc.Value.ToString("o", CultureInfo.InvariantCulture));
            }

            if (entry.OperationType != null)
            {
                writer.WriteFieldName("OperationType");
                writer.WriteStringValue(entry.OperationType);
            }

            if (entry.ResourceType != null)
            {
                writer.WriteFieldName("ResourceType");
                writer.WriteStringValue(entry.ResourceType);
            }

            writer.WriteObjectEnd();
        }

        private static void WriteAggregatedGroup(
            IJsonWriter writer,
            int statusCode,
            int subStatusCode,
            List<RequestEntry> entries)
        {
            writer.WriteObjectStart();

            writer.WriteFieldName("StatusCode");
            writer.WriteNumberValue(statusCode);

            writer.WriteFieldName("SubStatusCode");
            writer.WriteNumberValue(subStatusCode);

            writer.WriteFieldName("Count");
            writer.WriteNumberValue(entries.Count);

            double totalCharge = 0;
            foreach (RequestEntry e in entries)
            {
                totalCharge += e.RequestCharge;
            }

            writer.WriteFieldName("TotalRequestCharge");
            writer.WriteNumberValue(totalCharge);

            // Sort durations for percentile computation
            List<double> durations = new List<double>(entries.Count);
            foreach (RequestEntry e in entries)
            {
                durations.Add(e.DurationMs);
            }

            durations.Sort();

            writer.WriteFieldName("MinDurationMs");
            writer.WriteNumberValue(durations[0]);

            writer.WriteFieldName("MaxDurationMs");
            writer.WriteNumberValue(durations[durations.Count - 1]);

            writer.WriteFieldName("P50DurationMs");
            writer.WriteNumberValue(ComputeP50(durations));

            double avgDuration = 0;
            foreach (double d in durations)
            {
                avgDuration += d;
            }

            avgDuration /= durations.Count;

            writer.WriteFieldName("AvgDurationMs");
            writer.WriteNumberValue(Math.Round(avgDuration, 1));

            writer.WriteObjectEnd();
        }

        private static double ComputeP50(List<double> sortedValues)
        {
            int count = sortedValues.Count;
            if (count == 1)
            {
                return sortedValues[0];
            }

            // For odd count, take the middle element.
            // For even count, take the lower of the two middle elements
            // (matching the Rust SDK's floor-based approach).
            int midIndex = (count - 1) / 2;
            return sortedValues[midIndex];
        }

        private static string BuildTruncatedJson(ITrace trace, int totalRequestCount)
        {
            IJsonWriter writer = JsonWriter.Create(JsonSerializationFormat.Text);
            writer.WriteObjectStart();
            writer.WriteFieldName("Summary");
            writer.WriteObjectStart();

            writer.WriteFieldName("DiagnosticsVerbosity");
            writer.WriteStringValue("Summary");

            writer.WriteFieldName("TotalDurationMs");
            writer.WriteNumberValue(trace.Duration.TotalMilliseconds);

            writer.WriteFieldName("TotalRequestCount");
            writer.WriteNumberValue(totalRequestCount);

            writer.WriteFieldName("Truncated");
            writer.WriteBoolValue(true);

            writer.WriteFieldName("Message");
            writer.WriteStringValue(
                "Summary output truncated to fit size limit. Set DiagnosticsVerbosity to Detailed for full diagnostics.");

            writer.WriteObjectEnd(); // Summary
            writer.WriteObjectEnd(); // root

            return Encoding.UTF8.GetString(writer.GetResult().Span);
        }

        /// <summary>
        /// Internal representation of a single request entry collected from the trace tree.
        /// </summary>
        private readonly struct RequestEntry
        {
            public RequestEntry(
                string region,
                int statusCode,
                int subStatusCode,
                double requestCharge,
                double durationMs,
                DateTime? requestStartTimeUtc,
                string endpoint,
                string operationType,
                string resourceType)
            {
                this.Region = region;
                this.StatusCode = statusCode;
                this.SubStatusCode = subStatusCode;
                this.RequestCharge = requestCharge;
                this.DurationMs = durationMs;
                this.RequestStartTimeUtc = requestStartTimeUtc;
                this.Endpoint = endpoint;
                this.OperationType = operationType;
                this.ResourceType = resourceType;
            }

            public string Region { get; }
            public int StatusCode { get; }
            public int SubStatusCode { get; }
            public double RequestCharge { get; }
            public double DurationMs { get; }
            public DateTime? RequestStartTimeUtc { get; }
            public string Endpoint { get; }
            public string OperationType { get; }
            public string ResourceType { get; }
        }
    }
}
