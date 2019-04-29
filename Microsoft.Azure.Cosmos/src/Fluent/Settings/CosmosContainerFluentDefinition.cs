//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// <see cref="CosmosContainer"/> fluent definition.
    /// </summary>
    public class CosmosContainerFluentDefinition
    {
        private readonly string containerName;
        private readonly CosmosContainers cosmosContainers;
        private readonly FluentSettingsOperation fluentSettingsOperation;

        private IndexingPolicyFluentDefinition indexingPolicyFluentDefinition;
        private int defaultTimeToLive;
        private int throughput;

        /// <summary>
        /// Empty constructor that can be used for unit testing
        /// </summary>
        public CosmosContainerFluentDefinition() { }

        internal CosmosContainerFluentDefinition(
            CosmosContainers cosmosContainers, 
            string name, 
            FluentSettingsOperation fluentSettingsOperation)
        {
            this.containerName = name;
            this.cosmosContainers = cosmosContainers;
            this.fluentSettingsOperation = fluentSettingsOperation;
        }

        /// <summary>
        /// <see cref="CosmosContainerSettings.DefaultTimeToLive"/> will be applied to all the items in the container as the default time-to-live policy.
        /// The individual item could override the default time-to-live policy by setting its time to live.
        /// </summary>
        /// <seealso cref="CosmosContainerSettings.DefaultTimeToLive"/>
        [IgnoreForUnitTest]
        public virtual CosmosContainerFluentDefinition WithDefaultTimeToLive(TimeSpan defaultTtlTimeSpan)
        {
            this.defaultTimeToLive = (int)defaultTtlTimeSpan.TotalSeconds;
            return this;
        }

        /// <summary>
        /// <see cref="CosmosContainerSettings.DefaultTimeToLive"/> will be applied to all the items in the container as the default time-to-live policy.
        /// The individual item could override the default time-to-live policy by setting its time to live.
        /// </summary>
        /// <seealso cref="CosmosContainerSettings.DefaultTimeToLive"/>
        [IgnoreForUnitTest]
        public virtual CosmosContainerFluentDefinition WithDefaultTimeToLive(int defaulTtlInSeconds)
        {
            this.defaultTimeToLive = defaulTtlInSeconds;
            return this;
        }

        /// <summary>
        /// Sets the throughput provisioned for the Azure Cosmos container in measurement of Requests-per-Unit in the Azure Cosmos DB service.
        /// </summary>
        /// <remarks>
        /// If multiple calls are made to this method within the same <see cref="CosmosContainerFluentDefinition"/>, the last one will apply.
        /// </remarks>
        public virtual CosmosContainerFluentDefinition WithThroughput(int throughput)
        {
            this.throughput = throughput;
            return this;
        }

        /// <summary>
        /// <see cref="IndexingPolicy"/> definition for the current Azure Cosmos container.
        /// </summary>
        public virtual IndexingPolicyFluentDefinition WithIndexingPolicy()
        {
            if (indexingPolicyFluentDefinition != null)
            {
                // Overwrite
                throw new NotSupportedException();
            }

            this.indexingPolicyFluentDefinition = new IndexingPolicyFluentDefinition(this);
            return this.indexingPolicyFluentDefinition;
        }

        /// <summary>
        /// Applies the current Fluent definition.
        /// </summary>
        public virtual Task<CosmosContainerResponse> ApplyAsync()
        {
            // TODO: Populate the settigns & options
            CosmosContainerSettings settings = new CosmosContainerSettings();
            CosmosContainerRequestOptions containerRequestOptions = new CosmosContainerRequestOptions();

            switch (this.fluentSettingsOperation)
            {
                case FluentSettingsOperation.Create:
                    return this.cosmosContainers.CreateContainerAsync(settings, this.throughput, containerRequestOptions);
                case FluentSettingsOperation.Replace:
                    return this.cosmosContainers[this.containerName].ReplaceAsync(settings, containerRequestOptions);
            }

            throw new NotImplementedException();
        }
    }
}
