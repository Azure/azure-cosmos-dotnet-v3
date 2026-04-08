// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Mcp.Tools
{
    using System;
    using System.ComponentModel;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Mcp.Security;
    using ModelContextProtocol.Server;

    using CosmosContainer = Microsoft.Azure.Cosmos.Container;

    /// <summary>
    /// MCP tool for point reading a single document by id and partition key.
    /// </summary>
    [McpServerToolType]
    public class PointReadTool
    {
        private readonly CosmosClient cosmosClient;
        private readonly OperationFilter operationFilter;

        public PointReadTool(CosmosClient cosmosClient, OperationFilter operationFilter)
        {
            this.cosmosClient = cosmosClient;
            this.operationFilter = operationFilter;
        }

        [McpServerTool(Name = "cosmos_read_item"), Description("Read a single document by its id and partition key. Returns the document, RU cost, and ETag.")]
        public async Task<string> ReadItemAsync(
            [Description("Database name")] string database,
            [Description("Container name")] string container,
            [Description("Document id")] string id,
            [Description("Partition key value")] string partition_key,
            CancellationToken cancellationToken = default)
        {
            if (!this.operationFilter.IsOperationAllowed(McpOperations.Read))
            {
                return JsonSerializer.Serialize(new { error = "Read operations are not enabled." });
            }

            string? accessError = await this.operationFilter.ValidateContainerAccessAsync(database, container);
            if (accessError is not null)
            {
                return JsonSerializer.Serialize(new { error = accessError });
            }

            CosmosContainer cosmosContainer = this.cosmosClient.GetContainer(database, container);

            try
            {
                ItemResponse<JsonElement> response = await cosmosContainer.ReadItemAsync<JsonElement>(
                    id,
                    new PartitionKey(partition_key),
                    cancellationToken: cancellationToken);

                var result = new
                {
                    document = response.Resource,
                    request_charge = Math.Round(response.RequestCharge, 2),
                    etag = response.ETag
                };

                return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (CosmosException ex)
            {
                return JsonSerializer.Serialize(new
                {
                    error = ex.Message,
                    status_code = (int)ex.StatusCode,
                    request_charge = Math.Round(ex.RequestCharge, 2)
                });
            }
        }
    }
}
