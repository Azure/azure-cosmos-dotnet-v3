//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal sealed class ChangeFeedProcessorCore<T> : ChangeFeedProcessor
    {
        private readonly ChangeFeedObserverFactory<T> observerFactory;
        private ContainerCore leaseContainer;
        private string monitoredContainerRid;
        private string instanceName;
        private ContainerCore monitoredContainer;
        private PartitionManager partitionManager;
        private ChangeFeedLeaseOptions changeFeedLeaseOptions;
        private ChangeFeedProcessorOptions changeFeedProcessorOptions;
        private DocumentServiceLeaseStoreManager documentServiceLeaseStoreManager;
        private bool initialized = false;

        public ChangeFeedProcessorCore(ChangeFeedObserverFactory<T> observerFactory)
        {
            if (observerFactory == null) throw new ArgumentNullException(nameof(observerFactory));

            this.observerFactory = observerFactory;
        }

        public void ApplyBuildConfiguration(
            DocumentServiceLeaseStoreManager customDocumentServiceLeaseStoreManager,
            ContainerCore leaseContainer,
            string monitoredContainerRid,
            string instanceName,
            ChangeFeedLeaseOptions changeFeedLeaseOptions,
            ChangeFeedProcessorOptions changeFeedProcessorOptions,
            ContainerCore monitoredContainer)
        {
            if (monitoredContainer == null) throw new ArgumentNullException(nameof(monitoredContainer));
            if (customDocumentServiceLeaseStoreManager == null && leaseContainer == null) throw new ArgumentNullException(nameof(leaseContainer));
            if (instanceName == null) throw new ArgumentNullException("InstanceName is required for the processor to initialize.");

            this.documentServiceLeaseStoreManager = customDocumentServiceLeaseStoreManager;
            this.leaseContainer = leaseContainer;
            this.monitoredContainerRid = monitoredContainerRid;
            this.instanceName = instanceName;
            this.changeFeedProcessorOptions = changeFeedProcessorOptions;
            this.changeFeedLeaseOptions = changeFeedLeaseOptions;
            this.monitoredContainer = monitoredContainer;
        }

        public override async Task StartAsync()
        {
            if (!this.initialized)
            {
                await this.InitializeAsync().ConfigureAwait(false);
            }

            DefaultTrace.TraceInformation("Starting processor...");
            await this.partitionManager.StartAsync().ConfigureAwait(false);
            DefaultTrace.TraceInformation("Processor started.");
        }

        public override async Task StopAsync()
        {
            DefaultTrace.TraceInformation("Stopping processor...");
            await this.partitionManager.StopAsync().ConfigureAwait(false);
            DefaultTrace.TraceInformation("Processor stopped.");
        }

        private async Task InitializeAsync()
        {
            string monitoredContainerRid = await this.monitoredContainer.GetMonitoredContainerRidAsync(this.monitoredContainerRid);
            this.monitoredContainerRid = this.monitoredContainer.GetLeasePrefix(this.changeFeedLeaseOptions, monitoredContainerRid);
            this.documentServiceLeaseStoreManager = await ChangeFeedProcessorCore<T>.InitializeLeaseStoreManagerAsync(this.documentServiceLeaseStoreManager, this.leaseContainer, this.monitoredContainerRid, this.instanceName).ConfigureAwait(false);
            this.partitionManager = this.BuildPartitionManager();
            this.initialized = true;
        }

        internal static async Task<DocumentServiceLeaseStoreManager> InitializeLeaseStoreManagerAsync(
            DocumentServiceLeaseStoreManager documentServiceLeaseStoreManager,
            ContainerCore leaseContainer,
            string leaseContainerPrefix,
            string instanceName)
        {
            if (documentServiceLeaseStoreManager == null)
            {
                CosmosContainerProperties containerProperties = await leaseContainer.ReadContainerAsync().ConfigureAwait(false);

                bool isPartitioned =
                    containerProperties.PartitionKey != null &&
                    containerProperties.PartitionKey.Paths != null &&
                    containerProperties.PartitionKey.Paths.Count > 0;
                bool isMigratedFixed = containerProperties.PartitionKey?.IsSystemKey == true;
                if (isPartitioned
                    && !isMigratedFixed
                    && (containerProperties.PartitionKey.Paths.Count != 1 || containerProperties.PartitionKey.Paths[0] != "/id"))
                {
                    throw new ArgumentException("The lease collection, if partitioned, must have partition key equal to id.");
                }

                RequestOptionsFactory requestOptionsFactory = isPartitioned && !isMigratedFixed ?
                    (RequestOptionsFactory)new PartitionedByIdCollectionRequestOptionsFactory() :
                    (RequestOptionsFactory)new SinglePartitionRequestOptionsFactory();

                DocumentServiceLeaseStoreManagerBuilder leaseStoreManagerBuilder = new DocumentServiceLeaseStoreManagerBuilder()
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
            CheckpointerObserverFactory<T> factory = new CheckpointerObserverFactory<T>(this.observerFactory, this.changeFeedProcessorOptions.CheckpointFrequency);
            PartitionSynchronizerCore synchronizer = new PartitionSynchronizerCore(
                this.monitoredContainer,
                this.documentServiceLeaseStoreManager.LeaseContainer,
                this.documentServiceLeaseStoreManager.LeaseManager,
                PartitionSynchronizerCore.DefaultDegreeOfParallelism,
                this.changeFeedProcessorOptions.QueryFeedMaxBatchSize);
            BootstrapperCore bootstrapper = new BootstrapperCore(synchronizer, this.documentServiceLeaseStoreManager.LeaseStore, BootstrapperCore.DefaultLockTime, BootstrapperCore.DefaultSleepTime);
            PartitionSupervisorFactoryCore<T> partitionSuperviserFactory = new PartitionSupervisorFactoryCore<T>(
                factory,
                this.documentServiceLeaseStoreManager.LeaseManager,
                new FeedProcessorFactoryCore<T>(this.monitoredContainer, this.changeFeedProcessorOptions, this.documentServiceLeaseStoreManager.LeaseCheckpointer, this.monitoredContainer.ClientContext.CosmosSerializer),
                this.changeFeedLeaseOptions);

            EqualPartitionsBalancingStrategy loadBalancingStrategy = new EqualPartitionsBalancingStrategy(
                    this.instanceName,
                    EqualPartitionsBalancingStrategy.DefaultMinLeaseCount,
                    EqualPartitionsBalancingStrategy.DefaultMaxLeaseCount,
                    this.changeFeedLeaseOptions.LeaseExpirationInterval);

            PartitionController partitionController = new PartitionControllerCore(this.documentServiceLeaseStoreManager.LeaseContainer, this.documentServiceLeaseStoreManager.LeaseManager, partitionSuperviserFactory, synchronizer);

            partitionController = new HealthMonitoringPartitionControllerDecorator(partitionController, new TraceHealthMonitor());
            PartitionLoadBalancerCore partitionLoadBalancer = new PartitionLoadBalancerCore(
                partitionController,
                this.documentServiceLeaseStoreManager.LeaseContainer,
                loadBalancingStrategy,
                this.changeFeedLeaseOptions.LeaseAcquireInterval);
            return new PartitionManagerCore(bootstrapper, partitionController, partitionLoadBalancer);
        }
    }
}