// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Mcp.Resources
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using ModelContextProtocol.Protocol;
    using ModelContextProtocol.Server;

    /// <summary>
    /// MCP resources for discovering Cosmos DB databases and containers.
    /// </summary>
    [McpServerResourceType]
    public class CosmosCatalogResources
    {
        private readonly CosmosClient cosmosClient;
        private readonly CosmosMcpOptions options;

        public CosmosCatalogResources(CosmosClient cosmosClient, CosmosMcpOptions options)
        {
            this.cosmosClient = cosmosClient;
            this.options = options;
        }

        [McpServerResource(UriTemplate = "cosmos://databases", Name = "Cosmos DB Databases", MimeType = "application/json")]
        [Description("Lists all accessible databases in the Cosmos DB account.")]
        public async Task<ResourceContents> GetDatabasesAsync(CancellationToken cancellationToken = default)
        {
            List<string> databases = new();

            using FeedIterator<DatabaseProperties> iterator = this.cosmosClient.GetDatabaseQueryIterator<DatabaseProperties>();

            while (iterator.HasMoreResults)
            {
                FeedResponse<DatabaseProperties> response = await iterator.ReadNextAsync(cancellationToken);
                foreach (DatabaseProperties db in response)
                {
                    if (this.options.DatabaseFilter is null || this.options.DatabaseFilter(db))
                    {
                        databases.Add(db.Id);
                    }
                }
            }

            string json = JsonSerializer.Serialize(new { databases }, new JsonSerializerOptions { WriteIndented = true });

            return new TextResourceContents
            {
                Uri = "cosmos://databases",
                MimeType = "application/json",
                Text = json
            };
        }

        [McpServerResource(UriTemplate = "cosmos://{database}/containers", Name = "Cosmos DB Containers", MimeType = "application/json")]
        [Description("Lists containers in a Cosmos DB database with partition key and indexing metadata.")]
        public async Task<ResourceContents> GetContainersAsync(
            [Description("Database name")] string database,
            CancellationToken cancellationToken = default)
        {
            if (this.options.DatabaseFilter is not null)
            {
                Database dbRef = this.cosmosClient.GetDatabase(database);
                try
                {
                    DatabaseProperties dbProps = await dbRef.ReadAsync(cancellationToken: cancellationToken);
                    if (!this.options.DatabaseFilter(dbProps))
                    {
                        return new TextResourceContents
                        {
                            Uri = $"cosmos://{database}/containers",
                            MimeType = "application/json",
                            Text = JsonSerializer.Serialize(new { error = "Database not accessible." })
                        };
                    }
                }
                catch (CosmosException)
                {
                    return new TextResourceContents
                    {
                        Uri = $"cosmos://{database}/containers",
                        MimeType = "application/json",
                        Text = JsonSerializer.Serialize(new { error = "Database not found." })
                    };
                }
            }

            Database cosmosDb = this.cosmosClient.GetDatabase(database);
            List<object> containers = new();

            using FeedIterator<ContainerProperties> iterator = cosmosDb.GetContainerQueryIterator<ContainerProperties>();

            while (iterator.HasMoreResults)
            {
                FeedResponse<ContainerProperties> response = await iterator.ReadNextAsync(cancellationToken);
                foreach (ContainerProperties props in response)
                {
                    if (this.options.ContainerFilter is not null && !this.options.ContainerFilter(database, props))
                    {
                        continue;
                    }

                    containers.Add(new
                    {
                        id = props.Id,
                        partitionKeyPath = string.Join(", ", props.PartitionKeyPaths ?? System.Array.Empty<string>()),
                        defaultTtl = props.DefaultTimeToLive,
                        indexingMode = props.IndexingPolicy?.IndexingMode.ToString()
                    });
                }
            }

            string json = JsonSerializer.Serialize(new { database, containers }, new JsonSerializerOptions { WriteIndented = true });

            return new TextResourceContents
            {
                Uri = $"cosmos://{database}/containers",
                MimeType = "application/json",
                Text = json
            };
        }
    }
}
