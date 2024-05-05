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

        public string ConnectionStringRef { get; set; }

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
        public int? RequestsPerSecond { get; set; }

        public int? MinConnectionPoolSize { get; set; }
        public int? MaxConnectionPoolSize { get; set; }
        public int? MaxInFlightRequestCount { get; set; }

        public int WarmupSeconds { get; set; }
        public int? MaxRuntimeInSeconds { get; set; }
        public int LatencyTracingIntervalInSeconds { get; set; }

        public bool ShouldDeleteContainerOnFinish { get; set; }

        public int? WorkerCount { get; set; }

        public int? WorkerIndex { get; set; }


        public void SetConnectionPoolLimits()
        {
            if (!this.MinConnectionPoolSize.HasValue)
            {
                if (this.MaxInFlightRequestCount.HasValue)
                {
                    this.MinConnectionPoolSize = this.MaxInFlightRequestCount;
                }
                else if (this.RequestsPerSecond.HasValue)
                {
                    this.MinConnectionPoolSize = (int)(this.RequestsPerSecond / 100); // assuming ~10msec avg latency of requests
                }
                else
                {
                    this.MinConnectionPoolSize = 1;
                }
            }

            if (!this.MaxConnectionPoolSize.HasValue)
            {
                this.MaxConnectionPoolSize = this.MinConnectionPoolSize;
            }
        }
    }
}
