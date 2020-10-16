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

    internal sealed class ChangeFeedProcessorCore : ChangeFeedProcessor
    {
        private readonly ChangeFeedObserverFactory observerFactory;
        private ContainerInternal leaseContainer;
        private string instanceName;
        private ContainerInternal monitoredContainer;
        private PartitionManager partitionManager;
        private ChangeFeedLeaseOptions changeFeedLeaseOptions;
        private ChangeFeedProcessorOptions changeFeedProcessorOptions;
        private DocumentServiceLeaseStoreManager documentServiceLeaseStoreManager;
        private bool initialized = false;

        public ChangeFeedProcessorCore(ChangeFeedObserverFactory observerFactory)
        {
            if (observerFactory == null)
            {
                throw new ArgumentNullException(nameof(observerFactory));
            }

            this.observerFactory = observerFactory;
        }

        public void ApplyBuildConfiguration(
            DocumentServiceLeaseStoreManager customDocumentServiceLeaseStoreManager,
            ContainerInternal leaseContainer,
            string instanceName,
            ChangeFeedLeaseOptions changeFeedLeaseOptions,
            ChangeFeedProcessorOptions changeFeedProcessorOptions,
            ContainerInternal monitoredContainer)
        {
            if (monitoredContainer == null)
            {
                throw new ArgumentNullException(nameof(monitoredContainer));
            }

            if (customDocumentServiceLeaseStoreManager == null && leaseContainer == null)
            {
                throw new ArgumentNullException(nameof(leaseContainer));
            }

            if (instanceName == null)
            {
                throw new ArgumentNullException("InstanceName is required for the processor to initialize.");
            }

            this.documentServiceLeaseStoreManager = customDocumentServiceLeaseStoreManager;
            this.leaseContainer = leaseContainer;
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
            string monitoredDatabaseAndContainerRid = await this.monitoredContainer.GetMonitoredDatabaseAndContainerRidAsync();
            string leaseContainerPrefix = this.monitoredContainer.GetLeasePrefix(this.changeFeedLeaseOptions.LeasePrefix, monitoredDatabaseAndContainerRid);
            if (this.documentServiceLeaseStoreManager == null)
            {
                this.documentServiceLeaseStoreManager = await DocumentServiceLeaseStoreManagerBuilder.InitializeAsync(this.leaseContainer, leaseContainerPrefix, this.instanceName).ConfigureAwait(false);
            }

            this.partitionManager = this.BuildPartitionManager();
            this.initialized = true;
        }

        private PartitionManager BuildPartitionManager()
        {
            CheckpointerObserverFactory factory = new CheckpointerObserverFactory(this.observerFactory, this.changeFeedProcessorOptions.CheckpointFrequency);

            PartitionSynchronizerCore synchronizer = new PartitionSynchronizerCore(
                this.monitoredContainer,
                this.documentServiceLeaseStoreManager.LeaseContainer,
                this.documentServiceLeaseStoreManager.LeaseManager,
                PartitionSynchronizerCore.DefaultDegreeOfParallelism,
                this.changeFeedProcessorOptions.QueryFeedMaxBatchSize);
            BootstrapperCore bootstrapper = new BootstrapperCore(synchronizer, this.documentServiceLeaseStoreManager.LeaseStore, BootstrapperCore.DefaultLockTime, BootstrapperCore.DefaultSleepTime);

            PartitionSupervisorFactoryCore partitionSuperviserFactory = new PartitionSupervisorFactoryCore(
                factory,
                this.documentServiceLeaseStoreManager.LeaseManager,
                new FeedProcessorFactoryCore(this.monitoredContainer, this.changeFeedProcessorOptions, this.documentServiceLeaseStoreManager.LeaseCheckpointer),
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