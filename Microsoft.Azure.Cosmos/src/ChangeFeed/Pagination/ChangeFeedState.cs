// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;

    internal abstract class ChangeFeedState : State
    {
        public abstract void Accept<TInput>(IChangeFeedStateVisitor<TInput> visitor, TInput input);

        public abstract TOutput Accept<TInput, TOutput>(IChangeFeedStateVisitor<TInput, TOutput> visitor, TInput input);

        public abstract TResult Accept<TResult>(IChangeFeedStateTransformer<TResult> visitor);

        public static ChangeFeedState Now() => ChangeFeedStateNow.Singleton;

        public static ChangeFeedState Beginning() => ChangeFeedStateBeginning.Singleton;

        public static ChangeFeedState Time(DateTime time) => new ChangeFeedStateTime(time);

        public static ChangeFeedState Continuation(string continuation) => new ChangeFeedStateContinuation(continuation);
    }
}