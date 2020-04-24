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
                if (value.HasValue && (value.Value < 0) && (value.Value != -1))
                {
                    throw new ArgumentOutOfRangeException($"{nameof(this.MaxItemCount)} must be a positive value or -1.");
                }

                this.maxItemCount = value;
            }
        }

        /// <summary>
        /// Gets or sets where the ChangeFeed operation should start from. If not set then the ChangeFeed operation will start from now.
        /// </summary>
        /// <remarks>
        /// Only applies in the case where no FeedToken is provided or the FeedToken was never used in a previous iterator.
        /// In order to read the Change Feed from the beginning, set this to DateTime.MinValue.ToUniversalTime().
        /// </remarks>
        public StartFrom From { get; set; } = StartFromNow.Singleton;

        public FeedRange FeedRange { get; set; }

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

            if (this.FeedRange != null)
            {
                FeedRangeVisitor feedRangeVisitor = new FeedRangeVisitor(request);
                ((FeedRangeInternal)this.FeedRange).Accept(feedRangeVisitor);
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
            protected StartFrom()
            {
            }

            internal abstract void Accept(StartFromVisitor visitor);

            /// <summary>
            /// Creates a <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from this moment onward.
            /// </summary>
            /// <returns>A <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from this moment onward.</returns>
            public static StartFrom CreateFromNow()
            {
                return StartFromNow.Singleton;
            }

            /// <summary>
            /// Creates a <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from some point in time onward.
            /// </summary>
            /// <param name="dateTime">The time to start reading from.</param>
            /// <returns>A <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from some point in time onward.</returns>
            public static StartFrom CreateFromTime(DateTime dateTime)
            {
                return new StartFromTime(dateTime);
            }

            /// <summary>
            /// Creates a <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from a save point.
            /// </summary>
            /// <param name="continuation">The continuation to resume from.</param>
            /// <returns>A <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from a save point.</returns>
            public static StartFrom CreateFromContinuation(string continuation)
            {
                return new StartFromContinuation(continuation);
            }

            /// <summary>
            /// Creates a <see cref="StartFrom"/> that tells the ChangeFeed operation to start from the beginning of time.
            /// </summary>
            /// <returns>A <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from the beginning of time.</returns>
            public static StartFrom CreateFromBeginning()
            {
                return StartFromBeginning.Singleton;
            }
        }

        internal abstract class StartFromVisitor
        {
            public abstract void Visit(StartFromNow startFromNow);
            public abstract void Visit(StartFromTime startFromTime);
            public abstract void Visit(StartFromContinuation startFromContinuation);
            public abstract void Visit(StartFromBeginning startFromBeginning);
        }

        internal sealed class PopulateStartFromRequestOptionVisitor : StartFromVisitor
        {
            private const string IfNoneMatchAllHeaderValue = "*";
            private static readonly DateTime StartFromBeginningTime = DateTime.MinValue.ToUniversalTime();

            private readonly RequestMessage requestMessage;

            public PopulateStartFromRequestOptionVisitor(RequestMessage requestMessage)
            {
                this.requestMessage = requestMessage ?? throw new ArgumentNullException(nameof(requestMessage));
            }

            public override void Visit(StartFromNow startFromNow)
            {
                this.requestMessage.Headers.IfNoneMatch = PopulateStartFromRequestOptionVisitor.IfNoneMatchAllHeaderValue;
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
            }

            public override void Visit(StartFromContinuation startFromContinuation)
            {
                // On REST level, change feed is using IfNoneMatch/ETag instead of continuation
                this.requestMessage.Headers.IfNoneMatch = startFromContinuation.Continuation;
            }

            public override void Visit(StartFromBeginning startFromBeginning)
            {
                // We don't need to set any headers to start from the beginning
            }
        }

        /// <summary>
        /// Derived instance of <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from this moment onward.
        /// </summary>
        internal sealed class StartFromNow : StartFrom
        {
            public static readonly StartFromNow Singleton = new StartFromNow();

            /// <summary>
            /// Intializes an instance of the <see cref="StartFromNow"/> class.
            /// </summary>
            public StartFromNow()
                : base()
            {
            }

            internal override void Accept(StartFromVisitor visitor)
            {
                visitor.Visit(this);
            }
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
            public StartFromTime(DateTime time)
                : base()
            {
                if (time.Kind != DateTimeKind.Utc)
                {
                    throw new ArgumentOutOfRangeException($"{nameof(time)}.{nameof(DateTime.Kind)} must be {nameof(DateTimeKind)}.{nameof(DateTimeKind.Utc)}");
                }

                this.Time = time;
            }

            /// <summary>
            /// Gets the time the ChangeFeed operation should start reading from.
            /// </summary>
            public DateTime Time { get; }

            internal override void Accept(StartFromVisitor visitor)
            {
                visitor.Visit(this);
            }
        }

        /// <summary>
        /// Derived instance of <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from a save point.
        /// </summary>
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

            internal override void Accept(StartFromVisitor visitor)
            {
                visitor.Visit(this);
            }
        }

        /// <summary>
        /// Derived instance of <see cref="StartFrom"/> that tells the ChangeFeed operation to start reading changes from the beginning of time.
        /// </summary>
        internal sealed class StartFromBeginning : StartFrom
        {
            public static readonly StartFromBeginning Singleton = new StartFromBeginning();

            public StartFromBeginning()
                : base()
            {
            }

            internal override void Accept(StartFromVisitor visitor)
            {
                visitor.Visit(this);
            }
        }
    }
}