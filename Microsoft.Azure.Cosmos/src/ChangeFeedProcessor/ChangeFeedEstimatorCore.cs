//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using static Microsoft.Azure.Cosmos.Container;

    internal sealed class ChangeFeedEstimatorCore : ChangeFeedProcessor
    {
        private const string EstimatorDefaultHostName = "Estimator";

        private readonly ChangesEstimationHandler initialEstimateDelegate;
        private CancellationTokenSource shutdownCts;
        private ContainerInternal leaseContainer;
        private string monitoredContainerRid;
        private TimeSpan? estimatorPeriod = null;
        private ContainerInternal monitoredContainer;
        private DocumentServiceLeaseStoreManager documentServiceLeaseStoreManager;
        private FeedEstimator feedEstimator;
        private RemainingWorkEstimator remainingWorkEstimator;
        private ChangeFeedLeaseOptions changeFeedLeaseOptions;
        private bool initialized = false;

        private Task runAsync;

        public ChangeFeedEstimatorCore(
            ChangesEstimationHandler initialEstimateDelegate,
            TimeSpan? estimatorPeriod)
        {
            if (estimatorPeriod.HasValue && estimatorPeriod.Value <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(estimatorPeriod));

            this.initialEstimateDelegate = initialEstimateDelegate ?? throw new ArgumentNullException(nameof(initialEstimateDelegate));
            this.estimatorPeriod = estimatorPeriod;
        }

        internal ChangeFeedEstimatorCore(
            ChangesEstimationHandler initialEstimateDelegate,
            TimeSpan? estimatorPeriod,
            RemainingWorkEstimator remainingWorkEstimator)
            : this(initialEstimateDelegate, estimatorPeriod)
        {
            this.remainingWorkEstimator = remainingWorkEstimator;
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
            if (leaseContainer == null && customDocumentServiceLeaseStoreManager == null) throw new ArgumentNullException(nameof(leaseContainer));

            this.documentServiceLeaseStoreManager = customDocumentServiceLeaseStoreManager;
            this.leaseContainer = leaseContainer;
            this.monitoredContainerRid = monitoredContainerRid;
            this.monitoredContainer = monitoredContainer ?? throw new ArgumentNullException(nameof(monitoredContainer));
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
            this.runAsync = this.feedEstimator.RunAsync(this.shutdownCts.Token);
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
            this.documentServiceLeaseStoreManager = await ChangeFeedProcessorCore<dynamic>.InitializeLeaseStoreManagerAsync(this.documentServiceLeaseStoreManager, this.leaseContainer, this.monitoredContainerRid, ChangeFeedEstimatorCore.EstimatorDefaultHostName).ConfigureAwait(false);
            this.feedEstimator = this.BuildFeedEstimator();
            this.initialized = true;
        }

        private FeedEstimator BuildFeedEstimator()
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

                this.remainingWorkEstimator = new RemainingWorkEstimatorCore(
                   this.documentServiceLeaseStoreManager.LeaseContainer,
                   feedCreator,
                   this.monitoredContainer.ClientContext.Client.ClientOptions?.GatewayModeMaxConnectionLimit ?? 1);
            }

            ChangeFeedEstimatorDispatcher estimatorDispatcher = new ChangeFeedEstimatorDispatcher(this.initialEstimateDelegate, this.estimatorPeriod);

            return new FeedEstimatorCore(estimatorDispatcher, this.remainingWorkEstimator);
        }
    }
}