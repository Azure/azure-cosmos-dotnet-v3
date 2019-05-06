//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
using System.Threading.Tasks;

namespace Microsoft.Azure.Cosmos.Fluent
{
    /// <summary>
    /// <see cref="CosmosContainer"/> fluent definition for creation flows.
    /// </summary>
    public class CosmosContainerFluentDefinitionForCreate : CosmosContainerFluentDefinition<CosmosContainerFluentDefinitionForCreate>
    {
        private UniqueKeyPolicy uniqueKeyPolicy;
        private readonly CosmosContainers cosmosContainers;

        /// <summary>
        /// Creates an instance for unit-testing
        /// </summary>
        public CosmosContainerFluentDefinitionForCreate() {}

        internal CosmosContainerFluentDefinitionForCreate(
            CosmosContainers cosmosContainers,
            string name,
            string partitionKeyPath = null) : base(name, partitionKeyPath)
        {
            this.cosmosContainers = cosmosContainers;
        }

        /// <summary>
        /// Defines a Unique Key policy for this Azure Cosmos container.
        /// </summary>
        public virtual UniqueKeyFluentDefinition UniqueKey()
        {
            return new UniqueKeyFluentDefinition(
                this,
                (uniqueKey) => this.AddUniqueKey(uniqueKey));
        }

        /// <summary>
        /// Creates a container with the current fluent definition.
        /// </summary>
        /// <param name="throughput">Desired throughput for the container</param>
        public virtual async Task<CosmosContainerResponse> CreateAsync(int? throughput = null)
        {
            CosmosContainerSettings settings = this.Build();

            return await this.cosmosContainers.CreateContainerAsync(settings, throughput);
        }

        /// <inheritdoc />
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
