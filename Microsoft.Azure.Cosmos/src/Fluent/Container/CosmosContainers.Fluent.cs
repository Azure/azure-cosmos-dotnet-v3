//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Cosmos.Fluent;

    public partial class CosmosContainers
    {
            /// <summary>
        /// Create an Azure Cosmos container through a Fluent API.
        /// </summary>
        /// <param name="name">Azure Cosmos container name to create.</param>
        /// <param name="partitionKeyPath">The path to the partition key. Example: /location</param>
        /// <returns>A fluent definition of an Azure Cosmos container.</returns>
        public abstract CosmosContainerFluentDefinitionForCreate Create(
            string name,
            string partitionKeyPath);
    }
}
