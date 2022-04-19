namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using System.Runtime.Serialization;

    internal class QueryStatisticsAccumulator : ITraceDatumVisitor
    {
        public readonly QueryStatisticsAccumulatorBuilder queryStatisticsAccumulatorBuilder = new();
        private class RequestTimeline
        {
            public DateTime StartTimeUtc { get; set; }

            public EventType Event { get; set; }
            public double DurationInMs { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            public enum EventType
            {
                Created,
                ChannelAcquisitionStarted,
                Pipelined,
                [EnumMember(Value = "Transit Time")]
                TransitTime,
                Received,
                Completed
            }
        }

        private class TransportStats
        {
            public List<RequestTimeline> RequestTimeline { get; set; }
        }

        public void Visit(QueryMetricsTraceDatum queryMetricsTraceDatum)
        {
            this.queryStatisticsAccumulatorBuilder.queryMetrics.RetrievedDocumentCount = queryMetricsTraceDatum.QueryMetrics.BackendMetrics.RetrievedDocumentCount;
            this.queryStatisticsAccumulatorBuilder.queryMetrics.RetrievedDocumentSize = queryMetricsTraceDatum.QueryMetrics.BackendMetrics.RetrievedDocumentSize;
            this.queryStatisticsAccumulatorBuilder.queryMetrics.OutputDocumentCount = queryMetricsTraceDatum.QueryMetrics.BackendMetrics.OutputDocumentCount;
            this.queryStatisticsAccumulatorBuilder.queryMetrics.OutputDocumentSize = queryMetricsTraceDatum.QueryMetrics.BackendMetrics.OutputDocumentSize;
            this.queryStatisticsAccumulatorBuilder.queryMetrics.TotalQueryExecutionTime = queryMetricsTraceDatum.QueryMetrics.BackendMetrics.TotalTime.TotalMilliseconds;
            this.queryStatisticsAccumulatorBuilder.queryMetrics.DocumentLoadTime = queryMetricsTraceDatum.QueryMetrics.BackendMetrics.DocumentLoadTime.TotalMilliseconds;
            this.queryStatisticsAccumulatorBuilder.queryMetrics.DocumentWriteTime = queryMetricsTraceDatum.QueryMetrics.BackendMetrics.DocumentWriteTime.TotalMilliseconds;
        }

        public void Visit(ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum)
        {
            if (clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList.Count > 0)
            {
                foreach (ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics storeResponse in clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList)
                {
                    if (storeResponse.StoreResult.StatusCode == StatusCodes.Ok)
                    {
                        TransportStats transportStats = JsonConvert.DeserializeObject<TransportStats>(storeResponse.StoreResult.TransportRequestStats.ToString());
                        if (transportStats.RequestTimeline[0].Event == RequestTimeline.EventType.Created)
                        {
                            this.queryStatisticsAccumulatorBuilder.queryMetrics.Created = transportStats.RequestTimeline[0].DurationInMs;
                        }
                        if (transportStats.RequestTimeline[1].Event == RequestTimeline.EventType.ChannelAcquisitionStarted)
                        {
                            this.queryStatisticsAccumulatorBuilder.queryMetrics.ChannelAcquisitionStarted = transportStats.RequestTimeline[1].DurationInMs;
                        }
                        if (transportStats.RequestTimeline[2].Event == RequestTimeline.EventType.Pipelined)
                        {
                            this.queryStatisticsAccumulatorBuilder.queryMetrics.Pipelined = transportStats.RequestTimeline[2].DurationInMs;
                        }
                        if (transportStats.RequestTimeline[3].Event == RequestTimeline.EventType.TransitTime)
                        {
                            this.queryStatisticsAccumulatorBuilder.queryMetrics.TransitTime = transportStats.RequestTimeline[3].DurationInMs;
                        }
                        if (transportStats.RequestTimeline[4].Event == RequestTimeline.EventType.Received)
                        {
                            this.queryStatisticsAccumulatorBuilder.queryMetrics.Received = transportStats.RequestTimeline[4].DurationInMs;
                        }
                        if (transportStats.RequestTimeline[5].Event == RequestTimeline.EventType.Completed)
                        {
                            this.queryStatisticsAccumulatorBuilder.queryMetrics.Completed = transportStats.RequestTimeline[5].DurationInMs;
                        }
                    }
                    if (storeResponse.StoreResult.StatusCode != StatusCodes.Ok)
                    {
                        TransportStats badRequestTransportStats = JsonConvert.DeserializeObject<TransportStats>(storeResponse.StoreResult.TransportRequestStats.ToString());
                        if (badRequestTransportStats.RequestTimeline[0].Event == RequestTimeline.EventType.Created)
                        {
                            this.queryStatisticsAccumulatorBuilder.queryMetrics.BadRequestCreated = badRequestTransportStats.RequestTimeline[0].DurationInMs;
                        }
                        if (badRequestTransportStats.RequestTimeline[1].Event == RequestTimeline.EventType.ChannelAcquisitionStarted)
                        {
                            this.queryStatisticsAccumulatorBuilder.queryMetrics.BadRequestChannelAcquisitionStarted = badRequestTransportStats.RequestTimeline[1].DurationInMs;
                        }
                        if (badRequestTransportStats.RequestTimeline[2].Event == RequestTimeline.EventType.Pipelined)
                        {
                            this.queryStatisticsAccumulatorBuilder.queryMetrics.BadRequestPipelined = badRequestTransportStats.RequestTimeline[2].DurationInMs;
                        }
                        if (badRequestTransportStats.RequestTimeline[3].Event == RequestTimeline.EventType.TransitTime)
                        {
                            this.queryStatisticsAccumulatorBuilder.queryMetrics.BadRequestTransitTime = badRequestTransportStats.RequestTimeline[3].DurationInMs;
                        }
                        if (badRequestTransportStats.RequestTimeline[4].Event == RequestTimeline.EventType.Received)
                        {
                            this.queryStatisticsAccumulatorBuilder.queryMetrics.BadRequestReceived = badRequestTransportStats.RequestTimeline[4].DurationInMs;
                        }
                        if (badRequestTransportStats.RequestTimeline[5].Event == RequestTimeline.EventType.Completed)
                        {
                            this.queryStatisticsAccumulatorBuilder.queryMetrics.BadRequestCompleted = badRequestTransportStats.RequestTimeline[5].DurationInMs;
                        }
                    }
                }
                this.PopulateMetrics();
            }
        }

        public void PopulateMetrics()
        {
            this.queryStatisticsAccumulatorBuilder.QueryMetricsList
                .Add(new QueryMetrics
                {
                    RetrievedDocumentCount = this.queryStatisticsAccumulatorBuilder.queryMetrics.RetrievedDocumentCount,
                    RetrievedDocumentSize = this.queryStatisticsAccumulatorBuilder.queryMetrics.RetrievedDocumentSize,
                    OutputDocumentCount = this.queryStatisticsAccumulatorBuilder.queryMetrics.OutputDocumentCount,
                    OutputDocumentSize = this.queryStatisticsAccumulatorBuilder.queryMetrics.OutputDocumentSize,
                    TotalQueryExecutionTime = this.queryStatisticsAccumulatorBuilder.queryMetrics.TotalQueryExecutionTime,
                    DocumentLoadTime = this.queryStatisticsAccumulatorBuilder.queryMetrics.DocumentLoadTime,
                    DocumentWriteTime = this.queryStatisticsAccumulatorBuilder.queryMetrics.DocumentWriteTime,
                    Created = this.queryStatisticsAccumulatorBuilder.queryMetrics.Created,
                    ChannelAcquisitionStarted = this.queryStatisticsAccumulatorBuilder.queryMetrics.ChannelAcquisitionStarted,
                    Pipelined = this.queryStatisticsAccumulatorBuilder.queryMetrics.Pipelined,
                    TransitTime = this.queryStatisticsAccumulatorBuilder.queryMetrics.TransitTime,
                    Received = this.queryStatisticsAccumulatorBuilder.queryMetrics.Received,
                    Completed = this.queryStatisticsAccumulatorBuilder.queryMetrics.Completed,
                    PocoTimeList = this.queryStatisticsAccumulatorBuilder.queryMetrics.PocoTimeList,
                    GetCosmosElementResponseTimeList = this.queryStatisticsAccumulatorBuilder.queryMetrics.GetCosmosElementResponseTimeList,
                    EndToEndTimeList = this.queryStatisticsAccumulatorBuilder.queryMetrics.EndToEndTimeList,
                    BadRequestCreated = this.queryStatisticsAccumulatorBuilder.queryMetrics.BadRequestCreated,
                    BadRequestChannelAcquisitionStarted = this.queryStatisticsAccumulatorBuilder.queryMetrics.BadRequestChannelAcquisitionStarted,
                    BadRequestPipelined = this.queryStatisticsAccumulatorBuilder.queryMetrics.BadRequestPipelined,
                    BadRequestTransitTime = this.queryStatisticsAccumulatorBuilder.queryMetrics.BadRequestTransitTime,
                    BadRequestReceived = this.queryStatisticsAccumulatorBuilder.queryMetrics.BadRequestReceived,
                    BadRequestCompleted = this.queryStatisticsAccumulatorBuilder.queryMetrics.BadRequestCompleted
                });
        }
        public void Visit(CpuHistoryTraceDatum cpuHistoryTraceDatum)
        {
        }

        public void Visit(ClientConfigurationTraceDatum clientConfigurationTraceDatum)
        {
        }

        public void Visit(PointOperationStatisticsTraceDatum pointOperationStatisticsTraceDatum)
        {
        }

    }
}
