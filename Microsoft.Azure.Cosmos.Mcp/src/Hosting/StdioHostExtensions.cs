// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Mcp.Hosting
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Extension methods for configuring stdio transport on the MCP server.
    /// </summary>
    public static class StdioHostExtensions
    {
        /// <summary>
        /// Configures the MCP server to use stdio (standard input/output) transport.
        /// This is the recommended transport for use with Claude Code, VS Code Copilot, and similar tools.
        /// </summary>
        public static IMcpServerBuilder WithStdioTransport(this IMcpServerBuilder builder)
        {
            builder.WithStdioServerTransport();
            return builder;
        }
    }
}
