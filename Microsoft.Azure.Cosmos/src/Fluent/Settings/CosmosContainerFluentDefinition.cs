//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;

    /// <summary>
    /// Azure Cosmos container fluent definition.
    /// </summary>
    /// <seealso cref="CosmosContainer"/>
    public abstract class CosmosContainerFluentDefinition<T> where T : CosmosContainerFluentDefinition<T>
    {
        private readonly string containerName;
        private string partitionKeyPath;
        private int? defaultTimeToLive;
        private IndexingPolicy indexingPolicy;
        private string timeToLivePropertyPath;

        /// <summary>
        /// Creates an instance for unit-testing
        /// </summary>
        public CosmosContainerFluentDefinition() { }

        internal CosmosContainerFluentDefinition(
            string name,
            string partitionKeyPath = null)
        {
            this.containerName = name;
            this.partitionKeyPath = partitionKeyPath;
        }

        /// <summary>
        /// <see cref="CosmosContainerSettings.DefaultTimeToLive"/> will be applied to all the items in the container as the default time-to-live policy.
        /// The individual item could override the default time-to-live policy by setting its time to live.
        /// </summary>
        /// <seealso cref="CosmosContainerSettings.DefaultTimeToLive"/>
        public virtual T DefaultTimeToLive(TimeSpan defaultTtlTimeSpan)
        {
            if (defaultTtlTimeSpan == null)
            {
                throw new ArgumentNullException(nameof(defaultTtlTimeSpan));
            }

            this.defaultTimeToLive = (int)defaultTtlTimeSpan.TotalSeconds;
            return (T)this;
        }

        /// <summary>
        /// <see cref="CosmosContainerSettings.DefaultTimeToLive"/> will be applied to all the items in the container as the default time-to-live policy.
        /// The individual item could override the default time-to-live policy by setting its time to live.
        /// </summary>
        /// <seealso cref="CosmosContainerSettings.DefaultTimeToLive"/>
        public virtual T DefaultTimeToLive(int defaulTtlInSeconds)
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
        /// <seealso cref="CosmosContainerSettings.TimeToLivePropertyPath"/>
        public virtual T TimeToLivePropertyPath(string propertyPath)
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
        public virtual IndexingPolicyFluentDefinition<T> IndexingPolicy()
        {
            if (this.indexingPolicy != null)
            {
                // Overwrite
                throw new NotSupportedException();
            }

            return new IndexingPolicyFluentDefinition<T>(
                (T)this,
                (indexingPolicy) => this.WithIndexingPolicy(indexingPolicy));
        }

        /// <summary>
        /// Applies the current Fluent definition and creates a container configuration.
        /// </summary>
        public virtual CosmosContainerSettings Build()
        {
            CosmosContainerSettings settings = new CosmosContainerSettings(id: this.containerName, partitionKeyPath: this.partitionKeyPath);
            if (this.indexingPolicy != null)
            {
                settings.IndexingPolicy = this.indexingPolicy;
            }

            if (this.defaultTimeToLive.HasValue)
            {
                settings.DefaultTimeToLive = this.defaultTimeToLive.Value;
            }

            if (this.timeToLivePropertyPath != null)
            {
                settings.TimeToLivePropertyPath = timeToLivePropertyPath;
            }

            settings.ValidateRequiredProperties();

            return settings;
        }

        private void WithIndexingPolicy(IndexingPolicy indexingPolicy)
        {
            this.indexingPolicy = indexingPolicy;
        }
    }
}
