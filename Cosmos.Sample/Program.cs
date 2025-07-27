// See https://aka.ms/new-console-template for more information
using Microsoft.Azure.Cosmos;

namespace Cosmos.Sample;

internal class Program
{
    private static async Task Main(string[] args)
    {
        const string CosmosBaseUri = "https://{0}.documents.azure.com:443/";
        string accountName = "cosmosaot2";
        string? primaryKey = Environment.GetEnvironmentVariable("KEY");
        Console.WriteLine($"COSMOS_PRIMARY_KEY: {primaryKey}");

        if (string.IsNullOrEmpty(primaryKey))
        {
            Console.WriteLine("ERROR: KEY environment variable is not set");
            return;
        }

        CosmosClientOptions clientOptions = new CosmosClientOptions { AllowBulkExecution = true };
        clientOptions.CosmosClientTelemetryOptions.DisableDistributedTracing = false;

        CosmosClient client = new CosmosClient(
            string.Format(CosmosBaseUri, accountName),
            primaryKey,
            clientOptions);

        FeedIterator<DatabaseProperties> db_feed_itr = client.GetDatabaseQueryIterator<DatabaseProperties>();
        while (db_feed_itr.HasMoreResults)
        {
            FeedResponse<DatabaseProperties> db_response = await db_feed_itr.ReadNextAsync();
            foreach (DatabaseProperties db_properties in db_response)
            {
                Console.WriteLine($"Database: {db_properties.Id}");

                Database database = client.GetDatabase(db_properties.Id);
                FeedIterator<ContainerProperties> container_feed_itr = database.GetContainerQueryIterator<ContainerProperties>();
                
                while (container_feed_itr.HasMoreResults)
                {
                    FeedResponse<ContainerProperties> container_respopnse = await container_feed_itr.ReadNextAsync();
                    foreach (ContainerProperties container_properties in container_respopnse)
                    {
                        Console.WriteLine($"Container: {container_properties.PartitionKeyPath}");

                    }
                }
            }
        }
    }
}