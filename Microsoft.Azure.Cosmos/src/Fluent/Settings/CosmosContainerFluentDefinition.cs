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
        /// <see cref="IndexingPolicy"/> definition for the current Azure Cosmos container.
        /// </summary>
        public abstract IndexingPolicyFluentDefinition WithIndexingPolicy();

        /// <summary>
        /// Applies the current Fluent definition.
        /// </summary>
        public abstract Task<CosmosContainerResponse> ApplyAsync();
    }
}
