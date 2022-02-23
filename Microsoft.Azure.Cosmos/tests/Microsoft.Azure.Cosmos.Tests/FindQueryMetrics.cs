namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Newtonsoft.Json;

    class FindMetrics : ITraceDatumVisitor
    {
        public readonly List<double> RetrievedDocumentCount = new List<double>();
        public readonly List<double> RetrievedDocumentSize = new List<double>();
        public readonly List<double> OutputDocumentCount = new List<double>();
        public readonly List<double> OutputDocumentSize = new List<double>();
        public readonly List<double> TotalQueryExecutionTime = new List<double>();
        public readonly List<double> DocumentLoadTime = new List<double>();
        public readonly List<double> DocumentWriteTime = new List<double>();
        public readonly List<double> Created = new List<double>();
        public readonly List<double> ChannelAcquisitionStarted = new List<double>();
        public readonly List<double> Pipelined = new List<double>();
        public readonly List<double> TransitTime = new List<double>();
        public readonly List<double> Received = new List<double>();
        public readonly List<double> Completed = new List<double>();
        public class RequestTimeline
        {
            public string Event { get; set; }
            public DateTime StartTimeUtc { get; set; }
            public double DurationInMs { get; set; }
        }

        public class TransportStats
        {
            public List<RequestTimeline> RequestTimeline { get; set; }
        }
        public void Visit(QueryMetricsTraceDatum queryMetricsTraceDatum)
        {
            this.RetrievedDocumentCount.Add(queryMetricsTraceDatum.QueryMetrics.BackendMetrics.RetrievedDocumentCount);
            this.RetrievedDocumentSize.Add(queryMetricsTraceDatum.QueryMetrics.BackendMetrics.RetrievedDocumentSize);
            this.OutputDocumentCount.Add(queryMetricsTraceDatum.QueryMetrics.BackendMetrics.OutputDocumentCount);
            this.OutputDocumentSize.Add(queryMetricsTraceDatum.QueryMetrics.BackendMetrics.OutputDocumentSize);
            this.TotalQueryExecutionTime.Add(queryMetricsTraceDatum.QueryMetrics.BackendMetrics.TotalTime.TotalMilliseconds);
            this.DocumentLoadTime.Add(queryMetricsTraceDatum.QueryMetrics.BackendMetrics.DocumentLoadTime.TotalMilliseconds);
            this.DocumentWriteTime.Add(queryMetricsTraceDatum.QueryMetrics.BackendMetrics.DocumentWriteTime.TotalMilliseconds);
        }

        public void Visit(PointOperationStatisticsTraceDatum pointOperationStatisticsTraceDatum)
        {
        }

        public void Visit(ClientSideRequestStatisticsTraceDatum clientSideRequestStatisticsTraceDatum)
        {
            if (clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList.Count > 0)
            {
                ClientSideRequestStatisticsTraceDatum.StoreResponseStatistics storeResponse = clientSideRequestStatisticsTraceDatum.StoreResponseStatisticsList[0];
                if (storeResponse.StoreResult.StatusCode == StatusCodes.Ok)
                {
                    TransportStats transportStats = JsonConvert.DeserializeObject<TransportStats>(storeResponse.StoreResult.TransportRequestStats.ToString());
                    if (transportStats.RequestTimeline[0].Event == "Created")
                    {
                        this.Created.Add(transportStats.RequestTimeline[0].DurationInMs);
                    }
                    if (transportStats.RequestTimeline[1].Event == "ChannelAcquisitionStarted")
                    {
                        this.ChannelAcquisitionStarted.Add(transportStats.RequestTimeline[1].DurationInMs);
                    }
                    if (transportStats.RequestTimeline[2].Event == "Pipelined")
                    {
                        this.Pipelined.Add(transportStats.RequestTimeline[2].DurationInMs);
                    }
                    if (transportStats.RequestTimeline[3].Event == "Transit Time")
                    {
                        this.TransitTime.Add(transportStats.RequestTimeline[3].DurationInMs);
                    }
                    if (transportStats.RequestTimeline[4].Event == "Received")
                    {
                        this.Received.Add(transportStats.RequestTimeline[4].DurationInMs);
                    }
                    if (transportStats.RequestTimeline[5].Event == "Completed")
                    {
                        this.Completed.Add(transportStats.RequestTimeline[5].DurationInMs);
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
