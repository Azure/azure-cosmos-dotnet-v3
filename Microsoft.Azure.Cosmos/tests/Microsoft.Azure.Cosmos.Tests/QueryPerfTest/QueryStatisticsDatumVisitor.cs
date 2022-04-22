namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    internal class QueryStatisticsDatumVisitor : ITraceDatumVisitor
    {
        private readonly List<QueryMetrics> queryMetricsList = new();
        private readonly QueryMetrics queryMetrics = new();
        private const int NumberOfEvents = 6;

        public IReadOnlyList<QueryMetrics> QueryMetricsList => this.queryMetricsList;

        public void AddEndToEndTime(double totalTime)
        {
            this.queryMetrics.EndToEndTime = totalTime;
        }

        public void AddPocoTime(double totalTime)
        {
            this.queryMetrics.PocoTime = totalTime;
        }

        public void AddGetCosmosElementResponseTime(double totalTime)
        {
            this.queryMetrics.GetCosmosElementResponseTime = totalTime;
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
                        for (int i = 0; i < NumberOfEvents; i++)
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
                        for (int i = 0; i < NumberOfEvents; i++)
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
            this.queryMetricsList.Add(this.queryMetrics);
        }

        public void Visit(CpuHistoryTraceDatum cpuHistoryTraceDatum)
        {
            Debug.Fail("QueryStatisticsDatumVisitor Assert", "CpuHistoryTraceDatum is not supported");
        }

        public void Visit(ClientConfigurationTraceDatum clientConfigurationTraceDatum)
        {
            Debug.Fail("QueryStatisticsDatumVisitor Assert", "ClientConfigurationTraceDatum is not supported");
        }

        public void Visit(PointOperationStatisticsTraceDatum pointOperationStatisticsTraceDatum)
        {
            Debug.Fail("QueryStatisticsDatumVisitor Assert", "PointOperationStatisticsTraceDatum is not supported");
        }
    }
}
