namespace TestWorkloadV2
{
    using System.Text.Json.Serialization;

    [JsonDerivedType(typeof(Mongo.Configuration), "Mongo")]
    [JsonDerivedType(typeof(CosmosDBNoSql.Configuration), "CosmosDBNoSql")]
    [JsonDerivedType(typeof(CosmosDBCassandra.Configuration), "CosmosDBCassandra")]
    [JsonDerivedType(typeof(Postgres.Configuration), "Postgres")]
    
    internal class CommonConfiguration
    {
        [JsonIgnore]
        public string ConnectionString { get; set; }

        // Expect this to be filled by derived class before completing initialization
        public string ConnectionStringForLogging { get; set; }

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
