namespace TestWorkloadV2
{
    using System.Collections.Generic;
    using System.Net;
    using System;

    internal class RunResult
    {
        internal class LatencyValues
        {
            public decimal Avg { get; set; }
            public decimal P50 { get; set; }
            public decimal P90 { get; set; }
            public decimal P95 { get; set; }

            public decimal P99 { get; set; }
            public decimal P999 { get; set; }

            public decimal Max { get; set; }
        }

        public string MachineName => Environment.MachineName;

        public DateTime RunStartTime { get; set; }

        public DateTime RunEndTime { get; set; }

        public CommonConfiguration Configuration { get; set; }

        public string PartitionKeyValuePrefix { get; set; } 

        public long InitialItemId { get; set; }

        public long ItemId { get; set; }

        public int NonFailedRequests { get; set; }

        public int NonFailedRequestsAfterWarmup { get; set; }

        public long RunDuration { get; set; }

        public LatencyValues Latencies { get; set; }

        public double AverageRUs { get; set; }

        public Dictionary<HttpStatusCode, int> CountsByStatus { get; set; }

        public long AchievedRequestsPerSecond { get; set; }
    }

}
