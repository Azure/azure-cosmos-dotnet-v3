// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;

    internal static partial class TraceWriter
    {
        private static class TraceJsonWriter
        {
            public static void WriteTrace(
                IJsonWriter writer,
                ITrace trace)
            {
                if (writer == null)
                {
                    throw new ArgumentNullException(nameof(writer));
                }

                if (trace == null)
                {
                    throw new ArgumentNullException(nameof(trace));
                }

                writer.WriteObjectStart();

                writer.WriteFieldName("name");
                writer.WriteStringValue(trace.Name);

                writer.WriteFieldName("id");
                writer.WriteStringValue(trace.Id.ToString());

                writer.WriteFieldName("component");
                writer.WriteStringValue(trace.Component.ToString());

#if INTERNAL
                writer.WriteFieldName("caller information");
                writer.WriteObjectStart();

                writer.WriteFieldName("member name");
                writer.WriteStringValue(trace.CallerInfo.MemberName);

                writer.WriteFieldName("file path");
                writer.WriteStringValue(trace.CallerInfo.FilePath);

                writer.WriteFieldName("line number");
                writer.WriteNumber64Value(trace.CallerInfo.LineNumber);

                writer.WriteObjectEnd();
#endif

                writer.WriteFieldName("start time");
                writer.WriteStringValue(trace.StartTime.ToString("hh:mm:ss:fff"));

                writer.WriteFieldName("duration in milliseconds");
                writer.WriteNumber64Value(trace.Duration.TotalMilliseconds);

                writer.WriteFieldName("data");
                writer.WriteObjectStart();

                foreach (KeyValuePair<string, object> kvp in trace.Data)
                {
                    string key = kvp.Key;
                    object value = kvp.Value;

                    writer.WriteFieldName(key);
                    WriteTraceDatum(writer, value);
                }

                writer.WriteObjectEnd();

                writer.WriteFieldName("children");
                writer.WriteArrayStart();

                foreach (ITrace child in trace.Children)
                {
                    WriteTrace(writer, child);
                }

                writer.WriteArrayEnd();

                writer.WriteObjectEnd();
            }
        }

        private static void WriteTraceDatum(IJsonWriter writer, object value)
        {
            if (value is TraceDatum traceDatum)
            {
                TraceDatumJsonWriter traceJsonWriter = new TraceDatumJsonWriter(writer);
                traceDatum.Accept(traceJsonWriter);
            }
            else if (value is double doubleValue)
            {
                writer.WriteNumber64Value(doubleValue);
            }
            else if (value is long longValue)
            {
                writer.WriteNumber64Value(longValue);
            }
            else if (value is IEnumerable<object> enumerable)
            {
                writer.WriteArrayStart();

                foreach (object item in enumerable)
                {
                    WriteTraceDatum(writer, item);
                }

                writer.WriteArrayEnd();
            }
            else if (value is IDictionary<string, object> dictionary)
            {
                writer.WriteObjectStart();

                foreach (KeyValuePair<string, object> kvp in dictionary)
                {
                    writer.WriteFieldName(kvp.Key);
                    WriteTraceDatum(writer, kvp.Value);
                }

                writer.WriteObjectEnd();
            }
            else if (value is string stringValue)
            {
                writer.WriteStringValue(stringValue);
            }
            else
            {
                writer.WriteStringValue(value.ToString());
            }
        }

        private sealed class TraceDatumJsonWriter : ITraceDatumVisitor
        {
            private readonly IJsonWriter jsonWriter;

            public TraceDatumJsonWriter(IJsonWriter jsonWriter)
            {
                this.jsonWriter = jsonWriter ?? throw new ArgumentNullException(nameof(jsonWriter));
            }

            public void Visit(QueryMetricsTraceDatum queryMetricsTraceDatum)
            {
                this.jsonWriter.WriteStringValue(queryMetricsTraceDatum.QueryMetrics.ToString());
            }

            public void Visit(PointOperationStatisticsTraceDatum pointOperationStatisticsTraceDatum)
            {
                this.jsonWriter.WriteObjectStart();
                this.jsonWriter.WriteFieldName("Id");
                this.jsonWriter.WriteStringValue("PointOperationStatistics");

                this.jsonWriter.WriteFieldName("ActivityId");
                this.WriteStringValueOrNull(pointOperationStatisticsTraceDatum.ActivityId);

                this.jsonWriter.WriteFieldName("ResponseTimeUtc");
                this.jsonWriter.WriteStringValue(pointOperationStatisticsTraceDatum.ResponseTimeUtc.ToString("o", CultureInfo.InvariantCulture));

                this.jsonWriter.WriteFieldName("StatusCode");
                this.jsonWriter.WriteNumber64Value((int)pointOperationStatisticsTraceDatum.StatusCode);

                this.jsonWriter.WriteFieldName("SubStatusCode");
                this.jsonWriter.WriteNumber64Value((int)pointOperationStatisticsTraceDatum.SubStatusCode);

                this.jsonWriter.WriteFieldName("RequestCharge");
                this.jsonWriter.WriteNumber64Value(pointOperationStatisticsTraceDatum.RequestCharge);

                this.jsonWriter.WriteFieldName("RequestUri");
                this.WriteStringValueOrNull(pointOperationStatisticsTraceDatum.RequestUri);

                this.jsonWriter.WriteFieldName("ErrorMessage");
                this.WriteStringValueOrNull(pointOperationStatisticsTraceDatum.ErrorMessage);

                this.jsonWriter.WriteFieldName("RequestSessionToken");
                this.WriteStringValueOrNull(pointOperationStatisticsTraceDatum.RequestSessionToken);

                this.jsonWriter.WriteFieldName("ResponseSessionToken");
                this.WriteStringValueOrNull(pointOperationStatisticsTraceDatum.ResponseSessionToken);

                this.jsonWriter.WriteObjectEnd();
            }

            public void Visit(ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum)
            {
                this.jsonWriter.WriteObjectStart();
                this.jsonWriter.WriteFieldName("Id");
                this.jsonWriter.WriteStringValue("AggregatedClientSideRequestStatistics");

                this.WriteJsonUriArrayWithDuplicatesCounted("ContactedReplicas", clientSideRequestStatisticsTraceDatum.ContactedReplicas);

                this.WriteJsonUriArray("RegionsContacted", clientSideRequestStatisticsTraceDatum.RegionsContacted);
                this.WriteJsonUriArray("FailedReplicas", clientSideRequestStatisticsTraceDatum.FailedReplicas);

                this.jsonWriter.WriteFieldName("AddressResolutionStatistics");
                this.jsonWriter.WriteArrayStart();

                foreach (ClientSideRequestStatisticsTraceDatum.AddressResolutionStatistics stat in clientSideRequestStatisticsTraceDatum.EndpointToAddressResolutionStatistics.Values)
                {
                    VisitAddressResolutionStatistics(stat, this.jsonWriter);
                }

                this.jsonWriter.WriteArrayEnd();

                this.jsonWriter.WriteFieldName("StoreResponseStatistics");
                this.jsonWriter.WriteArrayStart();

                foreach (ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics stat in clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList)
                {
                    VisitStoreResponseStatistics(stat, this.jsonWriter);
                }

                this.jsonWriter.WriteArrayEnd();

                this.jsonWriter.WriteObjectEnd();
            }

            private static void VisitAddressResolutionStatistics(
                ClientSideRequestStatisticsTraceDatum.AddressResolutionStatistics addressResolutionStatistics,
                IJsonWriter jsonWriter)
            {
                jsonWriter.WriteObjectStart();

                jsonWriter.WriteFieldName("StartTimeUTC");
                jsonWriter.WriteStringValue(addressResolutionStatistics.StartTime.ToString("o", CultureInfo.InvariantCulture));

                jsonWriter.WriteFieldName("EndTimeUTC");
                if (addressResolutionStatistics.EndTime.HasValue)
                {
                    jsonWriter.WriteStringValue(addressResolutionStatistics.EndTime.Value.ToString("o", CultureInfo.InvariantCulture));
                }
                else
                {
                    jsonWriter.WriteStringValue("EndTime Never Set.");
                }

                jsonWriter.WriteFieldName("TargetEndpoint");
                if (addressResolutionStatistics.TargetEndpoint == null)
                {
                    jsonWriter.WriteNullValue();
                }
                else
                {
                    jsonWriter.WriteStringValue(addressResolutionStatistics.TargetEndpoint);
                }

                jsonWriter.WriteObjectEnd();
            }

            private static void VisitStoreResponseStatistics(
                ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics storeResponseStatistics,
                IJsonWriter jsonWriter)
            {
                jsonWriter.WriteObjectStart();

                jsonWriter.WriteFieldName("ResponseTimeUTC");
                jsonWriter.WriteStringValue(storeResponseStatistics.RequestResponseTime.ToString("o", CultureInfo.InvariantCulture));

                jsonWriter.WriteFieldName("ResourceType");
                jsonWriter.WriteStringValue(storeResponseStatistics.RequestResourceType.ToString());

                jsonWriter.WriteFieldName("OperationType");
                jsonWriter.WriteStringValue(storeResponseStatistics.RequestOperationType.ToString());

                jsonWriter.WriteFieldName("LocationEndpoint");
                if (storeResponseStatistics.LocationEndpoint == null)
                {
                    jsonWriter.WriteNullValue();
                }
                else
                {
                    jsonWriter.WriteStringValue(storeResponseStatistics.LocationEndpoint.ToString());
                }

                if (storeResponseStatistics.StoreResult != null)
                {
                    jsonWriter.WriteFieldName("StoreResult");
                    jsonWriter.WriteStringValue(storeResponseStatistics.StoreResult.ToString());
                }

                jsonWriter.WriteObjectEnd();
            }

            public void Visit(CpuHistoryTraceDatum cpuHistoryTraceDatum)
            {
                this.jsonWriter.WriteObjectStart();

                this.jsonWriter.WriteFieldName("CPU History");
                this.jsonWriter.WriteStringValue(cpuHistoryTraceDatum.Value.ToString());

                this.jsonWriter.WriteObjectEnd();
            }

            private void WriteJsonUriArray(string propertyName, IEnumerable<Uri> uris)
            {
                this.jsonWriter.WriteFieldName(propertyName);
                this.jsonWriter.WriteArrayStart();

                if (uris != null)
                {
                    foreach (Uri contactedReplica in uris)
                    {
                        this.WriteStringValueOrNull(contactedReplica?.ToString());
                    }
                }

                this.jsonWriter.WriteArrayEnd();
            }

            /// <summary>
            /// Writes the list of URIs to JSON.
            /// Sequential duplicates are counted and written as a single object to prevent
            /// writing the same URI multiple times.
            /// </summary>
            private void WriteJsonUriArrayWithDuplicatesCounted(string propertyName, IReadOnlyList<Uri> uris)
            {
                this.jsonWriter.WriteFieldName(propertyName);
                this.jsonWriter.WriteArrayStart();

                if (uris != null)
                {
                    Uri previous = null;
                    int duplicateCount = 1;
                    int totalCount = uris.Count;
                    for (int i = 0; i < totalCount; i++)
                    {
                        Uri contactedReplica = uris[i];
                        if (contactedReplica == null)
                        {
                            continue;
                        }

                        if (contactedReplica.Equals(previous))
                        {
                            duplicateCount++;
                            // Don't continue for last link so it get's printed
                            if (i < totalCount - 1)
                            {
                                continue;
                            }
                        }

                        // The URI is not a duplicate.
                        // Write previous URI and count.
                        // Then update them to the new URI and count
                        if (previous != null)
                        {
                            this.jsonWriter.WriteObjectStart();
                            this.jsonWriter.WriteFieldName("Count");
                            this.jsonWriter.WriteNumber64Value(duplicateCount);
                            this.jsonWriter.WriteFieldName("Uri");
                            this.WriteStringValueOrNull(contactedReplica?.ToString());
                            this.jsonWriter.WriteObjectEnd();
                        }

                        previous = contactedReplica;
                        duplicateCount = 1;
                    }
                }

                this.jsonWriter.WriteArrayEnd();
            }

            private void WriteStringValueOrNull(string value)
            {
                if (value == null)
                {
                    this.jsonWriter.WriteNullValue();
                }
                else
                {
                    this.jsonWriter.WriteStringValue(value);
                }
            }
        }
    }
}
