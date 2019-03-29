//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Bootstrapping;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.FeedProcessing;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Monitoring;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.FeedManagement;

    internal sealed class ChangeFeedProcessorBuilderInstance<T>
    {
        internal ChangeFeedProcessorOptions changeFeedProcessorOptions;
        internal ChangeFeedLeaseOptions changeFeedLeaseOptions;
        internal ChangeFeedObserverFactory<T> observerFactory = null;
        internal LoadBalancingStrategy loadBalancingStrategy;
        internal FeedProcessorFactory<T> partitionProcessorFactory = null;
        internal HealthMonitor healthMonitor;
        internal CosmosContainer monitoredContainer;
        internal CosmosContainer leaseContainer;
        internal string InstanceName;
        internal DocumentServiceLeaseStoreManager LeaseStoreManager;

        private string databaseResourceId;
        private string collectionResourceId;

        public async Task<ChangeFeedProcessor> BuildAsync()
        {
            if (this.InstanceName == null)
            {
                throw new InvalidOperationException("Instance name was not specified");
            }

            if (this.monitoredContainer == null)
            {
                throw new InvalidOperationException(nameof(this.monitoredContainer) + " was not specified");
            }

            if (this.leaseContainer == null && this.LeaseStoreManager == null)
            {
                throw new InvalidOperationException($"Defining the lease store by WithCosmosLeaseContainer, WithInMemoryLeaseContainer, or WithLeaseStoreManager is required.");
            }

            if (this.observerFactory == null)
            {
                throw new InvalidOperationException("Observer was not specified");
            }

            this.InitializeDefaultOptions();
            this.InitializeCollectionPropertiesForBuild();

            DocumentServiceLeaseStoreManager leaseStoreManager = await this.GetLeaseStoreManagerAsync().ConfigureAwait(false);
            PartitionManager partitionManager = this.BuildPartitionManager(leaseStoreManager);
            return new ChangeFeedProcessorCore(partitionManager);
        }

        /// <summary>
        /// Builds a new instance of the <see cref="RemainingWorkEstimator"/> to estimate pending work with the specified configuration.
        /// </summary>
        /// <returns>An instance of <see cref="RemainingWorkEstimator"/>.</returns>
        public async Task<RemainingWorkEstimator> BuildEstimatorAsync()
        {
            if (this.monitoredContainer == null)
            {
                throw new InvalidOperationException(nameof(this.monitoredContainer) + " was not specified");
            }

            if (this.leaseContainer == null && this.LeaseStoreManager == null)
            {
                throw new InvalidOperationException($"Defining the lease store by WithCosmosLeaseContainer, WithInMemoryLeaseContainer, or WithLeaseStoreManager is required.");
            }

            this.InitializeDefaultOptions();
            this.InitializeCollectionPropertiesForBuild();

            var leaseStoreManager = await this.GetLeaseStoreManagerAsync().ConfigureAwait(false);

            RemainingWorkEstimator remainingWorkEstimator = new RemainingWorkEstimatorCore(
                leaseStoreManager.LeaseContainer,
                this.monitoredContainer,
                this.monitoredContainer.Client.Configuration?.MaxConnectionLimit ?? 1);
            return remainingWorkEstimator;
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
}