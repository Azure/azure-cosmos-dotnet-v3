﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// <see cref="Container"/> fluent definition for creation flows.
    /// </summary>
    public class ContainerBuilder : ContainerDefinition<ContainerBuilder>
    {
        private readonly Database database;
        private readonly CosmosClientContext clientContext;
        private readonly Uri containerUri;
        private UniqueKeyPolicy uniqueKeyPolicy;
        private ConflictResolutionPolicy conflictResolutionPolicy;
        private ChangeFeedPolicy changeFeedPolicy;
        private ClientEncryptionPolicy clientEncryptionPolicy;
        private VectorEmbeddingPolicy vectorEmbeddingPolicy;
        private FullTextPolicy fullTextPolicy;

        /// <summary>
        /// Creates an instance for unit-testing
        /// </summary>
        protected ContainerBuilder()
        {
        }

        /// <summary>
        /// Creates an instance of ContainerBuilder .
        /// </summary>
        /// <param name="database"> The Microsoft.Azure.Cosmos.Database object.</param>
        /// <param name="name"> Azure Cosmos container name to create. </param>
        /// <param name="partitionKeyPath"> The path to the partition key. Example: /partitionKey </param>
        public ContainerBuilder(
            Database database,
            string name,
            string partitionKeyPath)
            : base(
                 string.IsNullOrEmpty(name) ? throw new ArgumentNullException(nameof(name)) : name,
                 string.IsNullOrEmpty(partitionKeyPath) ? throw new ArgumentNullException(nameof(partitionKeyPath)) : partitionKeyPath)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            this.clientContext = database.Client.ClientContext;
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
        /// Defined the conflict resolution for Azure Cosmos container
        /// </summary>
        /// <returns>An instance of <see cref="ConflictResolutionDefinition"/>.</returns>
        public ConflictResolutionDefinition WithConflictResolution()
        {
            return new ConflictResolutionDefinition(
                this,
                (conflictPolicy) => this.AddConflictResolution(conflictPolicy));
        }

        /// <summary>
        /// Defined the change feed policy for this Azure Cosmos container
        /// </summary>
        /// <param name="retention"> Indicates for how long operation logs have to be retained. <see cref="ChangeFeedPolicy.FullFidelityRetention"/>.</param>
        /// <returns>An instance of <see cref="ChangeFeedPolicyDefinition"/>.</returns>
#if PREVIEW
        public
#else
        internal
#endif
        ChangeFeedPolicyDefinition WithChangeFeedPolicy(TimeSpan retention)
        {
            return new ChangeFeedPolicyDefinition(
                this,
                retention,
                (changeFeedPolicy) => this.AddChangeFeedPolicy(changeFeedPolicy));
        }

        /// <summary>
        /// Defines the <see cref="ClientEncryptionPolicy"/> for Azure Cosmos container.
        /// The <see cref="ClientEncryptionPolicy.PolicyFormatVersion"/> will be set to 1.
        /// Note: If you need to include partition key or id field paths as part of <see cref="ClientEncryptionPolicy"/>, please set <see cref="ClientEncryptionPolicy.PolicyFormatVersion"/> to 2.
        /// </summary>        
        /// <returns>An instance of <see cref="ClientEncryptionPolicyDefinition"/>.</returns>
        public ClientEncryptionPolicyDefinition WithClientEncryptionPolicy()
        {
            return new ClientEncryptionPolicyDefinition(
                this,
                (clientEncryptionPolicy) => this.AddClientEncryptionPolicy(clientEncryptionPolicy), 1);
        }

        /// <summary>
        /// Defines the ClientEncryptionPolicy for Azure Cosmos container
        /// Note: If you need to include partition key or id field paths as part of <see cref="ClientEncryptionPolicy"/>, please set <see cref="ClientEncryptionPolicy.PolicyFormatVersion"/> to 2.
        /// </summary>
        /// <param name="policyFormatVersion">Version of the client encryption policy definition. Current supported versions are 1 and 2.</param>
        /// <returns>An instance of <see cref="ClientEncryptionPolicyDefinition"/>.</returns>
        public ClientEncryptionPolicyDefinition WithClientEncryptionPolicy(int policyFormatVersion)
        {
            return new ClientEncryptionPolicyDefinition(
                this,
                (clientEncryptionPolicy) => this.AddClientEncryptionPolicy(clientEncryptionPolicy),
                policyFormatVersion);
        }

        /// <summary>
        /// Defines the vector embedding policy for this Azure Cosmos container
        /// </summary>
        /// <param name="embeddings">List of vector embeddings to include in the policy definition.</param>
        /// <returns>An instance of <see cref="VectorEmbeddingPolicyDefinition"/>.</returns>
        public VectorEmbeddingPolicyDefinition WithVectorEmbeddingPolicy(
            Collection<Embedding> embeddings)
        {
            return new VectorEmbeddingPolicyDefinition(
                this,
                embeddings,
                (embeddingPolicy) => this.AddVectorEmbeddingPolicy(embeddingPolicy));
        }

        /// <summary>
        /// Defines the full text policy for this Azure Cosmos container
        /// </summary>
        /// <param name="defaultLanguage">A string indicating the default language.</param>
        /// <param name="fullTextPaths">List of full text paths to include in the policy definition.</param>
        /// <returns>An instance of <see cref="FullTextPolicyDefinition"/>.</returns>
        public FullTextPolicyDefinition WithFullTextPolicy(
            string defaultLanguage,
            Collection<FullTextPath> fullTextPaths)
        {
            return new FullTextPolicyDefinition(
                this,
                defaultLanguage,
                fullTextPaths,
                (fullTextPolicy) => this.AddFullTextSearchPolicy(fullTextPolicy));
        }

        /// <summary>
        /// Creates a container with the current fluent definition.
        /// </summary>
        /// <param name="throughputProperties">Desired throughput for the container expressed in Request Units per second.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An asynchronous Task representing the creation of a <see cref="Container"/> based on the Fluent definition.</returns>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        public async Task<ContainerResponse> CreateAsync(
            ThroughputProperties throughputProperties,
            CancellationToken cancellationToken = default)
        {
            ContainerProperties containerProperties = this.Build();

            return await this.database.CreateContainerAsync(
                containerProperties: containerProperties,
                throughputProperties: throughputProperties,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Creates a container if it does not exist with the current fluent definition.
        /// </summary>
        /// <param name="throughputProperties">Desired throughput for the container expressed in Request Units per second.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An asynchronous Task representing the creation of a <see cref="Container"/> based on the Fluent definition.</returns>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        public async Task<ContainerResponse> CreateIfNotExistsAsync(
            ThroughputProperties throughputProperties,
            CancellationToken cancellationToken = default)
        {
            ContainerProperties containerProperties = this.Build();

            return await this.database.CreateContainerIfNotExistsAsync(
                containerProperties: containerProperties,
                throughputProperties: throughputProperties,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Creates a container with the current fluent definition.
        /// </summary>
        /// <param name="throughput">Desired throughput for the container expressed in Request Units per second.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An asynchronous Task representing the creation of a <see cref="Container"/> based on the Fluent definition.</returns>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        public async Task<ContainerResponse> CreateAsync(
            int? throughput = null,
            CancellationToken cancellationToken = default)
        {
            ContainerProperties containerProperties = this.Build();

            return await this.database.CreateContainerAsync(
                containerProperties: containerProperties,
                throughput: throughput,
                requestOptions: null,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Creates a container if it does not exist with the current fluent definition.
        /// </summary>
        /// <param name="throughput">Desired throughput for the container expressed in Request Units per second.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An asynchronous Task representing the creation of a <see cref="Container"/> based on the Fluent definition.</returns>
        /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/request-units">Request Units</seealso>
        public async Task<ContainerResponse> CreateIfNotExistsAsync(
            int? throughput = null,
            CancellationToken cancellationToken = default)
        {
            ContainerProperties containerProperties = this.Build();

            return await this.database.CreateContainerIfNotExistsAsync(
                containerProperties: containerProperties,
                throughput: throughput,
                requestOptions: null,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Applies the current Fluent definition and creates a container configuration.
        /// </summary>
        /// <returns>Builds the current Fluent configuration into an instance of <see cref="ContainerProperties"/>.</returns>
        public new ContainerProperties Build()
        {
            ContainerProperties containerProperties = base.Build();

            if (this.uniqueKeyPolicy != null)
            {
                containerProperties.UniqueKeyPolicy = this.uniqueKeyPolicy;
            }

            if (this.conflictResolutionPolicy != null)
            {
                containerProperties.ConflictResolutionPolicy = this.conflictResolutionPolicy;
            }

            if (this.changeFeedPolicy != null)
            {
                containerProperties.ChangeFeedPolicy = this.changeFeedPolicy;
            }

            if (this.clientEncryptionPolicy != null)
            {
                containerProperties.ClientEncryptionPolicy = this.clientEncryptionPolicy;
            }

            if (this.vectorEmbeddingPolicy != null)
            {
                containerProperties.VectorEmbeddingPolicy = this.vectorEmbeddingPolicy;
            }

            if (this.fullTextPolicy != null)
            {
                containerProperties.FullTextPolicy = this.fullTextPolicy;
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

        private void AddChangeFeedPolicy(ChangeFeedPolicy changeFeedPolicy)
        {
            this.changeFeedPolicy = changeFeedPolicy;
        }

        private void AddClientEncryptionPolicy(ClientEncryptionPolicy clientEncryptionPolicy)
        {
            this.clientEncryptionPolicy = clientEncryptionPolicy;
        }

        private void AddVectorEmbeddingPolicy(VectorEmbeddingPolicy embeddingPolicy)
        {
            this.vectorEmbeddingPolicy = embeddingPolicy;
        }

        private void AddFullTextSearchPolicy(FullTextPolicy fullTextPolicy)
        {
            this.fullTextPolicy = fullTextPolicy;
        }
    }
}
