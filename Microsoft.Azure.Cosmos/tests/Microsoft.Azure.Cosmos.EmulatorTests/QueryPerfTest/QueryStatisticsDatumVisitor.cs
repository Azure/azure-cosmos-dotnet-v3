﻿namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    internal class QueryStatisticsDatumVisitor : ITraceDatumVisitor
    {
        private const int NumberOfEvents = 6;

        private readonly List<QueryStatisticsMetrics> queryMetricsList;
        private readonly List<QueryStatisticsMetrics> badRequestMetricsList;
        private QueryStatisticsMetrics queryMetrics;

        public QueryStatisticsDatumVisitor()
        {
            this.queryMetricsList = new();
            this.badRequestMetricsList = new();
            this.queryMetrics = new();
        }
        public IReadOnlyList<QueryStatisticsMetrics> QueryMetricsList => this.queryMetricsList;
        public IReadOnlyList<QueryStatisticsMetrics> BadRequestMetricsList => this.badRequestMetricsList;

        public void AddEndToEndTime(double totalTime)
        {
            this.queryMetrics.EndToEndTime = totalTime;
        }

        public void AddPocoTime(double time)
        {
            this.queryMetrics.PocoTime = time;
        }

        public void AddGetCosmosElementResponseTime(double time)
        {
            this.queryMetrics.GetCosmosElementResponseTime = time;
        }

        public void Visit(QueryMetricsTraceDatum queryMetricsTraceDatum)
        {
            this.queryMetrics.RetrievedDocumentCount = queryMetricsTraceDatum.QueryMetrics.ServerSideMetrics.RetrievedDocumentCount;
            this.queryMetrics.RetrievedDocumentSize = queryMetricsTraceDatum.QueryMetrics.ServerSideMetrics.RetrievedDocumentSize;
            this.queryMetrics.OutputDocumentCount = queryMetricsTraceDatum.QueryMetrics.ServerSideMetrics.OutputDocumentCount;
            this.queryMetrics.OutputDocumentSize = queryMetricsTraceDatum.QueryMetrics.ServerSideMetrics.OutputDocumentSize;
            this.queryMetrics.TotalQueryExecutionTime = queryMetricsTraceDatum.QueryMetrics.ServerSideMetrics.TotalTime.TotalMilliseconds;
            this.queryMetrics.DocumentLoadTime = queryMetricsTraceDatum.QueryMetrics.ServerSideMetrics.DocumentLoadTime.TotalMilliseconds;
            this.queryMetrics.DocumentWriteTime = queryMetricsTraceDatum.QueryMetrics.ServerSideMetrics.DocumentWriteTime.TotalMilliseconds;
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
                                    Debug.Fail("Unknown event ignored", $"Event Type not supported '{transportStats.RequestTimeline[i].Event}'");
                                    break;
                            }
                        }
                    }
                    else
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
                                    Debug.Fail("Unknown event ignored", $"Event Type not supported '{badRequestTransportStats.RequestTimeline[i].Event}'");
                                    break;
                            }
                        }

                        this.badRequestMetricsList.Add(this.queryMetrics);
                    }
                }
            }
        }

        public void PopulateMetrics()
        {
            this.queryMetricsList.Add(this.queryMetrics);
            this.queryMetrics = new();
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

        public void Visit(PartitionKeyRangeCacheTraceDatum partitionKeyRangeCacheTraceDatum)
        {
            Debug.Fail("QueryStatisticsDatumVisitor Assert", "PartitionKeyRangeCacheTraceDatum is not supported");
        }
    }
}
