// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    /// <summary>
    /// Represents the continuation for an operation using FeedRange.
    /// </summary>
    internal abstract class FeedRangeContinuation
    {
        public string ContainerRid { get; }

        public virtual FeedRangeInternal FeedRange { get; }

        /// <summary>
        /// For mocking
        /// </summary>
        protected FeedRangeContinuation()
        {
        }

        public FeedRangeContinuation(
            string containerRid,
            FeedRangeInternal feedRange)
        {
            this.FeedRange = feedRange ?? throw new ArgumentNullException(nameof(feedRange));
            this.ContainerRid = containerRid;
        }

        public abstract void Accept(
            FeedRangeVisitor visitor,
            Action<RequestMessage, string> fillContinuation);

        public abstract string GetContinuation();

        public abstract void ReplaceContinuation(string continuationToken);

        public abstract bool IsDone { get; }

        public abstract TryCatch ValidateContainer(string containerRid);

        public static bool TryParse(
            string toStringValue,
            out FeedRangeContinuation parsedToken)
        {
            if (!FeedRangeCompositeContinuation.TryParse(toStringValue, out parsedToken))
            {
                parsedToken = null;
                return false;
            }

            return true;
        }

        public abstract Documents.ShouldRetryResult HandleChangeFeedNotModified(ResponseMessage responseMessage);

        public abstract Task<Documents.ShouldRetryResult> HandleSplitAsync(
            ContainerInternal containerCore,
            ResponseMessage responseMessage,
            CancellationToken cancellationToken);
    }
}
