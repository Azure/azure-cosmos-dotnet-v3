// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Mcp.Tools
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Mcp.Security;
    using ModelContextProtocol.Server;

    using CosmosContainer = Microsoft.Azure.Cosmos.Container;

    /// <summary>
    /// MCP tool for vector similarity search in a Cosmos DB container.
    /// </summary>
    [McpServerToolType]
    public class VectorSearchTool
    {
        private readonly CosmosClient cosmosClient;
        private readonly CosmosMcpOptions options;
        private readonly OperationFilter operationFilter;

        public VectorSearchTool(CosmosClient cosmosClient, CosmosMcpOptions options, OperationFilter operationFilter)
        {
            this.cosmosClient = cosmosClient;
            this.options = options;
            this.operationFilter = operationFilter;
        }

        [McpServerTool(Name = "cosmos_vector_search"), Description("Perform vector similarity search to find nearest neighbors. Returns documents ranked by similarity score, excluding the embedding vector from results by default.")]
        public async Task<string> VectorSearchAsync(
            [Description("Database name")] string database,
            [Description("Container name")] string container,
            [Description("Query embedding vector as JSON array of numbers")] string query_vector,
            [Description("Number of nearest neighbors to return (default 10)")] int top_k = 10,
            [Description("SQL WHERE clause to pre-filter candidates (e.g. \"c.category = 'electronics'\")")] string? filter = null,
            [Description("Fields to return as comma-separated list (embedding excluded by default)")] string? projection = null,
            CancellationToken cancellationToken = default)
        {
            if (!this.operationFilter.IsOperationAllowed(McpOperations.VectorSearch))
            {
                return JsonSerializer.Serialize(new { error = "Vector search is not enabled." });
            }

            if (!this.options.VectorSearch.Enabled)
            {
                return JsonSerializer.Serialize(new { error = "Vector search is not configured. Enable it in CosmosMcpOptions.VectorSearch." });
            }

            string? accessError = await this.operationFilter.ValidateContainerAccessAsync(database, container);
            if (accessError is not null)
            {
                return JsonSerializer.Serialize(new { error = accessError });
            }

            // Parse the query vector
            List<float> vectorValues;
            try
            {
                vectorValues = JsonSerializer.Deserialize<List<float>>(query_vector)
                    ?? throw new JsonException("Null vector");
            }
            catch (JsonException)
            {
                return JsonSerializer.Serialize(new { error = "Invalid query_vector. Provide a JSON array of numbers." });
            }

            string vectorPath = this.options.VectorSearch.VectorPath.TrimStart('/');
            string distanceFunction = this.options.VectorSearch.DistanceFunction.ToString().ToLowerInvariant();

            // Build projection list (exclude embedding by default)
            string selectClause;
            if (!string.IsNullOrEmpty(projection))
            {
                string[] fields = projection.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                selectClause = string.Join(", ", fields.Select(f => $"c.{f}"));
            }
            else
            {
                selectClause = "c";
            }

            // Build the vector search query
            StringBuilder queryBuilder = new();
            queryBuilder.Append($"SELECT TOP {top_k} {selectClause}, ");
            queryBuilder.Append($"VectorDistance(c.{vectorPath}, @queryVector) AS SimilarityScore ");
            queryBuilder.Append("FROM c ");

            if (!string.IsNullOrEmpty(filter))
            {
                queryBuilder.Append($"WHERE {filter} ");
            }

            queryBuilder.Append("ORDER BY VectorDistance(c.{vectorPath}, @queryVector)");

            string queryText = queryBuilder.ToString();

            QueryDefinition queryDef = new QueryDefinition(queryText)
                .WithParameter("@queryVector", vectorValues.ToArray());

            CosmosContainer cosmosContainer = this.cosmosClient.GetContainer(database, container);

            try
            {
                List<JsonElement> results = new();
                double totalRU = 0;

                using FeedIterator<JsonElement> iterator = cosmosContainer.GetItemQueryIterator<JsonElement>(
                    queryDef,
                    requestOptions: new QueryRequestOptions { MaxItemCount = top_k });

                while (iterator.HasMoreResults)
                {
                    FeedResponse<JsonElement> response = await iterator.ReadNextAsync(cancellationToken);
                    totalRU += response.RequestCharge;

                    foreach (JsonElement item in response)
                    {
                        results.Add(item);
                    }
                }

                var result = new
                {
                    results,
                    result_count = results.Count,
                    request_charge = Math.Round(totalRU, 2)
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
