// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Mcp.Tools
{
    using System;
    using System.ComponentModel;
    using System.Net;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Mcp.Security;
    using ModelContextProtocol.Server;

    using CosmosContainer = Microsoft.Azure.Cosmos.Container;

    /// <summary>
    /// MCP tool for upserting (create or replace) a document in a Cosmos DB container.
    /// </summary>
    [McpServerToolType]
    public class UpsertTool
    {
        private readonly CosmosClient cosmosClient;
        private readonly OperationFilter operationFilter;

        public UpsertTool(CosmosClient cosmosClient, OperationFilter operationFilter)
        {
            this.cosmosClient = cosmosClient;
            this.operationFilter = operationFilter;
        }

        [McpServerTool(Name = "cosmos_upsert_item"), Description("Create or replace a document in a Cosmos DB container. The document must include an 'id' field and the partition key field. Requires write permissions to be enabled.")]
        public async Task<string> UpsertItemAsync(
            [Description("Database name")] string database,
            [Description("Container name")] string container,
            [Description("The document to upsert as a JSON string")] string document,
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

            JsonElement docElement;
            try
            {
                docElement = JsonDocument.Parse(document).RootElement;
            }
            catch (JsonException)
            {
                return JsonSerializer.Serialize(new { error = "Invalid JSON document." });
            }

            if (!docElement.TryGetProperty("id", out _))
            {
                return JsonSerializer.Serialize(new { error = "Document must contain an 'id' field." });
            }

            CosmosContainer cosmosContainer = this.cosmosClient.GetContainer(database, container);

            try
            {
                ItemResponse<JsonElement> response = await cosmosContainer.UpsertItemAsync(
                    docElement,
                    cancellationToken: cancellationToken);

                string status = response.StatusCode == HttpStatusCode.Created ? "created" : "replaced";

                var result = new
                {
                    document = response.Resource,
                    request_charge = Math.Round(response.RequestCharge, 2),
                    status
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
