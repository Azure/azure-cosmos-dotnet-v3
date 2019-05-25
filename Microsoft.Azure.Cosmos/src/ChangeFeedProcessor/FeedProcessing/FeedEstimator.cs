//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
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