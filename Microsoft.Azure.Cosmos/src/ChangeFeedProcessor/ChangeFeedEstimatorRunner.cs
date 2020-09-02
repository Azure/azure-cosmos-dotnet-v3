//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using static Microsoft.Azure.Cosmos.Container;

    /// <summary>
    /// Implementation of the Estimator as a push model.
    /// </summary>
    internal sealed class ChangeFeedEstimatorRunner : ChangeFeedProcessor
    {
        private const string EstimatorDefaultHostName = "Estimator";

        private readonly ChangesEstimationHandler initialEstimateDelegate;
        private readonly TimeSpan? estimatorPeriod;
        private CancellationTokenSource shutdownCts;
        private ContainerInternal leaseContainer;
        private string monitoredContainerRid;
        private ContainerInternal monitoredContainer;
        private DocumentServiceLeaseStoreManager documentServiceLeaseStoreManager;
        private FeedEstimatorRunner feedEstimatorRunner;
        private ChangeFeedEstimator remainingWorkEstimator;
        private ChangeFeedLeaseOptions changeFeedLeaseOptions;
        private bool initialized = false;

        private Task runAsync;

        public ChangeFeedEstimatorRunner(
            ChangesEstimationHandler initialEstimateDelegate,
            TimeSpan? estimatorPeriod)
            : this(estimatorPeriod)
        {
            if (initialEstimateDelegate == null) throw new ArgumentNullException(nameof(initialEstimateDelegate));

            this.initialEstimateDelegate = initialEstimateDelegate;
        }

        /// <summary>
        /// Used for tests
        /// </summary>
        internal ChangeFeedEstimatorRunner(
            ChangesEstimationHandler initialEstimateDelegate,
            TimeSpan? estimatorPeriod,
            ChangeFeedEstimator remainingWorkEstimator)
            : this(initialEstimateDelegate, estimatorPeriod)
        {
            this.remainingWorkEstimator = remainingWorkEstimator;
        }

        private ChangeFeedEstimatorRunner(TimeSpan? estimatorPeriod)
        {
            if (estimatorPeriod.HasValue && estimatorPeriod.Value <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(estimatorPeriod));

            this.estimatorPeriod = estimatorPeriod;
        }

        public void ApplyBuildConfiguration(
            DocumentServiceLeaseStoreManager customDocumentServiceLeaseStoreManager,
            ContainerInternal leaseContainer,
            string monitoredContainerRid,
            string instanceName,
            ChangeFeedLeaseOptions changeFeedLeaseOptions,
            ChangeFeedProcessorOptions changeFeedProcessorOptions,
            ContainerInternal monitoredContainer)
        {
            if (monitoredContainer == null) throw new ArgumentNullException(nameof(monitoredContainer));
            if (leaseContainer == null && customDocumentServiceLeaseStoreManager == null) throw new ArgumentNullException(nameof(leaseContainer));

            this.documentServiceLeaseStoreManager = customDocumentServiceLeaseStoreManager;
            this.leaseContainer = leaseContainer;
            this.monitoredContainerRid = monitoredContainerRid;
            this.monitoredContainer = monitoredContainer;
            this.changeFeedLeaseOptions = changeFeedLeaseOptions;
        }

        public override async Task StartAsync()
        {
            if (!this.initialized)
            {
                await this.InitializeAsync().ConfigureAwait(false);
            }

            this.shutdownCts = new CancellationTokenSource();
            DefaultTrace.TraceInformation("Starting estimator...");
            this.runAsync = this.feedEstimatorRunner.RunAsync(this.shutdownCts.Token);
        }

        public override async Task StopAsync()
        {
            DefaultTrace.TraceInformation("Stopping estimator...");
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
            string monitoredContainerRid = await this.monitoredContainer.GetMonitoredContainerRidAsync(this.monitoredContainerRid);
            this.monitoredContainerRid = this.monitoredContainer.GetLeasePrefix(this.changeFeedLeaseOptions, monitoredContainerRid);
            this.documentServiceLeaseStoreManager = await ChangeFeedProcessorCore<dynamic>.InitializeLeaseStoreManagerAsync(this.documentServiceLeaseStoreManager, this.leaseContainer, this.monitoredContainerRid, ChangeFeedEstimatorRunner.EstimatorDefaultHostName).ConfigureAwait(false);
            this.feedEstimatorRunner = this.BuildFeedEstimatorRunner();
            this.initialized = true;
        }

        private FeedEstimatorRunner BuildFeedEstimatorRunner()
        {
            if (this.remainingWorkEstimator == null)
            {
                Func<string, string, bool, FeedIterator> feedCreator = (string partitionKeyRangeId, string continuationToken, bool startFromBeginning) =>
                {
                    return ResultSetIteratorUtils.BuildResultSetIterator(
                        partitionKeyRangeId: partitionKeyRangeId,
                        continuationToken: continuationToken,
                        maxItemCount: 1,
                        container: this.monitoredContainer,
                        startTime: null,
                        startFromBeginning: string.IsNullOrEmpty(continuationToken));
                };

                this.remainingWorkEstimator = new ChangeFeedEstimatorCore(
                   this.documentServiceLeaseStoreManager.LeaseContainer,
                   feedCreator);
            }

            return new FeedEstimatorRunner(this.initialEstimateDelegate, this.remainingWorkEstimator, this.estimatorPeriod);
        }
    }
}