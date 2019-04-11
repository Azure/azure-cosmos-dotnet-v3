//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.Logging;

    internal sealed class ChangeFeedEstimatorCore : ChangeFeedProcessor
    {
        private const string EstimatorDefaultHostName = "Estimator";

        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();
        private readonly Func<long, CancellationToken, Task> initialEstimateDelegate;
        private CancellationTokenSource shutdownCts = new CancellationTokenSource();
        private CosmosContainer leaseContainer;
        private string leaseContainerPrefix;
        private TimeSpan? estimatorPeriod = null;
        private CosmosContainer monitoredContainer;
        private DocumentServiceLeaseStoreManager documentServiceLeaseStoreManager;
        private FeedEstimator feedEstimator;
        private RemainingWorkEstimator remainingWorkEstimator;
        private bool initialized = false;

        private Task runAsync;

        public ChangeFeedEstimatorCore(
            Func<long, CancellationToken, Task> initialEstimateDelegate, 
            TimeSpan? estimatorPeriod)
        {
            if (initialEstimateDelegate == null) throw new ArgumentNullException(nameof(initialEstimateDelegate));
            if (estimatorPeriod.HasValue && estimatorPeriod.Value <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(estimatorPeriod));

            this.initialEstimateDelegate = initialEstimateDelegate;
            this.estimatorPeriod = estimatorPeriod;
        }

        internal ChangeFeedEstimatorCore(
            Func<long, CancellationToken, Task> initialEstimateDelegate,
            TimeSpan? estimatorPeriod,
            RemainingWorkEstimator remainingWorkEstimator): this(initialEstimateDelegate, estimatorPeriod)
        {
            this.remainingWorkEstimator = remainingWorkEstimator;
        }

        public void ApplyBuildConfiguration(
            DocumentServiceLeaseStoreManager customDocumentServiceLeaseStoreManager,
            CosmosContainer leaseContainer,
            string leaseContainerPrefix,
            string instanceName,
            ChangeFeedLeaseOptions changeFeedLeaseOptions,
            ChangeFeedProcessorOptions changeFeedProcessorOptions,
            CosmosContainer monitoredContainer)
        {
            if (monitoredContainer == null) throw new ArgumentNullException(nameof(monitoredContainer));
            if (leaseContainer == null && customDocumentServiceLeaseStoreManager == null) throw new ArgumentNullException(nameof(leaseContainer));

            this.documentServiceLeaseStoreManager = customDocumentServiceLeaseStoreManager;
            this.leaseContainer = leaseContainer;
            this.leaseContainerPrefix = leaseContainerPrefix;
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
            this.documentServiceLeaseStoreManager = await ChangeFeedProcessorCore<dynamic>.InitializeLeaseStoreManagerAsync(this.documentServiceLeaseStoreManager, this.leaseContainer, this.leaseContainerPrefix, ChangeFeedEstimatorCore.EstimatorDefaultHostName).ConfigureAwait(false);
            this.feedEstimator = this.BuildFeedEstimator();
            this.initialized = true;
        }

        private FeedEstimator BuildFeedEstimator()
        {
            if (this.remainingWorkEstimator == null)
            {
                this.remainingWorkEstimator = new RemainingWorkEstimatorCore(
                   this.documentServiceLeaseStoreManager.LeaseContainer,
                   this.monitoredContainer,
                   this.monitoredContainer.Client.Configuration?.MaxConnectionLimit ?? 1);
            }

            ChangeFeedEstimatorDispatcher estimatorDispatcher = new ChangeFeedEstimatorDispatcher(this.initialEstimateDelegate, this.estimatorPeriod);

            return new FeedEstimatorCore(estimatorDispatcher, this.remainingWorkEstimator);
        }
    }
}