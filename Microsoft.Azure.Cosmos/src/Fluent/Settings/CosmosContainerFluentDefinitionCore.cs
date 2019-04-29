//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Fluent
{
    using System;
    using System.Threading.Tasks;

    internal class CosmosContainerFluentDefinitionCore : CosmosContainerFluentDefinitionForCreate
    {
        private readonly string containerName;
        private readonly FluentSettingsOperation fluentSettingsOperation;
        private readonly CosmosContainers CosmosContainers;
        private string partitionKeyPath;
        private UniqueKeyPolicy uniqueKeyPolicy;
        private int? defaultTimeToLive;
        private IndexingPolicy indexingPolicy;
        private int throughput;
        private string timeToLivePropertyPath;

        public CosmosContainerFluentDefinitionCore(
            CosmosContainers cosmosContainers,
            string name,
            FluentSettingsOperation fluentSettingsOperation,
            string partitionKeyPath = null)
        {
            this.containerName = name;
            this.CosmosContainers = cosmosContainers;
            this.fluentSettingsOperation = fluentSettingsOperation;
            this.partitionKeyPath = partitionKeyPath;
        }

        public override CosmosContainerFluentDefinition WithThroughput(int throughput)
        {
            if (throughput <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(throughput));
            }

            this.throughput = throughput;
            return this;
        }

        public override CosmosContainerFluentDefinition WithTimeToLivePropertyPath(string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath))
            {
                throw new ArgumentNullException(nameof(propertyPath));
            }

            this.timeToLivePropertyPath = propertyPath;
            return this;
        }

        public override UniqueKeyFluentDefinition WithUniqueKey()
        {
            return new UniqueKeyFluentDefinitionCore(this);
        }

        public override CosmosContainerFluentDefinition WithDefaultTimeToLive(TimeSpan defaultTtlTimeSpan)
        {
            if (defaultTtlTimeSpan == null)
            {
                throw new ArgumentNullException(nameof(defaultTtlTimeSpan));
            }

            this.defaultTimeToLive = (int)defaultTtlTimeSpan.TotalSeconds;
            return this;
        }

        public override CosmosContainerFluentDefinition WithDefaultTimeToLive(int defaulTtlInSeconds)
        {
            if (defaulTtlInSeconds < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(defaulTtlInSeconds));
            }

            this.defaultTimeToLive = defaulTtlInSeconds;
            return this;
        }

        public override IndexingPolicyFluentDefinition WithIndexingPolicy()
        {
            if (this.indexingPolicy != null)
            {
                // Overwrite
                throw new NotSupportedException();
            }

            return new IndexingPolicyFluentDefinitionCore(this);
        }

        public void WithIndexingPolicy(IndexingPolicy indexingPolicy)
        {
            this.indexingPolicy = indexingPolicy;
        }

        public void WithUniqueKey(UniqueKey uniqueKey)
        {
            if (this.uniqueKeyPolicy == null)
            {
                this.uniqueKeyPolicy = new UniqueKeyPolicy();
            }

            this.uniqueKeyPolicy.UniqueKeys.Add(uniqueKey);
        }

        public override async Task<CosmosContainerResponse> ApplyAsync()
        {
            if (this.partitionKeyPath == null)
            {
                CosmosContainerSettings currentConfiguration = await this.CosmosContainers[this.containerName].ReadAsync();
                this.partitionKeyPath = currentConfiguration.PartitionKeyPath;
            }

            CosmosContainerSettings settings = new CosmosContainerSettings(id: this.containerName, partitionKeyPath: this.partitionKeyPath );
            if (this.indexingPolicy != null)
            {
                settings.IndexingPolicy = this.indexingPolicy;
            }

            if (this.uniqueKeyPolicy != null)
            {
                settings.UniqueKeyPolicy = this.uniqueKeyPolicy;
            }

            if (this.defaultTimeToLive.HasValue)
            {
                settings.DefaultTimeToLive = this.defaultTimeToLive.Value;
            }

            if (this.timeToLivePropertyPath != null)
            {
                settings.TimeToLivePropertyPath = timeToLivePropertyPath;
            }

            switch (this.fluentSettingsOperation)
            {
                case FluentSettingsOperation.Create:
                    return await this.CosmosContainers.CreateContainerAsync(settings, this.throughput);
                case FluentSettingsOperation.Replace:
                    return await this.CosmosContainers[this.containerName].ReplaceAsync(settings);
            }

            throw new NotImplementedException();
        }
    }
}
