// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using static Microsoft.Azure.Cosmos.Tracing.TraceData.ClientSideRequestStatisticsTraceDatum;

    internal sealed class CosmosTraceDiagnostics : CosmosDiagnostics
    {
        private readonly Lazy<ServerSideCumulativeMetrics> accumulatedMetrics;
        private readonly List<CosmosTraceDiagnostics> traceDiagnostics;
        private readonly bool isMergedDiagnostics;

        public CosmosTraceDiagnostics(ITrace trace)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            // Need to set to the root trace, since we don't know which layer of the stack the response message was returned from.
            ITrace rootTrace = trace;
            while (rootTrace.Parent != null)
            {
                rootTrace = rootTrace.Parent;
            }

            this.Value = rootTrace;
            this.accumulatedMetrics = new Lazy<ServerSideCumulativeMetrics>(() => PopulateServerSideCumulativeMetrics(this.Value));
            this.isMergedDiagnostics = false;
        }

        private CosmosTraceDiagnostics(List<CosmosTraceDiagnostics> traceDiagnostics, string mergeReason)
        {
            this.traceDiagnostics = traceDiagnostics;
            this.isMergedDiagnostics = true;

            TraceSummary traceSummary = new TraceSummary();
            traceSummary.SetRegionsContacted(this.GetContactedRegions());
            traceSummary.SetFailedRequestCount(this.GetFailedRequestCount());

            this.Value = new MergedTrace(
                new List<ITrace>(this.traceDiagnostics.Select(trace => trace.Value)),
                this.GetStartTimeUtc().Value,
                this.GetClientElapsedTime(),
                traceSummary, 
                mergeReason);           
        }

        public static CosmosTraceDiagnostics MergeDiagnostics(List<CosmosTraceDiagnostics> traceDiagnostics, string mergeReason)
        {
            return new CosmosTraceDiagnostics(traceDiagnostics, mergeReason);
        }

        public ITrace Value { get; }

        public override string ToString()
        {
            if (!this.isMergedDiagnostics)
            {
                return this.ToJsonString();
            }

            return this.MultiDiagnosticsToJsonString();
        }

        public override TimeSpan GetClientElapsedTime()
        {
            if (!this.isMergedDiagnostics)
            {
                return this.Value.Duration;
            }

            DateTime startTime = DateTime.MaxValue;
            DateTime endTime = DateTime.MinValue;
            foreach (CosmosTraceDiagnostics trace in this.traceDiagnostics)
            {
                startTime = startTime < trace.GetStartTimeUtc().Value ? startTime : trace.GetStartTimeUtc().Value;
                endTime = endTime > trace.GetStartTimeUtc().Value + trace.GetClientElapsedTime() 
                    ? endTime 
                    : trace.GetStartTimeUtc().Value + trace.GetClientElapsedTime();
            }
            
            return endTime - startTime;
        }

        public override IReadOnlyList<(string regionName, Uri uri)> GetContactedRegions()
        {
            if (!this.isMergedDiagnostics)
            {
                return this.Value?.Summary?.RegionsContacted;
            }

            HashSet<(string regionName, Uri uri)> contactedRegions = null;
            foreach (CosmosTraceDiagnostics trace in this.traceDiagnostics)
            {
                if (contactedRegions == null)
                {
                    contactedRegions = new HashSet<(string regionName, Uri uri)>(trace.GetContactedRegions());
                }
                else
                {
                    foreach ((string regionName, Uri uri) in trace.GetContactedRegions())
                    {
                        contactedRegions.Add((regionName, uri));
                    }

                }
            }

            return contactedRegions.ToList().AsReadOnly();
        }

        public override ServerSideCumulativeMetrics GetQueryMetrics()
        {
            if (!this.isMergedDiagnostics)
            {
                return this.accumulatedMetrics.Value;
            }

            ServerSideMetricsInternalAccumulator accumulator = new ServerSideMetricsInternalAccumulator();
            foreach (CosmosTraceDiagnostics traceDiagnostics in this.traceDiagnostics)
            {
                ServerSideMetricsTraceExtractor.WalkTraceTreeForQueryMetrics(traceDiagnostics.GetTrace(), accumulator);
            }

            IReadOnlyList<ServerSidePartitionedMetricsInternal> serverSideMetricsList = accumulator.GetPartitionedServerSideMetrics().Select(metrics => new ServerSidePartitionedMetricsInternal(metrics)).ToList();

            ServerSideCumulativeMetrics accumulatedMetrics = new ServerSideCumulativeMetricsInternal(serverSideMetricsList);
            return accumulatedMetrics.PartitionedMetrics.Count != 0 ? accumulatedMetrics : null;
        }

        internal ITrace GetTrace()
        {
            return this.Value;
        }

        internal bool IsGoneExceptionHit()
        {
            if (!this.isMergedDiagnostics)
            {
                return this.WalkTraceTreeForGoneException(this.Value);
            }
            
            bool isGoneExceptionHit = false;
            foreach (CosmosTraceDiagnostics trace in this.traceDiagnostics)
            {
                isGoneExceptionHit |= trace.IsGoneExceptionHit();
            }
            return isGoneExceptionHit;
        }

        private bool WalkTraceTreeForGoneException(ITrace currentTrace)
        {
            if (currentTrace == null)
            {
                return false;
            }

            foreach (object datum in currentTrace.Data.Values)
            {
                if (datum is ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum)
                {
                    foreach (StoreResponseStatistics responseStatistics in clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList)
                    {
                        if (responseStatistics.StoreResult != null && responseStatistics.StoreResult.StatusCode == Documents.StatusCodes.Gone)
                        {
                            return true;
                        }
                    }
                }
            }

            foreach (ITrace childTrace in currentTrace.Children)
            {
                if (this.WalkTraceTreeForGoneException(childTrace))
                {
                    return true;
                }
            }

            return false;
        }

        private string ToJsonString()
        {
            ReadOnlyMemory<byte> utf8String = this.WriteTraceToJsonWriter(JsonSerializationFormat.Text);
            return Encoding.UTF8.GetString(utf8String.Span);
        }

        private ReadOnlyMemory<byte> WriteTraceToJsonWriter(JsonSerializationFormat jsonSerializationFormat)
        {
            IJsonWriter jsonTextWriter = JsonWriter.Create(jsonSerializationFormat);
            TraceWriter.WriteTrace(jsonTextWriter, this.Value);
            return jsonTextWriter.GetResult();
        }

        private static ServerSideCumulativeMetrics PopulateServerSideCumulativeMetrics(ITrace trace)
        {
            ServerSideMetricsInternalAccumulator accumulator = new ServerSideMetricsInternalAccumulator();
            ServerSideMetricsTraceExtractor.WalkTraceTreeForQueryMetrics(trace, accumulator);

            IReadOnlyList<ServerSidePartitionedMetricsInternal> serverSideMetricsList = accumulator.GetPartitionedServerSideMetrics().Select(metrics => new ServerSidePartitionedMetricsInternal(metrics)).ToList();

            ServerSideCumulativeMetrics accumulatedMetrics = new ServerSideCumulativeMetricsInternal(serverSideMetricsList);
            return accumulatedMetrics.PartitionedMetrics.Count != 0 ? accumulatedMetrics : null;
        }

        public override DateTime? GetStartTimeUtc()
        {
            if (!this.isMergedDiagnostics)
            {
                if (this.Value == null || this.Value.StartTime == null)
                {
                    return null;
                }

                return this.Value.StartTime;
            }
            
            DateTime minStartTime = DateTime.MaxValue;
            foreach (CosmosTraceDiagnostics trace in this.traceDiagnostics)
            {
                minStartTime = minStartTime < trace.GetStartTimeUtc().Value ? minStartTime : trace.GetStartTimeUtc().Value;
            }

            return minStartTime == DateTime.MaxValue ? null : minStartTime;
        }

        public override int GetFailedRequestCount()
        {
            if (this.Value == null || this.Value.Summary == null)
            {
                return 0;
            }

            if (!this.isMergedDiagnostics)
            {
                return this.Value.Summary.GetFailedCount();
            }

            int failedRequestCount = 0;
            foreach (CosmosTraceDiagnostics trace in this.traceDiagnostics)
            {
                failedRequestCount += trace.GetFailedRequestCount();
            }
            return failedRequestCount;        
        }

        private string MultiDiagnosticsToJsonString()
        {
            ReadOnlyMemory<byte> utf8String = this.WriteTracesToJsonWriter(JsonSerializationFormat.Text);
            return Encoding.UTF8.GetString(utf8String.Span);
        }

        private ReadOnlyMemory<byte> WriteTracesToJsonWriter(JsonSerializationFormat jsonSerializationFormat)
        {
            IJsonWriter jsonTextWriter = JsonWriter.Create(jsonSerializationFormat);
            TraceWriter.WriteTrace(jsonTextWriter, this.Value);
            return jsonTextWriter.GetResult();
        }
    }
}