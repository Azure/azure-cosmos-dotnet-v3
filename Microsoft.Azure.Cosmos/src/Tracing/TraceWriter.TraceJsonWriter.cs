// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;

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

                // Request handler use the base class to create the trace.
                // This makes it pointless to log the caller info because 
                // it is always just the base class info.
                if (trace.Component != TraceComponent.RequestHandler)
                {
                    writer.WriteFieldName("caller info");
                    writer.WriteObjectStart();

                    writer.WriteFieldName("member");
                    writer.WriteStringValue(trace.CallerInfo.MemberName);

                    writer.WriteFieldName("file");
                    writer.WriteStringValue(GetFileNameFromPath(trace.CallerInfo.FilePath));

                    writer.WriteFieldName("line");
                    writer.WriteNumber64Value(trace.CallerInfo.LineNumber);

                    writer.WriteObjectEnd();
                }

                writer.WriteFieldName("start time");
                writer.WriteStringValue(trace.StartTime.ToString("hh:mm:ss:fff"));

                writer.WriteFieldName("duration in milliseconds");
                writer.WriteNumber64Value(trace.Duration.TotalMilliseconds);

                if (trace.Data.Any())
                {
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
                }

                if (trace.Children.Any())
                {
                    writer.WriteFieldName("children");
                    writer.WriteArrayStart();

                    foreach (ITrace child in trace.Children)
                    {
                        WriteTrace(writer, child);
                    }

                    writer.WriteArrayEnd();
                }
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

                this.jsonWriter.WriteFieldName("BELatencyInMs");
                this.WriteStringValueOrNull(pointOperationStatisticsTraceDatum.BELatencyInMs);

                this.jsonWriter.WriteObjectEnd();
            }

            public void Visit(ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum)
            {
                this.jsonWriter.WriteObjectStart();
                this.jsonWriter.WriteFieldName("Id");
                this.jsonWriter.WriteStringValue("AggregatedClientSideRequestStatistics");

                this.WriteJsonUriArrayWithDuplicatesCounted("ContactedReplicas", clientSideRequestStatisticsTraceDatum.ContactedReplicas);

                this.WriteRegionsContactedArray("RegionsContacted", clientSideRequestStatisticsTraceDatum.RegionsContactedWithName);
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
                    this.VisitStoreResponseStatistics(stat);
                }

                this.jsonWriter.WriteArrayEnd();

                if (clientSideRequestStatisticsTraceDatum.HttpResponseStatisticsList.Count > 0)
                {
                    this.jsonWriter.WriteFieldName("HttpResponseStats");
                    this.jsonWriter.WriteArrayStart();

                    foreach (ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics stat in clientSideRequestStatisticsTraceDatum.HttpResponseStatisticsList)
                    {
                        this.VisitHttpResponseStatistics(stat, this.jsonWriter);
                    }

                    this.jsonWriter.WriteArrayEnd();
                }

                this.jsonWriter.WriteObjectEnd();
            }

            private void VisitHttpResponseStatistics(ClientSideRequestStatisticsTraceDatum.HttpResponseStatistics stat, IJsonWriter jsonWriter)
            {
                jsonWriter.WriteObjectStart();

                jsonWriter.WriteFieldName("StartTimeUTC");
                jsonWriter.WriteStringValue(stat.RequestStartTime.ToString("o", CultureInfo.InvariantCulture));

                jsonWriter.WriteFieldName("EndTimeUTC");
                jsonWriter.WriteStringValue(stat.RequestEndTime.ToString("o", CultureInfo.InvariantCulture));

                jsonWriter.WriteFieldName("RequestUri");
                jsonWriter.WriteStringValue(stat.RequestUri.ToString());

                jsonWriter.WriteFieldName("ResourceType");
                jsonWriter.WriteStringValue(stat.ResourceType.ToString());

                jsonWriter.WriteFieldName("HttpMethod");
                jsonWriter.WriteStringValue(stat.HttpMethod.ToString());

                jsonWriter.WriteFieldName("ActivityId");
                this.WriteStringValueOrNull(stat.ActivityId);

                if (stat.Exception != null)
                {
                    jsonWriter.WriteFieldName("ExceptionType");
                    jsonWriter.WriteStringValue(stat.Exception.GetType().ToString());

                    jsonWriter.WriteFieldName("ExceptionMessage");
                    jsonWriter.WriteStringValue(stat.Exception.Message);
                }

                if (stat.HttpResponseMessage != null)
                {
                    jsonWriter.WriteFieldName("StatusCode");
                    jsonWriter.WriteStringValue(stat.HttpResponseMessage.StatusCode.ToString());

                    if (!stat.HttpResponseMessage.IsSuccessStatusCode)
                    {
                        jsonWriter.WriteFieldName("ReasonPhrase");
                        jsonWriter.WriteStringValue(stat.HttpResponseMessage.ReasonPhrase);
                    }
                }

                jsonWriter.WriteObjectEnd();
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

            private void VisitStoreResponseStatistics(
                ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics storeResponseStatistics)
            {
                this.jsonWriter.WriteObjectStart();

                this.jsonWriter.WriteFieldName("ResponseTimeUTC");
                this.jsonWriter.WriteStringValue(storeResponseStatistics.RequestResponseTime.ToString("o", CultureInfo.InvariantCulture));

                this.jsonWriter.WriteFieldName("ResourceType");
                this.jsonWriter.WriteStringValue(storeResponseStatistics.RequestResourceType.ToString());

                this.jsonWriter.WriteFieldName("OperationType");
                this.jsonWriter.WriteStringValue(storeResponseStatistics.RequestOperationType.ToString());

                this.jsonWriter.WriteFieldName("LocationEndpoint");
                this.WriteStringValueOrNull(storeResponseStatistics.LocationEndpoint?.ToString());

                if (storeResponseStatistics.StoreResult != null)
                {
                    this.jsonWriter.WriteFieldName("StoreResult");
                    this.Visit(storeResponseStatistics.StoreResult);
                }

                this.jsonWriter.WriteObjectEnd();
            }

            public void Visit(CpuHistoryTraceDatum cpuHistoryTraceDatum)
            {
                this.jsonWriter.WriteObjectStart();

                this.jsonWriter.WriteFieldName("CPU History");
                this.jsonWriter.WriteStringValue(cpuHistoryTraceDatum.Value.ToString());

                this.jsonWriter.WriteObjectEnd();
            }

            public void Visit(ClientConfigurationTraceDatum clientConfigurationTraceDatum)
            {
                if (this.jsonWriter is IJsonTextWriterExtensions jsonTextWriter)
                {
                    jsonTextWriter.WriteRawJsonValue(clientConfigurationTraceDatum.SerializedJson,
                                                     isFieldName: false);
                }
                else
                {
                    throw new NotImplementedException("Writing Raw Json directly to the buffer is currently only supported for text and not for binary, hybridrow");
                }
            }

            public void Visit(StoreResult storeResult)
            {
                this.jsonWriter.WriteObjectStart();

                this.jsonWriter.WriteFieldName(nameof(storeResult.ActivityId));
                this.WriteStringValueOrNull(storeResult.ActivityId);

                this.jsonWriter.WriteFieldName(nameof(storeResult.StatusCode));
                this.jsonWriter.WriteStringValue(storeResult.StatusCode.ToString());

                this.jsonWriter.WriteFieldName(nameof(storeResult.SubStatusCode));
                this.jsonWriter.WriteStringValue(storeResult.SubStatusCode.ToString());

                this.jsonWriter.WriteFieldName(nameof(storeResult.LSN));
                this.jsonWriter.WriteNumber64Value(storeResult.LSN);

                this.jsonWriter.WriteFieldName(nameof(storeResult.PartitionKeyRangeId));
                this.WriteStringValueOrNull(storeResult.PartitionKeyRangeId);

                this.jsonWriter.WriteFieldName(nameof(storeResult.GlobalCommittedLSN));
                this.jsonWriter.WriteNumber64Value(storeResult.GlobalCommittedLSN);

                this.jsonWriter.WriteFieldName(nameof(storeResult.ItemLSN));
                this.jsonWriter.WriteNumber64Value(storeResult.ItemLSN);

                this.jsonWriter.WriteFieldName(nameof(storeResult.UsingLocalLSN));
                this.jsonWriter.WriteBoolValue(storeResult.UsingLocalLSN);

                this.jsonWriter.WriteFieldName(nameof(storeResult.QuorumAckedLSN));
                this.jsonWriter.WriteNumber64Value(storeResult.QuorumAckedLSN);

                this.jsonWriter.WriteFieldName(nameof(storeResult.SessionToken));
                this.WriteStringValueOrNull(storeResult.SessionToken?.ConvertToString());

                this.jsonWriter.WriteFieldName(nameof(storeResult.CurrentWriteQuorum));
                this.jsonWriter.WriteNumber64Value(storeResult.CurrentWriteQuorum);

                this.jsonWriter.WriteFieldName(nameof(storeResult.CurrentReplicaSetSize));
                this.jsonWriter.WriteNumber64Value(storeResult.CurrentReplicaSetSize);

                this.jsonWriter.WriteFieldName(nameof(storeResult.NumberOfReadRegions));
                this.jsonWriter.WriteNumber64Value(storeResult.NumberOfReadRegions);

                this.jsonWriter.WriteFieldName(nameof(storeResult.IsClientCpuOverloaded));
                this.jsonWriter.WriteBoolValue(storeResult.IsClientCpuOverloaded);

                this.jsonWriter.WriteFieldName(nameof(storeResult.IsValid));
                this.jsonWriter.WriteBoolValue(storeResult.IsValid);

                this.jsonWriter.WriteFieldName(nameof(storeResult.StorePhysicalAddress));
                this.WriteStringValueOrNull(storeResult.StorePhysicalAddress?.ToString());

                this.jsonWriter.WriteFieldName(nameof(storeResult.RequestCharge));
                this.jsonWriter.WriteNumber64Value(storeResult.RequestCharge);

                this.jsonWriter.WriteFieldName("BELatencyInMs");
                this.WriteStringValueOrNull(storeResult.BackendRequestDurationInMs);

                this.jsonWriter.WriteFieldName("TransportException");
                TransportException transportException = storeResult.Exception?.InnerException as TransportException;
                this.WriteStringValueOrNull(transportException?.Message);

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

            private void WriteRegionsContactedArray(string propertyName, IEnumerable<(string, Uri)> uris)
            {
                this.jsonWriter.WriteFieldName(propertyName);
                this.jsonWriter.WriteArrayStart();

                if (uris != null)
                {
                    foreach ((string _, Uri uri) in uris)
                    {
                        this.WriteStringValueOrNull(uri?.ToString());
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
