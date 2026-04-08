// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Mcp.Tools
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Mcp.Security;
    using ModelContextProtocol.Server;

    /// <summary>
    /// MCP tool for listing containers in a Cosmos DB database with metadata.
    /// </summary>
    [McpServerToolType]
    public class ContainerListTool
    {
        private readonly CosmosClient cosmosClient;
        private readonly CosmosMcpOptions options;
        private readonly OperationFilter operationFilter;

        public ContainerListTool(CosmosClient cosmosClient, CosmosMcpOptions options, OperationFilter operationFilter)
        {
            this.cosmosClient = cosmosClient;
            this.options = options;
            this.operationFilter = operationFilter;
        }

        [McpServerTool(Name = "cosmos_list_containers"), Description("List containers in a Cosmos DB database with partition key paths and metadata.")]
        public async Task<string> ListContainersAsync(
            [Description("Database name")] string database,
            CancellationToken cancellationToken = default)
        {
            string? dbError = await this.operationFilter.ValidateDatabaseAccessAsync(database);
            if (dbError is not null)
            {
                return JsonSerializer.Serialize(new { error = dbError });
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

                    string partitionKeyPath = string.Join(", ", props.PartitionKeyPaths ?? Array.Empty<string>());

                    var containerInfo = new
                    {
                        id = props.Id,
                        partitionKeyPath,
                        defaultTtl = props.DefaultTimeToLive,
                        indexingMode = props.IndexingPolicy?.IndexingMode.ToString(),
                        includedPathCount = props.IndexingPolicy?.IncludedPaths?.Count ?? 0,
                        excludedPathCount = props.IndexingPolicy?.ExcludedPaths?.Count ?? 0,
                        uniqueKeyCount = props.UniqueKeyPolicy?.UniqueKeys?.Count ?? 0
                    };

                    containers.Add(containerInfo);
                }
            }

            return JsonSerializer.Serialize(new { containers }, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
