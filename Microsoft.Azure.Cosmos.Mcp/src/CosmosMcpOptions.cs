// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Mcp
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Configuration options for the Cosmos DB MCP server.
    /// </summary>
    public class CosmosMcpOptions
    {
        /// <summary>
        /// Display name of the MCP server. Defaults to "cosmos-db-mcp".
        /// </summary>
        public string ServerName { get; set; } = "cosmos-db-mcp";

        /// <summary>
        /// Version string reported to MCP clients. Defaults to "0.1.0".
        /// </summary>
        public string ServerVersion { get; set; } = "0.1.0";

        /// <summary>
        /// Operations exposed to agents. Defaults to read-only (Read | Query | SchemaDiscovery).
        /// </summary>
        public McpOperations AllowedOperations { get; set; } = McpOperations.ReadOnly;

        /// <summary>
        /// Optional filter predicate for databases. Return true to expose a database.
        /// When null, all databases are exposed.
        /// </summary>
        public Func<DatabaseProperties, bool>? DatabaseFilter { get; set; }

        /// <summary>
        /// Optional filter predicate for containers. Return true to expose a container.
        /// When null, all containers in exposed databases are visible.
        /// </summary>
        public Func<string, ContainerProperties, bool>? ContainerFilter { get; set; }

        /// <summary>
        /// Names of tools to explicitly disable, even if their operation category is allowed.
        /// </summary>
        public HashSet<string> DisabledTools { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Query-related settings.
        /// </summary>
        public QueryOptions Query { get; set; } = new();

        /// <summary>
        /// Schema discovery settings.
        /// </summary>
        public SchemaDiscoveryOptions SchemaDiscovery { get; set; } = new();

        /// <summary>
        /// Vector search settings.
        /// </summary>
        public VectorSearchOptions VectorSearch { get; set; } = new();

        /// <summary>
        /// Whether to include the diagnostics analysis tool.
        /// </summary>
        public bool IncludeDiagnosticsTools { get; set; } = false;

        /// <summary>
        /// Latency threshold above which diagnostics are flagged. Defaults to 500ms.
        /// </summary>
        public TimeSpan DiagnosticsLatencyThreshold { get; set; } = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Checks whether the given operation is allowed.
        /// </summary>
        internal bool IsOperationAllowed(McpOperations operation)
        {
            return (this.AllowedOperations & operation) == operation;
        }
    }

    /// <summary>
    /// Query execution settings.
    /// </summary>
    public class QueryOptions
    {
        /// <summary>Maximum number of items returned per query. Defaults to 100.</summary>
        public int MaxItemCount { get; set; } = 100;

        /// <summary>Maximum allowed query text length in characters. Defaults to 2000.</summary>
        public int MaxQueryLengthChars { get; set; } = 2000;

        /// <summary>Whether cross-partition queries are allowed. Defaults to true.</summary>
        public bool AllowCrossPartitionQueries { get; set; } = true;

        /// <summary>Default consistency level for queries. Null uses the client default.</summary>
        public ConsistencyLevel? DefaultConsistencyLevel { get; set; }
    }

    /// <summary>
    /// Schema discovery settings.
    /// </summary>
    public class SchemaDiscoveryOptions
    {
        /// <summary>Number of documents to sample per container. Defaults to 20.</summary>
        public int SampleSize { get; set; } = 20;

        /// <summary>How long to cache inferred schemas. Defaults to 30 minutes.</summary>
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(30);
    }

    /// <summary>
    /// Vector search configuration.
    /// </summary>
    public class VectorSearchOptions
    {
        /// <summary>Whether vector search tools are enabled. Defaults to false.</summary>
        public bool Enabled { get; set; }

        /// <summary>Dimensionality of embedding vectors. Defaults to 1536.</summary>
        public int EmbeddingDimensions { get; set; } = 1536;

        /// <summary>JSON path to the vector property in documents. Defaults to "/embedding".</summary>
        public string VectorPath { get; set; } = "/embedding";

        /// <summary>Distance function for similarity computation. Defaults to Cosine.</summary>
        public DistanceFunction DistanceFunction { get; set; } = DistanceFunction.Cosine;
    }
}
