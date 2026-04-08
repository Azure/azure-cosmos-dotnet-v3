// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Mcp.Tools
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Mcp.Security;
    using ModelContextProtocol.Server;

    using CosmosContainer = Microsoft.Azure.Cosmos.Container;

    /// <summary>
    /// MCP tool for executing SQL queries against Cosmos DB containers.
    /// </summary>
    [McpServerToolType]
    public class QueryTool
    {
        private readonly CosmosClient cosmosClient;
        private readonly CosmosMcpOptions options;
        private readonly OperationFilter operationFilter;

        public QueryTool(CosmosClient cosmosClient, CosmosMcpOptions options, OperationFilter operationFilter)
        {
            this.cosmosClient = cosmosClient;
            this.options = options;
            this.operationFilter = operationFilter;
        }

        [McpServerTool(Name = "cosmos_query"), Description("Execute a SQL query against a Cosmos DB container. Returns documents, RU cost, and pagination token.")]
        public async Task<string> ExecuteQueryAsync(
            [Description("Database name")] string database,
            [Description("Container name")] string container,
            [Description("SQL query string (SELECT only)")] string query,
            [Description("Query parameters as JSON object (e.g. {\"@city\": \"Seattle\"})")] string? parameters = null,
            [Description("Partition key value to scope the query")] string? partition_key = null,
            [Description("Maximum number of documents to return (default 25, max configured limit)")] int max_items = 25,
            CancellationToken cancellationToken = default)
        {
            if (!this.operationFilter.IsOperationAllowed(McpOperations.Query))
            {
                return JsonSerializer.Serialize(new { error = "Query operations are not enabled." });
            }

            string? accessError = await this.operationFilter.ValidateContainerAccessAsync(database, container);
            if (accessError is not null)
            {
                return JsonSerializer.Serialize(new { error = accessError });
            }

            string? validationError = QuerySanitizer.Validate(query, this.options.Query.MaxQueryLengthChars);
            if (validationError is not null)
            {
                return JsonSerializer.Serialize(new { error = validationError });
            }

            int effectiveMaxItems = Math.Min(max_items, this.options.Query.MaxItemCount);

            CosmosContainer cosmosContainer = this.cosmosClient.GetContainer(database, container);

            QueryDefinition queryDef = new QueryDefinition(query);

            if (!string.IsNullOrEmpty(parameters))
            {
                try
                {
                    using JsonDocument paramDoc = JsonDocument.Parse(parameters);
                    foreach (JsonProperty prop in paramDoc.RootElement.EnumerateObject())
                    {
                        string paramName = prop.Name.StartsWith("@") ? prop.Name : "@" + prop.Name;
                        queryDef = queryDef.WithParameter(paramName, prop.Value.ToString());
                    }
                }
                catch (JsonException)
                {
                    return JsonSerializer.Serialize(new { error = "Invalid JSON in parameters." });
                }
            }

            QueryRequestOptions requestOptions = new QueryRequestOptions
            {
                MaxItemCount = effectiveMaxItems
            };

            if (this.options.Query.DefaultConsistencyLevel.HasValue)
            {
                requestOptions.ConsistencyLevel = this.options.Query.DefaultConsistencyLevel.Value;
            }

            if (!string.IsNullOrEmpty(partition_key))
            {
                requestOptions.PartitionKey = new PartitionKey(partition_key);
            }

            try
            {
                List<JsonElement> documents = new();
                double totalRU = 0;
                string? continuationToken = null;

                using FeedIterator<JsonElement> iterator = cosmosContainer.GetItemQueryIterator<JsonElement>(
                    queryDef,
                    requestOptions: requestOptions);

                if (iterator.HasMoreResults)
                {
                    FeedResponse<JsonElement> response = await iterator.ReadNextAsync(cancellationToken);
                    totalRU += response.RequestCharge;
                    continuationToken = response.ContinuationToken;

                    foreach (JsonElement item in response)
                    {
                        documents.Add(item);
                    }
                }

                var result = new
                {
                    documents,
                    document_count = documents.Count,
                    request_charge = Math.Round(totalRU, 2),
                    continuation_token = continuationToken,
                    has_more_results = iterator.HasMoreResults
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
