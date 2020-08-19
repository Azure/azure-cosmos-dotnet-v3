//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using static Microsoft.Azure.Cosmos.Container;

    internal sealed class FeedEstimatorCore : FeedEstimator
    {
        private static TimeSpan defaultMonitoringDelay = TimeSpan.FromSeconds(5);
        private readonly ChangeFeedEstimator remainingWorkEstimator;
        private readonly TimeSpan monitoringDelay;
        private readonly ChangesEstimationHandler dispatchEstimation;
        private readonly Func<CancellationToken, Task> estimateAndDispatchAsync;

        public FeedEstimatorCore(
            ChangesEstimationHandler dispatchEstimation,
            ChangeFeedEstimator remainingWorkEstimator,
            TimeSpan? estimationPeriod = null)
        {
            this.dispatchEstimation = dispatchEstimation;
            this.estimateAndDispatchAsync = this.EstimateAsync;
            this.remainingWorkEstimator = remainingWorkEstimator;
            this.monitoringDelay = estimationPeriod.HasValue ? estimationPeriod.Value : FeedEstimatorCore.defaultMonitoringDelay;
        }

        public override async Task RunAsync(CancellationToken cancellationToken)
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
            long estimation = await this.remainingWorkEstimator.GetEstimatedRemainingWorkAsync(cancellationToken).ConfigureAwait(false);
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
    }
}