// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Mcp.Hosting
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Routing;

    /// <summary>
    /// Extension methods for mapping the Cosmos DB MCP server to a Streamable HTTP endpoint.
    /// </summary>
    public static class HttpHostExtensions
    {
        /// <summary>
        /// Maps the MCP server to a Streamable HTTP endpoint at the specified route.
        /// Requires <c>ModelContextProtocol.AspNetCore</c> to be installed.
        /// </summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <param name="pattern">The route pattern (default "/mcp").</param>
        /// <returns>The endpoint convention builder.</returns>
        public static IEndpointConventionBuilder MapCosmosMcpServer(
            this IEndpointRouteBuilder endpoints,
            string pattern = "/mcp")
        {
            return endpoints.MapMcp(pattern);
        }
    }
}
