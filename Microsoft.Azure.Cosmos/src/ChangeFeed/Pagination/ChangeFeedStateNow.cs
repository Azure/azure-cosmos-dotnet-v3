// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    internal sealed class ChangeFeedStateNow : ChangeFeedState
    {
        public static readonly ChangeFeedStateNow Singleton = new ChangeFeedStateNow();

        private ChangeFeedStateNow()
        {
        }

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
