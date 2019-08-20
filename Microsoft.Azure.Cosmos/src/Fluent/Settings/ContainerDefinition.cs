//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;

    /// <summary>
    /// Azure Cosmos container fluent definition.
    /// </summary>
    /// <seealso cref="Container"/>
    public abstract class ContainerDefinition<T>
        where T : ContainerDefinition<T>
    {
        private readonly string containerName;
        private string partitionKeyPath;
        private int? defaultTimeToLive;
        private IndexingPolicy indexingPolicy;
        private string timeToLivePropertyPath;

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
        /// <see cref="ContainerProperties.DefaultTimeToLive"/> will be applied to all the items in the container as the default time-to-live policy.
        /// The individual item could override the default time-to-live policy by setting its time to live.
        /// </summary>
        /// <param name="defaultTtlTimeSpan">The default Time To Live.</param>
        /// <returns>An instance of the current Fluent builder.</returns>
        /// <seealso cref="ContainerProperties.DefaultTimeToLive"/>
        public T WithDefaultTimeToLive(TimeSpan defaultTtlTimeSpan)
        {
            if (defaultTtlTimeSpan == null)
            {
                throw new ArgumentNullException(nameof(defaultTtlTimeSpan));
            }

            this.defaultTimeToLive = (int)defaultTtlTimeSpan.TotalSeconds;
            return (T)this;
        }

        /// <summary>
        /// <see cref="ContainerProperties.DefaultTimeToLive"/> will be applied to all the items in the container as the default time-to-live policy.
        /// The individual item could override the default time-to-live policy by setting its time to live.
        /// </summary>
        /// <param name="defaulTtlInSeconds">The default Time To Live.</param>
        /// <returns>An instance of the current Fluent builder.</returns>
        /// <seealso cref="ContainerProperties.DefaultTimeToLive"/>
        public T WithDefaultTimeToLive(int defaulTtlInSeconds)
        {
            if (defaulTtlInSeconds < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(defaulTtlInSeconds));
            }

            this.defaultTimeToLive = defaulTtlInSeconds;
            return (T)this;
        }

        /// <summary>
        /// Sets the time to live base timestamp property path.
        /// </summary>
        /// <param name="propertyPath">This property should be only present when DefaultTimeToLive is set. When this property is present, time to live for a item is decided based on the value of this property in an item. By default, time to live is based on the _ts property in an item. Example: /property</param>
        /// <returns>An instance of the current Fluent builder.</returns>
        /// <seealso cref="ContainerProperties.TimeToLivePropertyPath"/>
        public T WithTimeToLivePropertyPath(string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath))
            {
                throw new ArgumentNullException(nameof(propertyPath));
            }

            this.timeToLivePropertyPath = propertyPath;
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
        /// <returns>Builds the current Fluent configuration into an instance of <see cref="ContainerProperties"/>.</returns>
        public ContainerProperties Build()
        {
            ContainerProperties containerProperties = new ContainerProperties(id: this.containerName, partitionKeyPath: this.partitionKeyPath);
            if (this.indexingPolicy != null)
            {
                containerProperties.IndexingPolicy = this.indexingPolicy;
            }

            if (this.defaultTimeToLive.HasValue)
            {
                containerProperties.DefaultTimeToLive = this.defaultTimeToLive.Value;
            }

            if (this.timeToLivePropertyPath != null)
            {
#pragma warning disable 0612
                containerProperties.TimeToLivePropertyPath = this.timeToLivePropertyPath;
#pragma warning restore 0612
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
