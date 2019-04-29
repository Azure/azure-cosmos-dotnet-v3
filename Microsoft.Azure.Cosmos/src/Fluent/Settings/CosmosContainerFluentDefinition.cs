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
    public abstract class CosmosContainerFluentDefinition
    {
        /// <summary>
        /// <see cref="CosmosContainerSettings.DefaultTimeToLive"/> will be applied to all the items in the container as the default time-to-live policy.
        /// The individual item could override the default time-to-live policy by setting its time to live.
        /// </summary>
        /// <seealso cref="CosmosContainerSettings.DefaultTimeToLive"/>
        public abstract CosmosContainerFluentDefinition WithDefaultTimeToLive(TimeSpan defaultTtlTimeSpan);

        /// <summary>
        /// <see cref="CosmosContainerSettings.DefaultTimeToLive"/> will be applied to all the items in the container as the default time-to-live policy.
        /// The individual item could override the default time-to-live policy by setting its time to live.
        /// </summary>
        /// <seealso cref="CosmosContainerSettings.DefaultTimeToLive"/>
        public abstract CosmosContainerFluentDefinition WithDefaultTimeToLive(int defaulTtlInSeconds);

        /// <summary>
        /// Sets the time to live base timestamp property path.
        /// </summary>
        /// <param name="propertyPath">This property should be only present when DefaultTimeToLive is set. When this property is present, time to live for a item is decided based on the value of this property in an item. By default, time to live is based on the _ts property in an item. Example: /property</param>
        public abstract CosmosContainerFluentDefinition WithTimeToLivePropertyPath(string propertyPath);

        /// <summary>
        /// <see cref="IndexingPolicy"/> definition for the current Azure Cosmos container.
        /// </summary>
        public abstract IndexingPolicyFluentDefinition WithIndexingPolicy();

        /// <summary>
        /// Applies the current Fluent definition.
        /// </summary>
        public abstract Task<CosmosContainerResponse> ApplyAsync();
    }
}
