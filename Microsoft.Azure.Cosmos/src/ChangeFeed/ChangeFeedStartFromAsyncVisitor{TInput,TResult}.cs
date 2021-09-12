// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System.Threading;
    using System.Threading.Tasks;

    internal abstract class ChangeFeedStartFromAsyncVisitor<TInput, TResult>
    {
        public abstract Task<TResult> VisitAsync(
            ChangeFeedStartFromNow startFromNow, 
            TInput input, 
            CancellationToken cancellationToken);
        public abstract Task<TResult> VisitAsync(
            ChangeFeedStartFromTime startFromTime,
            TInput input, 
            CancellationToken cancellationToken);
        public abstract Task<TResult> VisitAsync(
            ChangeFeedStartFromContinuation startFromContinuation, 
            TInput input, 
            CancellationToken cancellationToken);
        public abstract Task<TResult> VisitAsync(
            ChangeFeedStartFromBeginning startFromBeginning,
            TInput input, 
            CancellationToken cancellationToken);
        public abstract Task<TResult> VisitAsync(
            ChangeFeedStartFromContinuationAndFeedRange startFromContinuationAndFeedRange,
            TInput input, 
            CancellationToken cancellationToken);
    }
}
