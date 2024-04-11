namespace TestWorkloadV2
{
    using System;
    using System.Text.Json.Serialization;

    enum RequestType
    {
        Create,
        PointRead,
    }

    [JsonDerivedType(typeof(Mongo.Configuration), "Mongo")]
    [JsonDerivedType(typeof(CosmosDBNoSql.Configuration), "CosmosDBNoSql")]
    [JsonDerivedType(typeof(CosmosDBCassandra.Configuration), "CosmosDBCassandra")]
    [JsonDerivedType(typeof(Postgres.Configuration), "Postgres")]
    internal class CommonConfiguration
    {
        public static readonly int RandomSeed = DateTime.UtcNow.Millisecond;

        [JsonIgnore]
        public string ConnectionString { get; set; }

        // Expect this to be filled by derived class before completing initialization
        public string ConnectionStringForLogging { get; set; }

        public string DatabaseName { get; set; }
        public string ContainerName { get; set; }

        public bool ShouldRecreateContainerOnStart { get; set; }


        public int? TotalRequestCount { get; set; }
        public int ItemSize { get; set; }
        public int PartitionKeyCount { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public RequestType RequestType {  get; set; }
        public int RequestsPerSecond { get; set; }

        public int? MinConnectionPoolSize { get; set; }
        public int? MaxConnectionPoolSize { get; set; }
        public int? MaxInFlightRequestCount { get; set; }

        public int WarmupSeconds { get; set; }
        public int? MaxRuntimeInSeconds { get; set; }
        public int LatencyTracingIntervalInSeconds { get; set; }
        public int NumWorkers { get; set; }

        public bool ShouldDeleteContainerOnFinish { get; set; }


        public void SetConnectionPoolAndMaxInflightRequestLimit()
        {
            if (!this.MinConnectionPoolSize.HasValue)
            {
                this.MinConnectionPoolSize = this.RequestsPerSecond / 100; // assume <10 msec per request avg. latency, todo: increase denominator for PointRead
            }

            if (!this.MaxConnectionPoolSize.HasValue)
            {
                this.MaxConnectionPoolSize = (int)(this.MinConnectionPoolSize * 1.5);
            }

            if (!this.MaxInFlightRequestCount.HasValue)
            {
                this.MaxInFlightRequestCount = (int)(this.MaxConnectionPoolSize * 3 / 2);
            }
        }
    }
}
