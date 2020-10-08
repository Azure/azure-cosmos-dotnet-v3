// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;

    /// <summary>
    /// Derived instance of <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading changes from this moment onward.
    /// </summary>
    internal sealed class ChangeFeedStartFromNow : ChangeFeedStartFrom
    {
        /// <summary>
        /// Intializes an instance of the <see cref="ChangeFeedStartFromNow"/> class.
        /// </summary>
        /// <param name="feedRange">The (optional) feed range to start from.</param>
        public ChangeFeedStartFromNow(FeedRangeInternal feedRange)
            : base()
        {
            this.FeedRange = feedRange ?? throw new ArgumentNullException(nameof(feedRange));
        }

        /// <summary>
        /// Gets the (optional) range to start from.
        /// </summary>
        public FeedRangeInternal FeedRange { get; }

        internal override void Accept(ChangeFeedStartFromVisitor visitor)
        {
            visitor.Visit(this);
        }

        internal override TResult Accept<TResult>(ChangeFeedStartFromVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }
    }
}
