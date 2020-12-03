// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

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
            : base(feedRange: null)
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
