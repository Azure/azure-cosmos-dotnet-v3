// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif  
        interface IChangeFeedStateVisitor<TInput, TOutput>
    {
        TOutput Visit(ChangeFeedStateBeginning changeFeedStateBeginning, TInput input);
        TOutput Visit(ChangeFeedStateTime changeFeedStateTime, TInput input);
        TOutput Visit(ChangeFeedStateContinuation changeFeedStateContinuation, TInput input);
        TOutput Visit(ChangeFeedStateNow changeFeedStateNow, TInput input);
    }
}
