//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    using System.Collections.Generic;

    /// <summary>
    /// <see cref="CosmosContainer"/> fluent definition for creation flows.
    /// </summary>
    public class CosmosContainerFluentDefinitionForCreate : CosmosContainerFluentDefinition
    {
        private readonly List<UniqueKeyFluentDefinition> uniqueueKeyBuilders = new List<UniqueKeyFluentDefinition>();

        /// <summary>
        /// Empty constructor that can be used for unit testing
        /// </summary>
        public CosmosContainerFluentDefinitionForCreate() { }

        internal CosmosContainerFluentDefinitionForCreate(
            CosmosContainers cosmosContainers, 
            string name, 
            string partitionKeyPath)
            : base (cosmosContainers, name, FluentSettingsOperation.Create)
        {
        }

        /// <summary>
        /// Defines a Unique Key policy for this Azure Cosmos container.
        /// </summary>
        public virtual UniqueKeyFluentDefinition WithUniqueKey()
        {
            UniqueKeyFluentDefinition newBuilder = new UniqueKeyFluentDefinition(this);
            this.uniqueueKeyBuilders.Add(newBuilder);
            return newBuilder;
        }
    }
}
