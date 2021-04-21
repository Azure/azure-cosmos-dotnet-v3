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
        private ContainerInternal monitoredContainer;
        private FeedEstimatorRunner feedEstimatorRunner;
        private ChangeFeedEstimator remainingWorkEstimator;
        private ChangeFeedLeaseOptions changeFeedLeaseOptions;
        private DocumentServiceLeaseContainer documentServiceLeaseContainer;
        private bool initialized = false;

        private Task runAsync;

        public ChangeFeedEstimatorRunner(
            ChangesEstimationHandler initialEstimateDelegate,
            TimeSpan? estimatorPeriod)
            : this(estimatorPeriod)
        {
            this.initialEstimateDelegate = initialEstimateDelegate ?? throw new ArgumentNullException(nameof(initialEstimateDelegate));
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
            string instanceName,
            ChangeFeedLeaseOptions changeFeedLeaseOptions,
            ChangeFeedProcessorOptions changeFeedProcessorOptions,
            ContainerInternal monitoredContainer)
        {
            if (leaseContainer == null && customDocumentServiceLeaseStoreManager == null) throw new ArgumentNullException(nameof(leaseContainer));

            this.leaseContainer = leaseContainer;
            this.monitoredContainer = monitoredContainer ?? throw new ArgumentNullException(nameof(monitoredContainer));
            this.changeFeedLeaseOptions = changeFeedLeaseOptions;
            this.documentServiceLeaseContainer = customDocumentServiceLeaseStoreManager?.LeaseContainer;
        }

        public override async Task StartAsync()
        {
            if (!this.initialized)
            {
                await this.InitializeLeaseStoreAsync();
                this.feedEstimatorRunner = this.BuildFeedEstimatorRunner();
                this.initialized = true;
            }

            this.shutdownCts = new CancellationTokenSource();
            DefaultTrace.TraceInformation("Starting estimator...");
            this.runAsync = this.feedEstimatorRunner.RunAsync(this.shutdownCts.Token);
        }

        public override async Task StopAsync()
        {
            DefaultTrace.TraceInformation("Stopping estimator...");
            if (this.initialized)
            {
                this.shutdownCts.Cancel();
                try
                {
                    await this.runAsync.ConfigureAwait(false);
                }
                catch (TaskCanceledException ex)
                {
                    // Expected during shutdown
                    Cosmos.Extensions.TraceException(ex);
                }
                catch (OperationCanceledException ex)
                {
                    // Expected during shutdown
                    Cosmos.Extensions.TraceException(ex);
                }
            }
        }

        private FeedEstimatorRunner BuildFeedEstimatorRunner()
        {
            if (this.remainingWorkEstimator == null)
            {
                this.remainingWorkEstimator = new ChangeFeedEstimatorCore(
                   this.changeFeedLeaseOptions.LeasePrefix,
                   this.monitoredContainer,
                   this.leaseContainer,
                   this.documentServiceLeaseContainer);
            }

            return new FeedEstimatorRunner(this.initialEstimateDelegate, this.remainingWorkEstimator, this.estimatorPeriod);
        }

        private async Task InitializeLeaseStoreAsync()
        {
            if (this.documentServiceLeaseContainer == null)
            {
                string monitoredContainerAndDatabaseRid = await this.monitoredContainer.GetMonitoredDatabaseAndContainerRidAsync(default);
                string leasePrefix = this.monitoredContainer.GetLeasePrefix(this.changeFeedLeaseOptions.LeasePrefix, monitoredContainerAndDatabaseRid);
                DocumentServiceLeaseStoreManager documentServiceLeaseStoreManager = await DocumentServiceLeaseStoreManagerBuilder.InitializeAsync(
                    monitoredContainer: this.monitoredContainer,
                    leaseContainer: this.leaseContainer,
                    leaseContainerPrefix: leasePrefix,
                    instanceName: ChangeFeedEstimatorRunner.EstimatorDefaultHostName);

                this.documentServiceLeaseContainer = documentServiceLeaseStoreManager.LeaseContainer;
            }
        }
    }
}