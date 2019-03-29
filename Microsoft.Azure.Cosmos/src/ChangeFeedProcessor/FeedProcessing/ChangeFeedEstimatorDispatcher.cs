// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.FeedProcessing
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Logging;

    internal sealed class ChangeFeedEstimatorDispatcher<T>
    {
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();
        Func<long, CancellationToken, Task> dispatchEstimation;

        public ChangeFeedEstimatorDispatcher(Func<long, CancellationToken, Task> dispatchEstimation)
        {
            this.dispatchEstimation = dispatchEstimation;
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
