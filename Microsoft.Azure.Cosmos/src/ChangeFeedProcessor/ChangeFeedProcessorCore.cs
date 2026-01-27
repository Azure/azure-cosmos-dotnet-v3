//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Bootstrapping;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal sealed class ChangeFeedProcessorCore : ChangeFeedProcessor
    {
        private readonly ChangeFeedObserverFactory observerFactory;
        private readonly SemaphoreSlim runningLock = new SemaphoreSlim(1, 1);
        private ContainerInternal leaseContainer;
        private string instanceName;
        private ContainerInternal monitoredContainer;
        private PartitionManager partitionManager;
        private ChangeFeedLeaseOptions changeFeedLeaseOptions;
        private ChangeFeedProcessorOptions changeFeedProcessorOptions;
        private DocumentServiceLeaseStoreManager documentServiceLeaseStoreManager;
        private bool initialized = false;
        private bool isRunning = false;

        public ChangeFeedProcessorCore(ChangeFeedObserverFactory observerFactory)
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
            await this.runningLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!this.initialized)
                {
                    await this.InitializeAsync().ConfigureAwait(false);
                }

                DefaultTrace.TraceInformation("Starting processor...");
                await this.partitionManager.StartAsync().ConfigureAwait(false);
                this.isRunning = true;
                DefaultTrace.TraceInformation("Processor started.");
            }
            finally
            {
                this.runningLock.Release();
            }
        }

        public override async Task StopAsync()
        {
            await this.runningLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!this.isRunning)
                {
                    DefaultTrace.TraceInformation("Processor is not running, nothing to stop.");
                    return;
                }

                DefaultTrace.TraceInformation("Stopping processor...");
                await this.partitionManager.StopAsync().ConfigureAwait(false);
                this.isRunning = false;
                DefaultTrace.TraceInformation("Processor stopped.");
            }
            finally
            {
                this.runningLock.Release();
            }
        }

        public override async Task<IReadOnlyList<LeaseExportData>> ExportLeasesAsync(CancellationToken cancellationToken = default)
        {
            // Wait for any ongoing start/stop operations to complete
            // If processor is running, this will wait until StopAsync is called by the user
            await this.runningLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try 
            {
                if (this.isRunning)
                {
                    // Release lock and throw - user must stop the processor first
                    throw new InvalidOperationException(
                        "Cannot export leases while the ChangeFeedProcessor is running. " +
                        "Please call StopAsync() before exporting leases.");
                }

                // Initialize if needed to access the lease container
                if (!this.initialized)
                {
                    await this.InitializeAsync().ConfigureAwait(false);
                }

                DefaultTrace.TraceInformation("Exporting leases...");
                IReadOnlyList<LeaseExportData> exportedLeases = await this.documentServiceLeaseStoreManager
                    .LeaseContainer
                    .ExportLeasesAsync(this.instanceName, cancellationToken)
                    .ConfigureAwait(false);

                DefaultTrace.TraceInformation("Exported {0} leases.", exportedLeases.Count);
                return exportedLeases;
            }
            finally
            {
                this.runningLock.Release();
            }
        }

        public override async Task ImportLeasesAsync(
            IReadOnlyList<LeaseExportData> leases,
            bool overwriteExisting = false,
            CancellationToken cancellationToken = default)
        {
            if (leases == null)
            {
                throw new ArgumentNullException(nameof(leases));
            }

            // Wait for any ongoing start/stop operations to complete
            await this.runningLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (this.isRunning)
                {
                    // Release lock and throw - user must stop the processor first
                    throw new InvalidOperationException(
                        "Cannot import leases while the ChangeFeedProcessor is running. " +
                        "Please call StopAsync() before importing leases.");
                }

                // Initialize if needed to access the lease container
                if (!this.initialized)
                {
                    await this.InitializeAsync().ConfigureAwait(false);
                }

                DefaultTrace.TraceInformation("Importing {0} leases (overwriteExisting={1})...", leases.Count, overwriteExisting);
                await this.documentServiceLeaseStoreManager
                    .LeaseContainer
                    .ImportLeasesAsync(leases, this.instanceName, overwriteExisting, cancellationToken)
                    .ConfigureAwait(false);

                DefaultTrace.TraceInformation("Imported {0} leases.", leases.Count);
            }
            finally
            {
                this.runningLock.Release();
            }
        }

        private async Task InitializeAsync()
        {
            string containerRid = await this.monitoredContainer.GetCachedRIDAsync(
                forceRefresh: false,
                NoOpTrace.Singleton,
                default);

            string monitoredDatabaseAndContainerRid = await this.monitoredContainer.GetMonitoredDatabaseAndContainerRidAsync();
            string leaseContainerPrefix = this.monitoredContainer.GetLeasePrefix(this.changeFeedLeaseOptions.LeasePrefix, monitoredDatabaseAndContainerRid);
            Routing.PartitionKeyRangeCache partitionKeyRangeCache = await this.monitoredContainer.ClientContext.DocumentClient.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);
            if (this.documentServiceLeaseStoreManager == null)
            {
                this.documentServiceLeaseStoreManager = await DocumentServiceLeaseStoreManagerBuilder
                    .InitializeAsync(
                        this.monitoredContainer,
                        this.leaseContainer,
                        leaseContainerPrefix,
                        this.instanceName,
                        changeFeedMode: this.changeFeedProcessorOptions.Mode)
                    .ConfigureAwait(false);
            }

            this.documentServiceLeaseStoreManager
                .LeaseManager
                .ChangeFeedModeSwitchingCheck(
                    documentServiceLeases: await this.documentServiceLeaseStoreManager
                        .LeaseContainer
                            .GetAllLeasesAsync()
                            .ConfigureAwait(false),
                    changeFeedLeaseOptionsMode: this.changeFeedLeaseOptions.Mode);

            this.partitionManager = this.BuildPartitionManager(
                containerRid,
                partitionKeyRangeCache);
            this.initialized = true;
        }

        private PartitionManager BuildPartitionManager(
            string containerRid,
            Routing.PartitionKeyRangeCache partitionKeyRangeCache)
        {
            PartitionSynchronizerCore synchronizer = new PartitionSynchronizerCore(
                this.monitoredContainer,
                this.documentServiceLeaseStoreManager.LeaseContainer,
                this.documentServiceLeaseStoreManager.LeaseManager,
                PartitionSynchronizerCore.DefaultDegreeOfParallelism,
                partitionKeyRangeCache,
                containerRid);
            BootstrapperCore bootstrapper = new BootstrapperCore(synchronizer, this.documentServiceLeaseStoreManager.LeaseStore, BootstrapperCore.DefaultLockTime, BootstrapperCore.DefaultSleepTime);
            PartitionSupervisorFactoryCore partitionSuperviserFactory = new PartitionSupervisorFactoryCore(
                this.observerFactory,
                this.documentServiceLeaseStoreManager.LeaseManager,
                new FeedProcessorFactoryCore(this.monitoredContainer, this.changeFeedProcessorOptions, this.documentServiceLeaseStoreManager.LeaseCheckpointer),
                this.changeFeedLeaseOptions);

            EqualPartitionsBalancingStrategy loadBalancingStrategy = new EqualPartitionsBalancingStrategy(
                    this.instanceName,
                    EqualPartitionsBalancingStrategy.DefaultMinLeaseCount,
                    EqualPartitionsBalancingStrategy.DefaultMaxLeaseCount,
                    this.changeFeedLeaseOptions.LeaseExpirationInterval);

            PartitionController partitionController = new PartitionControllerCore(
                this.documentServiceLeaseStoreManager.LeaseContainer, 
                this.documentServiceLeaseStoreManager.LeaseManager, 
                partitionSuperviserFactory, 
                synchronizer,
                this.changeFeedProcessorOptions.HealthMonitor);

            PartitionLoadBalancerCore partitionLoadBalancer = new PartitionLoadBalancerCore(
                partitionController,
                this.documentServiceLeaseStoreManager.LeaseContainer,
                loadBalancingStrategy,
                this.changeFeedLeaseOptions.LeaseAcquireInterval);
            return new PartitionManagerCore(bootstrapper, partitionController, partitionLoadBalancer);
        }
    }
}