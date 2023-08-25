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
        interface IChangeFeedStateTransformer<TResult>
    {
        TResult Transform(ChangeFeedStateBeginning changeFeedStateBeginning);
        TResult Transform(ChangeFeedStateTime changeFeedStateTime);
        TResult Transform(ChangeFeedStateContinuation changeFeedStateContinuation);
        TResult Transform(ChangeFeedStateNow changeFeedStateNow);
    }
}
