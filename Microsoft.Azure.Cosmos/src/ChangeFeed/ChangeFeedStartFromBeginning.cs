// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;

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
            : base()
        {
            this.FeedRange = feedRange ?? throw new ArgumentNullException(nameof(feedRange));
        }

        /// <summary>
        /// Gets the (optional) range to start from.
        /// </summary>
        public FeedRangeInternal FeedRange { get; }

        internal override void Accept(ChangeFeedStartFromVisitor visitor) => visitor.Visit(this);

        internal override TResult Accept<TResult>(ChangeFeedStartFromVisitor<TResult> visitor) => visitor.Visit(this);
    }
}
