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
    /// MCP tool for deleting a document by id and partition key.
    /// </summary>
    [McpServerToolType]
    public class DeleteTool
    {
        private readonly CosmosClient cosmosClient;
        private readonly OperationFilter operationFilter;

        public DeleteTool(CosmosClient cosmosClient, OperationFilter operationFilter)
        {
            this.cosmosClient = cosmosClient;
            this.operationFilter = operationFilter;
        }

        [McpServerTool(Name = "cosmos_delete_item"), Description("Delete a document by its id and partition key. Requires write permissions to be enabled.")]
        public async Task<string> DeleteItemAsync(
            [Description("Database name")] string database,
            [Description("Container name")] string container,
            [Description("Document id")] string id,
            [Description("Partition key value")] string partition_key,
            CancellationToken cancellationToken = default)
        {
            if (!this.operationFilter.IsOperationAllowed(McpOperations.Write))
            {
                return JsonSerializer.Serialize(new { error = "Write operations are not enabled. Set AllowedOperations to include McpOperations.Write." });
            }

            string? accessError = await this.operationFilter.ValidateContainerAccessAsync(database, container);
            if (accessError is not null)
            {
                return JsonSerializer.Serialize(new { error = accessError });
            }

            CosmosContainer cosmosContainer = this.cosmosClient.GetContainer(database, container);

            try
            {
                ItemResponse<object> response = await cosmosContainer.DeleteItemAsync<object>(
                    id,
                    new PartitionKey(partition_key),
                    cancellationToken: cancellationToken);

                var result = new
                {
                    deleted = true,
                    request_charge = Math.Round(response.RequestCharge, 2)
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
