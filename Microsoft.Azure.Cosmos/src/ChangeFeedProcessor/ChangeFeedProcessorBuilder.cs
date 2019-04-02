//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement;

    /// <summary>
    /// Provides a flexible way to to create an instance of <see cref="ChangeFeedProcessor"/> with custom set of parameters.
    /// </summary>

    public class ChangeFeedProcessorBuilder
    {
        private const string InMemoryDefaultHostName = "InMemory";

        private readonly CosmosContainer monitoredContainer;
        private readonly ChangeFeedProcessor changeFeedProcessor;

        private ChangeFeedProcessorOptions changeFeedProcessorOptions;
        private ChangeFeedLeaseOptions changeFeedLeaseOptions;
        
        private CosmosContainer leaseContainer;
        private string InstanceName;
        private DocumentServiceLeaseStoreManager LeaseStoreManager;
        private string databaseResourceId;
        private string collectionResourceId;
        private bool isBuilt;

        internal ChangeFeedProcessorBuilder(CosmosContainer cosmosContainer, ChangeFeedProcessor changeFeedProcessor)
        {
            this.monitoredContainer = cosmosContainer;
            this.changeFeedProcessor = changeFeedProcessor;
        }

        /// <summary>
        /// Sets the Host name.
        /// </summary>
        /// <param name="instanceName">Name to be used for the processor instance. When using multiple processor hosts, each host must have a unique name.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder"/> to use.</returns>
        public ChangeFeedProcessorBuilder WithInstanceName(string instanceName)
        {
            this.InstanceName = instanceName;
            return this;
        }

        /// <summary>
        /// Sets the logical operational grouping for a group of processor instances managing a particular workflow.
        /// </summary>
        /// <param name="workflowName">Name of the logical workflow.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder"/> to use.</returns>
        public ChangeFeedProcessorBuilder WithWorkflowName(string workflowName)
        {
            this.changeFeedLeaseOptions = this.changeFeedLeaseOptions ?? new ChangeFeedLeaseOptions();
            this.changeFeedLeaseOptions.LeasePrefix = workflowName;
            return this;
        }

        /// <summary>
        /// Sets a custom configuration to be used by this instance of <see cref="ChangeFeedProcessor"/> to control how leases are maintained in a container when using <see cref="WithCosmosLeaseContainer(CosmosContainer)"/>.
        /// </summary>
        /// <param name="acquireInterval">Interval to kick off a task to verify if leases are distributed evenly among known host instances.</param>
        /// <param name="expirationInterval">Interval for which the lease is taken. If the lease is not renewed within this interval, it will cause it to expire and ownership of the lease will move to another processor instance.</param>
        /// <param name="renewInterval">Renew interval for all leases currently held by a particular processor instance.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder"/> to use.</returns>
        public ChangeFeedProcessorBuilder WithLeaseConfiguration(TimeSpan? acquireInterval = null, TimeSpan? expirationInterval = null, TimeSpan? renewInterval = null)
        {
            this.changeFeedLeaseOptions = this.changeFeedLeaseOptions ?? new ChangeFeedLeaseOptions();
            this.changeFeedLeaseOptions.LeaseRenewInterval = renewInterval ?? ChangeFeedLeaseOptions.DefaultRenewInterval;
            this.changeFeedLeaseOptions.LeaseAcquireInterval = acquireInterval ?? ChangeFeedLeaseOptions.DefaultAcquireInterval;
            this.changeFeedLeaseOptions.LeaseExpirationInterval = expirationInterval ?? ChangeFeedLeaseOptions.DefaultExpirationInterval;
            return this;
        }

        /// <summary>
        /// Gets or sets the delay in between polling the change feed for new changes, after all current changes are drained.
        /// </summary>
        /// <remarks>
        /// Applies only after a read on the change feed yielded no results.
        /// </remarks>
        /// <param name="feedPollDelay">Polling interval value.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder"/> to use.</returns>
        public ChangeFeedProcessorBuilder WithFeedPollDelay(TimeSpan feedPollDelay)
        {
            if (feedPollDelay == null) throw new ArgumentNullException(nameof(feedPollDelay));

            this.changeFeedProcessorOptions = this.changeFeedProcessorOptions ?? new ChangeFeedProcessorOptions();
            this.changeFeedProcessorOptions.FeedPollDelay = feedPollDelay;
            return this;
        }

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
        internal ChangeFeedProcessorBuilder WithStartFromBeginning()
        {
            this.changeFeedProcessorOptions = this.changeFeedProcessorOptions ?? new ChangeFeedProcessorOptions();
            this.changeFeedProcessorOptions.StartFromBeginning = true;
            return this;
        }

        /// <summary>
        /// Sets the start request continuation token to start looking for changes after.
        /// </summary>
        /// <remarks>
        /// This is only used when lease store is not initialized and is ignored if a lease exists and has continuation token.
        /// If this is specified, both StartTime and StartFromBeginning are ignored.
        /// </remarks>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder"/> to use.</returns>
        public ChangeFeedProcessorBuilder WithContinuation(string startContinuation)
        {
            this.changeFeedProcessorOptions = this.changeFeedProcessorOptions ?? new ChangeFeedProcessorOptions();
            this.changeFeedProcessorOptions.StartContinuation = startContinuation;
            return this;
        }

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
        public ChangeFeedProcessorBuilder WithStartTime(DateTime startTime)
        {
            if (startTime == null) throw new ArgumentNullException(nameof(startTime));

            this.changeFeedProcessorOptions = this.changeFeedProcessorOptions ?? new ChangeFeedProcessorOptions();
            this.changeFeedProcessorOptions.StartTime = startTime;
            return this;
        }

        /// <summary>
        /// Sets the maximum number of items to be returned in the enumeration operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="maxItemCount">Maximum amount of items to be returned in a Change Feed request.</param>
        /// <returns></returns>
        public ChangeFeedProcessorBuilder WithMaxItems(int maxItemCount)
        {
            if (maxItemCount <= 0) throw new ArgumentOutOfRangeException(nameof(maxItemCount));

            this.changeFeedProcessorOptions = this.changeFeedProcessorOptions ?? new ChangeFeedProcessorOptions();
            this.changeFeedProcessorOptions.MaxItemCount = maxItemCount;
            return this;
        }

        /// <summary>
        /// Sets the Cosmos Container to hold the leases state
        /// </summary>
        /// <param name="leaseContainer">Instance of a Cosmos Container to hold the leases.</param>
        /// <returns></returns>
        public ChangeFeedProcessorBuilder WithCosmosLeaseContainer(CosmosContainer leaseContainer)
        {
            if (leaseContainer == null) throw new ArgumentNullException(nameof(leaseContainer));
            if (this.leaseContainer != null) throw new InvalidOperationException("The builder already defined a lease container.");
            if (this.LeaseStoreManager != null) throw new InvalidOperationException("The builder already defined an in-memory lease container instance.");

            this.leaseContainer = leaseContainer;
            return this;
        }

        /// <summary>
        /// Uses an in-memory container to maintain state of the leases
        /// </summary>
        /// <remarks>
        /// Using an in-memory container restricts the scaling capability to just the instance running the current processor.
        /// </remarks>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder"/> to use.</returns>
        public ChangeFeedProcessorBuilder WithInMemoryLeaseContainer()
        {
            if (this.leaseContainer != null) throw new InvalidOperationException("The builder already defined a lease container.");
            if (this.LeaseStoreManager != null) throw new InvalidOperationException("The builder already defined an in-memory lease container instance.");

            if (string.IsNullOrEmpty(this.InstanceName))
            {
                this.InstanceName = ChangeFeedProcessorBuilder.InMemoryDefaultHostName;
            }

            this.LeaseStoreManager = new DocumentServiceLeaseStoreManagerInMemory();
            return this;
        }

        /// <summary>
        /// Builds a new instance of the <see cref="ChangeFeedProcessor"/> with the specified configuration.
        /// </summary>
        /// <returns>An instance of <see cref="ChangeFeedProcessor"/>.</returns>
        public ChangeFeedProcessor Build()
        {
            if (this.isBuilt)
            {
                throw new InvalidOperationException("This builder instance has already been used to build a processor. Create a new instance to build another.");
            }

            if (this.monitoredContainer == null)
            {
                throw new InvalidOperationException(nameof(this.monitoredContainer) + " was not specified");
            }

            if (this.leaseContainer == null && this.LeaseStoreManager == null)
            {
                throw new InvalidOperationException($"Defining the lease store by WithCosmosLeaseContainer, WithInMemoryLeaseContainer, or WithLeaseStoreManager is required.");
            }

            if (this.changeFeedLeaseOptions?.LeasePrefix == null)
            {
                throw new InvalidOperationException("Workflow name was not specified using WithWorkflowName");
            }

            this.InitializeDefaultOptions();
            this.InitializeCollectionPropertiesForBuild();

            this.changeFeedProcessor.ApplyBuildConfiguration(this.LeaseStoreManager, this.leaseContainer, this.GetLeasePrefix(), this.InstanceName, this.changeFeedLeaseOptions, this.changeFeedProcessorOptions, this.monitoredContainer);

            this.isBuilt = true;
            return this.changeFeedProcessor;
        }

        private string GetLeasePrefix()
        {
            string optionsPrefix = this.changeFeedLeaseOptions.LeasePrefix ?? string.Empty;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}{1}_{2}_{3}",
                optionsPrefix,
                this.monitoredContainer.Client.Configuration.AccountEndPoint.Host,
                this.databaseResourceId,
                this.collectionResourceId);
        }

        private void InitializeCollectionPropertiesForBuild()
        {
            string[] containerLinkSegments = this.monitoredContainer.LinkUri.OriginalString.Split('/');
            this.databaseResourceId = containerLinkSegments[2];
            this.collectionResourceId = containerLinkSegments[4];
        }

        private void InitializeDefaultOptions()
        {
            this.changeFeedProcessorOptions = this.changeFeedProcessorOptions ?? new ChangeFeedProcessorOptions();
            this.changeFeedLeaseOptions = this.changeFeedLeaseOptions ?? new ChangeFeedLeaseOptions();
        }
    }
}
