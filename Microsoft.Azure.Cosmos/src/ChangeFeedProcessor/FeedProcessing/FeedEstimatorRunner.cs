//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using static Microsoft.Azure.Cosmos.Container;

    internal sealed class FeedEstimatorRunner
    {
        private static TimeSpan defaultMonitoringDelay = TimeSpan.FromSeconds(5);
        private readonly ChangeFeedEstimator remainingWorkEstimator;
        private readonly TimeSpan monitoringDelay;
        private readonly ChangesEstimationHandler dispatchEstimation;
        private readonly Func<CancellationToken, Task> estimateAndDispatchAsync;

        public FeedEstimatorRunner(
            ChangesEstimationHandler dispatchEstimation,
            ChangeFeedEstimator remainingWorkEstimator,
            TimeSpan? estimationPeriod = null)
        {
            this.dispatchEstimation = dispatchEstimation;
            this.estimateAndDispatchAsync = this.EstimateAsync;
            this.remainingWorkEstimator = remainingWorkEstimator;
            this.monitoringDelay = estimationPeriod.HasValue ? estimationPeriod.Value : FeedEstimatorRunner.defaultMonitoringDelay;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await this.estimateAndDispatchAsync(cancellationToken);
                }
                catch (TaskCanceledException canceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                        throw;

                    Extensions.TraceException(new Exception("exception within estimator", canceledException));

                    // ignore as it is caused by client
                }

                await Task.Delay(this.monitoringDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task EstimateAsync(CancellationToken cancellationToken)
        {
            long estimation = await this.GetEstimatedRemainingWorkAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await this.dispatchEstimation(estimation, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception userException)
            {
                Extensions.TraceException(userException);
                DefaultTrace.TraceWarning("Exception happened on ChangeFeedEstimatorDispatcher.DispatchEstimation");
            }
        }

        private async Task<long> GetEstimatedRemainingWorkAsync(CancellationToken cancellationToken)
        {
            using FeedIterator<RemainingLeaseWork> feedIterator = this.remainingWorkEstimator.GetRemainingLeaseWorkIterator();
            FeedResponse<RemainingLeaseWork> estimations = await feedIterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            if (estimations.Count == 0)
            {
                return 1;
            }

            // Gets all results in first page
            return estimations.Sum(estimation => estimation.RemainingWork);
        }
    }
}