// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Documents;

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
        internal protected ChangeFeedStartFrom()
        {
            // Internal so people can't derive from this type.
        }

        internal abstract void Accept(StartFromVisitor visitor);

        internal abstract TResult Accept<TResult>(StartFromVisitor<TResult> visitor);

        /// <summary>
        /// Creates a <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading changes from this moment onward.
        /// </summary>
        /// <returns>A <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading changes from this moment onward.</returns>
        public static ChangeFeedStartFrom Now() => Now(FeedRangeEpk.FullRange);

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
        /// <param name="dateTime">The time to start reading from.</param>
        /// <returns>A <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading changes from some point in time onward.</returns>
        public static ChangeFeedStartFrom Time(DateTime dateTime) => Time(dateTime, FeedRangeEpk.FullRange);

        /// <summary>
        /// Creates a <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading changes from some point in time onward.
        /// </summary>
        /// <param name="dateTime">The time to start reading from.</param>
        /// <param name="feedRange">The range to start from.</param>
        /// <returns>A <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading changes from some point in time onward.</returns>
        public static ChangeFeedStartFrom Time(DateTime dateTime, FeedRange feedRange)
        {
            if (!(feedRange is FeedRangeInternal feedRangeInternal))
            {
                throw new ArgumentException($"{nameof(feedRange)} needs to be a {nameof(FeedRangeInternal)}.");
            }

            return new ChangeFeedStartFromTime(dateTime, feedRangeInternal);
        }

        /// <summary>
        /// Creates a <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading changes from a save point.
        /// </summary>
        /// <param name="continuation">The continuation to resume from.</param>
        /// <returns>A <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading changes from a save point.</returns>
        public static ChangeFeedStartFrom ContinuationToken(string continuation) => new ChangeFeedStartFromContinuation(continuation);

        /// <summary>
        /// Creates a <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start from the beginning of time.
        /// </summary>
        /// <returns>A <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading changes from the beginning of time.</returns>
        public static ChangeFeedStartFrom Beginning() => Beginning(FeedRangeEpk.FullRange);

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

    internal abstract class StartFromVisitor
    {
        public abstract void Visit(ChangeFeedStartFromNow startFromNow);
        public abstract void Visit(ChangeFeedStartFromTime startFromTime);
        public abstract void Visit(ChangeFeedStartFromContinuation startFromContinuation);
        public abstract void Visit(ChangeFeedStartFromBeginning startFromBeginning);
        public abstract void Visit(ChangeFeedStartFromContinuationAndFeedRange startFromContinuationAndFeedRange);
    }

    internal abstract class StartFromVisitor<TResult>
    {
        public abstract TResult Visit(ChangeFeedStartFromNow startFromNow);
        public abstract TResult Visit(ChangeFeedStartFromTime startFromTime);
        public abstract TResult Visit(ChangeFeedStartFromContinuation startFromContinuation);
        public abstract TResult Visit(ChangeFeedStartFromBeginning startFromBeginning);
        public abstract TResult Visit(ChangeFeedStartFromContinuationAndFeedRange startFromContinuationAndFeedRange);
    }

    internal sealed class PopulateStartFromRequestOptionVisitor : StartFromVisitor
    {
        private const string IfNoneMatchAllHeaderValue = "*";
        private static readonly DateTime StartFromBeginningTime = DateTime.MinValue.ToUniversalTime();

        private readonly RequestMessage requestMessage;
        private readonly FeedRangeRequestMessagePopulatorVisitor feedRangeVisitor;

        public PopulateStartFromRequestOptionVisitor(RequestMessage requestMessage)
        {
            this.requestMessage = requestMessage ?? throw new ArgumentNullException(nameof(requestMessage));
            this.feedRangeVisitor = new FeedRangeRequestMessagePopulatorVisitor(requestMessage);
        }

        public override void Visit(ChangeFeedStartFromNow startFromNow)
        {
            this.requestMessage.Headers.IfNoneMatch = PopulateStartFromRequestOptionVisitor.IfNoneMatchAllHeaderValue;

            if (startFromNow.FeedRange != null)
            {
                startFromNow.FeedRange.Accept(this.feedRangeVisitor);
            }
        }

        public override void Visit(ChangeFeedStartFromTime startFromTime)
        {
            // Our current public contract for ChangeFeedProcessor uses DateTime.MinValue.ToUniversalTime as beginning.
            // We need to add a special case here, otherwise it would send it as normal StartTime.
            // The problem is Multi master accounts do not support StartTime header on ReadFeed, and thus,
            // it would break multi master Change Feed Processor users using Start From Beginning semantics.
            // It's also an optimization, since the backend won't have to binary search for the value.
            if (startFromTime.StartTime != PopulateStartFromRequestOptionVisitor.StartFromBeginningTime)
            {
                this.requestMessage.Headers.Add(
                    HttpConstants.HttpHeaders.IfModifiedSince,
                    startFromTime.StartTime.ToString("r", CultureInfo.InvariantCulture));
            }

            startFromTime.FeedRange.Accept(this.feedRangeVisitor);
        }

        public override void Visit(ChangeFeedStartFromContinuation startFromContinuation)
        {
            // On REST level, change feed is using IfNoneMatch/ETag instead of continuation
            this.requestMessage.Headers.IfNoneMatch = startFromContinuation.Continuation;
        }

        public override void Visit(ChangeFeedStartFromBeginning startFromBeginning)
        {
            // We don't need to set any headers to start from the beginning

            // Except for the feed range.
            startFromBeginning.FeedRange.Accept(this.feedRangeVisitor);
        }

        public override void Visit(ChangeFeedStartFromContinuationAndFeedRange startFromContinuationAndFeedRange)
        {
            // On REST level, change feed is using IfNoneMatch/ETag instead of continuation
            this.requestMessage.Headers.IfNoneMatch = startFromContinuationAndFeedRange.Etag;

            startFromContinuationAndFeedRange.FeedRange.Accept(this.feedRangeVisitor);
        }
    }

    internal sealed class FeedRangeExtractor : StartFromVisitor<FeedRange>
    {
        public static readonly FeedRangeExtractor Singleton = new FeedRangeExtractor();

        private FeedRangeExtractor()
        {
        }

        public override FeedRange Visit(ChangeFeedStartFromNow startFromNow) => startFromNow.FeedRange;

        public override FeedRange Visit(ChangeFeedStartFromTime startFromTime) => startFromTime.FeedRange;

        public override FeedRange Visit(ChangeFeedStartFromContinuation startFromContinuation)
            => throw new NotSupportedException($"{nameof(ChangeFeedStartFromContinuation)} does not have a feed range.");

        public override FeedRange Visit(ChangeFeedStartFromBeginning startFromBeginning) => startFromBeginning.FeedRange;

        public override FeedRange Visit(ChangeFeedStartFromContinuationAndFeedRange startFromContinuationAndFeedRange) => startFromContinuationAndFeedRange.FeedRange;
    }

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

        internal override void Accept(StartFromVisitor visitor) => visitor.Visit(this);

        internal override TResult Accept<TResult>(StartFromVisitor<TResult> visitor) => visitor.Visit(this);
    }

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

        internal override void Accept(StartFromVisitor visitor) => visitor.Visit(this);

        internal override TResult Accept<TResult>(StartFromVisitor<TResult> visitor) => visitor.Visit(this);
    }

    /// <summary>
    /// Derived instance of <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading changes from a save point.
    /// </summary>
    /// <remarks>This class is used to temporarily store the fully serialized composite continuation token and needs to transformed into a <see cref="ChangeFeedStartFromContinuationAndFeedRange"/>.</remarks>
    internal sealed class ChangeFeedStartFromContinuation : ChangeFeedStartFrom
    {
        /// <summary>
        /// Initializes an instance of the <see cref="ChangeFeedStartFromContinuation"/> class.
        /// </summary>
        /// <param name="continuation">The continuation to resume from.</param>
        public ChangeFeedStartFromContinuation(string continuation)
            : base()
        {
            if (string.IsNullOrWhiteSpace(continuation))
            {
                throw new ArgumentOutOfRangeException($"{nameof(continuation)} must not be null, empty, or whitespace.");
            }

            this.Continuation = continuation;
        }

        /// <summary>
        /// Gets the continuation to resume from.
        /// </summary>
        public string Continuation { get; }

        internal override void Accept(StartFromVisitor visitor) => visitor.Visit(this);

        internal override TResult Accept<TResult>(StartFromVisitor<TResult> visitor) => visitor.Visit(this);
    }

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

        internal override void Accept(StartFromVisitor visitor) => visitor.Visit(this);

        internal override TResult Accept<TResult>(StartFromVisitor<TResult> visitor) => visitor.Visit(this);
    }

    /// <summary>
    /// Derived instance of <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading from an LSN for a particular feed range.
    /// </summary>
    internal sealed class ChangeFeedStartFromContinuationAndFeedRange : ChangeFeedStartFrom
    {
        public ChangeFeedStartFromContinuationAndFeedRange(string etag, FeedRangeInternal feedRange)
        {
            this.Etag = etag ?? throw new ArgumentNullException(nameof(etag));
            this.FeedRange = feedRange ?? throw new ArgumentNullException(nameof(feedRange));
        }

        public string Etag { get; }

        public FeedRangeInternal FeedRange { get; }

        internal override void Accept(StartFromVisitor visitor) => visitor.Visit(this);

        internal override TResult Accept<TResult>(StartFromVisitor<TResult> visitor) => visitor.Visit(this);
    }
}
