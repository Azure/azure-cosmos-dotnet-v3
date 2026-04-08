// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Mcp.Tools
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Mcp.Security;
    using ModelContextProtocol.Server;

    /// <summary>
    /// MCP tool for listing databases in the Cosmos DB account.
    /// </summary>
    [McpServerToolType]
    public class DatabaseListTool
    {
        private readonly CosmosClient cosmosClient;
        private readonly CosmosMcpOptions options;

        public DatabaseListTool(CosmosClient cosmosClient, CosmosMcpOptions options)
        {
            this.cosmosClient = cosmosClient;
            this.options = options;
        }

        [McpServerTool(Name = "cosmos_list_databases"), Description("List all databases in the Cosmos DB account.")]
        public async Task<string> ListDatabasesAsync(CancellationToken cancellationToken = default)
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

            return JsonSerializer.Serialize(new { databases }, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
