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
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

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

            await this
                .ChangeFeedModeSwitchingCheckAsync(
                    key: monitoredDatabaseAndContainerRid)
                .ConfigureAwait(false);

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

            this.partitionManager = this.BuildPartitionManager(
                containerRid,
                partitionKeyRangeCache);
            this.initialized = true;
        }

        /// <summary>
        /// If the lease<see cref="Container"/>'s lease document is found, this method checks for lease 
        /// document's <see cref="ChangeFeedMode"/> and if the new <see cref="ChangeFeedMode"/> is different
        /// from the current <see cref="ChangeFeedMode"/>, a <see cref="CosmosException"/> is thrown.
        /// This is based on an issue located at <see href="https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4308"/>.
        /// </summary>
        /// <param name="key"></param>
        private async Task ChangeFeedModeSwitchingCheckAsync(string key)
        {
            FeedIterator<dynamic> feedIterator = this.leaseContainer.GetItemQueryIterator<dynamic>(queryText: "SELECT * FROM c");

            while (feedIterator.HasMoreResults)
            {
                FeedResponse<dynamic> feedResponses = await feedIterator
                    .ReadNextAsync()
                    .ConfigureAwait(false);

                bool shouldThrowException = false;
                string currentMode = default;
                string newMode = this.GetChangeFeedMode();

                foreach (dynamic response in feedResponses)
                {
                    // NOTE(philipthomas-MSFT): ChangeFeedMode is not set for older leases.
                    // Since Full-Fidelity Feed is not public at the time we are implementing
                    // this, all lease documents are Incremental Feeds by default. So if the
                    // new incoming request is for a Full-Fidelity Feed, then we know a switch
                    // is happening, and we want to throw the BadRequest CosmosException.
                    // This is based on an issue located at https://github.com/Azure/azure-cosmos-dotnet-v3/issues/4308.

                    if (response.Mode != null)
                    {
                        currentMode = response.Mode?.ToString();
                    }

                    if (currentMode == string.Empty)
                    {
                        if (this.changeFeedLeaseOptions.Mode != ChangeFeedMode.Incremental)
                        {
                            shouldThrowException = true;

                            break;
                        }
                    }

                    if (response.id.ToString().Contains(key) && currentMode != newMode)
                    {
                        shouldThrowException = true;

                        break;
                    }
                }

                if (shouldThrowException)
                {
                    CosmosException cosmosException = CosmosExceptionFactory.CreateBadRequestException(
                        message: $"Switching {nameof(ChangeFeedMode)} {currentMode} to {newMode} is not allowed.",
                        headers: default);

                    throw cosmosException;
                }
            }
        }

        private string GetChangeFeedMode()
        {
            return this.changeFeedLeaseOptions.Mode == ChangeFeedMode.AllVersionsAndDeletes
                ? HttpConstants.A_IMHeaderValues.FullFidelityFeed
                : HttpConstants.A_IMHeaderValues.IncrementalFeed;
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