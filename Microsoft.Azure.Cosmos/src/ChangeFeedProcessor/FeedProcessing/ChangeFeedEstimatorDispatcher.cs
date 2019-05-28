//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal sealed class ChangeFeedEstimatorDispatcher
    {
        private readonly Func<IReadOnlyList<RemainingLeaseTokenWork>, CancellationToken, Task> dispatchEstimation;

        public ChangeFeedEstimatorDispatcher(
            Func<IReadOnlyList<RemainingLeaseTokenWork>, CancellationToken, Task> dispatchEstimation, 
            TimeSpan? estimationPeriod = null)
        {
            this.dispatchEstimation = dispatchEstimation;
            this.DispatchPeriod = estimationPeriod;
        }

        public TimeSpan? DispatchPeriod { get; private set; }

        public async Task DispatchEstimationAsync(
            IReadOnlyList<RemainingLeaseTokenWork> estimation, 
            CancellationToken cancellationToken)
        {
            try
            {
                await this.dispatchEstimation(estimation, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception userException)
            {
                DefaultTrace.TraceException(userException);
                DefaultTrace.TraceWarning("Exception happened on ChangeFeedEstimatorDispatcher.DispatchEstimation");
            }
        }
    }
}
