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
    using System.Text;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;

    internal static partial class TraceWriter
    {
        private static class TraceJsonWriter
        {
            public static void WriteTrace(
                IJsonWriter writer,
                ITrace trace,
                bool isRootTrace)
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

                if (isRootTrace)
                {
                    writer.WriteFieldName("Summary");
                    SummaryDiagnostics summaryDiagnostics = new SummaryDiagnostics(trace);
                    summaryDiagnostics.WriteSummaryDiagnostics(writer);
                }
                writer.WriteFieldName("name");
                writer.WriteStringValue(trace.Name);

                if (isRootTrace)
                {
                    writer.WriteFieldName("start datetime");
                    writer.WriteStringValue(trace.StartTime.ToString(TraceWriter.DateTimeFormatString));
                }
                writer.WriteFieldName("duration in milliseconds");
                writer.WriteNumberValue(trace.Duration.TotalMilliseconds);

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
                        WriteTrace(writer, 
                            child, 
                            isRootTrace: false);
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
                writer.WriteNumberValue(doubleValue);
            }
            else if (value is long longValue)
            {
                writer.WriteNumberValue(longValue);
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
                this.WriteDateTimeStringValue(pointOperationStatisticsTraceDatum.ResponseTimeUtc);

                this.jsonWriter.WriteFieldName("StatusCode");
                this.jsonWriter.WriteNumberValue((int)pointOperationStatisticsTraceDatum.StatusCode);

                this.jsonWriter.WriteFieldName("SubStatusCode");
                this.jsonWriter.WriteNumberValue((int)pointOperationStatisticsTraceDatum.SubStatusCode);

                this.jsonWriter.WriteFieldName("RequestCharge");
                this.jsonWriter.WriteNumberValue(pointOperationStatisticsTraceDatum.RequestCharge);

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

                this.WriteRegionsContactedArray("RegionsContacted", clientSideRequestStatisticsTraceDatum.RegionsContacted);
                this.WriteJsonUriArray("FailedReplicas", clientSideRequestStatisticsTraceDatum.FailedReplicas);

                clientSideRequestStatisticsTraceDatum.WriteAddressCachRefreshContent(this.jsonWriter);

                this.jsonWriter.WriteFieldName("AddressResolutionStatistics");
                this.jsonWriter.WriteArrayStart();

                foreach (KeyValuePair<string, ClientSideRequestStatisticsTraceDatum.AddressResolutionStatistics> stat in clientSideRequestStatisticsTraceDatum.EndpointToAddressResolutionStatistics)
                {
                   this.VisitAddressResolutionStatistics(stat.Value);
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
                this.WriteDateTimeStringValue(stat.RequestStartTime);

                jsonWriter.WriteFieldName("DurationInMs");
                jsonWriter.WriteNumberValue(stat.Duration.TotalMilliseconds);

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

            private void VisitAddressResolutionStatistics(
                ClientSideRequestStatisticsTraceDatum.AddressResolutionStatistics addressResolutionStatistics)
            {
                this.jsonWriter.WriteObjectStart();

                this.jsonWriter.WriteFieldName("StartTimeUTC");
                this.WriteDateTimeStringValue(addressResolutionStatistics.StartTime);

                this.jsonWriter.WriteFieldName("EndTimeUTC");
                if (addressResolutionStatistics.EndTime.HasValue)
                {
                    this.WriteDateTimeStringValue(addressResolutionStatistics.EndTime.Value);
                }
                else
                {
                    this.jsonWriter.WriteStringValue("EndTime Never Set.");
                }

                this.jsonWriter.WriteFieldName("TargetEndpoint");
                if (addressResolutionStatistics.TargetEndpoint == null)
                {
                    this.jsonWriter.WriteNullValue();
                }
                else
                {
                    this.jsonWriter.WriteStringValue(addressResolutionStatistics.TargetEndpoint);
                }

                this.jsonWriter.WriteObjectEnd();
            }

            private void VisitStoreResponseStatistics(
                ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics storeResponseStatistics)
            {
                this.jsonWriter.WriteObjectStart();

                this.jsonWriter.WriteFieldName("ResponseTimeUTC");
                this.WriteDateTimeStringValue(storeResponseStatistics.RequestResponseTime);

                this.jsonWriter.WriteFieldName("DurationInMs");
                if (storeResponseStatistics.RequestStartTime.HasValue)
                {
                    TimeSpan latency = storeResponseStatistics.RequestResponseTime - storeResponseStatistics.RequestStartTime.Value;
                    this.jsonWriter.WriteNumberValue(latency.TotalMilliseconds);
                }
                else
                {
                    this.jsonWriter.WriteNullValue();
                }

                this.jsonWriter.WriteFieldName("ResourceType");
                this.jsonWriter.WriteStringValue(storeResponseStatistics.RequestResourceType.ToString());

                this.jsonWriter.WriteFieldName("OperationType");
                this.jsonWriter.WriteStringValue(storeResponseStatistics.RequestOperationType.ToString());

                if (!string.IsNullOrEmpty(storeResponseStatistics.RequestSessionToken))
                {
                    this.jsonWriter.WriteFieldName("RequestSessionToken");
                    this.jsonWriter.WriteStringValue(storeResponseStatistics.RequestSessionToken);
                }

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
                if (this.jsonWriter is IJsonTextWriterExtensions jsonTextWriter)
                {
                    jsonTextWriter.WriteRawJsonValue(Encoding.UTF8.GetBytes(cpuHistoryTraceDatum.Value.ToString()),
                                                     isFieldName: false);
                }
                else
                {
                    throw new NotImplementedException("Writing Raw Json directly to the buffer is currently only supported for text and not for binary, hybridrow");
                }
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
                this.jsonWriter.WriteNumberValue(storeResult.LSN);

                this.jsonWriter.WriteFieldName(nameof(storeResult.PartitionKeyRangeId));
                this.WriteStringValueOrNull(storeResult.PartitionKeyRangeId);

                this.jsonWriter.WriteFieldName(nameof(storeResult.GlobalCommittedLSN));
                this.jsonWriter.WriteNumberValue(storeResult.GlobalCommittedLSN);

                this.jsonWriter.WriteFieldName(nameof(storeResult.ItemLSN));
                this.jsonWriter.WriteNumberValue(storeResult.ItemLSN);

                this.jsonWriter.WriteFieldName(nameof(storeResult.UsingLocalLSN));
                this.jsonWriter.WriteBoolValue(storeResult.UsingLocalLSN);

                this.jsonWriter.WriteFieldName(nameof(storeResult.QuorumAckedLSN));
                this.jsonWriter.WriteNumberValue(storeResult.QuorumAckedLSN);

                this.jsonWriter.WriteFieldName(nameof(storeResult.SessionToken));
                this.WriteStringValueOrNull(storeResult.SessionToken?.ConvertToString());

                this.jsonWriter.WriteFieldName(nameof(storeResult.CurrentWriteQuorum));
                this.jsonWriter.WriteNumberValue(storeResult.CurrentWriteQuorum);

                this.jsonWriter.WriteFieldName(nameof(storeResult.CurrentReplicaSetSize));
                this.jsonWriter.WriteNumberValue(storeResult.CurrentReplicaSetSize);

                this.jsonWriter.WriteFieldName(nameof(storeResult.NumberOfReadRegions));
                this.jsonWriter.WriteNumberValue(storeResult.NumberOfReadRegions);

                this.jsonWriter.WriteFieldName(nameof(storeResult.IsValid));
                this.jsonWriter.WriteBoolValue(storeResult.IsValid);

                this.jsonWriter.WriteFieldName(nameof(storeResult.StorePhysicalAddress));
                this.WriteStringValueOrNull(storeResult.StorePhysicalAddress?.ToString());

                this.jsonWriter.WriteFieldName(nameof(storeResult.RequestCharge));
                this.jsonWriter.WriteNumberValue(storeResult.RequestCharge);

                this.jsonWriter.WriteFieldName(nameof(storeResult.RetryAfterInMs));
                this.WriteStringValueOrNull(storeResult.RetryAfterInMs);

                this.jsonWriter.WriteFieldName("BELatencyInMs");
                this.WriteStringValueOrNull(storeResult.BackendRequestDurationInMs);

                this.WriteJsonUriArray("ReplicaHealthStatuses", storeResult.ReplicaHealthStatuses);

                this.VisitTransportRequestStats(storeResult.TransportRequestStats);

                this.jsonWriter.WriteFieldName("TransportException");
                TransportException transportException = storeResult.Exception?.InnerException as TransportException;
                this.WriteStringValueOrNull(transportException?.Message);

                this.jsonWriter.WriteObjectEnd();
            }

            public void Visit(PartitionKeyRangeCacheTraceDatum partitionKeyRangeCacheTraceDatum)
            {
                this.jsonWriter.WriteObjectStart();

                this.jsonWriter.WriteFieldName("Previous Continuation Token");
                this.WriteStringValueOrNull(partitionKeyRangeCacheTraceDatum.PreviousContinuationToken);

                this.jsonWriter.WriteFieldName("Continuation Token");
                this.WriteStringValueOrNull(partitionKeyRangeCacheTraceDatum.ContinuationToken);

                this.jsonWriter.WriteObjectEnd();
            }

            private void WriteJsonUriArray(string propertyName, IEnumerable<TransportAddressUri> uris)
            {
                this.jsonWriter.WriteFieldName(propertyName);
                this.jsonWriter.WriteArrayStart();

                if (uris != null)
                {
                    foreach (TransportAddressUri contactedReplica in uris)
                    {
                        this.WriteStringValueOrNull(contactedReplica?.ToString());
                    }
                }

                this.jsonWriter.WriteArrayEnd();
            }

            private void WriteJsonUriArray(string propertyName, IEnumerable<string> replicaHealthStatuses)
            {
                this.jsonWriter.WriteFieldName(propertyName);
                this.jsonWriter.WriteArrayStart();

                if (replicaHealthStatuses != null)
                {
                    foreach (string replicaHealthStatus in replicaHealthStatuses)
                    {
                        this.WriteStringValueOrNull(replicaHealthStatus);
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

            private void VisitTransportRequestStats(TransportRequestStats transportRequestStats)
            {
                this.jsonWriter.WriteFieldName("transportRequestTimeline");
                if (transportRequestStats == null)
                {
                    this.jsonWriter.WriteNullValue();
                    return;
                }

                if (this.jsonWriter is IJsonTextWriterExtensions jsonTextWriter)
                {
                    jsonTextWriter.WriteRawJsonValue(Encoding.UTF8.GetBytes(transportRequestStats.ToString()),
                                                     isFieldName: false);
                }
                else
                {
                    throw new NotImplementedException("Writing Raw Json directly to the buffer is currently only supported for text and not for binary, hybridrow");
                }
            }

            /// <summary>
            /// Writes the list of URIs to JSON.
            /// Sequential duplicates are counted and written as a single object to prevent
            /// writing the same URI multiple times.
            /// </summary>
            private void WriteJsonUriArrayWithDuplicatesCounted(string propertyName, IReadOnlyList<TransportAddressUri> uris)
            {
                this.jsonWriter.WriteFieldName(propertyName);
                this.jsonWriter.WriteArrayStart();

                if (uris != null)
                {
                    Dictionary<TransportAddressUri, int> uriCount = new ();
                    foreach (TransportAddressUri transportAddressUri in uris)
                    {
                        if (transportAddressUri == null)
                        {
                            continue;
                        }

                        if (uriCount.ContainsKey(transportAddressUri))
                        {
                            uriCount[transportAddressUri]++;
                        }
                        else
                        {
                            uriCount.Add(transportAddressUri, 1);
                        }
                    }

                    foreach (KeyValuePair<TransportAddressUri, int> contactedCount in uriCount) 
                    {
                        this.jsonWriter.WriteObjectStart();
                        this.jsonWriter.WriteFieldName("Count");
                        this.jsonWriter.WriteNumberValue(contactedCount.Value);
                        this.jsonWriter.WriteFieldName("Uri");
                        this.WriteStringValueOrNull(contactedCount.Key.ToString());
                        this.jsonWriter.WriteObjectEnd();
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

            private void WriteDateTimeStringValue(DateTime value)
            {
                if (value == null)
                {
                    this.jsonWriter.WriteNullValue();
                }
                else
                {
                    this.jsonWriter.WriteStringValue(value.ToString("o", CultureInfo.InvariantCulture));
                }
            }

        }
    }
}
