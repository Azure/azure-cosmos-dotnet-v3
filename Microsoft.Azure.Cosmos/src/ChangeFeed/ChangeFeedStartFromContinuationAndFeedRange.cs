// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Derived instance of <see cref="ChangeFeedStartFrom"/> that tells the ChangeFeed operation to start reading from an LSN for a particular feed range.
    /// </summary>
    internal sealed class ChangeFeedStartFromContinuationAndFeedRange : ChangeFeedStartFrom
    {
        public ChangeFeedStartFromContinuationAndFeedRange(string etag, FeedRangeInternal feedRange)
            : base(feedRange)
        {
            this.Etag = etag;
        }

        public string Etag { get; }

        internal override void Accept(ChangeFeedStartFromVisitor visitor)
        {
            visitor.Visit(this);
        }

        internal override TResult Accept<TResult>(ChangeFeedStartFromVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        internal override Task<TResult> AcceptAsync<TInput, TResult>(
            ChangeFeedStartFromAsyncVisitor<TInput, TResult> visitor,
            TInput input,
            CancellationToken cancellationToken)
        {
            return visitor.VisitAsync(this, input, cancellationToken);
        }
    }
}
