// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal sealed class ChangeFeedStateContinuation : ChangeFeedState
    {
        public ChangeFeedStateContinuation(CosmosElement continuation)
        {
            this.ContinuationToken = continuation ?? throw new ArgumentNullException(nameof(continuation));
        }

        public CosmosElement ContinuationToken { get; }

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
