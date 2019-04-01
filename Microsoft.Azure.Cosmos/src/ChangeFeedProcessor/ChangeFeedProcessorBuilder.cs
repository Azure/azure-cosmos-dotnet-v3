//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.FeedProcessing;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Monitoring;

    /// <summary>
    /// Provides a flexible way to to create an instance of <see cref="ChangeFeedProcessor"/> with custom set of parameters.
    /// </summary>

    public class ChangeFeedProcessorBuilder<T>
    {
        private const string InMemoryDefaultHostName = "InMemory";
        private const string EstimatorDefaultHostName = "Estimator";

        private readonly CosmosContainer initialCosmosContainer;
        private readonly Func<IReadOnlyList<T>, CancellationToken, Task> initialChangesDelegate;
        private readonly Func<long, CancellationToken, Task> initialEstimateDelegate;

        private ChangeFeedProcessorBuilderInstance<T> changeFeedProcessorBuilderInstance;

        private bool IsBuildingEstimator => this.initialEstimateDelegate != null;

        internal string InstanceName
        {
            get => this.changeFeedProcessorBuilderInstance.InstanceName;
        }

        /// <summary>
        /// Gets the lease manager.
        /// </summary>
        /// <remarks>
        /// Internal for testing only, otherwise it would be private.
        /// </remarks>
        internal DocumentServiceLeaseStoreManager LeaseStoreManager
        {
            get => this.changeFeedProcessorBuilderInstance.LeaseStoreManager;
        }

        internal ChangeFeedProcessorBuilder(CosmosContainer cosmosContainer): base()
        {
            this.initialCosmosContainer = cosmosContainer;
            this.Reset();
        }

        internal ChangeFeedProcessorBuilder(CosmosContainer cosmosContainer, Func<IReadOnlyList<T>, CancellationToken, Task> onChangesDelegate)
            : this(cosmosContainer)
        {
            this.initialChangesDelegate = onChangesDelegate;
            this.changeFeedProcessorBuilderInstance.observerFactory = new ChangeFeedObserverFactoryCore<T>(onChangesDelegate);
        }

        internal ChangeFeedProcessorBuilder(CosmosContainer cosmosContainer, Func<long, CancellationToken, Task> estimateDelegate, TimeSpan? estimationPeriod = null)
            : this(cosmosContainer)
        {
            this.initialEstimateDelegate = estimateDelegate;
            this.changeFeedProcessorBuilderInstance.estimatorDispatcher = new ChangeFeedEstimatorDispatcher(estimateDelegate, estimationPeriod);
        }

        /// <summary>
        /// Sets the Host name.
        /// </summary>
        /// <param name="instanceName">Name to be used for the processor instance. When using multiple processor hosts, each host must have a unique name.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder{T}"/> to use.</returns>
        public ChangeFeedProcessorBuilder<T> WithInstanceName(string instanceName)
        {
            this.changeFeedProcessorBuilderInstance.InstanceName = instanceName;
            return this;
        }

        /// <summary>
        /// Sets a custom configuration to be used by this instance of <see cref="ChangeFeedProcessor"/> to control how leases are maintained in a container when using <see cref="WithCosmosLeaseContainer(CosmosContainer)"/>.
        /// </summary>
        /// <param name="leasePrefix">Prefix to use for the leases. Used for when the same lease container is shared across different processors.</param>
        /// <param name="acquireInterval">Interval to kick off a task to verify if leases are distributed evenly among known host instances.</param>
        /// <param name="expirationInterval">Interval for which the lease is taken. If the lease is not renewed within this interval, it will cause it to expire and ownership of the lease will move to another processor instance.</param>
        /// <param name="renewInterval">Renew interval for all leases currently held by a particular processor instance.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder{T}"/> to use.</returns>
        public ChangeFeedProcessorBuilder<T> WithCustomLeaseConfiguration(string leasePrefix, TimeSpan? acquireInterval = null, TimeSpan? expirationInterval = null, TimeSpan? renewInterval = null)
        {
            this.changeFeedProcessorBuilderInstance.changeFeedLeaseOptions = this.changeFeedProcessorBuilderInstance.changeFeedLeaseOptions ?? new ChangeFeedLeaseOptions();
            this.changeFeedProcessorBuilderInstance.changeFeedLeaseOptions.LeasePrefix = leasePrefix;
            this.changeFeedProcessorBuilderInstance.changeFeedLeaseOptions.LeaseRenewInterval = renewInterval ?? ChangeFeedLeaseOptions.DefaultRenewInterval;
            this.changeFeedProcessorBuilderInstance.changeFeedLeaseOptions.LeaseAcquireInterval = acquireInterval ?? ChangeFeedLeaseOptions.DefaultAcquireInterval;
            this.changeFeedProcessorBuilderInstance.changeFeedLeaseOptions.LeaseExpirationInterval = expirationInterval ?? ChangeFeedLeaseOptions.DefaultExpirationInterval;
            return this;
        }

        /// <summary>
        /// Gets or sets the delay in between polling the change feed for new changes, after all current changes are drained.
        /// </summary>
        /// <remarks>
        /// Applies only after a read on the change feed yielded no results.
        /// </remarks>
        /// <param name="feedPollDelay">Polling interval value.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder{T}"/> to use.</returns>
        public ChangeFeedProcessorBuilder<T> WithFeedPollDelay(TimeSpan feedPollDelay)
        {
            if (feedPollDelay == null) throw new ArgumentNullException(nameof(feedPollDelay));
            this.changeFeedProcessorBuilderInstance.changeFeedProcessorOptions = this.changeFeedProcessorBuilderInstance.changeFeedProcessorOptions ?? new ChangeFeedProcessorOptions();
            this.changeFeedProcessorBuilderInstance.changeFeedProcessorOptions.FeedPollDelay = feedPollDelay;
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
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder{T}"/> to use.</returns>
        public ChangeFeedProcessorBuilder<T> WithStartFromBeginning()
        {
            this.changeFeedProcessorBuilderInstance.changeFeedProcessorOptions = this.changeFeedProcessorBuilderInstance.changeFeedProcessorOptions ?? new ChangeFeedProcessorOptions();
            this.changeFeedProcessorBuilderInstance.changeFeedProcessorOptions.StartFromBeginning = true;
            return this;
        }

        /// <summary>
        /// Sets the start request continuation token to start looking for changes after.
        /// </summary>
        /// <remarks>
        /// This is only used when lease store is not initialized and is ignored if a lease exists and has continuation token.
        /// If this is specified, both StartTime and StartFromBeginning are ignored.
        /// </remarks>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder{T}"/> to use.</returns>
        public ChangeFeedProcessorBuilder<T> WithStartContinuation(string startContinuation)
        {
            this.changeFeedProcessorBuilderInstance.changeFeedProcessorOptions = this.changeFeedProcessorBuilderInstance.changeFeedProcessorOptions ?? new ChangeFeedProcessorOptions();
            this.changeFeedProcessorBuilderInstance.changeFeedProcessorOptions.StartContinuation = startContinuation;
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
        public ChangeFeedProcessorBuilder<T> WithStartTime(DateTime startTime)
        {
            if (startTime == null) throw new ArgumentNullException(nameof(startTime));
            this.changeFeedProcessorBuilderInstance.changeFeedProcessorOptions = this.changeFeedProcessorBuilderInstance.changeFeedProcessorOptions ?? new ChangeFeedProcessorOptions();
            this.changeFeedProcessorBuilderInstance.changeFeedProcessorOptions.StartTime = startTime;
            return this;
        }

        /// <summary>
        /// Sets the maximum number of items to be returned in the enumeration operation in the Azure Cosmos DB service.
        /// </summary>
        /// <param name="maxItemCount">Maximum amount of items to be returned in a Change Feed request.</param>
        /// <returns></returns>
        public ChangeFeedProcessorBuilder<T> WithMaxItems(int maxItemCount)
        {
            if (maxItemCount <= 0) throw new ArgumentOutOfRangeException(nameof(maxItemCount));
            this.changeFeedProcessorBuilderInstance.changeFeedProcessorOptions = this.changeFeedProcessorBuilderInstance.changeFeedProcessorOptions ?? new ChangeFeedProcessorOptions();
            this.changeFeedProcessorBuilderInstance.changeFeedProcessorOptions.MaxItemCount = maxItemCount;
            return this;
        }

        /// <summary>
        /// Sets the Cosmos Container to hold the leases state
        /// </summary>
        /// <param name="leaseContainer">Instance of a Cosmos Container to hold the leases.</param>
        /// <returns></returns>
        public ChangeFeedProcessorBuilder<T> WithCosmosLeaseContainer(CosmosContainer leaseContainer)
        {
            if (leaseContainer == null) throw new ArgumentNullException(nameof(leaseContainer));
            if (this.changeFeedProcessorBuilderInstance.leaseContainer != null) throw new InvalidOperationException("The builder already defined a lease container.");
            if (this.changeFeedProcessorBuilderInstance.LeaseStoreManager != null) throw new InvalidOperationException("The builder already defined a custom Lease Store Manager instance.");
            this.changeFeedProcessorBuilderInstance.leaseContainer = leaseContainer;
            return this;
        }

        /// <summary>
        /// Uses an in-memory container to maintain state of the leases
        /// </summary>
        /// <remarks>
        /// Using an in-memory container restricts the scaling capability to just the instance running the current processor.
        /// </remarks>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder{T}"/> to use.</returns>
        public ChangeFeedProcessorBuilder<T> WithInMemoryLeaseContainer()
        {
            if (this.changeFeedProcessorBuilderInstance.leaseContainer != null) throw new InvalidOperationException("The builder already defined a lease container.");
            if (this.changeFeedProcessorBuilderInstance.LeaseStoreManager != null) throw new InvalidOperationException("The builder already defined an in-memory lease container or a custom Lease Store Manager instance.");
            if (string.IsNullOrEmpty(this.InstanceName))
            {
                this.changeFeedProcessorBuilderInstance.InstanceName = ChangeFeedProcessorBuilder<T>.InMemoryDefaultHostName;
            }

            this.changeFeedProcessorBuilderInstance.LeaseStoreManager = new DocumentServiceLeaseStoreManagerInMemory();
            return this;
        }

        /// <summary>
        /// Builds a new instance of the <see cref="ChangeFeedProcessor"/> with the specified configuration.
        /// </summary>
        /// <returns>An instance of <see cref="ChangeFeedProcessor"/>.</returns>
        public async Task<ChangeFeedProcessor> BuildAsync()
        {
            if (IsBuildingEstimator)
            {
                this.changeFeedProcessorBuilderInstance.InstanceName = ChangeFeedProcessorBuilder<T>.EstimatorDefaultHostName;
            }

            ChangeFeedProcessor changeFeedProcessor =  await this.changeFeedProcessorBuilderInstance.BuildAsync().ConfigureAwait(false);
            this.Reset();
            return changeFeedProcessor;
        }

        private void Reset()
        {
            this.changeFeedProcessorBuilderInstance = new ChangeFeedProcessorBuilderInstance<T>();
            this.changeFeedProcessorBuilderInstance.monitoredContainer = this.initialCosmosContainer;
            if (this.initialChangesDelegate != null)
            {
                this.changeFeedProcessorBuilderInstance.observerFactory = new ChangeFeedObserverFactoryCore<T>(this.initialChangesDelegate);
            }

            if (this.initialEstimateDelegate != null)
            {
                this.changeFeedProcessorBuilderInstance.estimatorDispatcher = new ChangeFeedEstimatorDispatcher(this.initialEstimateDelegate);
            }
        }
    }
}