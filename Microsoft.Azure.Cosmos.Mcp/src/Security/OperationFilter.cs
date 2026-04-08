// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Mcp.Security
{
    using System.Threading.Tasks;

    using CosmosContainer = Microsoft.Azure.Cosmos.Container;

    /// <summary>
    /// Validates that a target database/container is accessible given the current filter configuration.
    /// </summary>
    public class OperationFilter
    {
        private readonly CosmosMcpOptions options;
        private readonly CosmosClient cosmosClient;

        public OperationFilter(CosmosMcpOptions options, CosmosClient cosmosClient)
        {
            this.options = options;
            this.cosmosClient = cosmosClient;
        }

        /// <summary>
        /// Returns null if the database is allowed, or an error message if filtered out.
        /// </summary>
        public async Task<string?> ValidateDatabaseAccessAsync(string databaseId)
        {
            if (this.options.DatabaseFilter is null)
            {
                return null;
            }

            Database database = this.cosmosClient.GetDatabase(databaseId);
            try
            {
                DatabaseProperties props = await database.ReadAsync();
                if (!this.options.DatabaseFilter(props))
                {
                    return $"Access denied: database '{databaseId}' is not accessible.";
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return $"Database '{databaseId}' not found.";
            }

            return null;
        }

        /// <summary>
        /// Returns null if the container is allowed, or an error message if filtered out.
        /// </summary>
        public async Task<string?> ValidateContainerAccessAsync(string databaseId, string containerId)
        {
            string? dbError = await this.ValidateDatabaseAccessAsync(databaseId);
            if (dbError is not null)
            {
                return dbError;
            }

            if (this.options.ContainerFilter is null)
            {
                return null;
            }

            CosmosContainer container = this.cosmosClient.GetContainer(databaseId, containerId);
            try
            {
                ContainerProperties props = await container.ReadContainerAsync();
                if (!this.options.ContainerFilter(databaseId, props))
                {
                    return $"Access denied: container '{containerId}' in database '{databaseId}' is not accessible.";
                }
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return $"Container '{containerId}' not found in database '{databaseId}'.";
            }

            return null;
        }

        /// <summary>
        /// Checks if a specific operation is allowed by the configuration.
        /// </summary>
        public bool IsOperationAllowed(McpOperations operation)
        {
            return this.options.IsOperationAllowed(operation);
        }
    }
}
