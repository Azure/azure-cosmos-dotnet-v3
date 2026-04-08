// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Mcp.Tools
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Mcp.Security;
    using ModelContextProtocol.Server;

    using CosmosContainer = Microsoft.Azure.Cosmos.Container;

    /// <summary>
    /// MCP tool for partial update (patch) operations on a Cosmos DB document.
    /// </summary>
    [McpServerToolType]
    public class PatchTool
    {
        private readonly CosmosClient cosmosClient;
        private readonly OperationFilter operationFilter;

        public PatchTool(CosmosClient cosmosClient, OperationFilter operationFilter)
        {
            this.cosmosClient = cosmosClient;
            this.operationFilter = operationFilter;
        }

        [McpServerTool(Name = "cosmos_patch_item"), Description("Partially update a document using patch operations. Each operation specifies an op (set, add, remove, replace, incr), a path, and optionally a value. Requires write permissions.")]
        public async Task<string> PatchItemAsync(
            [Description("Database name")] string database,
            [Description("Container name")] string container,
            [Description("Document id")] string id,
            [Description("Partition key value")] string partition_key,
            [Description("JSON array of patch operations. Each: {\"op\": \"set|add|remove|replace|incr\", \"path\": \"/field\", \"value\": ...}")] string operations,
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

            List<PatchOperation> patchOps;
            try
            {
                patchOps = ParsePatchOperations(operations);
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = $"Invalid patch operations: {ex.Message}" });
            }

            if (patchOps.Count == 0)
            {
                return JsonSerializer.Serialize(new { error = "At least one patch operation is required." });
            }

            CosmosContainer cosmosContainer = this.cosmosClient.GetContainer(database, container);

            try
            {
                ItemResponse<JsonElement> response = await cosmosContainer.PatchItemAsync<JsonElement>(
                    id,
                    new PartitionKey(partition_key),
                    patchOps,
                    cancellationToken: cancellationToken);

                var result = new
                {
                    document = response.Resource,
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

        private static List<PatchOperation> ParsePatchOperations(string operationsJson)
        {
            List<PatchOperation> patchOps = new();

            using JsonDocument doc = JsonDocument.Parse(operationsJson);

            foreach (JsonElement opElement in doc.RootElement.EnumerateArray())
            {
                string op = opElement.GetProperty("op").GetString()
                    ?? throw new ArgumentException("Each operation must have an 'op' field.");
                string path = opElement.GetProperty("path").GetString()
                    ?? throw new ArgumentException("Each operation must have a 'path' field.");

                switch (op.ToLowerInvariant())
                {
                    case "set":
                        patchOps.Add(PatchOperation.Set(path, GetPatchValue(opElement)));
                        break;
                    case "add":
                        patchOps.Add(PatchOperation.Add(path, GetPatchValue(opElement)));
                        break;
                    case "remove":
                        patchOps.Add(PatchOperation.Remove(path));
                        break;
                    case "replace":
                        patchOps.Add(PatchOperation.Replace(path, GetPatchValue(opElement)));
                        break;
                    case "incr":
                    case "increment":
                        JsonElement incrValue = opElement.GetProperty("value");
                        if (incrValue.ValueKind == JsonValueKind.Number)
                        {
                            if (incrValue.TryGetInt64(out long longVal))
                            {
                                patchOps.Add(PatchOperation.Increment(path, longVal));
                            }
                            else
                            {
                                patchOps.Add(PatchOperation.Increment(path, incrValue.GetDouble()));
                            }
                        }
                        else
                        {
                            throw new ArgumentException($"Increment operation requires a numeric value for path '{path}'.");
                        }
                        break;
                    default:
                        throw new ArgumentException($"Unknown patch operation: '{op}'. Supported: set, add, remove, replace, incr.");
                }
            }

            return patchOps;
        }

        private static object? GetPatchValue(JsonElement opElement)
        {
            if (!opElement.TryGetProperty("value", out JsonElement value))
            {
                throw new ArgumentException("Patch operation requires a 'value' field (except for 'remove').");
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.TryGetInt64(out long l) ? l : value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => value.GetRawText()
            };
        }
    }
}
