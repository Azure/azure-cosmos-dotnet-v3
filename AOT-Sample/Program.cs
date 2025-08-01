namespace AOTSample
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Text.Json.Serialization.Metadata;
    using Microsoft.Azure.Cosmos;

    public class Program
    {
        public static async Task Main(string[] _)
        {
            const string CosmosBaseUri = "https://localhost:8081";
            string? primaryKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw=="; // Environment.GetEnvironmentVariable("KEY");
            Console.WriteLine($"COSMOS_PRIMARY_KEY: {primaryKey}");

            if (string.IsNullOrEmpty(primaryKey))
            {
                Console.WriteLine("ERROR: KEY environment variable is not set");
                return;
            }

            CosmosClient client = new CosmosClient(
                CosmosBaseUri,
                primaryKey);

            AccountProperties accountProperties = await client.ReadAccountAsync();
            Console.WriteLine($"Account Name: {accountProperties.Id}");

            FeedIterator db_feed_itr = client.GetDatabaseQueryStreamIterator();
            while (db_feed_itr.HasMoreResults)
            {
                ResponseMessage db_response = await db_feed_itr.ReadNextAsync();
                if (db_response.IsSuccessStatusCode)
                {
                    using JsonDocument dbsQueryResultDoc = JsonDocument.Parse(db_response.Content);
                    // Console.WriteLine($"Raw JSON (Dbs result): {dbsQueryResultDoc.RootElement.GetRawText()}");

                    if (dbsQueryResultDoc.RootElement.TryGetProperty("Databases", out JsonElement documentsElement))
                    {
                        foreach (JsonElement databaseElement in documentsElement.EnumerateArray())
                        {
                            string? databaseId = databaseElement.GetProperty("id").GetString();
                            Console.WriteLine($"Database: {databaseId}");

                            Database database = client.GetDatabase(databaseId);
                            FeedIterator container_feed_itr = database.GetContainerQueryStreamIterator();

                            while (container_feed_itr.HasMoreResults)
                            {
                                ResponseMessage cont_response = await container_feed_itr.ReadNextAsync();
                                if (cont_response.IsSuccessStatusCode)
                                {
                                    using JsonDocument containersQueryResultDoc = JsonDocument.Parse(cont_response.Content);
                                    Console.WriteLine($"Raw JSON (cont result): {containersQueryResultDoc.RootElement.GetRawText()}");

                                    if (containersQueryResultDoc.RootElement.TryGetProperty("DocumentCollections", out JsonElement containersElement))
                                    {
                                        foreach (JsonElement containerElement in containersElement.EnumerateArray())
                                        {
                                            string? containerId = containerElement.GetProperty("id").GetString();
                                            string? PartitionKeyPath = containerElement.TryGetProperty("partitionKey", out JsonElement partitionKeyElement)
                                                ? partitionKeyElement.GetProperty("paths").EnumerateArray().FirstOrDefault().GetString()
                                                : null;
                                            Console.WriteLine($"Container: {containerId} PartitionKeyPath: {PartitionKeyPath}");

                                            Container container = database.GetContainer(containerId);

                                            ContainerProperties containerProperties = await container.ReadContainerAsync();
                                            Console.WriteLine($"Container-Read: {containerProperties.Id} PartitionKeyPath: {containerProperties.PartitionKeyPath}");

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

                                                    if (itemsQueryResultDoc.RootElement.TryGetProperty("Documents", out JsonElement docsElement))
                                                    {
                                                        foreach (JsonElement item in docsElement.EnumerateArray())
                                                        {
                                                            Console.WriteLine($"Data JSON (Query result): {item.GetRawText()}");
                                                        }
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
                    }
                }
            }

            Console.WriteLine("!!SUCCESS");
            Console.ReadLine();

            //        Database database = client.GetDatabase(db_properties.Id);
            //        FeedIterator<ContainerProperties> container_feed_itr = database.GetContainerQueryIterator<ContainerProperties>();

            //        while (container_feed_itr.HasMoreResults)
            //        {
            //            FeedResponse<ContainerProperties> container_response = await container_feed_itr.ReadNextAsync();
            //            foreach (ContainerProperties container_properties in container_response)
            //            {
            //                Console.WriteLine($"Container: {container_properties.Id}");

            //                Console.WriteLine($"Container PartitionKeyPath: {container_properties.PartitionKeyPath}");
            //                if (container_properties.PartitionKeyPaths != null && container_properties.PartitionKeyPaths.Count > 0)
            //                {
            //                    Console.WriteLine($"Container PartitionKeyPaths: [{string.Join(", ", container_properties.PartitionKeyPaths)}]");
            //                }

            //                Container container = client.GetContainer(db_properties.Id, container_properties.Id);

            //                String itemsQuery = "SELECT * FROM c";
            //                QueryDefinition itemsQueryDef = new QueryDefinition(itemsQuery);

            //                FeedIterator queryIterator = container.GetItemQueryStreamIterator(
            //                    itemsQueryDef,
            //                    requestOptions: new QueryRequestOptions { MaxItemCount = -1 }
            //                );

            //                while (queryIterator.HasMoreResults)
            //                {
            //                    ResponseMessage response = await queryIterator.ReadNextAsync();

            //                    if (response.Content == null)
            //                    {
            //                        Console.WriteLine("QueryResponse.Content is null");
            //                        continue;
            //                    }
            //                    if (response.Content.CanSeek && response.Content.Length == 0)
            //                    {
            //                        Console.WriteLine("QueryResponse.Content stream is empty");
            //                        continue;
            //                    }

            //                    try
            //                    {
            //                        using JsonDocument itemsQueryResultDoc = JsonDocument.Parse(response.Content);
            //                        Console.WriteLine($"Raw JSON (Query result): {itemsQueryResultDoc.RootElement.GetRawText()}");

            //                        if (itemsQueryResultDoc.RootElement.TryGetProperty("Documents", out JsonElement documentsElement))
            //                        {
            //                            foreach (JsonElement item in documentsElement.EnumerateArray())
            //                            {
            //                                string itemId = ExtractItemId(item);
            //                                PartitionKey itemPartitionKey = CreatePartitionKey(item, container_properties.PartitionKeyPath);
            //                                Console.WriteLine($"Item PartitionKey: {itemPartitionKey}");

            //                                try
            //                                {
            //                                    ItemResponse<JsonElement> itemResponse = await container.ReadItemAsync<JsonElement>(
            //                                        id: itemId,
            //                                        partitionKey: itemPartitionKey,
            //                                        requestOptions: new ItemRequestOptions
            //                                        {
            //                                            // EnableContentResponseOnWrite = false,
            //                                        }
            //                                    );

            //                                    Console.WriteLine($"Raw JSON (Read item result): {itemResponse.Resource.GetRawText()}");
            //                                }
            //                                catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            //                                {
            //                                    Console.WriteLine("ReadItemAsync: Item not found");
            //                                }
            //                                catch (CosmosException ex)
            //                                {
            //                                    Console.WriteLine($"ReadItemAsync: Error reading. {ex.Message}");
            //                                }
            //                            }
            //                        }
            //                        else
            //                        {
            //                            Console.WriteLine($"Raw JSON: {itemsQueryResultDoc.RootElement.GetRawText()}");
            //                        }
            //                    }
            //                    catch (JsonException ex)
            //                    {
            //                        Console.WriteLine($"JsonException: {ex.Message}");
            //                    }
            //                }
            //            }
            //        }
            //    }
            //}
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