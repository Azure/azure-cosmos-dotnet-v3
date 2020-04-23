//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Fluent
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// <see cref="CosmosContainer"/> fluent definition for creation flows.
    /// </summary>
    public class ContainerBuilder : ContainerDefinition<ContainerBuilder>
    {
        private readonly CosmosDatabase database;
        private readonly CosmosClientContext clientContext;
        private readonly Uri containerUri;
        private UniqueKeyPolicy uniqueKeyPolicy;
        private ConflictResolutionPolicy conflictResolutionPolicy;

        /// <summary>
        /// Creates an instance for unit-testing
        /// </summary>
        protected ContainerBuilder()
        {
        }

        internal ContainerBuilder(
            CosmosDatabase cosmosContainers,
            CosmosClientContext clientContext,
            string name,
            string partitionKeyPath = null)
            : base(name, partitionKeyPath)
        {
            this.database = cosmosContainers;
            this.clientContext = clientContext;
            this.containerUri = UriFactory.CreateDocumentCollectionUri(this.database.Id, name);
        }

        /// <summary>
        /// Defines a Unique Key policy for this Azure Cosmos container.
        /// </summary>
        /// <returns>An instance of <see cref="UniqueKeyDefinition"/>.</returns>
        public UniqueKeyDefinition WithUniqueKey()
        {
            return new UniqueKeyDefinition(
                this,
                (uniqueKey) => this.AddUniqueKey(uniqueKey));
        }

        /// <summary>
        /// Defined the conflict resoltuion for Azure Cosmos container
        /// </summary>
        /// <returns>An instance of <see cref="ConflictResolutionDefinition"/>.</returns>
        public ConflictResolutionDefinition WithConflictResolution()
        {
            return new ConflictResolutionDefinition(
                this,
                (conflictPolicy) => this.AddConflictResolution(conflictPolicy));
        }

        /// <summary>
        /// Creates a container with the current fluent definition.
        /// </summary>
        /// <param name="throughput">Desired throughput for the container expressed in Request Units per second.</param>
        /// <returns>An asynchronous Task representing the creation of a <see cref="CosmosContainer"/> based on the Fluent definition.</returns>
        /// <remarks>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units"/> for details on provision throughput.
        /// </remarks>
        public async Task<ContainerResponse> CreateAsync(int? throughput = null)
        {
            CosmosContainerProperties containerProperties = this.Build();

            return await this.database.CreateContainerAsync(containerProperties, throughput);
        }

        /// <summary>
        /// Creates a container if it does not exist with the current fluent definition.
        /// </summary>
        /// <param name="throughput">Desired throughput for the container expressed in Request Units per second.</param>
        /// <returns>An asynchronous Task representing the creation of a <see cref="CosmosContainer"/> based on the Fluent definition.</returns>
        /// <remarks>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units"/> for details on provision throughput.
        /// </remarks>
        public async Task<ContainerResponse> CreateIfNotExistsAsync(int? throughput = null)
        {
            CosmosContainerProperties containerProperties = this.Build();

            return await this.database.CreateContainerIfNotExistsAsync(containerProperties, throughput);
        }

        /// <summary>
        /// Applies the current Fluent definition and creates a container configuration.
        /// </summary>
        /// <returns>Builds the current Fluent configuration into an instance of <see cref="CosmosContainerProperties"/>.</returns>
        public new CosmosContainerProperties Build()
        {
            CosmosContainerProperties containerProperties = base.Build();

            if (this.uniqueKeyPolicy != null)
            {
                containerProperties.UniqueKeyPolicy = this.uniqueKeyPolicy;
            }

            if (this.conflictResolutionPolicy != null)
            {
                containerProperties.ConflictResolutionPolicy = this.conflictResolutionPolicy;
            }

            return containerProperties;
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
            if (conflictResolutionPolicy.Mode == ConflictResolutionMode.Custom
                && !string.IsNullOrEmpty(conflictResolutionPolicy.ResolutionProcedure))
            {
                this.clientContext.ValidateResource(conflictResolutionPolicy.ResolutionProcedure);
                conflictResolutionPolicy.ResolutionProcedure = UriFactory.CreateStoredProcedureUri(this.containerUri.ToString(), conflictResolutionPolicy.ResolutionProcedure).ToString();
            }

            this.conflictResolutionPolicy = conflictResolutionPolicy;
        }
    }
}
