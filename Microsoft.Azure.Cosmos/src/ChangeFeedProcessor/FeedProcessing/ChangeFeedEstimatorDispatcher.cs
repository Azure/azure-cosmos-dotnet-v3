// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal sealed class ChangeFeedEstimatorDispatcher
    {
        private readonly Func<long, CancellationToken, Task> dispatchEstimation;

        public TimeSpan? DispatchPeriod { get; private set; }

        public ChangeFeedEstimatorDispatcher(Func<long, CancellationToken, Task> dispatchEstimation, TimeSpan? estimationPeriod = null)
        {
            this.dispatchEstimation = dispatchEstimation;
            this.DispatchPeriod = estimationPeriod;
        }

        public async Task DispatchEstimation(long estimation, CancellationToken cancellation)
        {
            try
            {
                await this.dispatchEstimation(estimation, cancellation).ConfigureAwait(false);
            }
            catch (Exception userException)
            {
                DefaultTrace.TraceException(userException);
                DefaultTrace.TraceWarning("Exception happened on ChangeFeedEstimatorDispatcher.DispatchEstimation");
            }
        }
    }
}
