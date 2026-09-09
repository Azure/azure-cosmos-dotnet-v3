// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif 
        sealed class ChangeFeedStateContinuationAndStartTime : ChangeFeedState
    {
        public ChangeFeedStateContinuationAndStartTime(CosmosElement continuation, DateTime startTime)
        {
            this.ContinuationToken = continuation ?? throw new ArgumentNullException(nameof(continuation));

            if (startTime.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentOutOfRangeException($"{nameof(startTime)}.{nameof(DateTime.Kind)} must be {nameof(DateTimeKind)}.{nameof(DateTimeKind.Utc)}");
            }

            this.StartTime = startTime;
        }

        public CosmosElement ContinuationToken { get; }

        public DateTime StartTime { get; }

        public override void Accept<TInput>(
            IChangeFeedStateVisitor<TInput> visitor,
            TInput input) => visitor.Visit(this, input);

        public override TOutput Accept<TInput, TOutput>(
            IChangeFeedStateVisitor<TInput, TOutput> visitor,
            TInput input) => visitor.Visit(this, input);

        public override TResult Accept<TResult>(
            IChangeFeedStateTransformer<TResult> visitor) => visitor.Transform(this);
    }
}
