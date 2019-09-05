//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using static Microsoft.Azure.Cosmos.Container;

    internal sealed class ChangeFeedEstimatorDispatcher
    {
        private readonly ChangesEstimationHandler dispatchEstimation;

        public ChangeFeedEstimatorDispatcher(
            ChangesEstimationHandler dispatchEstimation,
            TimeSpan? estimationPeriod = null)
        {
            this.dispatchEstimation = dispatchEstimation;
            this.DispatchPeriod = estimationPeriod;
        }

        public TimeSpan? DispatchPeriod { get; private set; }

        public async Task DispatchEstimationAsync(
            long estimation,
            CancellationToken cancellationToken)
        {
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
