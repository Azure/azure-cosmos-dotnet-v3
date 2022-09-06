//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using static Microsoft.Azure.Cosmos.Container;

    /// <summary>
    /// Obtains the <see cref="ChangeFeedEstimator"/> estimation as a periodic Task and notifies it to a <see cref="ChangesEstimationHandler"/>..
    /// </summary>
    internal sealed class FeedEstimatorRunner
    {
        private static readonly string EstimationLeaseIdentifier = "Change Feed Estimator";
        private static TimeSpan defaultMonitoringDelay = TimeSpan.FromSeconds(5);
        private readonly ChangeFeedEstimator remainingWorkEstimator;
        private readonly TimeSpan monitoringDelay;
        private readonly ChangesEstimationHandler dispatchEstimation;
        private readonly ChangeFeedProcessorHealthMonitor healthMonitor;

        public FeedEstimatorRunner(
            ChangesEstimationHandler dispatchEstimation,
            ChangeFeedEstimator remainingWorkEstimator,
            ChangeFeedProcessorHealthMonitor healthMonitor,
            TimeSpan? estimationPeriod = null)
        {
            this.dispatchEstimation = dispatchEstimation;
            this.remainingWorkEstimator = remainingWorkEstimator;
            this.healthMonitor = healthMonitor;
            this.monitoringDelay = estimationPeriod ?? FeedEstimatorRunner.defaultMonitoringDelay;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await this.EstimateAsync(cancellationToken);
                }
                catch (OperationCanceledException canceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }

                    Extensions.TraceException(new Exception("exception within estimator", canceledException));

                    // ignore as it is caused by client
                }

                await Task.Delay(this.monitoringDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task EstimateAsync(CancellationToken cancellationToken)
        {
            try
            {
                long estimation = await this.GetEstimatedRemainingWorkAsync(cancellationToken).ConfigureAwait(false);
                await this.dispatchEstimation(estimation, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception userException)
            {
                await this.healthMonitor.NotifyErrorAsync(FeedEstimatorRunner.EstimationLeaseIdentifier, userException);
            }
        }

        private async Task<long> GetEstimatedRemainingWorkAsync(CancellationToken cancellationToken)
        {
            using FeedIterator<ChangeFeedProcessorState> feedIterator = this.remainingWorkEstimator.GetCurrentStateIterator();
            FeedResponse<ChangeFeedProcessorState> estimations = await feedIterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            if (estimations.Count == 0)
            {
                return 1;
            }

            // Gets all results in first page
            return estimations.Sum(estimation => estimation.EstimatedLag);
        }
    }
}