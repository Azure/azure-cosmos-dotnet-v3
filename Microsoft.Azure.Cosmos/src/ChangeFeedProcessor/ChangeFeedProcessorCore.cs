//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Bootstrapping;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.Monitoring;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class ChangeFeedProcessorCore<T> : ChangeFeedProcessor
    {
        private readonly ChangeFeedObserverFactory<T> observerFactory;
        private ContainerInternal leaseContainer;
        private string instanceName;
        private ContainerInternal monitoredContainer;
        private PartitionManager partitionManager;
        private ChangeFeedLeaseOptions changeFeedLeaseOptions;
        private ChangeFeedProcessorOptions changeFeedProcessorOptions;
        private DocumentServiceLeaseStoreManager documentServiceLeaseStoreManager;
        private bool initialized = false;

        public ChangeFeedProcessorCore(ChangeFeedObserverFactory<T> observerFactory)
        {
            this.observerFactory = observerFactory ?? throw new ArgumentNullException(nameof(observerFactory));
        }

        public void ApplyBuildConfiguration(
            DocumentServiceLeaseStoreManager customDocumentServiceLeaseStoreManager,
            ContainerInternal leaseContainer,
            string instanceName,
            ChangeFeedLeaseOptions changeFeedLeaseOptions,
            ChangeFeedProcessorOptions changeFeedProcessorOptions,
            ContainerInternal monitoredContainer)
        {
            if (customDocumentServiceLeaseStoreManager == null && leaseContainer == null)
            {
                throw new ArgumentNullException(nameof(leaseContainer));
            }

            this.documentServiceLeaseStoreManager = customDocumentServiceLeaseStoreManager;
            this.leaseContainer = leaseContainer;
            this.instanceName = instanceName ?? throw new ArgumentNullException("InstanceName is required for the processor to initialize.");
            this.changeFeedProcessorOptions = changeFeedProcessorOptions;
            this.changeFeedLeaseOptions = changeFeedLeaseOptions;
            this.monitoredContainer = monitoredContainer ?? throw new ArgumentNullException(nameof(monitoredContainer));
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
            string containerRid = await this.monitoredContainer.GetCachedRIDAsync(
                forceRefresh: false, 
                NoOpTrace.Singleton, 
                default);
            string monitoredDatabaseAndContainerRid = await this.monitoredContainer.GetMonitoredDatabaseAndContainerRidAsync();
            string leaseContainerPrefix = this.monitoredContainer.GetLeasePrefix(this.changeFeedLeaseOptions.LeasePrefix, monitoredDatabaseAndContainerRid);
            Routing.PartitionKeyRangeCache partitionKeyRangeCache = await this.monitoredContainer.ClientContext.DocumentClient.GetPartitionKeyRangeCacheAsync();
            if (this.documentServiceLeaseStoreManager == null)
            {
                this.documentServiceLeaseStoreManager = await DocumentServiceLeaseStoreManagerBuilder.InitializeAsync(this.monitoredContainer, this.leaseContainer, leaseContainerPrefix, this.instanceName).ConfigureAwait(false);
            }

            this.partitionManager = this.BuildPartitionManager(
                containerRid, 
                partitionKeyRangeCache);
            this.initialized = true;
        }

        private PartitionManager BuildPartitionManager(
            string containerRid,
            Routing.PartitionKeyRangeCache partitionKeyRangeCache)
        {
            CheckpointerObserverFactory<T> factory = new CheckpointerObserverFactory<T>(this.observerFactory, this.changeFeedProcessorOptions.CheckpointFrequency);
            PartitionSynchronizerCore synchronizer = new PartitionSynchronizerCore(
                this.monitoredContainer,
                this.documentServiceLeaseStoreManager.LeaseContainer,
                this.documentServiceLeaseStoreManager.LeaseManager,
                PartitionSynchronizerCore.DefaultDegreeOfParallelism,
                partitionKeyRangeCache,
                containerRid);
            BootstrapperCore bootstrapper = new BootstrapperCore(synchronizer, this.documentServiceLeaseStoreManager.LeaseStore, BootstrapperCore.DefaultLockTime, BootstrapperCore.DefaultSleepTime);
            PartitionSupervisorFactoryCore<T> partitionSuperviserFactory = new PartitionSupervisorFactoryCore<T>(
                factory,
                this.documentServiceLeaseStoreManager.LeaseManager,
                new FeedProcessorFactoryCore<T>(this.monitoredContainer, this.changeFeedProcessorOptions, this.documentServiceLeaseStoreManager.LeaseCheckpointer, this.monitoredContainer.ClientContext.SerializerCore),
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