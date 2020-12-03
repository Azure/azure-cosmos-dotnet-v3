// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Derived instance of <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading changes from the beginning of time.
    /// </summary>
    internal sealed class ChangeFeedStartFromBeginning : ChangeFeedStartFrom
    {
        /// <summary>
        /// Initializes an instance of the <see cref="ChangeFeedStartFromBeginning"/> class.
        /// </summary>
        /// <param name="feedRange">The (optional) range to start from.</param>
        public ChangeFeedStartFromBeginning(FeedRangeInternal feedRange)
            : base(feedRange)
        {
        }

        internal override void Accept(ChangeFeedStartFromVisitor visitor)
        {
            visitor.Visit(this);
        }

        internal override TResult Accept<TResult>(ChangeFeedStartFromVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        internal override Task<TOutput> AcceptAsync<TInput, TOutput>(
            ChangeFeedStartFromAsyncVisitor<TInput, TOutput> visitor,
            TInput input,
            CancellationToken cancellationToken)
        {
            return visitor.VisitAsync(this, input, cancellationToken);
        }
    }
}
