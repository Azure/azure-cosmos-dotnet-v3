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

    class QueryStatisticsAccumulator : ITraceDatumVisitor
    {
        public readonly ImmutableQueryStatisticsAccumulator.QueryStatisticsAccumulatorBuilder queryStatisticsAccumulatorBuilder = new();
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
            this.queryStatisticsAccumulatorBuilder
                .AddRetrievedDocumentCount(queryMetricsTraceDatum.QueryMetrics.BackendMetrics.RetrievedDocumentCount)
                .AddRetrievedDocumentSize(queryMetricsTraceDatum.QueryMetrics.BackendMetrics.RetrievedDocumentSize)
                .AddOutputDocumentCount(queryMetricsTraceDatum.QueryMetrics.BackendMetrics.OutputDocumentCount)
                .AddOutputDocumentSize(queryMetricsTraceDatum.QueryMetrics.BackendMetrics.OutputDocumentSize)
                .AddTotalQueryExecutionTime(queryMetricsTraceDatum.QueryMetrics.BackendMetrics.TotalTime.TotalMilliseconds)
                .AddDocumentLoadTime(queryMetricsTraceDatum.QueryMetrics.BackendMetrics.DocumentLoadTime.TotalMilliseconds)
                .AddDocumentWriteTime(queryMetricsTraceDatum.QueryMetrics.BackendMetrics.DocumentWriteTime.TotalMilliseconds)
                .Build();
        }

        public void Visit(PointOperationStatisticsTraceDatum pointOperationStatisticsTraceDatum)
        {
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
                            this.queryStatisticsAccumulatorBuilder
                                .AddCreated(transportStats.RequestTimeline[0].DurationInMs);
                        }
                        if (transportStats.RequestTimeline[1].Event == RequestTimeline.EventType.ChannelAcquisitionStarted)
                        {
                            this.queryStatisticsAccumulatorBuilder
                                .AddChannelAcquisitionStarted(transportStats.RequestTimeline[1].DurationInMs);
                        }
                        if (transportStats.RequestTimeline[2].Event == RequestTimeline.EventType.Pipelined)
                        {
                            this.queryStatisticsAccumulatorBuilder
                                .AddPipelined(transportStats.RequestTimeline[2].DurationInMs);
                        }
                        if (transportStats.RequestTimeline[3].Event == RequestTimeline.EventType.TransitTime)
                        {
                            this.queryStatisticsAccumulatorBuilder
                                   .AddTransitTime(transportStats.RequestTimeline[3].DurationInMs);
                        }
                        if (transportStats.RequestTimeline[4].Event == RequestTimeline.EventType.Received)
                        {
                            this.queryStatisticsAccumulatorBuilder
                                   .AddReceived(transportStats.RequestTimeline[4].DurationInMs);
                        }
                        if (transportStats.RequestTimeline[5].Event == RequestTimeline.EventType.Completed)
                        {
                            this.queryStatisticsAccumulatorBuilder
                                .AddCompleted(transportStats.RequestTimeline[5].DurationInMs);
                        }
                    }
                    if (storeResponse.StoreResult.StatusCode != StatusCodes.Ok)
                    {
                        TransportStats badRequestTransportStats = JsonConvert.DeserializeObject<TransportStats>(storeResponse.StoreResult.TransportRequestStats.ToString());
                        if (badRequestTransportStats.RequestTimeline[0].Event == RequestTimeline.EventType.Created)
                        {
                            this.queryStatisticsAccumulatorBuilder
                                .AddBadRequestCreated(badRequestTransportStats.RequestTimeline[0].DurationInMs);
                        }
                        if (badRequestTransportStats.RequestTimeline[1].Event == RequestTimeline.EventType.ChannelAcquisitionStarted)
                        {
                            this.queryStatisticsAccumulatorBuilder
                                .AddBadRequestChannelAcquisitionStarted(badRequestTransportStats.RequestTimeline[1].DurationInMs);
                        }
                        if (badRequestTransportStats.RequestTimeline[2].Event == RequestTimeline.EventType.Pipelined)
                        {
                            this.queryStatisticsAccumulatorBuilder
                                .AddBadRequestPipelined(badRequestTransportStats.RequestTimeline[2].DurationInMs);
                        }
                        if (badRequestTransportStats.RequestTimeline[3].Event == RequestTimeline.EventType.TransitTime)
                        {
                            this.queryStatisticsAccumulatorBuilder
                                .AddBadRequestTransitTime(badRequestTransportStats.RequestTimeline[3].DurationInMs);
                        }
                        if (badRequestTransportStats.RequestTimeline[4].Event == RequestTimeline.EventType.Received)
                        {
                            this.queryStatisticsAccumulatorBuilder
                                .AddBadRequestReceived(badRequestTransportStats.RequestTimeline[4].DurationInMs);
                        }
                        if (badRequestTransportStats.RequestTimeline[5].Event == RequestTimeline.EventType.Completed)
                        {
                            this.queryStatisticsAccumulatorBuilder
                                .AddBadRequestCompleted(badRequestTransportStats.RequestTimeline[5].DurationInMs);
                        }
                    }
                }

            }
        }

        public void Visit(CpuHistoryTraceDatum cpuHistoryTraceDatum)
        {
        }

        public void Visit(ClientConfigurationTraceDatum clientConfigurationTraceDatum)
        {
        }

    }
}
