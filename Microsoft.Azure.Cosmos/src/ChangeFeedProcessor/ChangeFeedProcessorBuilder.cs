//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using static Microsoft.Azure.Cosmos.Container;

    /// <summary>
    /// Provides a flexible way to create an instance of <see cref="ChangeFeedProcessor"/> with custom set of parameters.
    /// </summary>
    public class ChangeFeedProcessorBuilder
    {
        private const string InMemoryDefaultHostName = "InMemory";

        private readonly ContainerInternal monitoredContainer;
        private readonly ChangeFeedProcessor changeFeedProcessor;
        private readonly ChangeFeedLeaseOptions changeFeedLeaseOptions;
        private readonly Action<DocumentServiceLeaseStoreManager,
                ContainerInternal,
                string,
                ChangeFeedLeaseOptions,
                ChangeFeedProcessorOptions,
                ContainerInternal> applyBuilderConfiguration;

        private readonly ChangeFeedProcessorOptions changeFeedProcessorOptions = new ChangeFeedProcessorOptions();

        private ContainerInternal leaseContainer;
        private string InstanceName;
        private DocumentServiceLeaseStoreManager LeaseStoreManager;
        private bool isBuilt;

        internal ChangeFeedProcessorBuilder(
            string processorName,
            ContainerInternal container,
            ChangeFeedProcessor changeFeedProcessor,
            Action<DocumentServiceLeaseStoreManager,
                ContainerInternal,
                string,
                ChangeFeedLeaseOptions,
                ChangeFeedProcessorOptions,
                ContainerInternal> applyBuilderConfiguration)
        {
            this.changeFeedLeaseOptions = new ChangeFeedLeaseOptions
            {
                LeasePrefix = processorName
            };
            this.monitoredContainer = container;
            this.changeFeedProcessor = changeFeedProcessor;
            this.applyBuilderConfiguration = applyBuilderConfiguration;
        }

        /// <summary>
        /// Sets the compute instance name that will host the processor.
        /// </summary>
        /// <param name="instanceName">Name of compute instance hosting the processor.</param>
        /// <remarks>
        /// Instance name refers to the unique identifier of the compute that is running the processor. 
        /// Examples could be a VM instance identifier, a machine name, a pod id.
        /// When distributing a processor across a cluster of compute hosts, each compute host should use a different instance name.
        /// </remarks>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder"/> to use.</returns>
        public ChangeFeedProcessorBuilder WithInstanceName(string instanceName)
        {
            this.InstanceName = instanceName;
            return this;
        }

        /// <summary>
        /// Sets the mode for the change feed processor.
        /// </summary>
        /// <param name="changeFeedMode"></param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder"/> to use.</returns>
        internal ChangeFeedProcessorBuilder WithChangeFeedMode(ChangeFeedMode changeFeedMode)
        {
            this.changeFeedProcessorOptions.Mode = changeFeedMode;
            this.changeFeedLeaseOptions.Mode = changeFeedMode;

            return this;
        }

        /// <summary>
        /// Sets a custom configuration to be used by this instance of <see cref="ChangeFeedProcessor"/> to control how leases are maintained in a container when using <see cref="WithLeaseContainer"/>.
        /// </summary>
        /// <param name="acquireInterval">Interval to kick off a task to verify if leases are distributed evenly among known host instances.</param>
        /// <param name="expirationInterval">Interval for which the lease is taken. If the lease is not renewed within this interval, it will cause it to expire and ownership of the lease will move to another processor instance.</param>
        /// <param name="renewInterval">Renew interval for all leases currently held by a particular processor instance.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder"/> to use.</returns>
        public ChangeFeedProcessorBuilder WithLeaseConfiguration(
            TimeSpan? acquireInterval = null,
            TimeSpan? expirationInterval = null,
            TimeSpan? renewInterval = null)
        {
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
        /// <param name="pollInterval">Polling interval value.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder"/> to use.</returns>
        public ChangeFeedProcessorBuilder WithPollInterval(TimeSpan pollInterval)
        {
            if (pollInterval == null)
            {
                throw new ArgumentNullException(nameof(pollInterval));
            }

            this.changeFeedProcessorOptions.FeedPollDelay = pollInterval;
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
        internal virtual ChangeFeedProcessorBuilder WithStartFromBeginning()
        {
            if (this.changeFeedProcessorOptions.Mode == ChangeFeedMode.AllVersionsAndDeletes)
            {
                throw new InvalidOperationException($"Using the '{nameof(WithStartFromBeginning)}' option with ChangeFeedProcessor is not supported with {ChangeFeedMode.AllVersionsAndDeletes} mode.");
            }

            this.changeFeedProcessorOptions.StartFromBeginning = true;
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
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder"/> to use.</returns>
        public ChangeFeedProcessorBuilder WithStartTime(DateTime startTime)
        {
            if (this.changeFeedProcessorOptions.Mode == ChangeFeedMode.AllVersionsAndDeletes)
            {
                throw new InvalidOperationException($"Using the '{nameof(WithStartTime)}' option with ChangeFeedProcessor is not supported with {ChangeFeedMode.AllVersionsAndDeletes} mode.");
            }

            if (startTime == null)
            {
                throw new ArgumentNullException(nameof(startTime));
            }

            this.changeFeedProcessorOptions.StartTime = startTime;
            return this;
        }

        /// <summary>
        /// Sets the maximum number of items to be returned in the enumeration operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="maxItemCount">Maximum amount of items to be returned in a Change Feed request.</param>
        /// <returns>An instance of <see cref="ChangeFeedProcessorBuilder"/>.</returns>
        /// <remarks>This is just a hint to the server which can return less or more items per page. If operations in the container are performed through stored procedures or transactional batch, <see href="https://docs.microsoft.com/azure/cosmos-db/stored-procedures-triggers-udfs#transactions">transaction scope</see> is preserved when reading items from the Change Feed. As a result, the number of items received could be higher than the specified value so that the items changed by the same transaction are returned as part of one atomic batch.</remarks>
        public ChangeFeedProcessorBuilder WithMaxItems(int maxItemCount)
        {
            if (maxItemCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxItemCount));
            }

            this.changeFeedProcessorOptions.MaxItemCount = maxItemCount;
            return this;
        }

        /// <summary>
        /// Sets the Cosmos Container to hold the leases state
        /// </summary>
        /// <param name="leaseContainer">Instance of a Cosmos Container to hold the leases.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder"/> to use.</returns>
        public ChangeFeedProcessorBuilder WithLeaseContainer(Container leaseContainer)
        {
            if (leaseContainer == null)
            {
                throw new ArgumentNullException(nameof(leaseContainer));
            }

            if (this.leaseContainer != null)
            {
                throw new InvalidOperationException("The builder already defined a lease container.");
            }

            if (this.LeaseStoreManager != null)
            {
                throw new InvalidOperationException("The builder already defined an in-memory lease container instance.");
            }

            this.leaseContainer = (ContainerInternal)leaseContainer;
            return this;
        }

        /// <summary>
        /// Uses an in-memory container to maintain state of the leases
        /// </summary>
        /// <remarks>
        /// Using an in-memory container restricts the scaling capability to just the instance running the current processor.
        /// </remarks>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder"/> to use.</returns>
        internal virtual ChangeFeedProcessorBuilder WithInMemoryLeaseContainer()
        {
            if (this.leaseContainer != null)
            {
                throw new InvalidOperationException("The builder already defined a lease container.");
            }

            if (this.LeaseStoreManager != null)
            {
                throw new InvalidOperationException("The builder already defined an in-memory lease container instance.");
            }

            if (string.IsNullOrEmpty(this.InstanceName))
            {
                this.InstanceName = ChangeFeedProcessorBuilder.InMemoryDefaultHostName;
            }

            this.LeaseStoreManager = new DocumentServiceLeaseStoreManagerInMemory();
            return this;
        }

        /// <summary>
        /// Defines a delegate to receive notifications on errors that occur during change feed processor execution.
        /// </summary>
        /// <param name="errorDelegate">A delegate to receive notifications for change feed processor related errors.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder"/> to use.</returns>
        public ChangeFeedProcessorBuilder WithErrorNotification(ChangeFeedMonitorErrorDelegate errorDelegate)
        {
            if (errorDelegate == null)
            {
                throw new ArgumentNullException(nameof(errorDelegate));
            }

            this.changeFeedProcessorOptions.HealthMonitor.SetErrorDelegate(errorDelegate);
            return this;
        }

        /// <summary>
        /// Defines a delegate to receive notifications on lease acquires that occur during change feed processor execution.
        /// </summary>
        /// <param name="acquireDelegate">A delegate to receive notifications when a change feed processor acquires a lease.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder"/> to use.</returns>
        public ChangeFeedProcessorBuilder WithLeaseAcquireNotification(ChangeFeedMonitorLeaseAcquireDelegate acquireDelegate)
        {
            if (acquireDelegate == null)
            {
                throw new ArgumentNullException(nameof(acquireDelegate));
            }

            this.changeFeedProcessorOptions.HealthMonitor.SetLeaseAcquireDelegate(acquireDelegate);
            return this;
        }

        /// <summary>
        /// Defines a delegate to receive notifications on lease releases that occur during change feed processor execution.
        /// </summary>
        /// <param name="releaseDelegate">A delegate to receive notifications when a change feed processor releases a lease.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder"/> to use.</returns>
        public ChangeFeedProcessorBuilder WithLeaseReleaseNotification(ChangeFeedMonitorLeaseReleaseDelegate releaseDelegate)
        {
            if (releaseDelegate == null)
            {
                throw new ArgumentNullException(nameof(releaseDelegate));
            }

            this.changeFeedProcessorOptions.HealthMonitor.SetLeaseReleaseDelegate(releaseDelegate);
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
                throw new InvalidOperationException($"Defining the lease store by WithLeaseContainer or WithInMemoryLeaseContainer is required.");
            }

            if (this.changeFeedLeaseOptions.LeasePrefix == null)
            {
                throw new InvalidOperationException("Processor name not specified during creation.");
            }

            this.applyBuilderConfiguration(this.LeaseStoreManager, this.leaseContainer, this.InstanceName, this.changeFeedLeaseOptions, this.changeFeedProcessorOptions, this.monitoredContainer);

            this.isBuilt = true;
            return this.changeFeedProcessor;
        }
    }
}
