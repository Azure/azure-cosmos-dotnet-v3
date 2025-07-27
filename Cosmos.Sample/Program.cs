// See https://aka.ms/new-console-template for more information
using System.Text.Json;
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
                        Console.WriteLine($"Container: {container_properties.Id}");


                    }

                    Container container = client.GetContainer(db_properties.Id, db_properties.Id);

                    String query = "SELECT * FROM c";
                    QueryDefinition queryDef = new QueryDefinition(query);

                    var items = new List<JsonElement>();
                    var queryIterator = container.GetItemQueryStreamIterator(
                        queryDef,
                        requestOptions: new QueryRequestOptions { MaxItemCount = -1 }
                    );

                    while (queryIterator.HasMoreResults)
                    {
                        ResponseMessage response = await queryIterator.ReadNextAsync();
                        
                        // Check if Content is set and not empty
                        if (response.Content == null)
                        {
                            Console.WriteLine("Response Content is null");
                            continue;
                        }

                        // Check if stream is empty
                        if (response.Content.CanSeek && response.Content.Length == 0)
                        {
                            Console.WriteLine("Response Content stream is empty (Length = 0)");
                            continue;
                        }

                        // If Content has data, process it
                        Console.WriteLine($"Query_Result: Response has content with length: {(response.Content.CanSeek ? response.Content.Length.ToString() : "unknown")}");
                        
                        // Reset stream position if it can seek (important for reading)
                        if (response.Content.CanSeek)
                        {
                            response.Content.Position = 0;
                        }

                        // Parse the JSON content
                        try
                        {
                            using var document = JsonDocument.Parse(response.Content);
                            // Process your JSON document here
                            Console.WriteLine($"JSON parsed successfully. Root element kind: {document.RootElement.ValueKind}");
                            
                            // Example: Print the raw JSON
                            Console.WriteLine($"Raw JSON: {document.RootElement.GetRawText()}");
                        }
                        catch (JsonException ex)
                        {
                            Console.WriteLine($"Error parsing JSON: {ex.Message}");
                        }
                    }
                }
            }
        }
    }
}