//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    /// <summary>
    /// <see cref="CosmosContainer"/> fluent definition for creation flows.
    /// </summary>
    public abstract class CosmosContainerFluentDefinitionForCreate : CosmosContainerFluentDefinition
    {
        /// <summary>
        /// Sets the throughput provisioned for the Azure Cosmos container in measurement of Requests-per-Unit in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// If multiple calls are made to this method within the same <see cref="CosmosContainerFluentDefinition"/>, the last one will apply.
        /// </remarks>
        public abstract CosmosContainerFluentDefinition WithThroughput(int throughput);

        /// <summary>
        /// Defines a Unique Key policy for this Azure Cosmos container.
        /// </summary>
        public abstract UniqueKeyFluentDefinition WithUniqueKey();
    }
}
