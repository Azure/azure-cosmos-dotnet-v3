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
        private readonly CosmosDatabase cosmosContainers;
        private UniqueKeyPolicy uniqueKeyPolicy;
        private ConflictResolutionPolicy conflictResolutionPolicy;

        /// <summary>
        /// Creates an instance for unit-testing
        /// </summary>
        public CosmosContainerFluentDefinitionForCreate()
        {
        }

        internal CosmosContainerFluentDefinitionForCreate(
            CosmosDatabase cosmosContainers,
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
        /// Defined the conflict resoltuion for Azure Cosmos container
        /// </summary>
        /// <returns>An instance of <see cref="ConflictResolutionFluentDefinition"/>.</returns>
        public virtual ConflictResolutionFluentDefinition WithConflictResolution()
        {
            return new ConflictResolutionFluentDefinition(
                this,
                (conflictPolicy) => this.AddConflictResolution(conflictPolicy));
        }

        /// <summary>
        /// Creates a container with the current fluent definition.
        /// </summary>
        /// <param name="requestUnitsPerSecond">Desired throughput for the container expressed in Request Units per second.</param>
        /// <returns>An asynchronous Task representing the creation of a <see cref="CosmosContainer"/> based on the Fluent definition.</returns>
        /// <remarks>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units"/> for details on provision throughput.
        /// </remarks>
        public virtual async Task<ContainerResponse> CreateAsync(int? requestUnitsPerSecond = null)
        {
            CosmosContainerProperties settings = this.Build();

            return await this.cosmosContainers.CreateContainerAsync(settings, requestUnitsPerSecond);
        }

        /// <summary>
        /// Applies the current Fluent definition and creates a container configuration.
        /// </summary>
        /// <returns>Builds the current Fluent configuration into an instance of <see cref="CosmosContainerProperties"/>.</returns>
        public virtual new CosmosContainerProperties Build()
        {
            CosmosContainerProperties settings = base.Build();

            if (this.uniqueKeyPolicy != null)
            {
                settings.UniqueKeyPolicy = this.uniqueKeyPolicy;
            }

            if (this.conflictResolutionPolicy != null)
            {
                settings.ConflictResolutionPolicy = this.conflictResolutionPolicy;
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

        private void AddConflictResolution(ConflictResolutionPolicy conflictResolutionPolicy)
        {
            this.conflictResolutionPolicy = conflictResolutionPolicy;
        }
    }
}
