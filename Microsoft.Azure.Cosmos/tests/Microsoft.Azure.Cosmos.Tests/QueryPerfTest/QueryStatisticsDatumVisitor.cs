using System;
using System.Collections.Generic;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Cosmos.Tracing;
using Microsoft.Azure.Cosmos.Tracing.TraceData;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;

internal class QueryStatisticsDatumVisitor : ITraceDatumVisitor
{
    public readonly QueryStatisticsAccumulator queryStatisticsAccumulator = new();
    public readonly QueryMetrics queryMetrics = new();
    private readonly int numberOfEvents = 6;
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
        this.queryMetrics.RetrievedDocumentCount = queryMetricsTraceDatum.QueryMetrics.BackendMetrics.RetrievedDocumentCount;
        this.queryMetrics.RetrievedDocumentSize = queryMetricsTraceDatum.QueryMetrics.BackendMetrics.RetrievedDocumentSize;
        this.queryMetrics.OutputDocumentCount = queryMetricsTraceDatum.QueryMetrics.BackendMetrics.OutputDocumentCount;
        this.queryMetrics.OutputDocumentSize = queryMetricsTraceDatum.QueryMetrics.BackendMetrics.OutputDocumentSize;
        this.queryMetrics.TotalQueryExecutionTime = queryMetricsTraceDatum.QueryMetrics.BackendMetrics.TotalTime.TotalMilliseconds;
        this.queryMetrics.DocumentLoadTime = queryMetricsTraceDatum.QueryMetrics.BackendMetrics.DocumentLoadTime.TotalMilliseconds;
        this.queryMetrics.DocumentWriteTime = queryMetricsTraceDatum.QueryMetrics.BackendMetrics.DocumentWriteTime.TotalMilliseconds;
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
                    for (int i = 0; i < this.numberOfEvents; i++)
                    {
                        switch (transportStats.RequestTimeline[i].Event)
                        {
                            case RequestTimeline.EventType.Created:
                                this.queryMetrics.Created = transportStats.RequestTimeline[i].DurationInMs;
                                break;
                            case RequestTimeline.EventType.ChannelAcquisitionStarted:
                                this.queryMetrics.ChannelAcquisitionStarted = transportStats.RequestTimeline[i].DurationInMs;
                                break;
                            case RequestTimeline.EventType.Pipelined:
                                this.queryMetrics.Pipelined = transportStats.RequestTimeline[i].DurationInMs;
                                break;
                            case RequestTimeline.EventType.TransitTime:
                                this.queryMetrics.TransitTime = transportStats.RequestTimeline[i].DurationInMs;
                                break;
                            case RequestTimeline.EventType.Received:
                                this.queryMetrics.Received = transportStats.RequestTimeline[i].DurationInMs;
                                break;
                            case RequestTimeline.EventType.Completed:
                                this.queryMetrics.Completed = transportStats.RequestTimeline[i].DurationInMs;
                                break;
                            default:
                                Console.WriteLine($"Unknown event ignored : '{transportStats.RequestTimeline[i].Event}'");
                                break;
                        }
                    }
                }
                else if (storeResponse.StoreResult.StatusCode != StatusCodes.Ok)
                {
                    TransportStats badRequestTransportStats = JsonConvert.DeserializeObject<TransportStats>(storeResponse.StoreResult.TransportRequestStats.ToString());
                    for (int i = 0; i < this.numberOfEvents; i++)
                    {
                        switch (badRequestTransportStats.RequestTimeline[i].Event)
                        {
                            case RequestTimeline.EventType.Created:
                                this.queryMetrics.BadRequestCreated = badRequestTransportStats.RequestTimeline[i].DurationInMs;
                                break;
                            case RequestTimeline.EventType.ChannelAcquisitionStarted:
                                this.queryMetrics.BadRequestChannelAcquisitionStarted = badRequestTransportStats.RequestTimeline[i].DurationInMs;
                                break;
                            case RequestTimeline.EventType.Pipelined:
                                this.queryMetrics.BadRequestPipelined = badRequestTransportStats.RequestTimeline[i].DurationInMs;
                                break;
                            case RequestTimeline.EventType.TransitTime:
                                this.queryMetrics.BadRequestTransitTime = badRequestTransportStats.RequestTimeline[i].DurationInMs;
                                break;
                            case RequestTimeline.EventType.Received:
                                this.queryMetrics.BadRequestReceived = badRequestTransportStats.RequestTimeline[i].DurationInMs;
                                break;
                            case RequestTimeline.EventType.Completed:
                                this.queryMetrics.BadRequestCompleted = badRequestTransportStats.RequestTimeline[i].DurationInMs;
                                break;
                            default:
                                Console.WriteLine($"Unknown event ignored : '{badRequestTransportStats.RequestTimeline[i].Event}'");
                                break;
                        }
                    }
                }
            }

            this.PopulateMetrics();
        }
    }

    public void PopulateMetrics()
    {
        this.queryStatisticsAccumulator.QueryMetricsList
            .Add(new QueryMetrics
            {
                RetrievedDocumentCount = this.queryMetrics.RetrievedDocumentCount,
                RetrievedDocumentSize = this.queryMetrics.RetrievedDocumentSize,
                OutputDocumentCount = this.queryMetrics.OutputDocumentCount,
                OutputDocumentSize = this.queryMetrics.OutputDocumentSize,
                TotalQueryExecutionTime = this.queryMetrics.TotalQueryExecutionTime,
                DocumentLoadTime = this.queryMetrics.DocumentLoadTime,
                DocumentWriteTime = this.queryMetrics.DocumentWriteTime,
                Created = this.queryMetrics.Created,
                ChannelAcquisitionStarted = this.queryMetrics.ChannelAcquisitionStarted,
                Pipelined = this.queryMetrics.Pipelined,
                TransitTime = this.queryMetrics.TransitTime,
                Received = this.queryMetrics.Received,
                Completed = this.queryMetrics.Completed,
                PocoTime = this.queryMetrics.PocoTime,
                GetCosmosElementResponseTime = this.queryMetrics.GetCosmosElementResponseTime,
                EndToEndTime = this.queryMetrics.EndToEndTime,
                BadRequestCreated = this.queryMetrics.BadRequestCreated,
                BadRequestChannelAcquisitionStarted = this.queryMetrics.BadRequestChannelAcquisitionStarted,
                BadRequestPipelined = this.queryMetrics.BadRequestPipelined,
                BadRequestTransitTime = this.queryMetrics.BadRequestTransitTime,
                BadRequestReceived = this.queryMetrics.BadRequestReceived,
                BadRequestCompleted = this.queryMetrics.BadRequestCompleted
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
