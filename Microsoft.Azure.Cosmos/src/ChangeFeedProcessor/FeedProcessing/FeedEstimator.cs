//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if AZURECORE
namespace Azure.Cosmos.ChangeFeed
#else
namespace Microsoft.Azure.Cosmos.ChangeFeed
#endif
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Process that frequently checks the estimated state of the processor and dispatches a calculation to a <see cref="ChangeFeedEstimatorDispatcher"/>.
    /// </summary>
    internal abstract class FeedEstimator
    {
        public abstract Task RunAsync(CancellationToken cancellationToken);
    }
}