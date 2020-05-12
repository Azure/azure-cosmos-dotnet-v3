//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Azure.Cosmos.Fluent
{
    using System;

    /// <summary>
    /// Azure Cosmos container fluent definition.
    /// </summary>
    /// <seealso cref="CosmosContainer"/>
    public abstract class ContainerDefinition<T>
        where T : ContainerDefinition<T>
    {
        private readonly string containerName;
        private string partitionKeyPath;
        private int? DefaultTimeToLiveInSeconds;
        private IndexingPolicy indexingPolicy;
        private PartitionKeyDefinitionVersion? partitionKeyDefinitionVersion = null;

        /// <summary>
        /// Creates an instance for unit-testing
        /// </summary>
        public ContainerDefinition()
        {
        }

        internal ContainerDefinition(
            string name,
            string partitionKeyPath = null)
        {
            this.containerName = name;
            this.partitionKeyPath = partitionKeyPath;
        }

        /// <summary>
        /// Sets the <see cref="Cosmos.PartitionKeyDefinitionVersion"/>
        ///
        /// The partition key definition version 1 uses a hash function that computes
        /// hash based on the first 100 bytes of the partition key. This can cause
        /// conflicts for documents with partition keys greater than 100 bytes.
        /// 
        /// The partition key definition version 2 uses a hash function that computes
        /// hash based on the first 2 KB of the partition key.
        /// </summary>
        /// <param name="partitionKeyDefinitionVersion">The partition key definition version</param>
        /// <returns>An instance of the current Fluent builder.</returns>
        /// <seealso cref="CosmosContainerProperties.PartitionKeyDefinitionVersion"/>
        public T WithPartitionKeyDefinitionVersion(PartitionKeyDefinitionVersion partitionKeyDefinitionVersion)
        {
            this.partitionKeyDefinitionVersion = partitionKeyDefinitionVersion;
            return (T)this;
        }

        /// <summary>
        /// <see cref="CosmosContainerProperties.DefaultTimeToLiveInSeconds"/> will be applied to all the items in the container as the default time-to-live policy.
        /// The individual item could override the default time-to-live policy by setting its time to live.
        /// </summary>
        /// <param name="defaultTtlTimeSpan">The default Time To Live.</param>
        /// <returns>An instance of the current Fluent builder.</returns>
        /// <seealso cref="CosmosContainerProperties.DefaultTimeToLiveInSeconds"/>
        public T WithDefaultTimeToLive(TimeSpan defaultTtlTimeSpan)
        {
            if (defaultTtlTimeSpan == null)
            {
                throw new ArgumentNullException(nameof(defaultTtlTimeSpan));
            }

            this.DefaultTimeToLiveInSeconds = (int)defaultTtlTimeSpan.TotalSeconds;
            return (T)this;
        }

        /// <summary>
        /// <see cref="CosmosContainerProperties.DefaultTimeToLiveInSeconds"/> will be applied to all the items in the container as the default time-to-live policy.
        /// The individual item could override the default time-to-live policy by setting its time to live.
        /// </summary>
        /// <param name="defaulTtlInSeconds">The default Time To Live.</param>
        /// <returns>An instance of the current Fluent builder.</returns>
        /// <seealso cref="CosmosContainerProperties.DefaultTimeToLiveInSeconds"/>
        public T WithDefaultTimeToLive(int defaulTtlInSeconds)
        {
            if (defaulTtlInSeconds < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(defaulTtlInSeconds));
            }

            this.DefaultTimeToLiveInSeconds = defaulTtlInSeconds;
            return (T)this;
        }

        /// <summary>
        /// <see cref="Cosmos.IndexingPolicy"/> definition for the current Azure Cosmos container.
        /// </summary>
        /// <returns>An instance of <see cref="IndexingPolicyDefinition{T}"/>.</returns>
        public IndexingPolicyDefinition<T> WithIndexingPolicy()
        {
            if (this.indexingPolicy != null)
            {
                // Overwrite
                throw new NotSupportedException();
            }

            return new IndexingPolicyDefinition<T>(
                (T)this,
                (indexingPolicy) => this.WithIndexingPolicy(indexingPolicy));
        }

        /// <summary>
        /// Applies the current Fluent definition and creates a container configuration.
        /// </summary>
        /// <returns>Builds the current Fluent configuration into an instance of <see cref="CosmosContainerProperties"/>.</returns>
        public CosmosContainerProperties Build()
        {
            CosmosContainerProperties containerProperties = new CosmosContainerProperties(id: this.containerName, partitionKeyPath: this.partitionKeyPath);
            if (this.indexingPolicy != null)
            {
                containerProperties.IndexingPolicy = this.indexingPolicy;
            }

            if (this.DefaultTimeToLiveInSeconds.HasValue)
            {
                containerProperties.DefaultTimeToLiveInSeconds = this.DefaultTimeToLiveInSeconds.Value;
            }

            if (this.partitionKeyDefinitionVersion.HasValue)
            {
                containerProperties.PartitionKeyDefinitionVersion = this.partitionKeyDefinitionVersion.Value;
            }

            containerProperties.ValidateRequiredProperties();

            return containerProperties;
        }

        private void WithIndexingPolicy(IndexingPolicy indexingPolicy)
        {
            this.indexingPolicy = indexingPolicy;
        }
    }
}
