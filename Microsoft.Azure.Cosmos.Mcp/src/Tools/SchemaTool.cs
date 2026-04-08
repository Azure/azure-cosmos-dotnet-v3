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
    using Microsoft.Azure.Cosmos.Mcp.Schema;
    using Microsoft.Azure.Cosmos.Mcp.Security;
    using ModelContextProtocol.Server;

    using CosmosContainer = Microsoft.Azure.Cosmos.Container;

    /// <summary>
    /// MCP tool for inferring the schema of documents in a Cosmos DB container.
    /// </summary>
    [McpServerToolType]
    public class SchemaTool
    {
        private readonly CosmosClient cosmosClient;
        private readonly CosmosMcpOptions options;
        private readonly OperationFilter operationFilter;
        private readonly SchemaCache schemaCache;

        public SchemaTool(CosmosClient cosmosClient, CosmosMcpOptions options, OperationFilter operationFilter, SchemaCache schemaCache)
        {
            this.cosmosClient = cosmosClient;
            this.options = options;
            this.operationFilter = operationFilter;
            this.schemaCache = schemaCache;
        }

        [McpServerTool(Name = "cosmos_get_schema"), Description("Infer the schema of documents in a Cosmos DB container by sampling recent documents. Returns a JSON Schema with field names, types, required fields, and Cosmos DB metadata.")]
        public async Task<string> GetSchemaAsync(
            [Description("Database name")] string database,
            [Description("Container name")] string container,
            [Description("Number of documents to sample (default 20)")] int sample_size = 20,
            CancellationToken cancellationToken = default)
        {
            if (!this.operationFilter.IsOperationAllowed(McpOperations.SchemaDiscovery))
            {
                return JsonSerializer.Serialize(new { error = "Schema discovery is not enabled." });
            }

            string? accessError = await this.operationFilter.ValidateContainerAccessAsync(database, container);
            if (accessError is not null)
            {
                return JsonSerializer.Serialize(new { error = accessError });
            }

            int effectiveSampleSize = Math.Min(Math.Max(sample_size, 1), 100);

            CosmosContainer cosmosContainer = this.cosmosClient.GetContainer(database, container);

            try
            {
                InferredSchema schema = await this.schemaCache.GetSchemaAsync(
                    cosmosContainer,
                    database,
                    container,
                    effectiveSampleSize,
                    cancellationToken);

                var result = new
                {
                    schema = schema.Schema,
                    sample_count = schema.SampleCount,
                    partition_key_path = schema.PartitionKeyPath,
                    unique_key_paths = schema.UniqueKeyPaths
                };

                return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (CosmosException ex)
            {
                return JsonSerializer.Serialize(new
                {
                    error = ex.Message,
                    status_code = (int)ex.StatusCode
                });
            }
        }
    }
}
