// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    internal interface IChangeFeedStateTransformer<TResult>
    {
        TResult Transform(ChangeFeedStateBeginning changeFeedStateBeginning);
        TResult Transform(ChangeFeedStateTime changeFeedStateTime);
        TResult Transform(ChangeFeedStateContinuation changeFeedStateContinuation);
        TResult Transform(ChangeFeedStateNow changeFeedStateNow);
    }
}
