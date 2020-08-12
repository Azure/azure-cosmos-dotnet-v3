// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;

    /// <summary>
    /// Derived instance of <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading changes from some point in time onward.
    /// </summary>
    internal sealed class ChangeFeedStartFromTime : ChangeFeedStartFrom
    {
        /// <summary>
        /// Initializes an instance of the <see cref="ChangeFeedStartFromTime"/> class.
        /// </summary>
        /// <param name="time">The time to start reading from.</param>
        /// <param name="feedRange">The (optional) range to start from.</param>
        public ChangeFeedStartFromTime(DateTime time, FeedRangeInternal feedRange)
            : base()
        {
            if (time.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentOutOfRangeException($"{nameof(time)}.{nameof(DateTime.Kind)} must be {nameof(DateTimeKind)}.{nameof(DateTimeKind.Utc)}");
            }

            this.StartTime = time;
            this.FeedRange = feedRange ?? throw new ArgumentNullException(nameof(feedRange));
        }

        /// <summary>
        /// Gets the time the ChangeFeed operation should start reading from.
        /// </summary>
        public DateTime StartTime { get; }

        /// <summary>
        /// Gets the (optional) range to start from.
        /// </summary>
        public FeedRangeInternal FeedRange { get; }

        internal override void Accept(ChangeFeedStartFromVisitor visitor) => visitor.Visit(this);

        internal override TResult Accept<TResult>(ChangeFeedStartFromVisitor<TResult> visitor) => visitor.Visit(this);
    }
}
