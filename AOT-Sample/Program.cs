namespace AOTSample
{
    using System;
    using System.Text.Json;
    using Microsoft.Azure.Cosmos;

    // cd .\AOT-Sample\
    // rm .\obj\ -Recurse -Force
    // rm .\bin\ -Recurse -Force
    // dotnet publish AOT-Sample.csproj -c Release --verbosity normal

    public class Program
    {
        public static async Task Main(string[] args)
        {
            const string CosmosBaseUri = "https://{0}.documents.azure.com:443/";
            string accountName = "cosmosaot2";
            string? primaryKey = Environment.GetEnvironmentVariable("KEY");
            Console.WriteLine($"COSMOS_PRIMARY_KEY: {primaryKey}");

            // Fallback to command line arguments if environment variable is not set
            if (string.IsNullOrEmpty(primaryKey))
            {
                Console.WriteLine("KEY environment variable is not set, checking command line arguments...");
                
                if (args.Length > 0)
                {
                    primaryKey = args[0];
                    Console.WriteLine("Using primary key from command line arguments");
                }
                else
                {
                    Console.WriteLine("ERROR: Primary key not found in environment variable 'KEY' or command line arguments");
                    Console.WriteLine("Usage: AOT-Sample.exe <primaryKey>");
                    Console.WriteLine("   or: Set environment variable KEY=<primaryKey>");
                    return;
                }
            }

            CosmosClientOptions clientOptions = new CosmosClientOptions { 
                AllowBulkExecution = true,
                ConnectionMode = ConnectionMode.Gateway
            };
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
                        FeedResponse<ContainerProperties> container_response = await container_feed_itr.ReadNextAsync();
                        foreach (ContainerProperties container_properties in container_response)
                        {
                            Console.WriteLine($"Container: ID -> {container_properties.Id} Rid -> {container_properties.ResourceId}");
                            Console.WriteLine($"Container PartitionKeyPath: {container_properties.PartitionKeyPath}");
                            if (container_properties.PartitionKeyPaths != null && container_properties.PartitionKeyPaths.Count > 0)
                            {
                                Console.WriteLine($"Container PartitionKeyPaths: [{string.Join(", ", container_properties.PartitionKeyPaths)}]");
                            }

                            Container container = client.GetContainer(db_properties.Id, container_properties.Id);

                            String itemsQuery = "SELECT * FROM c";
                            QueryDefinition itemsQueryDef = new QueryDefinition(itemsQuery);

                            FeedIterator queryIterator = container.GetItemQueryStreamIterator(
                                itemsQueryDef,
                                requestOptions: new QueryRequestOptions { MaxItemCount = -1 }
                            );

                            while (queryIterator.HasMoreResults)
                            {
                                ResponseMessage response = await queryIterator.ReadNextAsync();

                                if (response.Content == null)
                                {
                                    Console.WriteLine("QueryResponse.Content is null");
                                    continue;
                                }
                                if (response.Content.CanSeek && response.Content.Length == 0)
                                {
                                    Console.WriteLine("QueryResponse.Content stream is empty");
                                    continue;
                                }

                                try
                                {
                                    using JsonDocument itemsQueryResultDoc = JsonDocument.Parse(response.Content);
                                    Console.WriteLine($"Raw JSON (Query result): {itemsQueryResultDoc.RootElement.GetRawText()}");

                                    if (itemsQueryResultDoc.RootElement.TryGetProperty("Documents", out JsonElement documentsElement))
                                    {
                                        foreach (JsonElement item in documentsElement.EnumerateArray())
                                        {
                                            string itemId = ExtractItemId(item);
                                            PartitionKey itemPartitionKey = CreatePartitionKey(item, container_properties.PartitionKeyPath);
                                            Console.WriteLine($"Item PartitionKey: {itemPartitionKey}");

                                            try
                                            {
                                                ItemResponse<JsonElement> itemResponse = await container.ReadItemAsync<JsonElement>(
                                                    id: itemId,
                                                    partitionKey: itemPartitionKey,
                                                    requestOptions: new ItemRequestOptions
                                                    {
                                                        // EnableContentResponseOnWrite = false,
                                                    }
                                                );

                                                Console.WriteLine($"Raw JSON (Read item result): {itemResponse.Resource.GetRawText()}");
                                            }
                                            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                                            {
                                                Console.WriteLine("ReadItemAsync: Item not found");
                                            }
                                            catch (CosmosException ex)
                                            {
                                                Console.WriteLine($"ReadItemAsync: Error reading. {ex.Message}");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Raw JSON: {itemsQueryResultDoc.RootElement.GetRawText()}");
                                    }
                                }
                                catch (JsonException ex)
                                {
                                    Console.WriteLine($"JsonException: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
        }

        private static string ExtractItemId(JsonElement item)
        {
            return item.TryGetProperty("id", out JsonElement idElement)
                ? idElement.GetString() ?? "unknown"
                : "unknown";
        }

        private static PartitionKey CreatePartitionKey(JsonElement jsonElement, string? partitionKeyPath)
        {
            if (string.IsNullOrEmpty(partitionKeyPath))
            {
                return PartitionKey.Null;
            }

            string[] pathParts = partitionKeyPath.TrimStart('/').Split('/');

            JsonElement currentElement = jsonElement;
            foreach (string part in pathParts)
            {
                if (currentElement.TryGetProperty(part, out JsonElement nextElement))
                {
                    currentElement = nextElement;
                }
                else
                {
                    // Property not found, return null partition key
                    return PartitionKey.Null;
                }
            }

            return currentElement.ValueKind switch
            {
                JsonValueKind.String => new PartitionKey(currentElement.GetString() ?? string.Empty),
                JsonValueKind.Number => currentElement.TryGetInt32(out int intValue)
                    ? new PartitionKey((double)intValue)
                    : new PartitionKey(currentElement.GetDouble()),
                JsonValueKind.True => new PartitionKey(true),
                JsonValueKind.False => new PartitionKey(false),
                JsonValueKind.Null => PartitionKey.Null,
                _ => new PartitionKey(currentElement.GetRawText())
            };
        }
    }
}