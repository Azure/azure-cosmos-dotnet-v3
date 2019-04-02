//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Logging;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.FeedProcessing;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Bootstrapping;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Monitoring;

    internal sealed class ChangeFeedProcessorCore<T> : ChangeFeedProcessor
    {
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();
        private readonly CosmosContainer leaseContainer;
        private readonly string leaseContainerPrefix;
        private readonly string instanceName;
        private readonly ChangeFeedObserverFactory<T> observerFactory;
        private readonly CosmosContainer monitoredContainer;
        private PartitionManager partitionManager;
        private ChangeFeedLeaseOptions changeFeedLeaseOptions;
        private ChangeFeedProcessorOptions changeFeedProcessorOptions;
        private DocumentServiceLeaseStoreManager documentServiceLeaseStoreManager;
        private bool initialized = false;

        public ChangeFeedProcessorCore(
            DocumentServiceLeaseStoreManager customDocumentServiceLeaseStoreManager,
            CosmosContainer leaseContainer,
            string leaseContainerPrefix,
            string instanceName,
            ChangeFeedLeaseOptions changeFeedLeaseOptions,
            ChangeFeedProcessorOptions changeFeedProcessorOptions,
            ChangeFeedObserverFactory<T> observerFactory,
            CosmosContainer monitoredContainer)
        {
            if (monitoredContainer == null) throw new ArgumentNullException(nameof(monitoredContainer));
            if (customDocumentServiceLeaseStoreManager == null && leaseContainer == null) throw new ArgumentNullException(nameof(leaseContainer));
            if (instanceName == null) throw new ArgumentNullException(nameof(instanceName));
            if (observerFactory == null) throw new ArgumentNullException(nameof(observerFactory));

            this.documentServiceLeaseStoreManager = customDocumentServiceLeaseStoreManager;
            this.leaseContainer = leaseContainer;
            this.leaseContainerPrefix = leaseContainerPrefix;
            this.instanceName = instanceName;
            this.changeFeedProcessorOptions = changeFeedProcessorOptions;
            this.changeFeedLeaseOptions = changeFeedLeaseOptions;
            this.observerFactory = observerFactory;
            this.monitoredContainer = monitoredContainer;
        }

        public override async Task StartAsync()
        {
            if (!this.initialized)
            {
                await this.InitializeAsync().ConfigureAwait(false);
            }

            Logger.InfoFormat("Starting processor...");
            await this.partitionManager.StartAsync().ConfigureAwait(false);
            Logger.InfoFormat("Processor started.");
        }

        public override async Task StopAsync()
        {
            Logger.InfoFormat("Stopping processor...");
            await this.partitionManager.StopAsync().ConfigureAwait(false);
            Logger.InfoFormat("Processor stopped.");
        }

        private async Task InitializeAsync()
        {
            this.documentServiceLeaseStoreManager = await ChangeFeedProcessorCore<T>.InitializeLeaseStoreManagerAsync(this.documentServiceLeaseStoreManager, this.leaseContainer, this.leaseContainerPrefix, this.instanceName).ConfigureAwait(false);
            this.partitionManager = this.BuildPartitionManager();
            this.initialized = true;
        }

        internal static async Task<DocumentServiceLeaseStoreManager> InitializeLeaseStoreManagerAsync(
            DocumentServiceLeaseStoreManager documentServiceLeaseStoreManager,
            CosmosContainer leaseContainer,
            string leaseContainerPrefix,
            string instanceName)
        {
            if (documentServiceLeaseStoreManager == null)
            {
                var cosmosContainerResponse = await leaseContainer.ReadAsync().ConfigureAwait(false);
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

                var leaseStoreManagerBuilder = new DocumentServiceLeaseStoreManagerBuilder()
                    .WithLeasePrefix(leaseContainerPrefix)
                    .WithLeaseContainer(leaseContainer)
                    .WithRequestOptionsFactory(requestOptionsFactory)
                    .WithHostName(instanceName);

                documentServiceLeaseStoreManager = await leaseStoreManagerBuilder.BuildAsync().ConfigureAwait(false);
            }

            return documentServiceLeaseStoreManager;
        }

        internal PartitionManager BuildPartitionManager()
        {
            var factory = new CheckpointerObserverFactory<T>(this.observerFactory, this.changeFeedProcessorOptions.CheckpointFrequency);
            var synchronizer = new PartitionSynchronizerCore(
                this.monitoredContainer,
                this.documentServiceLeaseStoreManager.LeaseContainer,
                this.documentServiceLeaseStoreManager.LeaseManager,
                PartitionSynchronizerCore.DefaultDegreeOfParallelism,
                this.changeFeedProcessorOptions.QueryFeedMaxBatchSize);
            var bootstrapper = new BootstrapperCore(synchronizer, this.documentServiceLeaseStoreManager.LeaseStore, BootstrapperCore.DefaultLockTime, BootstrapperCore.DefaultSleepTime);
            var partitionSuperviserFactory = new PartitionSupervisorFactoryCore<T>(
                factory,
                this.documentServiceLeaseStoreManager.LeaseManager,
                new FeedProcessorFactoryCore<T>(this.monitoredContainer, this.changeFeedProcessorOptions, this.documentServiceLeaseStoreManager.LeaseCheckpointer),
                this.changeFeedLeaseOptions);

            var loadBalancingStrategy = new EqualPartitionsBalancingStrategy(
                    this.instanceName,
                    EqualPartitionsBalancingStrategy.DefaultMinLeaseCount,
                    EqualPartitionsBalancingStrategy.DefaultMaxLeaseCount,
                    this.changeFeedLeaseOptions.LeaseExpirationInterval);

            PartitionController partitionController = new PartitionControllerCore(this.documentServiceLeaseStoreManager.LeaseContainer, this.documentServiceLeaseStoreManager.LeaseManager, partitionSuperviserFactory, synchronizer);

            partitionController = new HealthMonitoringPartitionControllerDecorator(partitionController, new TraceHealthMonitor());
            var partitionLoadBalancer = new PartitionLoadBalancerCore(
                partitionController,
                this.documentServiceLeaseStoreManager.LeaseContainer,
                loadBalancingStrategy,
                this.changeFeedLeaseOptions.LeaseAcquireInterval);
            return new PartitionManagerCore(bootstrapper, partitionController, partitionLoadBalancer);
        }
    }
}