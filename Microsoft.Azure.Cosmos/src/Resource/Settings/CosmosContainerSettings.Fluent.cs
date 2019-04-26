//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// CoosmosContainerSettings Builder 
    /// </summary>
    public class CosmosContainerBuilder : IndexingPolicyBuilder
    {
        private IndexingPolicyBuilder indexingPolicyBuilder;
        private int defaultTimeToLive;
        private int throughput;
        private string containerName;
        private List<UniqueueKeyBuilder> uniqueueKeyBuilders = new List<UniqueueKeyBuilder>();
        CosmosContainers cosmosContainers;

        /// <summary>
        /// Creates a new builder instance 
        /// </summary>
        public CosmosContainerBuilder(CosmosContainers cosmosContainers, string name)
        {
            this.containerName = name;
            this.cosmosContainers = cosmosContainers;
        }

        public CosmosContainerBuilder WithPartitionKeyPath(string partitionKeyPath)
        {
            return this;
        }

        public CosmosContainerBuilder WithPartitionKeyDefinitionV2()
        {
            return this;
        }

        /// <summary>
        /// <see cref="DefaultTimeToLive"/> will be applied to all the items in the container as the default time-to-live policy.
        /// The individual item could override the default time-to-live policy by setting its time to live.
        /// </summary>
        /// <seealso cref="CosmosContainerBuilder.DefaultTimeToLive"/>
        [IgnoreForUnitTest]
        public CosmosContainerBuilder WithDefaultTimeToLive(TimeSpan defaultTtlTimeSpan)
        {
            this.defaultTimeToLive = (int)defaultTtlTimeSpan.TotalSeconds;
            return this;
        }

        /// <summary>
        /// <see cref="DefaultTimeToLive"/> will be applied to all the items in the container as the default time-to-live policy.
        /// The individual item could override the default time-to-live policy by setting its time to live.
        /// </summary>
        /// <seealso cref="CosmosContainerBuilder.DefaultTimeToLive"/>
        [IgnoreForUnitTest]
        public CosmosContainerBuilder WithDefaultTimeToLive(int defaulTtlInSeconds)
        {
            this.defaultTimeToLive = defaulTtlInSeconds;
            return this;
        }

        public CosmosContainerBuilder WithThroughput(int throughput)
        {
            this.throughput = throughput;
            return this;
        }

        public UniqueueKeyBuilder WithUniqueKey()
        {
            UniqueueKeyBuilder newBuilder = new UniqueueKeyBuilder(this);
            this.uniqueueKeyBuilders.Add(newBuilder);
            return newBuilder;
        }

        public IndexingPolicyBuilder WithIndexingPolicy()
        {
            if (indexingPolicyBuilder != null)
            {
                // Overwrite
                throw new NotSupportedException();
            }

            this.indexingPolicyBuilder = new IndexingPolicyBuilder(this);
            return this.indexingPolicyBuilder;
        }

        public Task<CosmosContainerResponse> CreateAsync()
        {
            // TODO: Populate the settigns & options
            CosmosContainerSettings settings = new CosmosContainerSettings();
            CosmosContainerRequestOptions reqeustOptions = new CosmosContainerRequestOptions();

            return this.cosmosContainers.CreateContainerAsync(settings, this.throughput, reqeustOptions);
        }
    }
}
