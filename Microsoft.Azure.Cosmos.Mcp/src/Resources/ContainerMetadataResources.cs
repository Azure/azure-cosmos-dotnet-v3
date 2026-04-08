// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Mcp.Resources
{
    using System;
    using System.ComponentModel;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Mcp.Schema;
    using Microsoft.Azure.Cosmos.Mcp.Security;
    using ModelContextProtocol.Protocol;
    using ModelContextProtocol.Server;

    using CosmosContainer = Microsoft.Azure.Cosmos.Container;

    /// <summary>
    /// MCP resources for container schema, indexing policy, and throughput metadata.
    /// </summary>
    [McpServerResourceType]
    public class ContainerMetadataResources
    {
        private readonly CosmosClient cosmosClient;
        private readonly CosmosMcpOptions options;
        private readonly OperationFilter operationFilter;
        private readonly SchemaCache schemaCache;

        public ContainerMetadataResources(
            CosmosClient cosmosClient,
            CosmosMcpOptions options,
            OperationFilter operationFilter,
            SchemaCache schemaCache)
        {
            this.cosmosClient = cosmosClient;
            this.options = options;
            this.operationFilter = operationFilter;
            this.schemaCache = schemaCache;
        }

        [McpServerResource(UriTemplate = "cosmos://{database}/{container}/schema", Name = "Container Schema", MimeType = "application/json")]
        [Description("Inferred JSON Schema from document sampling for a Cosmos DB container.")]
        public async Task<ResourceContents> GetSchemaAsync(
            [Description("Database name")] string database,
            [Description("Container name")] string container,
            CancellationToken cancellationToken = default)
        {
            string uri = $"cosmos://{database}/{container}/schema";

            string? accessError = await this.operationFilter.ValidateContainerAccessAsync(database, container);
            if (accessError is not null)
            {
                return ErrorResource(uri, accessError);
            }

            try
            {
                CosmosContainer cosmosContainer = this.cosmosClient.GetContainer(database, container);
                InferredSchema schema = await this.schemaCache.GetSchemaAsync(
                    cosmosContainer,
                    database,
                    container,
                    this.options.SchemaDiscovery.SampleSize,
                    cancellationToken);

                var result = new
                {
                    schema = schema.Schema,
                    sample_count = schema.SampleCount,
                    partition_key_path = schema.PartitionKeyPath,
                    unique_key_paths = schema.UniqueKeyPaths,
                    x_cosmos_partition_key = schema.PartitionKeyPath
                };

                return new TextResourceContents
                {
                    Uri = uri,
                    MimeType = "application/json",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                };
            }
            catch (CosmosException ex)
            {
                return ErrorResource(uri, ex.Message);
            }
        }

        [McpServerResource(UriTemplate = "cosmos://{database}/{container}/indexing-policy", Name = "Indexing Policy", MimeType = "application/json")]
        [Description("Current indexing policy configuration for a Cosmos DB container.")]
        public async Task<ResourceContents> GetIndexingPolicyAsync(
            [Description("Database name")] string database,
            [Description("Container name")] string container,
            CancellationToken cancellationToken = default)
        {
            string uri = $"cosmos://{database}/{container}/indexing-policy";

            string? accessError = await this.operationFilter.ValidateContainerAccessAsync(database, container);
            if (accessError is not null)
            {
                return ErrorResource(uri, accessError);
            }

            try
            {
                CosmosContainer cosmosContainer = this.cosmosClient.GetContainer(database, container);
                ContainerProperties props = await cosmosContainer.ReadContainerAsync(cancellationToken: cancellationToken);

                IndexingPolicy policy = props.IndexingPolicy;
                var result = new
                {
                    indexingMode = policy.IndexingMode.ToString(),
                    automatic = policy.Automatic,
                    includedPaths = policy.IncludedPaths.Select(p => new
                    {
                        path = p.Path
                    }),
                    excludedPaths = policy.ExcludedPaths.Select(p => new
                    {
                        path = p.Path
                    }),
                    compositeIndexes = policy.CompositeIndexes.Select(ci => ci.Select(p => new
                    {
                        path = p.Path,
                        order = p.Order.ToString()
                    })),
                    spatialIndexes = policy.SpatialIndexes.Select(si => new
                    {
                        path = si.Path,
                        spatialTypes = si.SpatialTypes.Select(t => t.ToString())
                    })
                };

                return new TextResourceContents
                {
                    Uri = uri,
                    MimeType = "application/json",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                };
            }
            catch (CosmosException ex)
            {
                return ErrorResource(uri, ex.Message);
            }
        }

        [McpServerResource(UriTemplate = "cosmos://{database}/{container}/throughput", Name = "Throughput Configuration", MimeType = "application/json")]
        [Description("Current provisioned throughput and autoscale settings for a Cosmos DB container.")]
        public async Task<ResourceContents> GetThroughputAsync(
            [Description("Database name")] string database,
            [Description("Container name")] string container,
            CancellationToken cancellationToken = default)
        {
            string uri = $"cosmos://{database}/{container}/throughput";

            string? accessError = await this.operationFilter.ValidateContainerAccessAsync(database, container);
            if (accessError is not null)
            {
                return ErrorResource(uri, accessError);
            }

            try
            {
                CosmosContainer cosmosContainer = this.cosmosClient.GetContainer(database, container);
                ThroughputResponse throughputResponse = await cosmosContainer.ReadThroughputAsync(
                    new RequestOptions(),
                    cancellationToken);

                ThroughputProperties throughputProps = throughputResponse.Resource;

                var result = new
                {
                    throughput = throughputProps.Throughput,
                    autoscaleMaxThroughput = throughputProps.AutoscaleMaxThroughput,
                    isAutoscale = throughputProps.AutoscaleMaxThroughput.HasValue,
                    requestCharge = Math.Round(throughputResponse.RequestCharge, 2)
                };

                return new TextResourceContents
                {
                    Uri = uri,
                    MimeType = "application/json",
                    Text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                };
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest || ex.Message.Contains("offer"))
            {
                // Container might use database-level throughput
                return new TextResourceContents
                {
                    Uri = uri,
                    MimeType = "application/json",
                    Text = JsonSerializer.Serialize(new
                    {
                        note = "This container uses database-level (shared) throughput. Query the database for throughput settings.",
                        status = "shared_throughput"
                    }, new JsonSerializerOptions { WriteIndented = true })
                };
            }
            catch (CosmosException ex)
            {
                return ErrorResource(uri, ex.Message);
            }
        }

        private static TextResourceContents ErrorResource(string uri, string errorMessage)
        {
            return new TextResourceContents
            {
                Uri = uri,
                MimeType = "application/json",
                Text = JsonSerializer.Serialize(new { error = errorMessage })
            };
        }
    }
}
