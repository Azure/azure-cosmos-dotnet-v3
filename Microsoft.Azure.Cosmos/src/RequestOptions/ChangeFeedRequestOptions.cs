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
        /// <summary>
        /// Gets or sets the maximum number of items to be returned in the enumeration operation in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The maximum number of items to be returned in the enumeration operation.
        /// </value> 
        public int? MaxItemCount { get; set; }

        /// <summary>
        /// Gets or sets where the ChangeFeed operation should start from. If not set then the ChangeFeed operation will start from the begining.
        /// </summary>
        /// <remarks>
        /// Only applies in the case where no FeedToken is provided or the FeedToken was never used in a previous iterator.
        /// In order to read the Change Feed from the beginning, set this to DateTime.MinValue.ToUniversalTime().
        /// </remarks>
        public StartFrom From { get; set; }

        internal string PartitionKeyRangeId { get; set; }

        /// <summary>
        /// Fill the CosmosRequestMessage headers with the set properties
        /// </summary>
        /// <param name="request">The <see cref="RequestMessage"/></param>
        internal override void PopulateRequestOptions(RequestMessage request)
        {
            Debug.Assert(request != null);

            base.PopulateRequestOptions(request);

            PopulateStartFromRequstOptionVisitor visitor = new PopulateStartFromRequstOptionVisitor(request);
            this.From.Accept(visitor);

            if (this.MaxItemCount.HasValue)
            {
                request.Headers.Add(
                    HttpConstants.HttpHeaders.PageSize,
                    this.MaxItemCount.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (!string.IsNullOrEmpty(this.PartitionKeyRangeId))
            {
                request.PartitionKeyRangeId = new PartitionKeyRangeIdentity(this.PartitionKeyRangeId);
            }

            request.Headers.Add(
                HttpConstants.HttpHeaders.A_IM,
                HttpConstants.A_IMHeaderValues.IncrementalFeed);
        }

        [Obsolete]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public new string IfMatchEtag
        {
            get => throw new NotSupportedException($"{nameof(ChangeFeedRequestOptions)} does not use the {nameof(this.IfMatchEtag)} property.");
            set => throw new NotSupportedException($"{nameof(ChangeFeedRequestOptions)} does not use the {nameof(this.IfMatchEtag)} property.");
        }

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
        }

        internal abstract class StartFromVisitor
        {
            public abstract void Visit(StartFromNow startFromNow);
            public abstract void Visit(StartFromTime startFromTime);
            public abstract void Visit(StartFromContinuation startFromContinuation);
        }

        internal sealed class PopulateStartFromRequstOptionVisitor : StartFromVisitor
        {
            private const string IfNoneMatchAllHeaderValue = "*";

            private readonly RequestMessage requestMessage;

            public PopulateStartFromRequstOptionVisitor(RequestMessage requestMessage)
            {
                if (requestMessage == null)
                {
                    throw new ArgumentNullException(nameof(requestMessage));
                }

                this.requestMessage = requestMessage;
            }

            public override void Visit(StartFromNow startFromNow)
            {
                this.requestMessage.Headers.IfNoneMatch = PopulateStartFromRequstOptionVisitor.IfNoneMatchAllHeaderValue;
            }

            public override void Visit(StartFromTime startFromTime)
            {
                this.requestMessage.Headers.Add(
                    HttpConstants.HttpHeaders.IfModifiedSince,
                    startFromTime.Time.ToString("r", CultureInfo.InvariantCulture));
            }

            public override void Visit(StartFromContinuation startFromContinuation)
            {
                // On REST level, change feed is using IfNoneMatch/ETag instead of continuation
                this.requestMessage.Headers.IfNoneMatch = startFromContinuation.Continuation;
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
    }
}