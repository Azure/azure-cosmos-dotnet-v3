//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Logging;
    using System.Threading;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.FeedProcessing;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.FeedManagement;

    internal sealed class ChangeFeedEstimatorCore : ChangeFeedProcessor
    {
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();
        private readonly CancellationTokenSource shutdownCts = new CancellationTokenSource();
        private readonly CosmosContainer leaseContainer;
        private readonly string leaseContainerPrefix;
        private readonly string instanceName;
        private readonly Func<long, CancellationToken, Task> initialEstimateDelegate;
        private readonly TimeSpan? estimatorPeriod = null;
        private readonly CosmosContainer monitoredContainer;
        private DocumentServiceLeaseStoreManager documentServiceLeaseStoreManager;
        private FeedEstimator feedEstimator;
        private bool initialized = false;

        private Task runAsync;

        public ChangeFeedEstimatorCore(
            DocumentServiceLeaseStoreManager customDocumentServiceLeaseStoreManager,
            CosmosContainer leaseContainer,
            string leaseContainerPrefix,
            string instanceName,
            Func<long, CancellationToken, Task> initialEstimateDelegate,
            TimeSpan? estimatorPeriod,
            CosmosContainer monitoredContainer)
        {
            if (monitoredContainer == null) throw new ArgumentNullException(nameof(monitoredContainer));
            if (leaseContainer == null) throw new ArgumentNullException(nameof(leaseContainer));
            if (instanceName == null) throw new ArgumentNullException(nameof(instanceName));
            if (initialEstimateDelegate == null) throw new ArgumentNullException(nameof(initialEstimateDelegate));

            this.documentServiceLeaseStoreManager = customDocumentServiceLeaseStoreManager;
            this.initialEstimateDelegate = initialEstimateDelegate;
            this.estimatorPeriod = estimatorPeriod;
            this.leaseContainer = leaseContainer;
            this.leaseContainerPrefix = leaseContainerPrefix;
            this.instanceName = instanceName;
            this.monitoredContainer = monitoredContainer;
        }

        public override async Task StartAsync()
        {
            if (!this.initialized)
            {
                await this.InitializeAsync().ConfigureAwait(false);
            }

            Logger.InfoFormat("Starting estimator...");
            this.runAsync = this.feedEstimator.RunAsync(this.shutdownCts.Token);
        }

        public override async Task StopAsync()
        {
            Logger.InfoFormat("Stopping estimator...");
            this.shutdownCts.Cancel();
            try
            {
                await this.runAsync.ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // Expected during shutdown
            }
        }

        private async Task InitializeAsync()
        {
            this.documentServiceLeaseStoreManager = await ChangeFeedProcessorCore<dynamic>.InitializeLeaseStoreManagerAsync(this.documentServiceLeaseStoreManager, this.leaseContainer, this.leaseContainerPrefix, this.instanceName).ConfigureAwait(false);
            this.feedEstimator = this.BuildFeedEstimator();
            this.initialized = true;
        }

        private FeedEstimator BuildFeedEstimator()
        {
            RemainingWorkEstimatorCore remainingWorkEstimator = new RemainingWorkEstimatorCore(
               this.documentServiceLeaseStoreManager.LeaseContainer,
               this.monitoredContainer,
               this.monitoredContainer.Client.Configuration?.MaxConnectionLimit ?? 1);

            ChangeFeedEstimatorDispatcher estimatorDispatcher = new ChangeFeedEstimatorDispatcher(this.initialEstimateDelegate, this.estimatorPeriod);

            return new FeedEstimatorCore(estimatorDispatcher, remainingWorkEstimator);
        }
    }
}