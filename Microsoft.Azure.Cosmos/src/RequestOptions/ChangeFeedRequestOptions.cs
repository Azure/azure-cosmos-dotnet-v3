//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The Cosmos Change Feed request options
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    sealed class ChangeFeedRequestOptions : RequestOptions
    {
        private int? maxItemCount;

        /// <summary>
        /// Gets or sets the maximum number of items to be returned in the enumeration operation in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The maximum number of items to be returned in the enumeration operation.
        /// </value> 
        public int? MaxItemCount
        {
            get => this.maxItemCount;
            set
            {
                if (value.HasValue && (value.Value <= 0))
                {
                    throw new ArgumentOutOfRangeException($"{nameof(this.MaxItemCount)} must be a positive value.");
                }

                this.maxItemCount = value;
            }
        }

        /// <summary>
        /// Gets or sets where the ChangeFeed operation should start from. If not set then the ChangeFeed operation will start from now.
        /// </summary>
        /// <remarks>
        /// Only applies in the case where no FeedToken is provided or the FeedToken was never used in a previous iterator.
        /// </remarks>
        public StartFrom From { get; set; } = new StartFromNow(FeedRangeEPK.FullRange);

        /// <summary>
        /// Fill the CosmosRequestMessage headers with the set properties
        /// </summary>
        /// <param name="request">The <see cref="RequestMessage"/></param>
        internal override void PopulateRequestOptions(RequestMessage request)
        {
            Debug.Assert(request != null);

            base.PopulateRequestOptions(request);

            PopulateStartFromRequestOptionVisitor visitor = new PopulateStartFromRequestOptionVisitor(request);
            if (this.From == null)
            {
                throw new InvalidOperationException($"{nameof(ChangeFeedRequestOptions)}.{nameof(ChangeFeedRequestOptions.StartFrom)} needs to be set to a value.");
            }

            this.From.Accept(visitor);

            if (this.MaxItemCount.HasValue)
            {
                request.Headers.Add(
                    HttpConstants.HttpHeaders.PageSize,
                    this.MaxItemCount.Value.ToString(CultureInfo.InvariantCulture));
            }

            request.Headers.Add(
                HttpConstants.HttpHeaders.A_IM,
                HttpConstants.A_IMHeaderValues.IncrementalFeed);
        }

        /// <summary>
        /// IfMatchEtag is inherited from the base class but not used. 
        /// </summary>
        [Obsolete("IfMatchEtag is inherited from the base class but not used.")]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public new string IfMatchEtag
        {
            get => throw new NotSupportedException($"{nameof(ChangeFeedRequestOptions)} does not use the {nameof(this.IfMatchEtag)} property.");
            set => throw new NotSupportedException($"{nameof(ChangeFeedRequestOptions)} does not use the {nameof(this.IfMatchEtag)} property.");
        }

        /// <summary>
        /// IfNoneMatchEtag is inherited from the base class but not used. 
        /// </summary>
        [Obsolete("IfNoneMatchEtag is inherited from the base class but not used.")]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public new string IfNoneMatchEtag
        {
            get => throw new NotSupportedException($"{nameof(ChangeFeedRequestOptions)} does not use the {nameof(this.IfNoneMatchEtag)} property.");
            set => throw new NotSupportedException($"{nameof(ChangeFeedRequestOptions)} does not use the {nameof(this.IfNoneMatchEtag)} property.");
        }

        /// <summary>
        /// Base class for where to start a ChangeFeed operation in <see cref="ChangeFeedRequestOptions"/>.
        /// </summary>
        /// <remarks>Use one of the static constructors to generate a StartFrom option.</remarks>
        public abstract class StartFrom
        {
            /// <summary>
            /// Initializes an instance of the <see cref="StartFrom"/> class.
            /// </summary>
            internal protected StartFrom()
            {
                // Internal so people can't derive from this type.
            }

            internal abstract void Accept(StartFromVisitor visitor);

            internal abstract TResult Accept<TResult>(StartFromVisitor<TResult> visitor);

            /// <summary>
            /// Creates a <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from this moment onward.
            /// </summary>
            /// <returns>A <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from this moment onward.</returns>
            public static StartFrom CreateFromNow() => CreateFromNowWithRange(FeedRangeEPK.FullRange);

            /// <summary>
            /// Creates a <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from this moment onward.
            /// </summary>
            /// <param name="feedRange">The range to start from.</param>
            /// <returns>A <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from this moment onward.</returns>
            public static StartFrom CreateFromNowWithRange(FeedRange feedRange)
            {
                if (!(feedRange is FeedRangeInternal feedRangeInternal))
                {
                    throw new ArgumentException($"{nameof(feedRange)} needs to be a {nameof(FeedRangeInternal)}.");
                }

                return new StartFromNow(feedRangeInternal);
            }

            /// <summary>
            /// Creates a <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from some point in time onward.
            /// </summary>
            /// <param name="dateTime">The time to start reading from.</param>
            /// <returns>A <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from some point in time onward.</returns>
            public static StartFrom CreateFromTime(DateTime dateTime) => CreateFromTimeWithRange(dateTime, FeedRangeEPK.FullRange);

            /// <summary>
            /// Creates a <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from some point in time onward.
            /// </summary>
            /// <param name="dateTime">The time to start reading from.</param>
            /// <param name="feedRange">The range to start from.</param>
            /// <returns>A <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from some point in time onward.</returns>
            public static StartFrom CreateFromTimeWithRange(DateTime dateTime, FeedRange feedRange)
            {
                if (!(feedRange is FeedRangeInternal feedRangeInternal))
                {
                    throw new ArgumentException($"{nameof(feedRange)} needs to be a {nameof(FeedRangeInternal)}.");
                }

                return new StartFromTime(dateTime, feedRangeInternal);
            }

            /// <summary>
            /// Creates a <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from a save point.
            /// </summary>
            /// <param name="continuation">The continuation to resume from.</param>
            /// <returns>A <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from a save point.</returns>
            public static StartFrom CreateFromContinuation(string continuation) => new StartFromContinuation(continuation);

            /// <summary>
            /// Creates a <see cref="StartFrom"/> that tells the ChangeFeed operation to start from the beginning of time.
            /// </summary>
            /// <returns>A <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from the beginning of time.</returns>
            public static StartFrom CreateFromBeginning() => CreateFromBeginningWithRange(FeedRangeEPK.FullRange);

            /// <summary>
            /// Creates a <see cref="StartFrom"/> that tells the ChangeFeed operation to start from the beginning of time.
            /// </summary>
            /// <param name="feedRange">The range to start from.</param>
            /// <returns>A <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from the beginning of time.</returns>
            public static StartFrom CreateFromBeginningWithRange(FeedRange feedRange)
            {
                if (!(feedRange is FeedRangeInternal feedRangeInternal))
                {
                    throw new ArgumentException($"{nameof(feedRange)} needs to be a {nameof(FeedRangeInternal)}.");
                }

                return new StartFromBeginning(feedRangeInternal);
            }
        }

        internal abstract class StartFromVisitor
        {
            public abstract void Visit(StartFromNow startFromNow);
            public abstract void Visit(StartFromTime startFromTime);
            public abstract void Visit(StartFromContinuation startFromContinuation);
            public abstract void Visit(StartFromBeginning startFromBeginning);
            public abstract void Visit(StartFromContinuationAndFeedRange startFromContinuationAndFeedRange);
        }

        internal abstract class StartFromVisitor<TResult>
        {
            public abstract TResult Visit(StartFromNow startFromNow);
            public abstract TResult Visit(StartFromTime startFromTime);
            public abstract TResult Visit(StartFromContinuation startFromContinuation);
            public abstract TResult Visit(StartFromBeginning startFromBeginning);
            public abstract TResult Visit(StartFromContinuationAndFeedRange startFromContinuationAndFeedRange);
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

            public override void Visit(StartFromNow startFromNow)
            {
                this.requestMessage.Headers.IfNoneMatch = PopulateStartFromRequestOptionVisitor.IfNoneMatchAllHeaderValue;

                if (startFromNow.FeedRange != null)
                {
                    startFromNow.FeedRange.Accept(this.feedRangeVisitor);
                }
            }

            public override void Visit(StartFromTime startFromTime)
            {
                // Our current public contract for ChangeFeedProcessor uses DateTime.MinValue.ToUniversalTime as beginning.
                // We need to add a special case here, otherwise it would send it as normal StartTime.
                // The problem is Multi master accounts do not support StartTime header on ReadFeed, and thus,
                // it would break multi master Change Feed Processor users using Start From Beginning semantics.
                // It's also an optimization, since the backend won't have to binary search for the value.
                if (startFromTime.Time != PopulateStartFromRequestOptionVisitor.StartFromBeginningTime)
                {
                    this.requestMessage.Headers.Add(
                        HttpConstants.HttpHeaders.IfModifiedSince,
                        startFromTime.Time.ToString("r", CultureInfo.InvariantCulture));
                }

                startFromTime.FeedRange.Accept(this.feedRangeVisitor);
            }

            public override void Visit(StartFromContinuation startFromContinuation)
            {
                // On REST level, change feed is using IfNoneMatch/ETag instead of continuation
                this.requestMessage.Headers.IfNoneMatch = startFromContinuation.Continuation;
            }

            public override void Visit(StartFromBeginning startFromBeginning)
            {
                // We don't need to set any headers to start from the beginning

                // Except for the feed range.
                startFromBeginning.FeedRange.Accept(this.feedRangeVisitor);
            }

            public override void Visit(StartFromContinuationAndFeedRange startFromContinuationAndFeedRange)
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

            public override FeedRange Visit(StartFromNow startFromNow) => startFromNow.FeedRange;

            public override FeedRange Visit(StartFromTime startFromTime) => startFromTime.FeedRange;

            public override FeedRange Visit(StartFromContinuation startFromContinuation)
                => throw new NotSupportedException($"{nameof(StartFromContinuation)} does not have a feed range.");

            public override FeedRange Visit(StartFromBeginning startFromBeginning) => startFromBeginning.FeedRange;

            public override FeedRange Visit(StartFromContinuationAndFeedRange startFromContinuationAndFeedRange) => startFromContinuationAndFeedRange.FeedRange;
        }

        /// <summary>
        /// Derived instance of <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from this moment onward.
        /// </summary>
        internal sealed class StartFromNow : StartFrom
        {
            /// <summary>
            /// Intializes an instance of the <see cref="StartFromNow"/> class.
            /// </summary>
            /// <param name="feedRange">The (optional) feed range to start from.</param>
            public StartFromNow(FeedRangeInternal feedRange)
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
        /// Derived instance of <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from some point in time onward.
        /// </summary>
        internal sealed class StartFromTime : StartFrom
        {
            /// <summary>
            /// Initializes an instance of the <see cref="StartFromTime"/> class.
            /// </summary>
            /// <param name="time">The time to start reading from.</param>
            /// <param name="feedRange">The (optional) range to start from.</param>
            public StartFromTime(DateTime time, FeedRangeInternal feedRange)
                : base()
            {
                if (time.Kind != DateTimeKind.Utc)
                {
                    throw new ArgumentOutOfRangeException($"{nameof(time)}.{nameof(DateTime.Kind)} must be {nameof(DateTimeKind)}.{nameof(DateTimeKind.Utc)}");
                }

                this.Time = time;
                this.FeedRange = feedRange ?? throw new ArgumentNullException(nameof(feedRange));
            }

            /// <summary>
            /// Gets the time the ChangeFeed operation should start reading from.
            /// </summary>
            public DateTime Time { get; }

            /// <summary>
            /// Gets the (optional) range to start from.
            /// </summary>
            public FeedRangeInternal FeedRange { get; }

            internal override void Accept(StartFromVisitor visitor) => visitor.Visit(this);

            internal override TResult Accept<TResult>(StartFromVisitor<TResult> visitor) => visitor.Visit(this);
        }

        /// <summary>
        /// Derived instance of <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from a save point.
        /// </summary>
        /// <remarks>This class is used to temporarily store the fully serialized composite continuation token and needs to transformed into a <see cref="StartFromContinuationAndFeedRange"/>.</remarks>
        internal sealed class StartFromContinuation : StartFrom
        {
            /// <summary>
            /// Initializes an instance of the <see cref="StartFromContinuation"/> class.
            /// </summary>
            /// <param name="continuation">The continuation to resume from.</param>
            public StartFromContinuation(string continuation)
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
        /// Derived instance of <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from the beginning of time.
        /// </summary>
        internal sealed class StartFromBeginning : StartFrom
        {
            /// <summary>
            /// Initializes an instance of the <see cref="StartFromBeginning"/> class.
            /// </summary>
            /// <param name="feedRange">The (optional) range to start from.</param>
            public StartFromBeginning(FeedRangeInternal feedRange)
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
        /// Derived instance of <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading from an LSN for a particular feed range.
        /// </summary>
        internal sealed class StartFromContinuationAndFeedRange : StartFrom
        {
            public StartFromContinuationAndFeedRange(string etag, FeedRangeInternal feedRange)
            {
                this.Etag = etag ?? throw new ArgumentNullException(nameof(etag));
                this.FeedRange = feedRange ?? throw new ArgumentNullException(nameof(feedRange));
            }

            public string Etag { get; }

            public FeedRangeInternal FeedRange { get; }

            internal override void Accept(StartFromVisitor visitor) => visitor.Visit(this);

            internal override TResult Accept<TResult>(StartFromVisitor<TResult> visitor) => visitor.Visit(this);
        }

        internal ChangeFeedRequestOptions Clone()
        {
            return new ChangeFeedRequestOptions()
            {
                MaxItemCount = this.maxItemCount,
                From = this.From,
            };
        }
    }
}