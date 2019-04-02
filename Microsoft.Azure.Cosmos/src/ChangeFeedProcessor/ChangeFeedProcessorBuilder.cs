//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Bootstrapping;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.FeedManagement;
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

        private readonly Func<IReadOnlyList<T>, CancellationToken, Task> initialChangesDelegate;
        private readonly Func<long, CancellationToken, Task> initialEstimateDelegate;

        private ChangeFeedProcessorOptions changeFeedProcessorOptions;
        private ChangeFeedLeaseOptions changeFeedLeaseOptions;
        private ChangeFeedObserverFactory<T> observerFactory = null;
        private TimeSpan? estimatorPeriod = null;
        private LoadBalancingStrategy loadBalancingStrategy;
        private FeedProcessorFactory<T> partitionProcessorFactory = null;
        private HealthMonitor healthMonitor;
        private CosmosContainer monitoredContainer;
        private CosmosContainer leaseContainer;
        private string InstanceName;
        private DocumentServiceLeaseStoreManager LeaseStoreManager;
        private string databaseResourceId;
        private string collectionResourceId;
        private bool isBuilt;

        private bool IsBuildingEstimator => this.initialEstimateDelegate != null;

        internal ChangeFeedProcessorBuilder(CosmosContainer cosmosContainer) : base()
        {
            this.monitoredContainer = cosmosContainer;
        }

        internal ChangeFeedProcessorBuilder(CosmosContainer cosmosContainer, Func<IReadOnlyList<T>, CancellationToken, Task> onChangesDelegate)
            : this(cosmosContainer)
        {
            this.initialChangesDelegate = onChangesDelegate;
            this.observerFactory = new ChangeFeedObserverFactoryCore<T>(onChangesDelegate);
        }

        internal ChangeFeedProcessorBuilder(CosmosContainer cosmosContainer, Func<long, CancellationToken, Task> estimateDelegate, TimeSpan? estimatorPeriod = null)
            : this(cosmosContainer)
        {
            this.initialEstimateDelegate = estimateDelegate;
            this.estimatorPeriod = estimatorPeriod;
        }

        /// <summary>
        /// Sets the Host name.
        /// </summary>
        /// <param name="instanceName">Name to be used for the processor instance. When using multiple processor hosts, each host must have a unique name.</param>
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder{T}"/> to use.</returns>
        public ChangeFeedProcessorBuilder<T> WithInstanceName(string instanceName)
        {
            this.InstanceName = instanceName;
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
        public ChangeFeedProcessorBuilder<T> WithLeaseConfiguration(string leasePrefix, TimeSpan? acquireInterval = null, TimeSpan? expirationInterval = null, TimeSpan? renewInterval = null)
        {
            this.changeFeedLeaseOptions = this.changeFeedLeaseOptions ?? new ChangeFeedLeaseOptions();
            this.changeFeedLeaseOptions.LeasePrefix = leasePrefix;
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
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder{T}"/> to use.</returns>
        public ChangeFeedProcessorBuilder<T> WithFeedPollDelay(TimeSpan feedPollDelay)
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
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder{T}"/> to use.</returns>
        public ChangeFeedProcessorBuilder<T> WithStartFromBeginning()
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
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder{T}"/> to use.</returns>
        public ChangeFeedProcessorBuilder<T> WithStartContinuation(string startContinuation)
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
        public ChangeFeedProcessorBuilder<T> WithStartTime(DateTime startTime)
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
        public ChangeFeedProcessorBuilder<T> WithMaxItems(int maxItemCount)
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
        public ChangeFeedProcessorBuilder<T> WithCosmosLeaseContainer(CosmosContainer leaseContainer)
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
        /// <returns>The instance of <see cref="ChangeFeedProcessorBuilder{T}"/> to use.</returns>
        public ChangeFeedProcessorBuilder<T> WithInMemoryLeaseContainer()
        {
            if (this.leaseContainer != null) throw new InvalidOperationException("The builder already defined a lease container.");
            if (this.LeaseStoreManager != null) throw new InvalidOperationException("The builder already defined an in-memory lease container instance.");

            if (string.IsNullOrEmpty(this.InstanceName))
            {
                this.InstanceName = ChangeFeedProcessorBuilder<T>.InMemoryDefaultHostName;
            }

            this.LeaseStoreManager = new DocumentServiceLeaseStoreManagerInMemory();
            return this;
        }

        /// <summary>
        /// Builds a new instance of the <see cref="ChangeFeedProcessor"/> with the specified configuration.
        /// </summary>
        /// <returns>An instance of <see cref="ChangeFeedProcessor"/>.</returns>
        public async Task<ChangeFeedProcessor> BuildAsync()
        {
            if (this.isBuilt)
            {
                throw new InvalidOperationException("This builder instance has already been used to build a processor. Create a new instance to build another.");
            }

            if (IsBuildingEstimator)
            {
                this.InstanceName = ChangeFeedProcessorBuilder<T>.EstimatorDefaultHostName;
            }

            if (this.monitoredContainer == null)
            {
                throw new InvalidOperationException(nameof(this.monitoredContainer) + " was not specified");
            }

            if (this.leaseContainer == null && this.LeaseStoreManager == null)
            {
                throw new InvalidOperationException($"Defining the lease store by WithCosmosLeaseContainer, WithInMemoryLeaseContainer, or WithLeaseStoreManager is required.");
            }

            if (this.observerFactory == null && this.initialEstimateDelegate == null)
            {
                throw new InvalidOperationException("Observer or Estimation delegate need to be specified.");
            }

            if (this.observerFactory != null && this.InstanceName == null)
            {
                // Processor requires Instace Name
                throw new InvalidOperationException("Instance name was not specified");
            }

            this.InitializeDefaultOptions();
            this.InitializeCollectionPropertiesForBuild();

            DocumentServiceLeaseStoreManager leaseStoreManager = await this.GetLeaseStoreManagerAsync().ConfigureAwait(false);

            ChangeFeedProcessor builtInstance;
            if (this.observerFactory != null)
            {
                PartitionManager partitionManager = this.BuildPartitionManager(leaseStoreManager);
                builtInstance = new ChangeFeedProcessorCore(partitionManager);
            }
            else
            {

                FeedEstimator remainingWorkEstimator = this.BuildFeedEstimator(leaseStoreManager);
                builtInstance = new ChangeFeedEstimatorCore(remainingWorkEstimator);
            }

            this.isBuilt = true;
            return builtInstance;
        }

        private FeedEstimator BuildFeedEstimator(DocumentServiceLeaseStoreManager leaseStoreManager)
        {
            RemainingWorkEstimatorCore remainingWorkEstimator = new RemainingWorkEstimatorCore(
               leaseStoreManager.LeaseContainer,
               this.monitoredContainer,
               this.monitoredContainer.Client.Configuration?.MaxConnectionLimit ?? 1);

            ChangeFeedEstimatorDispatcher estimatorDispatcher = new ChangeFeedEstimatorDispatcher(this.initialEstimateDelegate, this.estimatorPeriod);

            return new FeedEstimatorCore(estimatorDispatcher, remainingWorkEstimator);
        }

        private PartitionManager BuildPartitionManager(DocumentServiceLeaseStoreManager leaseStoreManager)
        {
            var factory = new CheckpointerObserverFactory<T>(this.observerFactory, this.changeFeedProcessorOptions.CheckpointFrequency);
            var synchronizer = new PartitionSynchronizerCore(
                this.monitoredContainer,
                leaseStoreManager.LeaseContainer,
                leaseStoreManager.LeaseManager,
                PartitionSynchronizerCore.DefaultDegreeOfParallelism,
                this.changeFeedProcessorOptions.QueryFeedMaxBatchSize);
            var bootstrapper = new BootstrapperCore(synchronizer, leaseStoreManager.LeaseStore, BootstrapperCore.DefaultLockTime, BootstrapperCore.DefaultSleepTime);
            var partitionSuperviserFactory = new PartitionSupervisorFactoryCore<T>(
                factory,
                leaseStoreManager.LeaseManager,
                this.partitionProcessorFactory ?? new FeedProcessorFactoryCore<T>(this.monitoredContainer, this.changeFeedProcessorOptions, leaseStoreManager.LeaseCheckpointer),
                this.changeFeedLeaseOptions);

            if (this.loadBalancingStrategy == null)
            {
                this.loadBalancingStrategy = new EqualPartitionsBalancingStrategy(
                    this.InstanceName,
                    EqualPartitionsBalancingStrategy.DefaultMinLeaseCount,
                    EqualPartitionsBalancingStrategy.DefaultMaxLeaseCount,
                    this.changeFeedLeaseOptions.LeaseExpirationInterval);
            }

            PartitionController partitionController = new PartitionControllerCore(leaseStoreManager.LeaseContainer, leaseStoreManager.LeaseManager, partitionSuperviserFactory, synchronizer);

            if (this.healthMonitor == null)
            {
                this.healthMonitor = new TraceHealthMonitor();
            }

            partitionController = new HealthMonitoringPartitionControllerDecorator(partitionController, this.healthMonitor);
            var partitionLoadBalancer = new PartitionLoadBalancerCore(
                partitionController,
                leaseStoreManager.LeaseContainer,
                this.loadBalancingStrategy,
                this.changeFeedLeaseOptions.LeaseAcquireInterval);
            return new PartitionManagerCore(bootstrapper, partitionController, partitionLoadBalancer);
        }

        private async Task<DocumentServiceLeaseStoreManager> GetLeaseStoreManagerAsync()
        {
            if (this.LeaseStoreManager == null)
            {
                var cosmosContainerResponse = await this.leaseContainer.ReadAsync().ConfigureAwait(false);
                var containerSettings = cosmosContainerResponse.Resource;

                bool isPartitioned =
                    containerSettings.PartitionKey != null &&
                    containerSettings.PartitionKey.Paths != null &&
                    containerSettings.PartitionKey.Paths.Count > 0;
                if (isPartitioned &&
                    (containerSettings.PartitionKey.Paths.Count != 1 || containerSettings.PartitionKey.Paths[0] != "/id"))
                {
                    throw new ArgumentException("The lease collection, if partitioned, must have partition key equal to id.");
                }

                var requestOptionsFactory = isPartitioned ?
                    (RequestOptionsFactory)new PartitionedByIdCollectionRequestOptionsFactory() :
                    (RequestOptionsFactory)new SinglePartitionRequestOptionsFactory();

                string leasePrefix = this.GetLeasePrefix();
                var leaseStoreManagerBuilder = new DocumentServiceLeaseStoreManagerBuilder()
                    .WithLeasePrefix(leasePrefix)
                    .WithLeaseContainer(this.leaseContainer)
                    .WithRequestOptionsFactory(requestOptionsFactory)
                    .WithHostName(this.InstanceName);

                this.LeaseStoreManager = await leaseStoreManagerBuilder.BuildAsync().ConfigureAwait(false);
            }

            return this.LeaseStoreManager;
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
            var containerLinkSegments = this.monitoredContainer.LinkUri.OriginalString.Split('/');
            this.databaseResourceId = containerLinkSegments[2];
            this.collectionResourceId = containerLinkSegments[4];
        }

        private void InitializeDefaultOptions()
        {
            this.changeFeedProcessorOptions = this.changeFeedProcessorOptions ?? new ChangeFeedProcessorOptions();
            this.changeFeedLeaseOptions = this.changeFeedLeaseOptions ?? new ChangeFeedLeaseOptions();
        }
    }

    /// <summary>
    /// Provides a flexible way to to create an instance of <see cref="ChangeFeedProcessor"/> with custom set of parameters.
    /// </summary>
    public class ChangeFeedProcessorBuilder : ChangeFeedProcessorBuilder<dynamic>
    {
        internal ChangeFeedProcessorBuilder(CosmosContainer cosmosContainer, Func<IReadOnlyList<dynamic>, CancellationToken, Task> onChangesDelegate) : base(cosmosContainer, onChangesDelegate)
        {
        }

        internal ChangeFeedProcessorBuilder(CosmosContainer cosmosContainer, Func<long, CancellationToken, Task> estimateDelegate, TimeSpan? estimationPeriod = null) : base(cosmosContainer, estimateDelegate, estimationPeriod)
        {
        }
    }
}
