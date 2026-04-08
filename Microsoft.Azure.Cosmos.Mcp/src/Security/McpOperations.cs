// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Mcp
{
    using System;

    /// <summary>
    /// Flags that control which MCP operations are exposed to agents.
    /// </summary>
    [Flags]
    public enum McpOperations
    {
        /// <summary>No operations allowed.</summary>
        None = 0,

        /// <summary>Point reads by id + partition key.</summary>
        Read = 1 << 0,

        /// <summary>SQL query execution.</summary>
        Query = 1 << 1,

        /// <summary>Write operations: upsert, delete, patch.</summary>
        Write = 1 << 2,

        /// <summary>Admin operations: create/delete containers and databases.</summary>
        Admin = 1 << 3,

        /// <summary>Schema discovery via document sampling.</summary>
        SchemaDiscovery = 1 << 4,

        /// <summary>Vector similarity search.</summary>
        VectorSearch = 1 << 5,

        /// <summary>Diagnostics analysis tools.</summary>
        Diagnostics = 1 << 6,

        /// <summary>All read-only operations (Read | Query | SchemaDiscovery).</summary>
        ReadOnly = Read | Query | SchemaDiscovery,

        /// <summary>All operations.</summary>
        All = Read | Query | Write | Admin | SchemaDiscovery | VectorSearch | Diagnostics
    }
}
