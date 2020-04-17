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
            this.ContainerRid = containerRid;
            this.FeedRange = feedRange;
        }

        public abstract void Accept(
            FeedRangeVisitor visitor,
            Action<RequestMessage, string> fillContinuation);

        public abstract string GetContinuation();

        public abstract void UpdateContinuation(string continuationToken);

        public abstract bool IsDone { get; }

        public abstract TryCatch ValidateContainer(string containerRid);

        public static bool TryParse(
            string toStringValue,
            out FeedRangeContinuation parsedToken)
        {
            if (FeedRangeCompositeContinuation.TryParse(toStringValue, out parsedToken))
            {
                return true;
            }

            parsedToken = null;
            return false;
        }

        public abstract Task<bool> ShouldRetryAsync(
            ContainerCore containerCore,
            ResponseMessage responseMessage,
            CancellationToken cancellationToken);
    }
}
