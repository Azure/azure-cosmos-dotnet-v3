// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif 
        abstract class ChangeFeedState : State
    {
        public abstract void Accept<TInput>(IChangeFeedStateVisitor<TInput> visitor, TInput input);

        public abstract TOutput Accept<TInput, TOutput>(IChangeFeedStateVisitor<TInput, TOutput> visitor, TInput input);

        public abstract TResult Accept<TResult>(IChangeFeedStateTransformer<TResult> visitor);

        public static ChangeFeedState Now() => ChangeFeedStateNow.Singleton;

        public static ChangeFeedState Beginning() => ChangeFeedStateBeginning.Singleton;

        public static ChangeFeedState Time(DateTime time) => new ChangeFeedStateTime(time);

        public static ChangeFeedState Continuation(CosmosElement continuation) => new ChangeFeedStateContinuation(continuation);
    }
}