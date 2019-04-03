// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Logging;

    internal sealed class ChangeFeedEstimatorDispatcher
    {
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();
        private readonly Func<long, CancellationToken, Task> dispatchEstimation;

        public TimeSpan? DispatchPeriod { get; private set; }

        public ChangeFeedEstimatorDispatcher(Func<long, CancellationToken, Task> dispatchEstimation, TimeSpan? estimationPeriod = null)
        {
            this.dispatchEstimation = dispatchEstimation;
            this.DispatchPeriod = estimationPeriod;
        }

        public async Task DispatchEstimation(long estimation, CancellationToken cancellationToken)
        {
            try
            {
                await this.dispatchEstimation(estimation, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception userException)
            {
                Logger.WarnException("Exception happened on ChangeFeedEstimatorDispatcher.DispatchEstimation", userException);
            }
        }
    }
}
