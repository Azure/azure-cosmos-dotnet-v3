//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// <see cref="CosmosContainers"/> extension for Fluent definitions.
    /// </summary>
    public static class CosmosContainersFluent
    {
        /// <summary>
        /// Create an Azure Cosmos container through a Fluent API.
        /// </summary>
        /// <param name="cosmosContainers">Current instance of the Azure Cosmos containers.</param>
        /// <param name="name">Azure Cosmos container name to create.</param>
        /// <param name="partitionKeyPath">The path to the partition key. Example: /location</param>
        /// <returns>A fluent definition of an Azure Cosmos container.</returns>
        public static CosmosContainerFluentDefinition Create(
            this CosmosContainers cosmosContainers, 
            string name,
            string partitionKeyPath)
        {
            return new CosmosContainerFluentDefinitionForCreate(cosmosContainers, name, partitionKeyPath);
        }

        /// <summary>
        /// Replace an Azure Cosmos container through a Fluent API.
        /// </summary>
        /// <param name="cosmosContainers">Current instance of the Azure Cosmos containers.</param>
        /// <param name="name">Azure Cosmos container name to replace.</param>
        /// <returns>A fluent definition of an Azure Cosmos container.</returns>
        public static CosmosContainerFluentDefinition Replace(
            this CosmosContainers cosmosContainers,
            string name)
        {
            return new CosmosContainerFluentDefinition(cosmosContainers, name, FluentSettingsOperation.Replace);
        }
    }
}
