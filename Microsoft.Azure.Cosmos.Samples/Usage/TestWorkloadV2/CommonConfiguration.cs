namespace TestWorkloadV2
{
    internal class CommonConfiguration
    {
        public string ConnectionString { get; set; }
        public string DatabaseName { get; set; }
        public string ContainerName { get; set; }

        public bool ShouldRecreateContainerOnStart { get; set; }


        public int TotalRequestCount { get; set; }
        public int ItemSize { get; set; }
        public int PartitionKeyCount { get; set; }


        public int RequestsPerSecond { get; set; }
        public int MaxInFlightRequestCount { get; set; }

        public int WarmupSeconds { get; set; }
        public int MaxRuntimeInSeconds { get; set; }
        public int LatencyTracingIntervalInSeconds { get; set; }
        public int NumWorkers { get; set; }

        public bool ShouldDeleteContainerOnFinish { get; set; }
    }
}
