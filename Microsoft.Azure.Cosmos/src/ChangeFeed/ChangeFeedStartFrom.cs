// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed;

    /// <summary>
    /// Base class for where to start a ChangeFeed operation in <see cref="ChangeFeedRequestOptions"/>.
    /// </summary>
    /// <remarks>Use one of the static constructors to generate a StartFrom option.</remarks>
#if PREVIEW
    public
#else
    internal
#endif
        abstract class ChangeFeedStartFrom
    {
        /// <summary>
        /// Initializes an instance of the <see cref="ChangeFeedStartFrom"/> class.
        /// </summary>
        internal ChangeFeedStartFrom(FeedRange feedRange)
        {
            // Internal so people can't derive from this type.
            this.FeedRange = feedRange;
        }

        /// <summary>
        /// Gets the (optional) range to start from.
        /// </summary>
        internal FeedRange FeedRange { get; }

        internal abstract void Accept(ChangeFeedStartFromVisitor visitor);

        internal abstract TResult Accept<TResult>(ChangeFeedStartFromVisitor<TResult> visitor);

        internal abstract Task<TResult> AcceptAsync<TInput, TResult>(ChangeFeedStartFromAsyncVisitor<TInput, TResult> visitor, TInput input, CancellationToken cancellationToken);

        /// <summary>
        /// Creates a <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading changes from this moment onward.
        /// </summary>
        /// <returns>A <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading changes from this moment onward.</returns>
        public static ChangeFeedStartFrom Now()
        {
            return Now(FeedRangeEpk.FullRange);
        }

        /// <summary>
        /// Creates a <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading changes from this moment onward.
        /// </summary>
        /// <param name="feedRange">The range to start from.</param>
        /// <returns>A <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading changes from this moment onward.</returns>
        public static ChangeFeedStartFrom Now(FeedRange feedRange)
        {
            if (!(feedRange is FeedRangeInternal feedRangeInternal))
            {
                throw new ArgumentException($"{nameof(feedRange)} needs to be a {nameof(FeedRangeInternal)}.");
            }

            return new ChangeFeedStartFromNow(feedRangeInternal);
        }

        /// <summary>
        /// Creates a <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading changes from some point in time onward.
        /// </summary>
        /// <param name="dateTimeUtc">The time (in UTC) to start reading from.</param>
        /// <returns>A <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading changes from some point in time onward.</returns>
        public static ChangeFeedStartFrom Time(DateTime dateTimeUtc)
        {
            return Time(dateTimeUtc, FeedRangeEpk.FullRange);
        }

        /// <summary>
        /// Creates a <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading changes from some point in time onward.
        /// </summary>
        /// <param name="dateTimeUtc">The time to start reading from.</param>
        /// <param name="feedRange">The range to start from.</param>
        /// <returns>A <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading changes from some point in time onward.</returns>
        public static ChangeFeedStartFrom Time(DateTime dateTimeUtc, FeedRange feedRange)
        {
            if (!(feedRange is FeedRangeInternal feedRangeInternal))
            {
                throw new ArgumentException($"{nameof(feedRange)} needs to be a {nameof(FeedRangeInternal)}.");
            }

            return new ChangeFeedStartFromTime(dateTimeUtc, feedRangeInternal);
        }

        /// <summary>
        /// Creates a <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading changes from a save point.
        /// </summary>
        /// <param name="continuationToken">The continuation to resume from.</param>
        /// <returns>A <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading changes from a save point.</returns>
        public static ChangeFeedStartFrom ContinuationToken(string continuationToken)
        {
            return new ChangeFeedStartFromContinuation(continuationToken);
        }

        /// <summary>
        /// Creates a <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start from the beginning of time.
        /// </summary>
        /// <returns>A <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading changes from the beginning of time.</returns>
        public static ChangeFeedStartFrom Beginning()
        {
            return Beginning(FeedRangeEpk.FullRange);
        }

        /// <summary>
        /// Creates a <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start from the beginning of time.
        /// </summary>
        /// <param name="feedRange">The range to start from.</param>
        /// <returns>A <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading changes from the beginning of time.</returns>
        public static ChangeFeedStartFrom Beginning(FeedRange feedRange)
        {
            if (!(feedRange is FeedRangeInternal feedRangeInternal))
            {
                throw new ArgumentException($"{nameof(feedRange)} needs to be a {nameof(FeedRangeInternal)}.");
            }

            return new ChangeFeedStartFromBeginning(feedRangeInternal);
        }
    }
}
