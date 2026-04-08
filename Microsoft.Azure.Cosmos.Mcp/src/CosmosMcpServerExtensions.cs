// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Mcp
{
    using System;
    using Microsoft.Azure.Cosmos.Mcp.Resources;
    using Microsoft.Azure.Cosmos.Mcp.Schema;
    using Microsoft.Azure.Cosmos.Mcp.Security;
    using Microsoft.Azure.Cosmos.Mcp.Tools;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Extension methods for registering the Cosmos DB MCP server in a DI container.
    /// </summary>
    public static class CosmosMcpServerExtensions
    {
        /// <summary>
        /// Adds the Cosmos DB MCP server with default (read-only) configuration.
        /// Requires a <see cref="CosmosClient"/> to already be registered in the service collection.
        /// </summary>
        public static IMcpServerBuilder AddCosmosMcpServer(
            this IServiceCollection services,
            Action<CosmosMcpOptions>? configure = null)
        {
            CosmosMcpOptions options = new();
            configure?.Invoke(options);

            services.AddSingleton(options);
            services.AddSingleton<OperationFilter>();
            services.AddSingleton<SchemaInferrer>();
            services.AddSingleton(sp => new SchemaCache(
                sp.GetRequiredService<SchemaInferrer>(),
                options.SchemaDiscovery.CacheDuration));

            IMcpServerBuilder builder = services.AddMcpServer(serverOptions =>
            {
                serverOptions.ServerInfo = new()
                {
                    Name = options.ServerName,
                    Version = options.ServerVersion
                };
            });

            // Always register read-only tools and resources
            builder.WithTools<DatabaseListTool>();
            builder.WithTools<ContainerListTool>();
            builder.WithResources<CosmosCatalogResources>();

            if (options.IsOperationAllowed(McpOperations.Query))
            {
                builder.WithTools<QueryTool>();
            }

            if (options.IsOperationAllowed(McpOperations.Read))
            {
                builder.WithTools<PointReadTool>();
            }

            if (options.IsOperationAllowed(McpOperations.SchemaDiscovery))
            {
                builder.WithTools<SchemaTool>();
                builder.WithResources<ContainerMetadataResources>();
            }

            return builder;
        }
    }
}
