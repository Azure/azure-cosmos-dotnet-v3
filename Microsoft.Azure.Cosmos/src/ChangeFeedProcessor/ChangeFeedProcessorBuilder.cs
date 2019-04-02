//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor
{
    using System;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Provides a flexible way to to create an instance of <see cref="ChangeFeedProcessor"/> with custom set of parameters.
    /// </summary>

    public abstract class ChangeFeedProcessorBuilder
    {
        /// <summary>
        /// Sets the Host name.
        /// </summary>
        /// <param name="instanceName">Name to be used for the processor instance. When using multiple processor hosts, each host must have a unique name.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder"/> to use.</returns>
        public abstract ChangeFeedProcessorBuilder WithInstanceName(string instanceName);

        /// <summary>
        /// Sets the logical operational grouping for a group of processor instances managing a particular workflow.
        /// </summary>
        /// <param name="workflowName">Name of the logical workflow.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder"/> to use.</returns>
        public abstract ChangeFeedProcessorBuilder WithWorkflowName(string workflowName);

        /// <summary>
        /// Sets a custom configuration to be used by this instance of <see cref="ChangeFeedProcessor"/> to control how leases are maintained in a container when using <see cref="WithCosmosLeaseContainer(CosmosContainer)"/>.
        /// </summary>
        /// <param name="acquireInterval">Interval to kick off a task to verify if leases are distributed evenly among known host instances.</param>
        /// <param name="expirationInterval">Interval for which the lease is taken. If the lease is not renewed within this interval, it will cause it to expire and ownership of the lease will move to another processor instance.</param>
        /// <param name="renewInterval">Renew interval for all leases currently held by a particular processor instance.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder"/> to use.</returns>
        public abstract ChangeFeedProcessorBuilder WithLeaseConfiguration(TimeSpan? acquireInterval = null, TimeSpan? expirationInterval = null, TimeSpan? renewInterval = null);

        /// <summary>
        /// Gets or sets the delay in between polling the change feed for new changes, after all current changes are drained.
        /// </summary>
        /// <remarks>
        /// Applies only after a read on the change feed yielded no results.
        /// </remarks>
        /// <param name="feedPollDelay">Polling interval value.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder"/> to use.</returns>
        public abstract ChangeFeedProcessorBuilder WithFeedPollDelay(TimeSpan feedPollDelay);

        /// <summary>
        /// Indicates whether change feed in the Azure Cosmos DB service should start from beginning.
        /// By default it's start from current time.
        /// </summary>
        /// <remarks>
        /// This is only used when:
        /// (1) Lease store is not initialized and is ignored if a lease exists and has continuation token.
        /// (2) StartContinuation is not specified.
        /// (3) StartTime is not specified.
        /// </remarks>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder"/> to use.</returns>
        internal abstract ChangeFeedProcessorBuilder WithStartFromBeginning();

        /// <summary>
        /// Sets the start request continuation token to start looking for changes after.
        /// </summary>
        /// <remarks>
        /// This is only used when lease store is not initialized and is ignored if a lease exists and has continuation token.
        /// If this is specified, both StartTime and StartFromBeginning are ignored.
        /// </remarks>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder"/> to use.</returns>
        public abstract ChangeFeedProcessorBuilder WithContinuation(string startContinuation);

        /// <summary>
        /// Sets the time (exclusive) to start looking for changes after.
        /// </summary>
        /// <remarks>
        /// This is only used when:
        /// (1) Lease store is not initialized and is ignored if a lease exists and has continuation token.
        /// (2) StartContinuation is not specified.
        /// If this is specified, StartFromBeginning is ignored.
        /// </remarks>
        /// <param name="startTime">Date and time when to start looking for changes.</param>
        /// <returns></returns>
        public abstract ChangeFeedProcessorBuilder WithStartTime(DateTime startTime);

        /// <summary>
        /// Sets the maximum number of items to be returned in the enumeration operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="maxItemCount">Maximum amount of items to be returned in a Change Feed request.</param>
        /// <returns></returns>
        public abstract ChangeFeedProcessorBuilder WithMaxItems(int maxItemCount);

        /// <summary>
        /// Sets the Cosmos Container to hold the leases state
        /// </summary>
        /// <param name="leaseContainer">Instance of a Cosmos Container to hold the leases.</param>
        /// <returns></returns>
        public abstract ChangeFeedProcessorBuilder WithCosmosLeaseContainer(CosmosContainer leaseContainer);

        /// <summary>
        /// Uses an in-memory container to maintain state of the leases
        /// </summary>
        /// <remarks>
        /// Using an in-memory container restricts the scaling capability to just the instance running the current processor.
        /// </remarks>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder"/> to use.</returns>
        public abstract ChangeFeedProcessorBuilder WithInMemoryLeaseContainer();

        /// <summary>
        /// Builds a new instance of the <see cref="ChangeFeedProcessor"/> with the specified configuration.
        /// </summary>
        /// <returns>An instance of <see cref="ChangeFeedProcessor"/>.</returns>
        public abstract ChangeFeedProcessor Build();
    }
}
