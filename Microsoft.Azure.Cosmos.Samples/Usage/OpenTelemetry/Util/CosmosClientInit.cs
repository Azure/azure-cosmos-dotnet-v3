namespace OpenTelemetry.Util
{
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos;
    using System.Threading.Tasks;

    public static class CosmosClientInit
    {
        public static async Task<Container> CreateClientAndContainer(
         string connectionString,
         ConnectionMode mode,
         string dbAndContainerNameSuffix = "",
         bool isLargeContainer = false,
         bool isEnableOpenTelemetry = false,
         bool isBulkExecution = false)
        {
            CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder(connectionString);

            if(isBulkExecution)
            {
                cosmosClientBuilder.WithBulkExecution(true);
            }
            
            CosmosClient cosmosClient = mode == ConnectionMode.Gateway
                ? cosmosClientBuilder.WithConnectionModeGateway().Build()
                : cosmosClientBuilder.Build();

            cosmosClient.ClientOptions.EnableDistributedTracing = isEnableOpenTelemetry;

            Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync("OTelSampleDb" + dbAndContainerNameSuffix);

            return await database.CreateContainerIfNotExistsAsync(
                id: "OTelSampleContainer" + dbAndContainerNameSuffix,
                partitionKeyPath: "/id",
                throughput: isLargeContainer ? 15000 : 400);
        }
    }
}
