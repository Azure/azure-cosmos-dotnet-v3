//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if AZURECORE
namespace Azure.Cosmos.ChangeFeed
#else
namespace Microsoft.Azure.Cosmos.ChangeFeed
#endif
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Core.Trace;
#if AZURECORE
    using static Azure.Cosmos.CosmosContainer;
#else
    using static Microsoft.Azure.Cosmos.Container;
#endif

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
