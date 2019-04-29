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
        private readonly string partitionKeyPath;
        private UniqueKeyPolicy uniqueKeyPolicy;
        private int defaultTimeToLive;
        private IndexingPolicy indexingPolicy;
        private int throughput;

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
            this.throughput = throughput;
            return this;
        }

        public override UniqueKeyFluentDefinition WithUniqueKey()
        {
            return new UniqueKeyFluentDefinitionCore(this);
        }

        public override CosmosContainerFluentDefinition WithDefaultTimeToLive(TimeSpan defaultTtlTimeSpan)
        {
            this.defaultTimeToLive = (int)defaultTtlTimeSpan.TotalSeconds;
            return this;
        }

        public override CosmosContainerFluentDefinition WithDefaultTimeToLive(int defaulTtlInSeconds)
        {
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

        public override Task<CosmosContainerResponse> ApplyAsync()
        {
            // TODO: Populate the settigns & options
            CosmosContainerSettings settings = new CosmosContainerSettings();
            CosmosContainerRequestOptions containerRequestOptions = new CosmosContainerRequestOptions();

            switch (this.fluentSettingsOperation)
            {
                case FluentSettingsOperation.Create:
                    return this.CosmosContainers.CreateContainerAsync(settings, this.throughput, containerRequestOptions);
                case FluentSettingsOperation.Replace:
                    return this.CosmosContainers[this.containerName].ReplaceAsync(settings, containerRequestOptions);
            }

            throw new NotImplementedException();
        }
    }
}
