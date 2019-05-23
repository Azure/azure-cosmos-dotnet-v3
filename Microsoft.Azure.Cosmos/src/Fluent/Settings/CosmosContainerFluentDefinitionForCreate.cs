//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Fluent
{
    using System.Threading.Tasks;

    /// <summary>
    /// <see cref="CosmosContainer"/> fluent definition for creation flows.
    /// </summary>
    public class CosmosContainerFluentDefinitionForCreate : CosmosContainerFluentDefinition<CosmosContainerFluentDefinitionForCreate>
    {
        private readonly CosmosContainers cosmosContainers;
        private UniqueKeyPolicy uniqueKeyPolicy;

        /// <summary>
        /// Creates an instance for unit-testing
        /// </summary>
        public CosmosContainerFluentDefinitionForCreate()
        {
        }

        internal CosmosContainerFluentDefinitionForCreate(
            CosmosContainers cosmosContainers,
            string name,
            string partitionKeyPath = null)
            : base(name, partitionKeyPath)
        {
            this.cosmosContainers = cosmosContainers;
        }

        /// <summary>
        /// Defines a Unique Key policy for this Azure Cosmos container.
        /// </summary>
        /// <returns>An instance of <see cref="UniqueKeyFluentDefinition"/>.</returns>
        public virtual UniqueKeyFluentDefinition WithUniqueKey()
        {
            return new UniqueKeyFluentDefinition(
                this,
                (uniqueKey) => this.AddUniqueKey(uniqueKey));
        }

        /// <summary>
        /// Creates a container with the current fluent definition.
        /// </summary>
        /// <param name="throughput">Desired throughput for the container</param>
        /// <returns>An asynchronous Task representing the creation of a <see cref="CosmosContainer"/> based on the Fluent definition.</returns>
        public virtual async Task<ContainerResponse> CreateAsync(int? throughput = null)
        {
            CosmosContainerSettings settings = this.Build();

            return await this.cosmosContainers.CreateContainerAsync(settings, throughput);
        }

        /// <summary>
        /// Applies the current Fluent definition and creates a container configuration.
        /// </summary>
        /// <returns>Builds the current Fluent configuration into an instance of <see cref="CosmosContainerSettings"/>.</returns>
        public virtual new CosmosContainerSettings Build()
        {
            CosmosContainerSettings settings = base.Build();

            if (this.uniqueKeyPolicy != null)
            {
                settings.UniqueKeyPolicy = this.uniqueKeyPolicy;
            }

            return settings;
        }

        private void AddUniqueKey(UniqueKey uniqueKey)
        {
            if (this.uniqueKeyPolicy == null)
            {
                this.uniqueKeyPolicy = new UniqueKeyPolicy();
            }

            this.uniqueKeyPolicy.UniqueKeys.Add(uniqueKey);
        }
    }
}
